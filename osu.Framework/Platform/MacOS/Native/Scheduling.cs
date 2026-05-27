// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Runtime.InteropServices;
using osu.Framework.Logging;
using osu.Framework.Platform.Apple.Native;

namespace osu.Framework.Platform.MacOS.Native
{
    /// <summary>
    /// Provides access to macOS thread scheduling APIs for latency-sensitive threads.
    /// </summary>
    internal static partial class Scheduling
    {
        private const uint qos_class_user_interactive = 0x21;

        [LibraryImport(Interop.LIB_DL)]
        private static partial int pthread_set_qos_class_self_np(uint qosClass, int relativePriority);

        /// <summary>
        /// Attempts to raise the calling thread to the user-interactive QoS class.
        /// </summary>
        public static bool TrySetUserInteractiveQoS()
        {
            try
            {
                if (pthread_set_qos_class_self_np(qos_class_user_interactive, 0) == 0)
                {
                    Logger.Log("Audio thread QoS set to user-interactive.", LoggingTarget.Runtime, LogLevel.Debug);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to set audio thread QoS: {ex.Message}", LoggingTarget.Runtime, LogLevel.Debug);
                return false;
            }

            Logger.Log("Failed to set audio thread QoS.", LoggingTarget.Runtime, LogLevel.Debug);
            return false;
        }
    }
}
