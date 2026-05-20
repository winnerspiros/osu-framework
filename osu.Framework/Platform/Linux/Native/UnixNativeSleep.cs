// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Runtime.InteropServices;

namespace osu.Framework.Platform.Linux.Native
{
    internal class UnixNativeSleep : INativeSleep
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct TimeSpec
        {
            public nint Seconds;
            public nint NanoSeconds;
        }

        // clock_nanosleep with CLOCK_MONOTONIC is preferred over nanosleep because:
        // 1. It uses the monotonic clock, which is immune to NTP adjustments (nanosleep uses CLOCK_REALTIME).
        // 2. This gives more consistent sleep durations when the system clock is being stepped.
        [DllImport("libc", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        private static extern int clock_nanosleep(int clockId, int flags, in TimeSpec request, out TimeSpec remain);

        // Fallback for platforms where clock_nanosleep is unavailable (e.g. some embedded libc).
        [DllImport("libc", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        private static extern int nanosleep(in TimeSpec duration, out TimeSpec rem);

        private const int clock_monotonic = 1;
        private const int timer_reltime = 0;
        private const int interrupt_error = 4; // EINTR

        public static bool Available { get; private set; }

        private static bool useClockNanosleep;

        private static bool testNanoSleep()
        {
            TimeSpec test = new TimeSpec { Seconds = 0, NanoSeconds = 1 };

            try
            {
                // Prefer clock_nanosleep(CLOCK_MONOTONIC).
                if (clock_nanosleep(clock_monotonic, timer_reltime, in test, out _) == 0 ||
                    Marshal.GetLastPInvokeError() == interrupt_error)
                {
                    useClockNanosleep = true;
                    return true;
                }
            }
            catch { }

            // Fall back to nanosleep.
            try
            {
                nanosleep(in test, out _);
                return true;
            }
            catch
            {
                return false;
            }
        }

        static UnixNativeSleep()
        {
            Available = testNanoSleep();
        }

        public bool Sleep(TimeSpan duration)
        {
            const int ns_per_second = 1000 * 1000 * 1000;

            long ns = (long)duration.TotalNanoseconds;

            TimeSpec timeSpec = new TimeSpec
            {
                Seconds = (nint)(ns / ns_per_second),
                NanoSeconds = (nint)(ns % ns_per_second),
            };

            int ret;

            if (useClockNanosleep)
            {
                while ((ret = clock_nanosleep(clock_monotonic, timer_reltime, in timeSpec, out var remaining)) != 0
                       && Marshal.GetLastPInvokeError() == interrupt_error)
                {
                    // Interrupted by a signal — sleep for the remaining time.
                    timeSpec = remaining;
                }

                return ret == 0;
            }

            while ((ret = nanosleep(in timeSpec, out var remaining)) == -1 && Marshal.GetLastPInvokeError() == interrupt_error)
            {
                // The pause can be interrupted by a signal that was delivered to the thread.
                // Sleep again with remaining time if it happened.
                timeSpec = remaining;
            }

            return ret == 0; // Any errors other than interrupt_error should return false.
        }

        public void Dispose()
        {
        }
    }
}
