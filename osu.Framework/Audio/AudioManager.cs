// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using JetBrains.Annotations;
using ManagedBass;
using ManagedBass.Fx;
using ManagedBass.Mix;
using osu.Framework.Audio.Mixing;
using osu.Framework.Audio.Mixing.Bass;
using osu.Framework.Audio.Sample;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Development;
using osu.Framework.Extensions.TypeExtensions;
using osu.Framework.IO.Stores;
using osu.Framework.Logging;
using osu.Framework.Threading;

namespace osu.Framework.Audio
{
    public class AudioManager : AudioCollectionManager<AudioComponent>
    {
        /// <summary>
        /// The number of BASS audio devices preceding the first real audio device.
        /// Consisting of <see cref="Bass.NoSoundDevice"/> and <see cref="bass_default_device"/>.
        /// </summary>
        protected const int BASS_INTERNAL_DEVICE_COUNT = 2;

        /// <summary>
        /// The index of the BASS audio device denoting the OS default.
        /// </summary>
        /// <remarks>
        /// See http://www.un4seen.com/doc/#bass/BASS_CONFIG_DEV_DEFAULT.html for more information on the included device.
        /// </remarks>
        private const int bass_default_device = 1;

        /// <summary>
        /// The manager component responsible for audio tracks (e.g. songs).
        /// </summary>
        public ITrackStore Tracks => globalTrackStore.Value;

        /// <summary>
        /// The manager component responsible for audio samples (e.g. sound effects).
        /// </summary>
        public ISampleStore Samples => globalSampleStore.Value;

        /// <summary>
        /// The thread audio operations (mainly Bass calls) are ran on.
        /// </summary>
        private readonly AudioThread thread;

        /// <summary>
        /// The global mixer which all tracks are routed into by default.
        /// </summary>
        public readonly AudioMixer TrackMixer;

        /// <summary>
        /// The global mixer which all samples are routed into by default.
        /// </summary>
        public readonly AudioMixer SampleMixer;

        /// <summary>
        /// The names of all available audio devices.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This property does not contain the names of disabled audio devices.
        /// </para>
        /// <para>
        /// This property may also not necessarily contain the name of the default audio device provided by the OS.
        /// Consumers should provide a "Default" audio device entry which sets <see cref="AudioDevice"/> to an empty string.
        /// </para>
        /// </remarks>
        public IEnumerable<string> AudioDeviceNames => audioDeviceNames;

        /// <summary>
        /// Is fired whenever a new audio device is discovered and provides its name.
        /// </summary>
        public event Action<string> OnNewDevice;

        /// <summary>
        /// Is fired whenever an audio device is lost and provides its name.
        /// </summary>
        public event Action<string> OnLostDevice;

        /// <summary>
        /// The preferred audio device we should use. A value of
        /// <see cref="string.Empty"/> denotes the OS default.
        /// </summary>
        public readonly Bindable<string> AudioDevice = new Bindable<string>();

        /// <summary>
        /// Whether to use experimental WASAPI initialisation on windows.
        /// This generally results in lower audio latency, but also changes the audio synchronisation from
        /// historical expectations, meaning users / application will have to account for different offsets.
        /// </summary>
        public readonly BindableBool UseExperimentalWasapi = new BindableBool();

        /// <summary>
        /// The audio output latency mode. Controls per-platform low-latency backend selection
        /// and buffer tuning (WASAPI on Windows, AAudio on Android, CoreAudio reduced buffers on macOS/iOS,
        /// PipeWire/JACK on Linux).
        /// </summary>
        public readonly Bindable<AudioLatencyMode> AudioLatencyModeSetting = new Bindable<AudioLatencyMode>(AudioLatencyMode.Standard);

        /// <summary>
        /// Volume of all samples played game-wide.
        /// </summary>
        public readonly BindableDouble VolumeSample = new BindableDouble(1)
        {
            MinValue = 0,
            MaxValue = 1
        };

        /// <summary>
        /// Volume of all tracks played game-wide.
        /// </summary>
        public readonly BindableDouble VolumeTrack = new BindableDouble(1)
        {
            MinValue = 0,
            MaxValue = 1
        };

        /// <summary>
        /// Whether a global mixer is being used for audio routing.
        /// For now, this is only the case on Windows when using shared mode WASAPI initialisation.
        /// </summary>
        public IBindable<bool> UsingGlobalMixer => usingGlobalMixer;

        private readonly Bindable<bool> usingGlobalMixer = new BindableBool();

