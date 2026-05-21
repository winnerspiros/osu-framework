// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using osu.Framework.Graphics.OpenGL;
using osu.Framework.Logging;
using SDL;
using static SDL.SDL3;

namespace osu.Framework.Platform.SDL3
{
    internal unsafe class SDL3GraphicsSurface : IGraphicsSurface, IOpenGLGraphicsSurface, IMetalGraphicsSurface, ILinuxGraphicsSurface, IAndroidGraphicsSurface
    {
        private readonly SDL3Window window;

        private SDL_GLContextState* context;

        public IntPtr WindowHandle => window.WindowHandle;

        public GraphicsSurfaceType Type { get; }

        public SDL3GraphicsSurface(SDL3Window window, GraphicsSurfaceType surfaceType)
        {
            this.window = window;
            Type = surfaceType;

            switch (surfaceType)
            {
                case GraphicsSurfaceType.OpenGL:
                    SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_RED_SIZE, 8).ThrowIfFailed();
                    SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_GREEN_SIZE, 8).ThrowIfFailed();
                    SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_BLUE_SIZE, 8).ThrowIfFailed();
                    SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_ACCUM_ALPHA_SIZE, 0).ThrowIfFailed();
                    SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_DEPTH_SIZE, 16).ThrowIfFailed();
                    SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_STENCIL_SIZE, 8).ThrowIfFailed();
                    if (OperatingSystem.IsAndroid())
                    {
                        // Avoid driver-selected sRGB framebuffer behaviour causing colour output shifts on some Android devices.
                        SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_FRAMEBUFFER_SRGB_CAPABLE, 0).LogErrorIfFailed();
                    }
                    // Explicitly request hardware-accelerated double-buffering.
                    // Without this, some drivers (especially Mesa on Linux) may silently fall back
                    // to software rendering or enable triple-buffering (one extra frame of latency).
                    SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_DOUBLEBUFFER, 1).ThrowIfFailed();
                    SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_ACCELERATED_VISUAL, 1).ThrowIfFailed();
                    break;

                case GraphicsSurfaceType.Vulkan:
                case GraphicsSurfaceType.Metal:
                case GraphicsSurfaceType.Direct3D11:
                case GraphicsSurfaceType.Direct3D12:
                    break;

                default:
                    throw new ArgumentException($"Unexpected graphics surface: {Type}.", nameof(surfaceType));
            }
        }

        public void Initialise()
        {
            if (Type == GraphicsSurfaceType.OpenGL)
                initialiseOpenGL();
        }

        public Size GetDrawableSize()
        {
            int width, height;
            SDL_GetWindowSizeInPixels(window.SDLWindowHandle, &width, &height).ThrowIfFailed();
            return new Size(width, height);
        }

        #region OpenGL-specific implementation

        private void initialiseOpenGL()
        {
            if (RuntimeInfo.IsMobile)
            {
                SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_CONTEXT_PROFILE_MASK, (int)SDL_GLProfile.SDL_GL_CONTEXT_PROFILE_ES).ThrowIfFailed();

                // Minimum OpenGL version for ES profile:
                SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_CONTEXT_MAJOR_VERSION, 3).ThrowIfFailed();
                SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_CONTEXT_MINOR_VERSION, 0).ThrowIfFailed();
            }
            else
            {
                SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_CONTEXT_PROFILE_MASK, (int)SDL_GLProfile.SDL_GL_CONTEXT_PROFILE_CORE).ThrowIfFailed();

                // Minimum OpenGL version for core profile:
                SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_CONTEXT_MAJOR_VERSION, 3).ThrowIfFailed();
                SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_CONTEXT_MINOR_VERSION, 2).ThrowIfFailed();
            }

            context = SDL_GL_CreateContext(window.SDLWindowHandle);

#pragma warning disable IDE0270 // Null check can be simplified - pointer types don't support ??
            if (context == null)
                throw new InvalidOperationException($"Failed to create an SDL3 GL context ({SDL_GetError()})");
