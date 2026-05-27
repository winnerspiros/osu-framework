// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using ManagedBass;
using ManagedBass.Mix;
using ManagedBass.Wasapi;
using osu.Framework.Audio;
using osu.Framework.Audio.NativeOutput;
using osu.Framework.Bindables;
using osu.Framework.Development;
using osu.Framework.Logging;
using osu.Framework.Platform.Linux.Native;
using osu.Framework.Platform.Windows.Native;
using LinuxScheduling = osu.Framework.Platform.Linux.Native.Scheduling;
using MacOSScheduling = osu.Framework.Platform.MacOS.Native.Scheduling;
using osu.Framework.Statistics;

namespace osu.Framework.Threading
{
    public class AudioThread : GameThread
    {
        public AudioThread()
            : base(name: "Audio")
        {
            OnNewFrame += onNewFrame;
            PreloadBass();
        }

        // Audio needs the highest practical scheduling priority: even a few milliseconds of
        // preemption causes audible glitches or stuttering in the BASS mixer callback.
        // Highest is used instead of AboveNormal because the audio thread runs at a very
        // controlled, low-CPU rate (UpdatePeriod = 5 ms) and will not starve other threads.
        protected override ThreadPriority Priority => ThreadPriority.Highest;

        public override bool IsCurrent => ThreadSafety.IsAudioThread;

        internal sealed override void MakeCurrent()
        {
            base.MakeCurrent();

            ThreadSafety.IsAudioThread = true;

            if (RuntimeInfo.OS == RuntimeInfo.Platform.Windows)
                mmcssHandle = Mmcss.SetThreadCharacteristics();
            else if (RuntimeInfo.OS == RuntimeInfo.Platform.Linux)
                LinuxScheduling.TrySetRealtimeScheduling(50);
            else if (RuntimeInfo.OS == RuntimeInfo.Platform.macOS)
                MacOSScheduling.TrySetUserInteractiveQoS();
        }

        private IntPtr mmcssHandle;

        internal override IEnumerable<StatisticsCounterType> StatisticsCounters => new[]
        {
            StatisticsCounterType.TasksRun,
            StatisticsCounterType.Tracks,
            StatisticsCounterType.Samples,
            StatisticsCounterType.SChannels,
            StatisticsCounterType.Components,
            StatisticsCounterType.MixChannels,
        };

        private readonly List<AudioManager> managers = new List<AudioManager>();
        private readonly Lock managersLock = new Lock();

        private static readonly HashSet<int> initialised_devices = new HashSet<int>();

        private static readonly GlobalStatistic<double> cpu_usage = GlobalStatistics.Get<double>("Audio", "Bass CPU%");

        private long frameCount;

        private void onNewFrame()
        {
            if (frameCount++ % 1000 == 0)
                cpu_usage.Value = Bass.CPUUsage;

            lock (managersLock)
            {
                for (int i = 0; i < managers.Count; i++)
                {
                    var m = managers[i];
                    m.Update();
                }
            }
        }

        internal void RegisterManager(AudioManager manager)
        {
            lock (managersLock)
            {
                if (managers.Contains(manager))
                    throw new InvalidOperationException($"{manager} was already registered");

                managers.Add(manager);
            }

            manager.GlobalMixerHandle.BindTo(globalMixerHandle);
        }

        internal void UnregisterManager(AudioManager manager)
        {
            lock (managersLock)
                managers.Remove(manager);

            manager.GlobalMixerHandle.UnbindFrom(globalMixerHandle);
        }

