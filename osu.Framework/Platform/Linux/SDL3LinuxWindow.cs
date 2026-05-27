// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Logging;
using osu.Framework.Platform.SDL3;
using static SDL.SDL3;

namespace osu.Framework.Platform.Linux
{
    internal class SDL3LinuxWindow : SDL3DesktopWindow
    {
        public SDL3LinuxWindow(GraphicsSurfaceType surfaceType, string appName, bool bypassCompositor)
            : base(surfaceType, appName)
        {
            // X11: bypass compositor for reduced latency in fullscreen.
            SDL_SetHint(SDL_HINT_VIDEO_X11_NET_WM_BYPASS_COMPOSITOR, bypassCompositor ? "1"u8 : "0"u8).LogErrorIfFailed();

            // Wayland: prefer libdecor for consistent client-side decorations.
            SDL_SetHint("SDL_VIDEO_WAYLAND_PREFER_LIBDECOR"u8, "1"u8).LogErrorIfFailed();

            // Wayland: allow mode emulation for exclusive fullscreen.
            SDL_SetHint("SDL_VIDEO_WAYLAND_MODE_EMULATION"u8, "1"u8).LogErrorIfFailed();

            // Use unscaled relative mouse motion on Linux compositors.
            SDL_SetHint(SDL_HINT_MOUSE_RELATIVE_SYSTEM_SCALE, "0"u8).LogErrorIfFailed();

            Logger.Log($"Linux window created (surface={surfaceType}, bypassCompositor={bypassCompositor})", LoggingTarget.Runtime, LogLevel.Debug);
        }
    }
}
