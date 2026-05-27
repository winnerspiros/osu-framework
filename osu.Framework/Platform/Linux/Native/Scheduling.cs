// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Runtime.InteropServices;
using osu.Framework.Logging;

namespace osu.Framework.Platform.Linux.Native
{
    /// <summary>
    /// Provides access to Linux real-time scheduling APIs for latency-sensitive threads.
    /// </summary>
    internal static class Scheduling
    {
        private const int sched_fifo = 1;

        [StructLayout(LayoutKind.Sequential)]
        private struct sched_param
        {
#pragma warning disable IDE1006 // Matches the native Linux struct field name for P/Invoke compatibility.
            public int sched_priority;
#pragma warning restore IDE1006
        }

        [DllImport("libc", SetLastError = true)]
        private static extern int sched_setscheduler(int pid, int policy, ref sched_param param);

        /// <summary>
        /// Attempts to set the calling thread to SCHED_FIFO real-time scheduling.
        /// Requires CAP_SYS_NICE or appropriate rtprio ulimit.
        /// </summary>
        /// <param name="priority">RT priority (1-99, higher = more priority). 50 is typical for audio.</param>
        /// <returns><c>true</c> if successfully set, <c>false</c> otherwise.</returns>
        public static bool TrySetRealtimeScheduling(int priority = 50)
        {
            if (priority is < 1 or > 99)
                throw new ArgumentOutOfRangeException(nameof(priority), "SCHED_FIFO priority must be in the range 1-99.");

            try
            {
                var param = new sched_param { sched_priority = priority };
                int result = sched_setscheduler(0, sched_fifo, ref param);

                if (result == 0)
                {
                    Logger.Log($"Audio thread set to SCHED_FIFO (priority={priority}).", LoggingTarget.Runtime, LogLevel.Important);
                    return true;
                }

                int errno = Marshal.GetLastPInvokeError();
                Logger.Log($"Failed to set SCHED_FIFO (errno={errno}). Check CAP_SYS_NICE or /etc/security/limits.conf.", LoggingTarget.Runtime, LogLevel.Debug);
                return false;
            }
            catch (Exception ex)
            {
                Logger.Log($"SCHED_FIFO unavailable: {ex.Message}", LoggingTarget.Runtime, LogLevel.Debug);
                return false;
            }
        }
    }
}