        protected override void OnExit()
        {
            base.OnExit();

            lock (managersLock)
            {
                // AudioManagers are iterated over backwards since disposal will unregister and remove them from the list.
                for (int i = managers.Count - 1; i >= 0; i--)
                {
                    var m = managers[i];

                    m.Dispose();

                    // Audio component disposal (including the AudioManager itself) is scheduled and only runs when the AudioThread updates.
                    // But the AudioThread won't run another update since it's exiting, so an update must be performed manually in order to finish the disposal.
                    m.Update();
                }

                managers.Clear();
            }

            // Safety net to ensure we have freed all devices before exiting.
            // This is mainly required for device-lost scenarios.
            // See https://github.com/ppy/osu-framework/pull/3378 for further discussion.
            foreach (int d in initialised_devices.ToArray())
                FreeDevice(d);

            // Revert MMCSS thread characteristics on Windows.
            if (RuntimeInfo.OS == RuntimeInfo.Platform.Windows)
                Mmcss.RevertThreadCharacteristics(mmcssHandle);
        }

        #region BASS Initialisation

        // TODO: All this bass init stuff should probably not be in this class.

        private WasapiProcedure? wasapiProcedure;
        private WasapiNotifyProcedure? wasapiNotifyProcedure;

        /// <summary>
        /// Whether to attempt WASAPI exclusive mode initialisation.
        /// Exclusive mode provides the lowest possible audio latency (~1-3ms) but takes
        /// sole ownership of the audio device, preventing other apps from producing sound.
        /// </summary>
        internal bool UseExclusiveWasapi { get; set; }

        /// <summary>
        /// If a global mixer is being used, this will be the BASS handle for it.
        /// If non-null, all game mixers should be added to this mixer.
        /// </summary>
        private readonly Bindable<int?> globalMixerHandle = new Bindable<int?>();

        internal bool InitDevice(int deviceId, bool useExperimentalWasapi)
        {
            Debug.Assert(ThreadSafety.IsAudioThread);
            Trace.Assert(deviceId != -1); // The real device ID should always be used, as the -1 device has special cases which are hard to work with.

            // Try to initialise the device, or request a re-initialise.
            if (!Bass.Init(deviceId, Flags: (DeviceInitFlags)128)) // 128 == BASS_DEVICE_REINIT
                return false;

            if (useExperimentalWasapi)
                attemptWasapiInitialisation();
            else
                freeWasapi();

            initialised_devices.Add(deviceId);
            return true;
        }

        internal void FreeDevice(int deviceId)
        {
            Debug.Assert(ThreadSafety.IsAudioThread);

            int selectedDevice = Bass.CurrentDevice;

            if (canSelectDevice(deviceId))
            {
                Bass.CurrentDevice = deviceId;
                Bass.Free();
            }

            freeWasapi();
            freeCoreAudio();
            freePipeWire();

            if (selectedDevice != deviceId && canSelectDevice(selectedDevice))
                Bass.CurrentDevice = selectedDevice;

            initialised_devices.Remove(deviceId);

            static bool canSelectDevice(int deviceId) => Bass.GetDeviceInfo(deviceId, out var deviceInfo) && deviceInfo.IsInitialized;
        }

        /// <summary>
        /// Makes BASS available to be consumed.
        /// </summary>
        internal static void PreloadBass()
        {
            if (RuntimeInfo.OS == RuntimeInfo.Platform.Linux)
            {
                // required for the time being to address libbass_fx.so load failures (see https://github.com/ppy/osu/issues/2852)
                Library.Load("libbass.so", Library.LoadFlags.RTLD_LAZY | Library.LoadFlags.RTLD_GLOBAL);
            }
        }

