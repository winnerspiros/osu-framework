// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Runtime.InteropServices;
using ManagedBass;
using osu.Framework.Logging;

namespace osu.Framework.Audio.NativeOutput
{
    /// <summary>
    /// Native Core Audio AudioUnit bridge for sub-2ms audio output on macOS.
    /// Bypasses BASS's own device output by pulling mixed PCM data from the global BASS mixer
    /// into a real-time AudioUnit render callback.
    /// </summary>
    /// <remarks>
    /// This follows the same architectural pattern as the WASAPI bridge in <see cref="Threading.AudioThread"/>:
    /// a global BASS mixer is created in decode mode, and the native audio backend's real-time callback
    /// pulls frames from it via <see cref="Bass.ChannelGetData(int, IntPtr, int)"/>.
    /// </remarks>
    internal sealed class CoreAudioOutput : IDisposable
    {
        private const string lib_audio_toolbox = "/System/Library/Frameworks/AudioToolbox.framework/AudioToolbox";
        private const string lib_core_audio = "/System/Library/Frameworks/CoreAudio.framework/CoreAudio";

        #region Native Structs

        [StructLayout(LayoutKind.Sequential)]
        private struct AudioComponentDescription
        {
            public uint ComponentType;
            public uint ComponentSubType;
            public uint ComponentManufacturer;
            public uint ComponentFlags;
            public uint ComponentFlagsMask;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AudioStreamBasicDescription
        {
            public double SampleRate;
            public uint FormatID;
            public uint FormatFlags;
            public uint BytesPerPacket;
            public uint FramesPerPacket;
            public uint BytesPerFrame;
            public uint ChannelsPerFrame;
            public uint BitsPerChannel;
            public uint Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AudioBuffer
        {
            public uint NumberChannels;
            public uint DataByteSize;
            public IntPtr Data;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AudioBufferList
        {
            public uint NumberBuffers;
            public AudioBuffer Buffer0;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AudioTimeStamp
        {
            public double SampleTime;
            public ulong HostTime;
            public double RateScalar;
            public ulong WordClockTime;
            public int SmpteTimeType;
            public short SmpteTimeHours;
            public short SmpteTimeMinutes;
            public short SmpteTimeSeconds;
            public short SmpteTimeFrames;
            public uint SmpteTimeFlags;
            public uint SmpteTimeHoursSmpteTime;
            public uint Flags;
            public uint Reserved;
        }

        #endregion

        #region Native Constants

        // AudioUnit component types
        private const uint k_audio_unit_type_output = 0x61756F75; // 'auou'
        private const uint k_audio_unit_sub_type_default_output = 0x64656620; // 'def '
        private const uint k_audio_unit_manufacturer_apple = 0x6170706C; // 'appl'

        // AudioUnit properties
        private const uint k_audio_unit_property_stream_format = 8;
        private const uint k_audio_unit_property_set_render_callback = 23;
        private const uint k_audio_device_property_buffer_frame_size = 0x6673697A; // 'fsiz'
        private const uint k_audio_unit_scope_input = 1;

        // CoreAudio HAL properties
        private const uint k_audio_hardware_property_default_output_device = 0x646F7574; // 'dout'
        private const uint k_audio_object_system_object = 1;

        // Audio format flags
        private const uint k_audio_format_linear_pcm = 0x6C70636D; // 'lpcm'
        private const uint k_audio_format_flag_is_float = 1;
        private const uint k_audio_format_flag_is_packed = 8;

        #endregion

        #region Native Imports

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int AuRenderCallback(
            IntPtr inRefCon,
            ref uint ioActionFlags,
            ref AudioTimeStamp inTimeStamp,
            uint inBusNumber,
            uint inNumberFrames,
            IntPtr ioData);

        [StructLayout(LayoutKind.Sequential)]
        private struct AuRenderCallbackStruct
        {
            public IntPtr InputProc;
            public IntPtr InputProcRefCon;
        }

        [DllImport(lib_audio_toolbox)]
        private static extern IntPtr AudioComponentFindNext(IntPtr inComponent, ref AudioComponentDescription inDesc);

        [DllImport(lib_audio_toolbox)]
        private static extern int AudioComponentInstanceNew(IntPtr inComponent, out IntPtr outInstance);

        [DllImport(lib_audio_toolbox)]
        private static extern int AudioComponentInstanceDispose(IntPtr inInstance);

        [DllImport(lib_audio_toolbox)]
        private static extern int AudioUnitInitialize(IntPtr inUnit);

        [DllImport(lib_audio_toolbox)]
        private static extern int AudioUnitUninitialize(IntPtr inUnit);

        [DllImport(lib_audio_toolbox)]
        private static extern int AudioOutputUnitStart(IntPtr inUnit);

        [DllImport(lib_audio_toolbox)]
        private static extern int AudioOutputUnitStop(IntPtr inUnit);

        [DllImport(lib_audio_toolbox)]
        private static extern int AudioUnitSetProperty(
            IntPtr inUnit,
            uint inID,
            uint inScope,
            uint inElement,
            ref AudioStreamBasicDescription inData,
            uint inDataSize);

        [DllImport(lib_audio_toolbox)]
        private static extern int AudioUnitSetProperty(
            IntPtr inUnit,
            uint inID,
            uint inScope,
            uint inElement,
            ref AuRenderCallbackStruct inData,
            uint inDataSize);

        [DllImport(lib_core_audio)]
        private static extern int AudioObjectGetPropertyData(
            uint inObjectID,
            ref AudioObjectPropertyAddress inAddress,
            uint inQualifierDataSize,
            IntPtr inQualifierData,
            ref uint ioDataSize,
            out uint outData);

        [DllImport(lib_core_audio)]
        private static extern int AudioObjectSetPropertyData(
            uint inObjectID,
            ref AudioObjectPropertyAddress inAddress,
            uint inQualifierDataSize,
            IntPtr inQualifierData,
            uint inDataSize,
            ref uint inData);

        [StructLayout(LayoutKind.Sequential)]
        private struct AudioObjectPropertyAddress
        {
            public uint Selector;
            public uint Scope;
            public uint Element;
        }

        private const uint k_audio_object_property_scope_global = 0x676C6F62; // 'glob'
        private const uint k_audio_object_property_element_main = 0;
        private const uint k_audio_object_property_scope_output = 0x6F757470; // 'outp'

        #endregion

        private IntPtr audioUnit;
        private AuRenderCallback? renderCallbackDelegate;
        private readonly Func<int?> getMixerHandle;
        private int sampleRate;

        /// <summary>
        /// Whether the native CoreAudio output is currently active and pulling audio.
        /// </summary>
        public bool IsRunning { get; private set; }

        /// <summary>
        /// Creates a new CoreAudio AudioUnit output bridge.
        /// </summary>
        /// <param name="getMixerHandle">
        /// Function returning the current global BASS mixer handle, or null if not yet available.
        /// </param>
        public CoreAudioOutput(Func<int?> getMixerHandle)
        {
            this.getMixerHandle = getMixerHandle;
        }

        /// <summary>
        /// Initialises and starts the AudioUnit output with the specified buffer size.
        /// </summary>
        /// <param name="bufferFrames">
        /// Preferred buffer size in frames. Lower = lower latency. 32-128 frames at 48kHz gives sub-2ms.
        /// CoreAudio will honour the nearest power-of-two the hardware supports.
        /// </param>
        /// <param name="requestedSampleRate">Sample rate to request (default 48000).</param>
        /// <returns>True if initialisation succeeded.</returns>
        public bool Start(int bufferFrames = 64, int requestedSampleRate = 48000)
        {
            if (IsRunning)
                return true;

            sampleRate = requestedSampleRate;

            try
            {
                // Find the default output AudioUnit component.
                var desc = new AudioComponentDescription
                {
                    ComponentType = k_audio_unit_type_output,
                    ComponentSubType = k_audio_unit_sub_type_default_output,
                    ComponentManufacturer = k_audio_unit_manufacturer_apple,
                };

                IntPtr component = AudioComponentFindNext(IntPtr.Zero, ref desc);

                if (component == IntPtr.Zero)
                {
                    Logger.Log("CoreAudio: Failed to find default output AudioUnit component.", LoggingTarget.Runtime, LogLevel.Error);
                    return false;
                }

                int status = AudioComponentInstanceNew(component, out audioUnit);

                if (status != 0 || audioUnit == IntPtr.Zero)
                {
                    Logger.Log($"CoreAudio: Failed to create AudioUnit instance (status={status}).", LoggingTarget.Runtime, LogLevel.Error);
                    return false;
                }

                // Set the buffer size on the hardware device for lowest latency.
                setDeviceBufferFrameSize(bufferFrames);

                // Set the stream format: 32-bit float, interleaved stereo.
                var streamFormat = new AudioStreamBasicDescription
                {
                    SampleRate = sampleRate,
                    FormatID = k_audio_format_linear_pcm,
                    FormatFlags = k_audio_format_flag_is_float | k_audio_format_flag_is_packed,
                    BytesPerPacket = 8, // 2 channels * 4 bytes
                    FramesPerPacket = 1,
                    BytesPerFrame = 8,
                    ChannelsPerFrame = 2,
                    BitsPerChannel = 32,
                };

                status = AudioUnitSetProperty(
                    audioUnit,
                    k_audio_unit_property_stream_format,
                    k_audio_unit_scope_input,
                    0, // output element
                    ref streamFormat,
                    (uint)Marshal.SizeOf<AudioStreamBasicDescription>());

                if (status != 0)
                {
                    Logger.Log($"CoreAudio: Failed to set stream format (status={status}).", LoggingTarget.Runtime, LogLevel.Error);
                    Dispose();
                    return false;
                }

                // Set the render callback — prevents GC collection by holding delegate reference.
                renderCallbackDelegate = renderCallback;
                var callbackStruct = new AuRenderCallbackStruct
                {
                    InputProc = Marshal.GetFunctionPointerForDelegate(renderCallbackDelegate),
                    InputProcRefCon = IntPtr.Zero,
                };

                status = AudioUnitSetProperty(
                    audioUnit,
                    k_audio_unit_property_set_render_callback,
                    k_audio_unit_scope_input,
                    0,
                    ref callbackStruct,
                    (uint)Marshal.SizeOf<AuRenderCallbackStruct>());

                if (status != 0)
                {
                    Logger.Log($"CoreAudio: Failed to set render callback (status={status}).", LoggingTarget.Runtime, LogLevel.Error);
                    Dispose();
                    return false;
                }

                // Initialize and start.
                status = AudioUnitInitialize(audioUnit);

                if (status != 0)
                {
                    Logger.Log($"CoreAudio: Failed to initialize AudioUnit (status={status}).", LoggingTarget.Runtime, LogLevel.Error);
                    Dispose();
                    return false;
                }

                status = AudioOutputUnitStart(audioUnit);

                if (status != 0)
                {
                    Logger.Log($"CoreAudio: Failed to start AudioUnit (status={status}).", LoggingTarget.Runtime, LogLevel.Error);
                    Dispose();
                    return false;
                }

                IsRunning = true;
                Logger.Log($"CoreAudio: Native AudioUnit output started (buffer={bufferFrames} frames, rate={sampleRate} Hz, latency\u2248{bufferFrames * 1000.0 / sampleRate:F1}ms).",
                    LoggingTarget.Runtime, LogLevel.Important);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"CoreAudio: Exception during initialisation: {ex.Message}", LoggingTarget.Runtime, LogLevel.Error);
                Dispose();
                return false;
            }
        }

        /// <summary>
        /// Sets the hardware I/O buffer size on the default output device for minimum latency.
        /// </summary>
        private void setDeviceBufferFrameSize(int frames)
        {
            // Get the default output device ID.
            var address = new AudioObjectPropertyAddress
            {
                Selector = k_audio_hardware_property_default_output_device,
                Scope = k_audio_object_property_scope_global,
                Element = k_audio_object_property_element_main,
            };

            uint dataSize = sizeof(uint);
            int status = AudioObjectGetPropertyData(k_audio_object_system_object, ref address, 0, IntPtr.Zero, ref dataSize, out uint deviceID);

            if (status != 0)
            {
                Logger.Log($"CoreAudio: Could not get default output device (status={status}).", LoggingTarget.Runtime, LogLevel.Debug);
                return;
            }

            // Set the buffer frame size on the device.
            var bufferAddress = new AudioObjectPropertyAddress
            {
                Selector = k_audio_device_property_buffer_frame_size,
                Scope = k_audio_object_property_scope_output,
                Element = k_audio_object_property_element_main,
            };

            uint bufferSize = (uint)frames;
            status = AudioObjectSetPropertyData(deviceID, ref bufferAddress, 0, IntPtr.Zero, sizeof(uint), ref bufferSize);

            Logger.Log(status != 0
                ? $"CoreAudio: Could not set buffer frame size to {frames} (status={status}). Hardware will use its default."
                : $"CoreAudio: Hardware buffer frame size set to {frames}.",
                LoggingTarget.Runtime, LogLevel.Debug);
        }

        /// <summary>
        /// The real-time render callback invoked by CoreAudio's I/O thread.
        /// Pulls interleaved float PCM data from the global BASS mixer.
        /// </summary>
        private int renderCallback(
            IntPtr inRefCon,
            ref uint ioActionFlags,
            ref AudioTimeStamp inTimeStamp,
            uint inBusNumber,
            uint inNumberFrames,
            IntPtr ioData)
        {
            // ioData points to an AudioBufferList. We need to fill its buffer with PCM data.
            var bufferList = Marshal.PtrToStructure<AudioBufferList>(ioData);

            int? mixerHandle = getMixerHandle();

            if (mixerHandle == null)
            {
                // No mixer available yet — output silence.
                unsafe
                {
                    new Span<byte>((void*)bufferList.Buffer0.Data, (int)bufferList.Buffer0.DataByteSize).Clear();
                }

                return 0;
            }

            int bytesNeeded = (int)(inNumberFrames * 8); // stereo float = 8 bytes per frame

            // Pull data from the BASS mixer. BASS_DATA_FLOAT ensures float output matching our format.
            int bytesRead = Bass.ChannelGetData(mixerHandle.Value, bufferList.Buffer0.Data, bytesNeeded | (int)DataFlags.Float);

            if (bytesRead < 0)
                bytesRead = 0;

            // Zero-fill any remainder if BASS returned less data than requested.
            if (bytesRead < bytesNeeded)
            {
                unsafe
                {
                    byte* ptr = (byte*)bufferList.Buffer0.Data + bytesRead;
                    new Span<byte>(ptr, bytesNeeded - bytesRead).Clear();
                }
            }

            return 0; // noErr
        }

        /// <summary>
        /// Stops the AudioUnit output and releases resources.
        /// </summary>
        public void Stop()
        {
            if (!IsRunning)
                return;

            IsRunning = false;

            if (audioUnit != IntPtr.Zero)
            {
                AudioOutputUnitStop(audioUnit);
                AudioUnitUninitialize(audioUnit);
                AudioComponentInstanceDispose(audioUnit);
                audioUnit = IntPtr.Zero;
            }

            renderCallbackDelegate = null;
            Logger.Log("CoreAudio: Native AudioUnit output stopped.", LoggingTarget.Runtime, LogLevel.Important);
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