        /// <summary>
        /// If a global mixer is being used, this will be the BASS handle for it.
        /// If non-null, all game mixers should be added to this mixer.
        /// </summary>
        /// <remarks>
        /// When this is non-null, all mixers created via <see cref="CreateAudioMixer"/>
        /// will themselves be added to the global mixer, which will handle playback itself.
        ///
        /// In this mode of operation, nested mixers will be created with the <see cref="BassFlags.Decode"/>
        /// flag, meaning they no longer handle playback directly.
        ///
        /// An eventual goal would be to use a global mixer across all platforms as it can result
        /// in more control and better playback performance.
        ///
        /// Exposed publicly so external integrations (e.g. the Android Oboe redirector in the
        /// winnerspiros/osu fork) can plug into the same handle without resorting to reflection
        /// or InternalsVisibleTo hacks.
        /// </remarks>
        public readonly IBindable<int?> GlobalMixerHandle = new Bindable<int?>();

        public override bool IsLoaded => base.IsLoaded &&
                                         // bass default device is a null device (-1), not the actual system default.
                                         Bass.CurrentDevice != Bass.DefaultDevice;

        // Mutated by multiple threads, must be thread safe.
        private ImmutableArray<DeviceInfo> audioDevices = ImmutableArray<DeviceInfo>.Empty;
        private ImmutableList<string> audioDeviceNames = ImmutableList<string>.Empty;

        private Scheduler scheduler => thread.Scheduler;

        private Scheduler eventScheduler => EventScheduler ?? scheduler;

        private readonly CancellationTokenSource cancelSource = new CancellationTokenSource();

        /// <summary>
        /// The scheduler used for invoking publicly exposed delegate events.
        /// </summary>
        public Scheduler EventScheduler;

        internal IBindableList<AudioMixer> ActiveMixers => activeMixers;
        private readonly BindableList<AudioMixer> activeMixers = new BindableList<AudioMixer>();

        private readonly Lazy<TrackStore> globalTrackStore;
        private readonly Lazy<SampleStore> globalSampleStore;