#pragma warning restore IDE0270

            SDL_GL_MakeCurrent(window.SDLWindowHandle, context).ThrowIfFailed();

            if (OperatingSystem.IsAndroid())
                tryEnableEglFrontBufferAutoRefresh();

            loadBindings();
        }

        /// <summary>
        /// Attempts to enable <c>EGL_ANDROID_front_buffer_auto_refresh</c> on the current EGL surface.
        /// When enabled, the display auto-refreshes from the front buffer, reducing latency
        /// in unlocked frame rate (non-VSync) scenarios on Android.
        /// </summary>
        [SupportedOSPlatform("android")]
        private void tryEnableEglFrontBufferAutoRefresh()
        {
            const int egl_extensions = 3;
            const int egl_true = 1;
            const int egl_front_buffer_auto_refresh_android = 0x314C;

            try
            {
                IntPtr eglDisplay = SDL_EGL_GetCurrentDisplay();
                IntPtr eglSurface = SDL_EGL_GetWindowSurface(window.SDLWindowHandle);

                if (eglDisplay == IntPtr.Zero || eglSurface == IntPtr.Zero)
                {
                    Logger.Log("EGL front buffer auto-refresh: could not obtain EGL display/surface.", LoggingTarget.Runtime, LogLevel.Debug);
                    return;
                }

                // Check extension availability before calling eglSurfaceAttrib.
                IntPtr extensionsPtr = eglQueryString(eglDisplay, egl_extensions);

                if (extensionsPtr == IntPtr.Zero)
                    return;

                string? extensions = Marshal.PtrToStringAnsi(extensionsPtr);

                if (extensions == null || !extensions.Contains("EGL_ANDROID_front_buffer_auto_refresh"))
                {
                    Logger.Log("EGL_ANDROID_front_buffer_auto_refresh extension not available.", LoggingTarget.Runtime, LogLevel.Debug);
                    return;
                }

                int result = eglSurfaceAttrib(eglDisplay, eglSurface, egl_front_buffer_auto_refresh_android, egl_true);

                Logger.Log($"EGL front buffer auto-refresh: {(result != 0 ? "enabled" : "failed to enable")}.", LoggingTarget.Runtime, LogLevel.Important);
            }
            catch (Exception ex)
            {
                Logger.Log($"EGL front buffer auto-refresh: exception during setup: {ex.Message}", LoggingTarget.Runtime, LogLevel.Debug);
            }
        }

        [DllImport("libEGL", EntryPoint = "eglSurfaceAttrib")]
        private static extern int eglSurfaceAttrib(IntPtr display, IntPtr surface, int attribute, int value);

        [DllImport("libEGL", EntryPoint = "eglQueryString")]
        private static extern IntPtr eglQueryString(IntPtr display, int name);

        private void loadBindings()
        {
            GL.Initialise(this);
        }

        int? IOpenGLGraphicsSurface.BackbufferFramebuffer
        {
            get
            {
                if (window.SDLWindowHandle == null)
                    return null;

                var props = SDL_GetWindowProperties(window.SDLWindowHandle);

                if (SDL_HasProperty(props, SDL_PROP_WINDOW_UIKIT_OPENGL_FRAMEBUFFER_NUMBER))
                    return (int)SDL_GetNumberProperty(props, SDL_PROP_WINDOW_UIKIT_OPENGL_FRAMEBUFFER_NUMBER, 0);

                return null;
            }
        }

        // cache value locally as requesting from SDL is not free.
        // it is assumed that we are the only thing changing vsync modes.
        private bool? verticalSync;

        bool IOpenGLGraphicsSurface.VerticalSync
        {
            get
            {
                if (verticalSync != null)
                    return verticalSync.Value;

                int interval;
                SDL_GL_GetSwapInterval(&interval);
                return (verticalSync = interval != 0).Value;
            }
            set
            {
                if (value)
                {
                    // Prefer adaptive VSync (-1) which tears instead of stalling when the frame arrives late,
                    // giving lower perceived latency when running slightly below the display refresh rate.
                    // Fall back to standard VSync (1) if the driver or platform does not support it.
                    if (!SDL_GL_SetSwapInterval(-1))
                    {
                        Logger.Log("Adaptive VSync (-1) not supported; falling back to standard VSync.", LoggingTarget.Runtime, LogLevel.Debug);

                        if (!SDL_GL_SetSwapInterval(1))
                        {
                            Logger.Log($"Standard VSync (1) also failed: {SDL_GetError()}", LoggingTarget.Runtime, LogLevel.Important);
                            return; // leave verticalSync cache unchanged so callers know the state
                        }
                    }
                }
                else
                {
                    SDL_GL_SetSwapInterval(0).LogErrorIfFailed();
                }

                verticalSync = value;
            }
        }

        IntPtr IOpenGLGraphicsSurface.WindowContext => (IntPtr)context;
        IntPtr IOpenGLGraphicsSurface.CurrentContext => (IntPtr)SDL_GL_GetCurrentContext();

        void IOpenGLGraphicsSurface.SwapBuffers() => SDL_GL_SwapWindow(window.SDLWindowHandle);
        void IOpenGLGraphicsSurface.CreateContext() => SDL_GL_CreateContext(window.SDLWindowHandle);
        void IOpenGLGraphicsSurface.DeleteContext(IntPtr context) => SDL_GL_DestroyContext((SDL_GLContextState*)context);
        void IOpenGLGraphicsSurface.MakeCurrent(IntPtr context) => SDL_GL_MakeCurrent(window.SDLWindowHandle, (SDL_GLContextState*)context);
        void IOpenGLGraphicsSurface.ClearCurrent() => SDL_GL_MakeCurrent(window.SDLWindowHandle, null);
        IntPtr IOpenGLGraphicsSurface.GetProcAddress(string symbol) => SDL_GL_GetProcAddress(symbol);

        #endregion

        #region Metal-specific implementation

        IntPtr IMetalGraphicsSurface.CreateMetalView() => SDL_Metal_CreateView(window.SDLWindowHandle);

        #endregion

        #region Linux-specific implementation

        bool ILinuxGraphicsSurface.IsWayland => window.IsWayland;

        [SupportedOSPlatform("linux")]
        IntPtr ILinuxGraphicsSurface.DisplayHandle => window.DisplayHandle;

        #endregion

        #region Android-specific implementation

        [SupportedOSPlatform("android")]
        IntPtr IAndroidGraphicsSurface.JniEnvHandle => SDL_GetAndroidJNIEnv().ThrowIfFailed();

        [SupportedOSPlatform("android")]
        IntPtr IAndroidGraphicsSurface.SurfaceHandle => window.SurfaceHandle;

        [SupportedOSPlatform("android")]
        bool IAndroidGraphicsSurface.IsSurfaceReady => window.IsSurfaceReady;

        #endregion
    }
}