        private bool attemptWasapiInitialisation()
        {
            if (RuntimeInfo.OS != RuntimeInfo.Platform.Windows)
                return false;

            Logger.Log("Attempting local BassWasapi initialisation");

            int wasapiDevice = -1;

            // WASAPI device indices don't match normal BASS devices.
            // Each device is listed multiple times with each supported channel/frequency pair.
            //
            // Working backwards to find the correct device is how bass does things internally (see BassWasapi.GetBassDevice).
            if (Bass.CurrentDevice > 0)
            {
                string driver = Bass.GetDeviceInfo(Bass.CurrentDevice).Driver;

                if (!string.IsNullOrEmpty(driver))
                {
                    // In the normal execution case, BassWasapi.GetDeviceInfo will return false as soon as we reach the end of devices.
                    // This while condition is just a safety to avoid looping forever.
                    // It's intentionally quite high because if a user has many audio devices, this list can get long.
                    //
                    // Retrieving device info here isn't free. In the future we may want to investigate a better method.
                    while (wasapiDevice < 16384)
                    {
                        if (!BassWasapi.GetDeviceInfo(++wasapiDevice, out WasapiDeviceInfo info))
                            break;

                        if (info.ID == driver)
                            break;
                    }
                }
            }

            // To keep things in a sane state let's only keep one device initialised via wasapi.
            freeWasapi();
            return initWasapi(wasapiDevice);
        }

        private bool initWasapi(int wasapiDevice)
        {
            // This is intentionally initialised inline and stored to a field.
            // If we don't do this, it gets GC'd away.
            wasapiProcedure = (buffer, length, _) =>
            {
                if (globalMixerHandle.Value == null)
                    return 0;

                return Bass.ChannelGetData(globalMixerHandle.Value!.Value, buffer, length);
            };
            wasapiNotifyProcedure = (notify, device, _) => Scheduler.Add(() =>
            {
                if (notify == WasapiNotificationType.DefaultOutput)
                {
                    freeWasapi();
                    initWasapi(device);
                }
            });

            bool initialised = false;

            // Try exclusive mode first if requested (Minimal latency mode).
            // Exclusive mode bypasses the Windows audio engine entirely for sub-3ms latency,
            // but takes sole ownership of the device.
            if (UseExclusiveWasapi)
            {
                initialised = BassWasapi.Init(wasapiDevice, Procedure: wasapiProcedure,
                    Flags: WasapiInitFlags.Exclusive | WasapiInitFlags.EventDriven | WasapiInitFlags.AutoFormat,
                    Buffer: 0f, Period: float.Epsilon);

                Logger.Log(initialised
                    ? $"WASAPI exclusive mode initialised for device {wasapiDevice}."
                    : $"WASAPI exclusive mode failed for device {wasapiDevice}, falling back to shared mode.");
            }

            // Fall back to (or directly use) shared event-driven mode.
            if (!initialised)
            {
                initialised = BassWasapi.Init(wasapiDevice, Procedure: wasapiProcedure,
                    Flags: WasapiInitFlags.EventDriven | WasapiInitFlags.AutoFormat,
                    Buffer: 0f, Period: float.Epsilon);
                Logger.Log($"Initialising BassWasapi (shared) for device {wasapiDevice}...{(initialised ? "success!" : "FAILED")}");
            }

            if (!initialised)
                return false;

            BassWasapi.GetInfo(out var wasapiInfo);
            globalMixerHandle.Value = BassMix.CreateMixerStream(wasapiInfo.Frequency, wasapiInfo.Channels, BassFlags.MixerNonStop | BassFlags.Decode | BassFlags.Float);
            BassWasapi.Start();

            BassWasapi.SetNotify(wasapiNotifyProcedure);
            return true;
        }

        private void freeWasapi()
        {
            if (globalMixerHandle.Value == null) return;

            // The mixer probably doesn't need to be recycled. Just keeping things sane for now.
            Bass.StreamFree(globalMixerHandle.Value.Value);
            BassWasapi.Stop();
            BassWasapi.Free();
            globalMixerHandle.Value = null;
        }

        #endregion

        #region Native CoreAudio (macOS)

        private CoreAudioOutput? coreAudioOutput;

        /// <summary>
        /// Preferred buffer frame size for native CoreAudio output.
        /// Set by <see cref="AudioManager"/> based on <see cref="AudioLatencyMode"/>.
        /// </summary>
        internal int CoreAudioBufferFrames { get; set; } = 64;

