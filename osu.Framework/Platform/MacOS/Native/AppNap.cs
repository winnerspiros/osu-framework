// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using osu.Framework.Logging;
using osu.Framework.Platform.Apple.Native;

namespace osu.Framework.Platform.MacOS.Native
{
    /// <summary>
    /// Prevents macOS App Nap from throttling latency-sensitive work.
    /// </summary>
    internal static partial class AppNap
    {
        private const ulong ns_activity_user_initiated = 0x00FFFFFF;
        private const ulong ns_activity_latency_critical = 0xFF00000000;

        private static readonly IntPtr class_process_info = Class.Get("NSProcessInfo");
        private static readonly IntPtr sel_process_info = Selector.Get("processInfo");
        private static readonly IntPtr sel_begin_activity = Selector.Get("beginActivityWithOptions:reason:");
        private static readonly IntPtr sel_end_activity = Selector.Get("endActivity:");
        private static readonly IntPtr sel_retain = Selector.Get("retain");
        private static readonly IntPtr sel_release = Selector.Get("release");

        private static readonly Lock activity_lock = new Lock();
        private static IntPtr activityToken;

        [LibraryImport(Interop.LIB_OBJ_C, EntryPoint = "objc_msgSend")]
        private static partial IntPtr sendBeginActivity(IntPtr receiver, IntPtr selector, ulong options, IntPtr reason);

        /// <summary>
        /// Prevents App Nap from throttling the process while the host is running.
        /// </summary>
        public static void Disable()
        {
            lock (activity_lock)
            {
                if (activityToken != IntPtr.Zero)
                    return;

                try
                {
                    using (NSAutoreleasePool.Init())
                    {
                        IntPtr processInfo = Interop.SendIntPtr(class_process_info, sel_process_info);
                        NSString reason = NSString.FromString("osu!framework: latency-critical audio/render");

                        activityToken = sendBeginActivity(processInfo, sel_begin_activity, ns_activity_user_initiated | ns_activity_latency_critical, reason.Handle);

                        if (activityToken == IntPtr.Zero)
                        {
                            Logger.Log("Failed to disable App Nap.", LoggingTarget.Runtime, LogLevel.Debug);
                            return;
                        }

                        Interop.SendIntPtr(activityToken, sel_retain);
                    }

                    Logger.Log("macOS App Nap disabled.", LoggingTarget.Runtime, LogLevel.Important);
                }
                catch (Exception ex)
                {
                    Logger.Log($"Failed to disable App Nap: {ex.Message}", LoggingTarget.Runtime, LogLevel.Debug);
                }
            }
        }

        /// <summary>
        /// Re-enables App Nap by ending the process activity.
        /// </summary>
        public static void Enable()
        {
            lock (activity_lock)
            {
                if (activityToken == IntPtr.Zero)
                    return;

                try
                {
                    IntPtr processInfo = Interop.SendIntPtr(class_process_info, sel_process_info);
                    Interop.SendVoid(processInfo, sel_end_activity, activityToken);
                    Interop.SendVoid(activityToken, sel_release);
                    activityToken = IntPtr.Zero;

                    Logger.Log("macOS App Nap re-enabled.", LoggingTarget.Runtime, LogLevel.Debug);
                }
                catch (Exception ex)
                {
                    Logger.Log($"Failed to re-enable App Nap: {ex.Message}", LoggingTarget.Runtime, LogLevel.Debug);
                }
            }
        }
    }
}
