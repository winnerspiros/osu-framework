// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Framework.Platform
{
    public interface IAndroidGraphicsSurface
    {
        /// <summary>
        /// Returns JNI environment handle.
        /// </summary>
        IntPtr JniEnvHandle { get; }

        /// <summary>
        /// Android Surface handle.
        /// </summary>
        /// <remarks>https://developer.android.com/reference/android/view/Surface.html</remarks>
        IntPtr SurfaceHandle { get; }

        /// <summary>
        /// Whether the Android surface is fully ready to be drawn into.
        /// </summary>
        /// <remarks>
        /// The default implementation returns <c>true</c> iff <see cref="SurfaceHandle"/> is non-zero,
        /// preserving source compatibility for external implementers.
        /// Android-specific implementations should additionally require that
        /// <c>surfaceChanged</c> has delivered non-zero dimensions and the app lifecycle is resumed.
        /// </remarks>
        bool IsSurfaceReady => SurfaceHandle != IntPtr.Zero;
    }
}