        /// <summary>
        /// Constructs an AudioStore given a track resource store, and a sample resource store.
        /// </summary>
        /// <param name="audioThread">The host's audio thread.</param>
        /// <param name="trackStore">The resource store containing all audio tracks to be used in the future.</param>
        /// <param name="sampleStore">The sample store containing all audio samples to be used in the future.</param>
        /// <param name="config"></param>
        public AudioManager(AudioThread audioThread, ResourceStore<byte[]> trackStore, ResourceStore<byte[]> sampleStore, [CanBeNull] FrameworkConfigManager config)
        {
            thread = audioThread;

            thread.RegisterManager(this);

            if (config != null)
            {
                // attach config bindables
                config.BindWith(FrameworkSetting.AudioDevice, AudioDevice);
                config.BindWith(FrameworkSetting.AudioUseExperimentalWasapi, UseExperimentalWasapi);
                config.BindWith(FrameworkSetting.AudioLatencyMode, AudioLatencyModeSetting);
                config.BindWith(FrameworkSetting.VolumeUniversal, Volume);
                config.BindWith(FrameworkSetting.VolumeEffect, VolumeSample);
                config.BindWith(FrameworkSetting.VolumeMusic, VolumeTrack);
            }

            AudioDevice.ValueChanged += _ => scheduler.AddOnce(initCurrentDevice);
            UseExperimentalWasapi.ValueChanged += _ => scheduler.AddOnce(initCurrentDevice);
            AudioLatencyModeSetting.ValueChanged += _ => scheduler.AddOnce(initCurrentDevice);
            // initCurrentDevice not required for changes to `GlobalMixerHandle` as it is only changed when experimental wasapi is toggled (handled above).
            GlobalMixerHandle.ValueChanged += handle => usingGlobalMixer.Value = handle.NewValue.HasValue;

            AddItem(TrackMixer = createAudioMixer(null, nameof(TrackMixer)));
            AddItem(SampleMixer = createAudioMixer(null, nameof(SampleMixer)));

            globalTrackStore = new Lazy<TrackStore>(() =>
            {
                var store = new TrackStore(trackStore, TrackMixer);
                AddItem(store);
                store.AddAdjustment(AdjustableProperty.Volume, VolumeTrack);
                return store;
            });

            globalSampleStore = new Lazy<SampleStore>(() =>
            {
                var store = new SampleStore(sampleStore, SampleMixer);
                AddItem(store);
                store.AddAdjustment(AdjustableProperty.Volume, VolumeSample);
                return store;
            });

            syncAudioDevices();

            // check for changes in any audio devices every 1000ms (slightly expensive operation)
            CancellationToken token = cancelSource.Token;
            scheduler.AddDelayed(() =>
            {
                new Thread(() =>
                {
                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            if (CheckForDeviceChanges(audioDevices))
                                syncAudioDevices();
                            Thread.Sleep(1000);
                        }
                        catch
                        {
                        }
                    }
                })
                {
                    IsBackground = true
                }.Start();
            }, 1000);
        }

        protected override void Dispose(bool disposing)
        {
            cancelSource.Cancel();

            thread.UnregisterManager(this);

            OnNewDevice = null;
            OnLostDevice = null;

            base.Dispose(disposing);
        }

        private static int userMixerID;

        /// <summary>
        /// Creates a new <see cref="AudioMixer"/>.
        /// </summary>
        /// <remarks>
        /// Channels removed from this <see cref="AudioMixer"/> fall back to the global <see cref="SampleMixer"/>.
        /// </remarks>
        /// <param name="identifier">An identifier displayed on the audio mixer visualiser.</param>
        public AudioMixer CreateAudioMixer(string identifier = null) =>
            createAudioMixer(SampleMixer, !string.IsNullOrEmpty(identifier) ? identifier : $"user #{Interlocked.Increment(ref userMixerID)}");

        private AudioMixer createAudioMixer(AudioMixer fallbackMixer, string identifier)
        {
            var mixer = new BassAudioMixer(this, fallbackMixer, identifier);
            AddItem(mixer);
            return mixer;
        }

        protected override void ItemAdded(AudioComponent item)
        {
            base.ItemAdded(item);
            if (item is AudioMixer mixer)
                activeMixers.Add(mixer);
        }

        protected override void ItemRemoved(AudioComponent item)
        {
            base.ItemRemoved(item);
            if (item is AudioMixer mixer)
                activeMixers.Remove(mixer);
        }

        /// <summary>
        /// Obtains the <see cref="TrackStore"/> corresponding to a given resource store.
        /// Returns the global <see cref="TrackStore"/> if no resource store is passed.
        /// </summary>
        /// <param name="store">The <see cref="IResourceStore{T}"/> of which to retrieve the <see cref="TrackStore"/>.</param>
        /// <param name="mixer">The <see cref="AudioMixer"/> to use for tracks created by this store. Defaults to the global <see cref="TrackMixer"/>.</param>
        public ITrackStore GetTrackStore(IResourceStore<byte[]> store = null, AudioMixer mixer = null)
        {
            if (store == null) return globalTrackStore.Value;

            TrackStore tm = new TrackStore(store, mixer ?? TrackMixer);
            globalTrackStore.Value.AddItem(tm);
            return tm;
        }

        /// <summary>
        /// Obtains the <see cref="SampleStore"/> corresponding to a given resource store.
        /// Returns the global <see cref="SampleStore"/> if no resource store is passed.
        /// </summary>
        /// <remarks>
        /// By default, <c>.wav</c> and <c>.ogg</c> extensions will be automatically appended to lookups on the returned store
        /// if the lookup does not correspond directly to an existing filename.
        /// Additional extensions can be added via <see cref="ISampleStore.AddExtension"/>.
        /// </remarks>
        /// <param name="store">The <see cref="IResourceStore{T}"/> of which to retrieve the <see cref="SampleStore"/>.</param>
        /// <param name="mixer">The <see cref="AudioMixer"/> to use for samples created by this store. Defaults to the global <see cref="SampleMixer"/>.</param>
        public ISampleStore GetSampleStore(IResourceStore<byte[]> store = null, AudioMixer mixer = null)
        {
            if (store == null) return globalSampleStore.Value;

            SampleStore sm = new SampleStore(store, mixer ?? SampleMixer);
            globalSampleStore.Value.AddItem(sm);
            return sm;
        }

        /// <summary>
        /// (Re-)Initialises BASS for the current <see cref="AudioDevice"/>.
        /// This will automatically fall back to the system default device on failure.
        /// </summary>
        private void initCurrentDevice()
        {
            string deviceName = AudioDevice.Value;

            // try using the specified device
            int deviceIndex = audioDeviceNames.FindIndex(d => d == deviceName);
            if (deviceIndex >= 0 && trySetDevice(BASS_INTERNAL_DEVICE_COUNT + deviceIndex)) return;

            // try using the system default if there is any device present.
            // mobiles are an exception as the built-in speakers may not be provided as an audio device name,
            // but they are still provided by BASS under the internal device name "Default".
            if ((audioDeviceNames.Count > 0 || RuntimeInfo.IsMobile) && trySetDevice(bass_default_device)) return;

            // no audio devices can be used, so try using Bass-provided "No sound" device as last resort.
            trySetDevice(Bass.NoSoundDevice);

            // we're boned. even "No sound" device won't initialise.
            return;

            bool trySetDevice(int deviceId)
            {
                var device = audioDevices.ElementAtOrDefault(deviceId);

                // device is invalid
                if (!device.IsEnabled)
                    return false;

                // we don't want bass initializing with real audio device on headless test runs.
                if (deviceId != Bass.NoSoundDevice && DebugUtils.IsNUnitRunning)
                    return false;

                // initialize new device
                if (!InitBass(deviceId))
                    return false;

                //we have successfully initialised a new device.
                UpdateDevice(deviceId);

                return true;
            }
        }

        /// <summary>
        /// This method calls <see cref="Bass.Init(int, int, DeviceInitFlags, IntPtr, IntPtr)"/>.
        /// It can be overridden for unit testing.
        /// </summary>
        /// <param name="device">The device to initialise.</param>
        protected virtual bool InitBass(int device)
        {
            if (int.TryParse(Environment.GetEnvironmentVariable("OSU_TEMP_TESTING_BASS_CONFIG_DEV_PERIOD"), out int devicePeriod))
            {
                Logger.Log(
                    $"Device period is set to \"{devicePeriod}\" via environment variable for testing purposes.\n\nThis is made available for testing so we can gather feedback on how to incorporate as a permanent game setting. Incorrect settings may lead to serious issues.",
                    level: LogLevel.Important);

                // Device period normally is in milliseconds, but it might be set to a negative
                // value too for an exact sample size, e.g. -256 for 256 samples.
                // https://www.un4seen.com/doc/#bass/BASS_CONFIG_DEV_PERIOD.html
                Bass.Configure(ManagedBass.Configuration.DevicePeriod, devicePeriod);

                // 1ms is definitely too low, but we're setting such low number on purpose,
                // in order for BASS to automatically set it to twice the length of BASS_CONFIG_DEV_PERIOD.
                //
                // See https://www.un4seen.com/doc/#bass/BASS_CONFIG_DEV_BUFFER.html
                Bass.DeviceBufferLength = 1;
            }
            else
            {
                configureAudioLatency();
            }

            // ensure there are no brief delays on audio operations (causing stream stalls etc.) after periods of silence.
            Bass.DeviceNonStop = true;

            // without this, if bass falls back to directsound legacy mode the audio playback offset will be way off.
            Bass.Configure(ManagedBass.Configuration.TruePlayPosition, 0);

            // Set BASS_IOS_SESSION_DISABLE here to leave session configuration in our hands (see iOS project).
            Bass.Configure(ManagedBass.Configuration.IOSSession, 16);

            // Always provide a default device. This should be a no-op, but we have asserts for this behaviour.
            Bass.Configure(ManagedBass.Configuration.IncludeDefaultDevice, true);

            // Enable custom BASS_CONFIG_MP3_OLDGAPS flag for backwards compatibility.
            // - This disables support for ItunSMPB tag parsing to match previous expectations.
            // - This also disables a change which assumes a 529 sample (2116 byte in stereo 16-bit) delay if the MP3 file doesn't specify one.
            //   (That was added in Bass for more consistent results across platforms and standard/mp3-free BASS versions, because OSX/iOS's MP3 decoder always removes 529 samples)
            Bass.Configure(ManagedBass.Configuration.Mp3OldGaps, 1);

            // Disable BASS_CONFIG_DEV_TIMEOUT flag to keep BASS audio output from pausing on device processing timeout.
            // See https://www.un4seen.com/forum/?topic=19601 for more information.
            Bass.DevTimeout = false;

            bool success = attemptInit();

            if (success || !UseExperimentalWasapi.Value)
                return success;

            // in the case we're using experimental WASAPI, give a second chance of initialisation by forcefully disabling it.
            Logger.Log($"BASS device {device} failed to initialise with experimental WASAPI, disabling", level: LogLevel.Error);
            UseExperimentalWasapi.Value = false;
            return attemptInit();

            bool attemptInit()
            {
                bool innerSuccess = thread.InitDevice(device, UseExperimentalWasapi.Value);
                bool alreadyInitialised = Bass.LastError == Errors.Already;

                if (alreadyInitialised)
                    return true;

                if (BassUtils.CheckFaulted(false))
                    return false;

                if (!innerSuccess)
                {
                    Logger.Log("BASS failed to initialize but did not provide an error code", level: LogLevel.Error);
                    return false;
                }

                var deviceInfo = audioDevices.ElementAtOrDefault(device);

                Logger.Log($@"🔈 BASS initialised
                          BASS version:           {Bass.Version}
                          BASS FX version:        {BassFx.Version}
                          BASS MIX version:       {BassMix.Version}
                          Device:                 {deviceInfo.Name}
                          Driver:                 {deviceInfo.Driver}
                          Device period length:   {devicePeriod}
                          Device buffer length:   {Bass.DeviceBufferLength} ms
                          Update period:          {Bass.UpdatePeriod} ms
                          Playback buffer length: {Bass.PlaybackBufferLength} ms");

                return true;
            }
        }

        /// <summary>
        /// Configures BASS audio buffer settings based on the current <see cref="AudioLatencyModeSetting"/>
        /// and the detected platform. Each platform uses its lowest-latency native backend where applicable:
        /// WASAPI on Windows (handled separately via <see cref="UseExperimentalWasapi"/>),
        /// AAudio on Android, CoreAudio on macOS/iOS, PipeWire/JACK/PulseAudio on Linux.
        /// </summary>
        private void configureAudioLatency()
        {
            var mode = AudioLatencyModeSetting.Value;

            switch (RuntimeInfo.OS)
            {
                case RuntimeInfo.Platform.Windows:
                    configureWindowsLatency(mode);
                    break;

                case RuntimeInfo.Platform.Linux:
                    configureLinuxLatency(mode);
                    break;

                case RuntimeInfo.Platform.macOS:
                    configureMacOSLatency(mode);
                    break;

                case RuntimeInfo.Platform.Android:
                    configureAndroidLatency(mode);
                    break;

                case RuntimeInfo.Platform.iOS:
                    configureIOSLatency(mode);
                    break;

                default:
                    // Fallback: safe defaults for unknown platforms.
                    Bass.UpdatePeriod = 5;
                    Bass.DeviceBufferLength = 10;
                    Bass.PlaybackBufferLength = 40;
                    break;
            }

            Logger.Log($"Audio latency mode: {mode} (platform: {RuntimeInfo.OS})");
        }

        /// <summary>
        /// Windows audio latency configuration.
        /// Standard mode uses DirectSound defaults; LowLatency enables WASAPI shared;
        /// Minimal uses WASAPI exclusive mode for sub-3ms latency (falls back to shared if device doesn't support it).
        /// </summary>
        private void configureWindowsLatency(AudioLatencyMode mode)
        {
            switch (mode)
            {
                case AudioLatencyMode.Minimal:
                    // Aggressive: tightest possible buffers + WASAPI exclusive mode.
                    // Exclusive bypasses the Windows audio engine entirely for ~1-3ms latency.
                    Bass.UpdatePeriod = 2;
                    Bass.DeviceBufferLength = 5;
                    Bass.PlaybackBufferLength = 15;
                    thread.UseExclusiveWasapi = true;
                    if (!UseExperimentalWasapi.Value)
                        UseExperimentalWasapi.Value = true;
                    break;

                case AudioLatencyMode.LowLatency:
                    // WASAPI shared mode, event-driven (~3–5 ms output latency).
                    Bass.UpdatePeriod = 3;
                    Bass.DeviceBufferLength = 8;
                    Bass.PlaybackBufferLength = 25;
                    thread.UseExclusiveWasapi = false;
                    if (!UseExperimentalWasapi.Value)
                        UseExperimentalWasapi.Value = true;
                    break;

                default: // Standard
                    Bass.UpdatePeriod = 5;
                    Bass.DeviceBufferLength = 10;
                    Bass.PlaybackBufferLength = 40;
                    thread.UseExclusiveWasapi = false;
                    break;
            }
        }

        /// <summary>
        /// Linux audio latency configuration.
        /// BASS on Linux uses ALSA/PulseAudio/PipeWire depending on system configuration.
        /// PipeWire is the modern low-latency audio server that replaces both PulseAudio and JACK.
        /// Lower DevicePeriod values request smaller buffers from the audio daemon.
        /// </summary>
        private void configureLinuxLatency(AudioLatencyMode mode)
        {
            // Detect the active audio backend for logging and optimal configuration.
            string backend = detectLinuxAudioBackend();
            Logger.Log($"Linux audio backend detected: {backend}", LoggingTarget.Runtime, LogLevel.Important);

            // Set PIPEWIRE_LATENCY environment variable before BASS init to hint PipeWire
            // to use smaller buffer quanta. PipeWire's ALSA compatibility layer reads this
            // to configure the quantum for our client. Format: "samples/rate".
            // This must be set before Bass.Init() is called to take effect.
            if (backend == "pipewire" && mode != AudioLatencyMode.Standard)
            {
                int samples = mode == AudioLatencyMode.Minimal ? 64 : 128;
                string latencyHint = $"{samples}/48000";

                Environment.SetEnvironmentVariable("PIPEWIRE_LATENCY", latencyHint);
                Logger.Log($"Set PIPEWIRE_LATENCY={latencyHint} for low-latency PipeWire quantum", LoggingTarget.Runtime, LogLevel.Important);
            }

            switch (mode)
            {
                case AudioLatencyMode.Minimal:
                    // Aggressive: request smallest buffer quantum.
                    // PipeWire natively supports 64-sample quanta; ALSA/PulseAudio may round up.
                    Bass.Configure(ManagedBass.Configuration.DevicePeriod, backend == "pipewire" ? -64 : -128);
                    Bass.DeviceBufferLength = 1;
                    Bass.PlaybackBufferLength = 10;
                    Bass.UpdatePeriod = 2;
                    break;

                case AudioLatencyMode.LowLatency:
                    // Low-latency: ~256 samples (~5.3 ms at 48 kHz).
                    // Reliable with PipeWire's default rt priority; safe for PulseAudio too.
                    Bass.Configure(ManagedBass.Configuration.DevicePeriod, -256);
                    Bass.DeviceBufferLength = 1;
                    Bass.PlaybackBufferLength = 20;
                    Bass.UpdatePeriod = 3;
                    break;

                default: // Standard
                    Bass.UpdatePeriod = 5;
                    Bass.DeviceBufferLength = 10;
                    Bass.PlaybackBufferLength = 40;
                    break;
            }
        }

        /// <summary>
        /// Detects the active Linux audio backend (pipewire, pulseaudio, jack, or alsa).
        /// </summary>
        private static string detectLinuxAudioBackend()
        {
            // PipeWire exposes itself as PulseAudio to clients, so check for the PipeWire
            // runtime directory or environment variable first.
            string pipewireRuntime = Environment.GetEnvironmentVariable("PIPEWIRE_RUNTIME_DIR");

            if (!string.IsNullOrEmpty(pipewireRuntime))
                return "pipewire";

            // Check XDG_RUNTIME_DIR for PipeWire socket
            string xdgRuntime = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");

            if (!string.IsNullOrEmpty(xdgRuntime) && System.IO.File.Exists(System.IO.Path.Combine(xdgRuntime, "pipewire-0")))
                return "pipewire";

            // Check if JACK is running
            if (!string.IsNullOrEmpty(xdgRuntime) && System.IO.File.Exists(System.IO.Path.Combine(xdgRuntime, "jack")))
                return "jack";

            // Check PULSE_SERVER or PulseAudio socket
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PULSE_SERVER")))
                return "pulseaudio";

            if (!string.IsNullOrEmpty(xdgRuntime) && System.IO.File.Exists(System.IO.Path.Combine(xdgRuntime, "pulse", "native")))
                return "pulseaudio";

            return "alsa";
        }

        /// <summary>
        /// macOS audio latency configuration using CoreAudio.
        /// BASS on macOS uses CoreAudio's AudioUnit API. DevicePeriod controls the preferred
        /// I/O buffer duration request sent to the HAL (Hardware Abstraction Layer).
        /// CoreAudio honours the nearest power-of-two frame count that the hardware supports.
        /// </summary>
        private void configureMacOSLatency(AudioLatencyMode mode)
        {
            switch (mode)
            {
                case AudioLatencyMode.Minimal:
                    // Aggressive: ~128 samples (~2.7 ms at 48 kHz) — requests minimum HAL buffer.
                    // CoreAudio will round up to nearest hardware-supported value (typically 128 or 256).
                    Bass.Configure(ManagedBass.Configuration.DevicePeriod, -128);
                    Bass.DeviceBufferLength = 1;
                    Bass.PlaybackBufferLength = 10;
                    Bass.UpdatePeriod = 2;
                    break;

                case AudioLatencyMode.LowLatency:
                    // Low-latency: ~256 samples (~5.3 ms at 48 kHz) — good balance.
                    Bass.Configure(ManagedBass.Configuration.DevicePeriod, -256);
                    Bass.DeviceBufferLength = 3;
                    Bass.PlaybackBufferLength = 15;
                    Bass.UpdatePeriod = 3;
                    break;

                default: // Standard — CoreAudio default (typically 512 samples / ~10.7ms).
                    Bass.UpdatePeriod = 5;
                    Bass.DeviceBufferLength = 8;
                    Bass.PlaybackBufferLength = 35;
                    break;
            }
        }

        /// <summary>
        /// Android audio latency configuration using AAudio (the native low-latency API).
        /// <para>
        /// AAudio (enabled via <c>Bass.AndroidAAudio = true</c>) is Android's native low-latency
        /// audio API that Oboe wraps. When enabled, BASS uses AAudio's
        /// <c>AAUDIO_PERFORMANCE_MODE_LOW_LATENCY</c> path internally, achieving similar results
        /// to a direct Oboe integration.
        /// </para>
        /// <para>
        /// For games needing even lower latency (e.g. sub-10ms tap-to-sound), the
        /// <see cref="GlobalMixerHandle"/> is exposed so the game layer can redirect the BASS
        /// mixer output through a native Oboe stream (bypassing BASS's own device output).
        /// This is the approach used by the winnerspiros/osu Android fork.
        /// </para>
        /// Negative DevicePeriod values set exact buffer sizes in samples.
        /// </summary>
        private void configureAndroidLatency(AudioLatencyMode mode)
        {
            // Always enable AAudio to prefer it over OpenSL ES for lower latency on Android 8.1+.
            // AAudio provides the same native audio path that Google's Oboe library wraps.
            // Must be set before Bass.Init().
            Bass.AndroidAAudio = true;

            switch (mode)
            {
                case AudioLatencyMode.Minimal:
                    // Aggressive: -128 samples (~2.7 ms at 48 kHz).
                    // Equivalent to AAUDIO_PERFORMANCE_MODE_LOW_LATENCY with smallest feasible buffer.
                    // May glitch on older/slower SoCs (pre-Snapdragon 8 Gen 1).
                    Bass.DevicePeriod = -128;
                    Bass.DeviceBufferLength = 1;
                    Bass.PlaybackBufferLength = 10;
                    Bass.UpdatePeriod = 1;
                    break;

                case AudioLatencyMode.LowLatency:
                    // Low-latency: -256 samples (~5.3 ms at 48 kHz).
                    // Reliable on most modern SoCs (Snapdragon 8xx, Tensor, Dimensity 9xxx).
                    Bass.DevicePeriod = -256;
                    Bass.DeviceBufferLength = 1;
                    Bass.PlaybackBufferLength = 15;
                    Bass.UpdatePeriod = 2;
                    break;

                default: // Standard
                    // Conservative: -512 samples (~10.7 ms at 48 kHz).
                    // Safe for the widest range of devices including budget phones.
                    Bass.DevicePeriod = -512;
                    Bass.DeviceBufferLength = 1;
                    Bass.PlaybackBufferLength = 25;
                    Bass.UpdatePeriod = 2;
                    break;
            }
        }

        /// <summary>
        /// iOS audio latency configuration using CoreAudio (AVAudioSession).
        /// The preferred I/O buffer duration is set in <c>GameApplicationDelegate.FinishedLaunching</c>
        /// via <c>AVAudioSession.SetPreferredIOBufferDuration</c>. BASS DevicePeriod further tunes
        /// the internal mixing buffer. Combined with the 5ms AVAudioSession preference, this achieves
        /// ~3-5ms total output latency on modern iOS devices.
        /// </summary>
        private void configureIOSLatency(AudioLatencyMode mode)
        {
            switch (mode)
            {
                case AudioLatencyMode.Minimal:
                    // Aggressive: ~128 samples (~2.7 ms at 48 kHz) internal buffer.
                    // AVAudioSession is already configured for 5ms in GameApplicationDelegate.
                    Bass.Configure(ManagedBass.Configuration.DevicePeriod, -128);
                    Bass.DeviceBufferLength = 1;
                    Bass.PlaybackBufferLength = 10;
                    Bass.UpdatePeriod = 2;
                    break;

                case AudioLatencyMode.LowLatency:
                    // Low-latency: ~256 samples (~5.3 ms at 48 kHz).
                    Bass.Configure(ManagedBass.Configuration.DevicePeriod, -256);
                    Bass.DeviceBufferLength = 3;
                    Bass.PlaybackBufferLength = 15;
                    Bass.UpdatePeriod = 3;
                    break;

                default: // Standard
                    Bass.DeviceBufferLength = 5;
                    Bass.PlaybackBufferLength = 30;
                    Bass.UpdatePeriod = 3;
                    break;
            }
        }

        private void syncAudioDevices()
        {
            audioDevices = GetAllDevices();

            // Bass should always be providing "No sound" and "Default" device.
            Trace.Assert(audioDevices.Length >= BASS_INTERNAL_DEVICE_COUNT, "Bass did not provide any audio devices.");

            var oldDeviceNames = audioDeviceNames;
            var builder = ImmutableList.CreateBuilder<string>();

            for (int i = BASS_INTERNAL_DEVICE_COUNT; i < audioDevices.Length; i++)
            {
                if (audioDevices[i].IsEnabled)
                    builder.Add(audioDevices[i].Name);
            }

            var newDeviceNames = audioDeviceNames = builder.ToImmutable();

            scheduler.Add(() =>
            {
                if (cancelSource.IsCancellationRequested)
                    return;

                if (!IsCurrentDeviceValid())
                    initCurrentDevice();
            }, false);

            var oldSet = oldDeviceNames.Count > 0 ? new HashSet<string>(oldDeviceNames) : null;
            var newSet = newDeviceNames.Count > 0 ? new HashSet<string>(newDeviceNames) : null;

            var newDevices = new List<string>();
            var lostDevices = new List<string>();

            if (oldSet != null)
            {
                foreach (string name in newDeviceNames)
                {
                    if (!oldSet.Contains(name))
                        newDevices.Add(name);
                }
            }

            if (newSet != null)
            {
                foreach (string name in oldDeviceNames)
                {
                    if (!newSet.Contains(name))
                        lostDevices.Add(name);
                }
            }

            if (newDevices.Count > 0 || lostDevices.Count > 0)
            {
                eventScheduler.Add(delegate
                {
                    foreach (string d in newDevices)
                        OnNewDevice?.Invoke(d);
                    foreach (string d in lostDevices)
                        OnLostDevice?.Invoke(d);
                });
            }
        }

        /// <summary>
        /// Check whether any audio device changes have occurred.
        ///
        /// Changes supported are:
        /// - A new device is added
        /// - An existing device is Enabled/Disabled or set as Default
        /// </summary>
        /// <remarks>
        /// This method is optimised to incur the lowest overhead possible.
        /// </remarks>
        /// <param name="previousDevices">The previous audio devices array.</param>
        /// <returns>Whether a change was detected.</returns>
        protected virtual bool CheckForDeviceChanges(ImmutableArray<DeviceInfo> previousDevices)
        {
            int deviceCount = Bass.DeviceCount;

            if (previousDevices.Length != deviceCount)
                return true;

            for (int i = 0; i < deviceCount; i++)
            {
                var prevInfo = previousDevices[i];

                Bass.GetDeviceInfo(i, out var info);

                if (info.IsEnabled != prevInfo.IsEnabled)
                    return true;

                if (info.IsDefault != prevInfo.IsDefault)
                    return true;
            }

            return false;
        }

        protected virtual ImmutableArray<DeviceInfo> GetAllDevices()
        {
            int deviceCount = Bass.DeviceCount;

            var devices = ImmutableArray.CreateBuilder<DeviceInfo>(deviceCount);
            for (int i = 0; i < deviceCount; i++)
                devices.Add(Bass.GetDeviceInfo(i));

            return devices.MoveToImmutable();
        }

        // The current device is considered valid if it is enabled, initialized, and not a fallback device.
        protected virtual bool IsCurrentDeviceValid()
        {
            var device = audioDevices.ElementAtOrDefault(Bass.CurrentDevice);
            bool isFallback = string.IsNullOrEmpty(AudioDevice.Value) ? !device.IsDefault : device.Name != AudioDevice.Value;
            return device.IsEnabled && device.IsInitialized && !isFallback;
        }

        public override string ToString()
        {
            string deviceName = audioDevices.ElementAtOrDefault(Bass.CurrentDevice).Name;
            return $@"{GetType().ReadableName()} ({deviceName ?? "Unknown"})";
        }
    }
}
