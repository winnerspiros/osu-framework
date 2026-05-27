// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;

namespace osu.Framework.Audio
{
    /// <summary>
    /// Controls the audio output latency profile used by the BASS audio engine.
    /// Each mode selects the lowest-latency backend available on the current platform.
    /// </summary>
    public enum AudioLatencyMode
    {
        /// <summary>
        /// Standard audio output using platform defaults.
        /// Provides the best compatibility with the widest range of hardware.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><b>Windows:</b> DirectSound (shared mode)</item>
        /// <item><b>Linux:</b> PulseAudio / ALSA (default buffer sizes)</item>
        /// <item><b>macOS:</b> CoreAudio (default buffer)</item>
        /// <item><b>Android:</b> AAudio with conservative buffer (~512 samples)</item>
        /// <item><b>iOS:</b> CoreAudio (AVAudioSession default category)</item>
        /// </list>
        /// </remarks>
        [Description("Standard")]
        Standard,

        /// <summary>
        /// Low-latency audio output using the best available backend for the platform.
        /// Reduces audio latency at the cost of slightly higher CPU usage and potential
        /// compatibility issues on some hardware.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><b>Windows:</b> WASAPI shared mode (event-driven, ~3–5 ms)</item>
        /// <item><b>Linux:</b> Native PipeWire pw_stream (~1–2 ms) or PipeWire/JACK via BASS (~5–10 ms)</item>
        /// <item><b>macOS:</b> Native CoreAudio AudioUnit (~2.7 ms) or CoreAudio via BASS (~3–5 ms)</item>
        /// <item><b>Android:</b> AAudio in low-latency performance mode (~10 ms via BASS_CONFIG_DEV_PERIOD=-256)</item>
        /// <item><b>iOS:</b> CoreAudio with reduced I/O buffer duration (~3–5 ms via BASS_CONFIG_DEV_PERIOD)</item>
        /// </list>
        /// </remarks>
        [Description("Low Latency")]
        LowLatency,

        /// <summary>
        /// Aggressive low-latency mode. Uses the smallest possible buffers on each platform.
        /// May cause audio glitches or crackling on underpowered devices or under heavy CPU load.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><b>Windows:</b> WASAPI exclusive mode (if supported) or shared with minimal period</item>
        /// <item><b>Linux:</b> Native PipeWire pw_stream (~0.5–1 ms) or JACK/PipeWire via BASS (~64–128 samples)</item>
        /// <item><b>macOS:</b> Native CoreAudio AudioUnit (~1.3 ms) or CoreAudio via BASS with minimum HAL buffer</item>
        /// <item><b>Android:</b> AAudio with AAUDIO_PERFORMANCE_MODE_LOW_LATENCY (-128 samples)</item>
        /// <item><b>iOS:</b> CoreAudio with 128-sample I/O buffer (~2.7 ms at 48 kHz)</item>
        /// </list>
        /// </remarks>
        [Description("Minimal (Aggressive)")]
        Minimal,
    }
}