        /// <summary>
        /// Attempts to initialise native CoreAudio AudioUnit output on macOS.
        /// This bypasses BASS's device output for sub-2ms latency.
        /// </summary>
        /// <returns>True if native CoreAudio output was successfully started.</returns>
        internal bool AttemptCoreAudioInitialisation()
        {
            if (RuntimeInfo.OS != RuntimeInfo.Platform.macOS)
                return false;

            freeCoreAudio();

            // Create the global mixer that all game mixers will feed into (decode-only mode).
            globalMixerHandle.Value = BassMix.CreateMixerStream(48000, 2, BassFlags.MixerNonStop | BassFlags.Decode | BassFlags.Float);

            if (globalMixerHandle.Value == null || globalMixerHandle.Value == 0)
            {
                Logger.Log("CoreAudio: Failed to create global BASS mixer.", LoggingTarget.Runtime, LogLevel.Error);
                globalMixerHandle.Value = null;
                return false;
            }

            coreAudioOutput = new CoreAudioOutput(() => globalMixerHandle.Value);

            if (!coreAudioOutput.Start(CoreAudioBufferFrames))
            {
                Logger.Log("CoreAudio: Native output failed to start, falling back to BASS device output.", LoggingTarget.Runtime, LogLevel.Important);
                freeCoreAudio();
                return false;
            }

            Logger.Log("CoreAudio: Native AudioUnit bridge active — bypassing BASS device output.", LoggingTarget.Runtime, LogLevel.Important);
            return true;
        }

        private void freeCoreAudio()
        {
            coreAudioOutput?.Dispose();
            coreAudioOutput = null;

            if (globalMixerHandle.Value != null && RuntimeInfo.OS == RuntimeInfo.Platform.macOS)
            {
                Bass.StreamFree(globalMixerHandle.Value.Value);
                globalMixerHandle.Value = null;
            }
        }

        #endregion

        #region Native PipeWire (Linux)

        private PipeWireOutput? pipeWireOutput;

        /// <summary>
        /// Preferred buffer frame size for native PipeWire output.
        /// Set by <see cref="AudioManager"/> based on <see cref="AudioLatencyMode"/>.
        /// </summary>
        internal int PipeWireBufferFrames { get; set; } = 48;

        /// <summary>
        /// Attempts to initialise native PipeWire pw_stream output on Linux.
        /// This bypasses BASS's device output for sub-1ms latency.
        /// </summary>
        /// <returns>True if native PipeWire output was successfully started.</returns>
        internal bool AttemptPipeWireInitialisation()
        {
            if (RuntimeInfo.OS != RuntimeInfo.Platform.Linux)
                return false;

            freePipeWire();

            // Create the global mixer that all game mixers will feed into (decode-only mode).
            globalMixerHandle.Value = BassMix.CreateMixerStream(48000, 2, BassFlags.MixerNonStop | BassFlags.Decode | BassFlags.Float);

            if (globalMixerHandle.Value == null || globalMixerHandle.Value == 0)
            {
                Logger.Log("PipeWire: Failed to create global BASS mixer.", LoggingTarget.Runtime, LogLevel.Error);
                globalMixerHandle.Value = null;
                return false;
            }

            pipeWireOutput = new PipeWireOutput(() => globalMixerHandle.Value);

            if (!pipeWireOutput.Start(PipeWireBufferFrames))
            {
                Logger.Log("PipeWire: Native output failed to start, falling back to BASS device output.", LoggingTarget.Runtime, LogLevel.Important);
                freePipeWire();
                return false;
            }

            Logger.Log("PipeWire: Native pw_stream bridge active — bypassing BASS device output.", LoggingTarget.Runtime, LogLevel.Important);
            return true;
        }

        private void freePipeWire()
        {
            pipeWireOutput?.Dispose();
            pipeWireOutput = null;

            if (globalMixerHandle.Value != null && RuntimeInfo.OS == RuntimeInfo.Platform.Linux)
            {
                Bass.StreamFree(globalMixerHandle.Value.Value);
                globalMixerHandle.Value = null;
            }
        }

        #endregion
    }
}
