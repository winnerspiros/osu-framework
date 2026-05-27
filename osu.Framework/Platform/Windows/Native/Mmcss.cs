// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Runtime.InteropServices;
using osu.Framework.Logging;

namespace osu.Framework.Platform.Windows.Native
{
    /// <summary>
    /// Provides access to the Windows Multimedia Class Scheduler Service (MMCSS).
    /// MMCSS boosts thread priority and provides guaranteed CPU time slices for
    /// multimedia threads, reducing audio glitching under load.
    /// </summary>
    internal static class Mmcss
    {
        [DllImport("avrt.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr AvSetMmThreadCharacteristics(string taskName, ref uint taskIndex);

        [DllImport("avrt.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AvRevertMmThreadCharacteristics(IntPtr avrtHandle);

        [DllImport("avrt.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AvSetMmThreadPriority(IntPtr avrtHandle, int priority);

        // AVRT_PRIORITY_CRITICAL = 2
        private const int avrt_priority_critical = 2;

        /// <summary>
        /// Registers the calling thread with MMCSS under the "Pro Audio" task.
        /// Returns a handle that must be passed to <see cref="RevertThreadCharacteristics"/> on thread exit.
        /// </summary>
        /// <returns>The MMCSS handle, or <see cref="IntPtr.Zero"/> if registration failed.</returns>
        public static IntPtr SetThreadCharacteristics()
        {
            try
            {
                uint taskIndex = 0;
                IntPtr handle = AvSetMmThreadCharacteristics("Pro Audio", ref taskIndex);

                if (handle == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    Logger.Log($"MMCSS registration failed (error={error}).", LoggingTarget.Runtime, LogLevel.Debug);
                    return IntPtr.Zero;
                }

                // Boost to critical priority within the MMCSS task group.
                AvSetMmThreadPriority(handle, avrt_priority_critical);

                Logger.Log("Audio thread registered with MMCSS (Pro Audio, critical priority).", LoggingTarget.Runtime, LogLevel.Important);
                return handle;
            }
            catch (Exception ex)
            {
                Logger.Log($"MMCSS unavailable: {ex.Message}", LoggingTarget.Runtime, LogLevel.Debug);
                return IntPtr.Zero;
            }
        }

        /// <summary>
        /// Reverts MMCSS thread characteristics. Should be called when the audio thread exits.
        /// </summary>
        public static void RevertThreadCharacteristics(IntPtr handle)
        {
            if (handle == IntPtr.Zero)
                return;

            try
            {
                AvRevertMmThreadCharacteristics(handle);
            }
            catch (Exception ex)
            {
                Logger.Log($"MMCSS revert failed: {ex.Message}", LoggingTarget.Runtime, LogLevel.Debug);
            }
        }
    }
}
