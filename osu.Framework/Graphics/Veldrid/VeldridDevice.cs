// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using osu.Framework.Development;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Logging;
using osu.Framework.Platform;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Veldrid;
using Veldrid.OpenGL;
using Veldrid.OpenGLBindings;

namespace osu.Framework.Graphics.Veldrid
{
    /// <summary>
    /// A Veldrid graphics device that provides support for device pipelines.
    /// </summary>
    internal class VeldridDevice
    {
        /// <summary>
        /// The platform graphics device.
        /// </summary>
        public GraphicsDevice Device { get; }

        /// <summary>
        /// The platform graphics resource factory.
        /// </summary>
        public ResourceFactory Factory
            => Device.ResourceFactory;

        /// <summary>
        /// The graphics surface type.
        /// </summary>
        public GraphicsSurfaceType SurfaceType
            => graphicsSurface.Type;

        /// <summary>
        /// Enables or disables vertical sync.
        /// </summary>
        public bool VerticalSync
        {
            get => Device.SyncToVerticalBlank;
            set => Device.SyncToVerticalBlank = value;
        }

        /// <summary>
        /// Gets or sets whether the device should render new frames without waiting for previous ones to finish compositing.
        /// </summary>
        public bool AllowTearing
        {
            get => Device.AllowTearing;
            set => Device.AllowTearing = value;
        }

        /// <summary>
        /// Whether the depth is in the range [0, 1] (i.e. Reversed-Z). If <c>false</c>, depth is in the range [-1, 1].
        /// </summary>
        public bool IsDepthRangeZeroToOne
            => Device.IsDepthRangeZeroToOne;

        /// <summary>
        /// Whether the texture coordinates begin in the top-left of the texture. If <c>false</c>, (0, 0) corresponds to the bottom-left texel of the texture.
        /// </summary>
        public bool IsUvOriginTopLeft
            => Device.IsUvOriginTopLeft;

        /// <summary>
        /// Whether the y-coordinate ranges from -1 (top) to 1 (bottom). If <c>false</c>, the y-coordinate ranges from -1 (bottom) to 1 (top).
        /// </summary>
        public bool IsClipSpaceYInverted
            => Device.IsClipSpaceYInverted;

        /// <summary>
        /// Whether shader storage buffer objects can be used.
        /// </summary>
        public bool UseStructuredBuffers
            => !FrameworkEnvironment.NoStructuredBuffers && Device.Features.StructuredBuffer;

        /// <summary>
        /// The maximum allowed texture size.
        /// </summary>
        public int MaxTextureSize { get; }

        private readonly IGraphicsSurface graphicsSurface;
        private Vector2I currentWindowSize;

        /// <summary>
        /// Number of consecutive surface-lost / swapchain failures observed in <see cref="SwapBuffers"/>.
        /// Reset to zero on any successful present. If this exceeds <see cref="max_consecutive_swapchain_failures"/>
        /// the exception is rethrown rather than swallowed, so a permanently dead device still surfaces as a crash
        /// instead of an infinite recovery loop.
        /// </summary>
        private int consecutiveSwapchainFailures;

        // Kept low (5) on purpose. The winnerspiros/veldrid fork now bounds vkAcquireNextImageKHR with a 100 ms
        // timeout and recreates the swapchain on VK_TIMEOUT/VK_NOT_READY, so a genuine surface-lost recovers in
        // 1–2 frames. Anything beyond a small handful of consecutive failures means the device is really dead and
        // it is better to surface a crash than to keep swallowing exceptions for a full second per drop, which is
        // long enough to trip the Android ANR / hang watchdogs the longer 60-frame loop was itself causing.
        private const int max_consecutive_swapchain_failures = 5;

        /// <summary>
        /// Creates a new <see cref="VeldridDevice"/>
        /// </summary>
        /// <param name="graphicsSurface"></param>
        /// <param name="pipelineCacheData">
        /// Optional pre-warmed VkPipelineCache blob (from a previous run, persisted to disk). Only
        /// consulted on the Vulkan backend; ignored otherwise. Pass <c>null</c> on first launch.
        /// The Vulkan driver header-validates the blob (vendorID / deviceID / driver UUID) and
        /// silently discards stale data, so the consumer doesn't need to do its own version checking.
        /// </param>
        /// <exception cref="InvalidOperationException"></exception>
        public VeldridDevice(IGraphicsSurface graphicsSurface, byte[]? pipelineCacheData = null)
        {
            // Veldrid must either be initialised on the main/"input" thread, or in a separate thread away from the draw thread at least.
            // Otherwise the window may not render anything on some platforms (macOS at least).
            Debug.Assert(!ThreadSafety.IsDrawThread, "Veldrid cannot be initialised on the draw thread.");

            this.graphicsSurface = graphicsSurface;

            var options = new GraphicsDeviceOptions
            {
                HasMainSwapchain = true,
                SwapchainDepthFormat = PixelFormat.R16UNorm,
                SyncToVerticalBlank = true,
                ResourceBindingModel = ResourceBindingModel.Improved,
            };

            var size = this.graphicsSurface.GetDrawableSize();

            var swapchain = new SwapchainDescription
            {
                Width = (uint)size.Width,
                Height = (uint)size.Height,
                ColorSrgb = options.SwapchainSrgbFormat,
                DepthFormat = options.SwapchainDepthFormat,
                SyncToVerticalBlank = options.SyncToVerticalBlank,
            };

            int maxTextureSize;

            switch (RuntimeInfo.OS)
            {
                case RuntimeInfo.Platform.Windows:
                {
                    swapchain.Source = SwapchainSource.CreateWin32(this.graphicsSurface.WindowHandle, IntPtr.Zero);
                    break;
                }

                case RuntimeInfo.Platform.macOS:
                {
                    // OpenGL doesn't use a swapchain, so it's only needed on Metal.
                    // Creating a Metal surface in general would otherwise destroy the GL context.
                    if (this.graphicsSurface.Type == GraphicsSurfaceType.Metal)
                    {
                        var metalGraphics = (IMetalGraphicsSurface)this.graphicsSurface;
                        swapchain.Source = SwapchainSource.CreateNSView(metalGraphics.CreateMetalView());
                    }

                    break;
                }

                case RuntimeInfo.Platform.iOS:
                {
                    // OpenGL doesn't use a swapchain, so it's only needed on Metal.
                    // Creating a Metal surface in general would otherwise destroy the GL context.
                    if (this.graphicsSurface.Type == GraphicsSurfaceType.Metal)
                    {
                        var metalGraphics = (IMetalGraphicsSurface)this.graphicsSurface;
                        swapchain.Source = SwapchainSource.CreateUIView(metalGraphics.CreateMetalView());
                    }

                    break;
                }

                case RuntimeInfo.Platform.Linux:
                {
                    var linuxGraphics = (ILinuxGraphicsSurface)this.graphicsSurface;
                    swapchain.Source = linuxGraphics.IsWayland
                        ? SwapchainSource.CreateWayland(linuxGraphics.DisplayHandle, this.graphicsSurface.WindowHandle)
                        : SwapchainSource.CreateXlib(linuxGraphics.DisplayHandle, this.graphicsSurface.WindowHandle);
                    break;
                }

                case RuntimeInfo.Platform.Android:
                {
                    var androidGraphics = (IAndroidGraphicsSurface)this.graphicsSurface;

                    // Android SurfaceView's native surface is not always ready when the SDL thread reaches
                    // VeldridDevice initialisation. On some OEMs (notably Adreno-based devices),
                    // surfaceCreated fires with the holder's Surface.Handle already non-zero while the
                    // underlying ANativeWindow is still 0×0; surfaceChanged delivers the real dimensions
                    // shortly after. Polling only for a non-zero SurfaceHandle would proceed immediately
                    // against the unsized window, baking a 0×0 swapchain that causes a permanent black
                    // screen with no crash. We therefore gate on IsSurfaceReady (which additionally
                    // requires surfaceChanged to have reported non-zero dimensions and the app lifecycle
                    // to be resumed) rather than just a non-zero handle.
                    // Note: this is a one-time startup-only blocking wait on the SDL/input thread; the
                    // surface is empirically ready within a few hundred ms, and 5s is just an upper
                    // bound to fail fast with a managed exception rather than hang indefinitely.
                    const int max_wait_ms = 5000;
                    const int poll_interval_ms = 50;
                    int waited = 0;

                    while (!androidGraphics.IsSurfaceReady && waited < max_wait_ms)
                    {
                        Thread.Sleep(poll_interval_ms);
                        waited += poll_interval_ms;
                    }

                    // Stability wait: even after IsSurfaceReady becomes true, AndroidGameSurface.SurfaceChanged
                    // may be racing with OsuGameActivity.SurfaceChanged which calls SetFormat(RGBA8888) if the
                    // surface was born RGB565. During the resulting SurfaceDestroyed→SurfaceCreated cycle,
                    // vkGetPhysicalDeviceSurfaceCapabilitiesKHR returns dp-scaled dimensions (e.g. 1029×480 on
                    // a 3088×1440 device). Sleeping 150 ms lets the teardown+recreate cycle complete; the
                    // re-poll ensures the surface is still ready before we proceed to vkCreateSwapchainKHR.
                    if (OperatingSystem.IsAndroid() && androidGraphics.IsSurfaceReady)
                    {
                        Thread.Sleep(150);

                        while (!androidGraphics.IsSurfaceReady && waited < max_wait_ms)
                        {
                            Thread.Sleep(poll_interval_ms);
                            waited += poll_interval_ms;
                        }
                    }

                    if (!androidGraphics.IsSurfaceReady)
                    {
                        // Distinguish between the two failure modes so future reports are easier to triage.
                        string reason = androidGraphics.SurfaceHandle == IntPtr.Zero
                            ? "SurfaceView.surfaceCreated has not fired — the Android Surface handle is still null."
                            : "SurfaceView.surfaceCreated fired but surfaceChanged with non-zero dimensions has not — the surface is still 0×0 or the app is not resumed.";
                        throw new InvalidOperationException(
                            $"Android surface was not ready within {max_wait_ms} ms. {reason} Cannot create the Vulkan swapchain.");
                    }

                    // Re-read the drawable size now that the surface is confirmed ready; the size captured
                    // earlier (before the poll) may have been 0×0 on Adreno devices where surfaceChanged
                    // fires after the initial GetDrawableSize() call.
                    var readySize = this.graphicsSurface.GetDrawableSize();
                    swapchain.Width = (uint)readySize.Width;
                    swapchain.Height = (uint)readySize.Height;

                    Logger.Log($"Android surface ready after {waited} ms — drawable size {readySize.Width}×{readySize.Height}.", level: LogLevel.Important);

                    // Re-snapshot immediately before use to narrow the race with a concurrent
                    // SurfaceView.surfaceDestroyed zeroing the handle between our last poll and
                    // the native vkCreateAndroidSurfaceKHR call. This does not eliminate the race
                    // (the underlying ANativeWindow can still be released by the platform after
                    // we read the handle), but it prevents the most common case of forwarding a
                    // stale non-zero pointer that has just been invalidated. A managed exception
                    // is preferable to a SIGSEGV inside the Vulkan driver.
                    // The JNIEnv handle is owned by SDL for the lifetime of the SDL thread and
                    // is not subject to the same lifetime concern; SDL_GetAndroidJNIEnv would
                    // throw if it returned null, so no extra guard is needed for it.
                    IntPtr surfaceHandleAtCreation = androidGraphics.SurfaceHandle;
                    IntPtr jniEnvHandle = androidGraphics.JniEnvHandle;

                    if (surfaceHandleAtCreation == IntPtr.Zero)
                        throw new InvalidOperationException(
                            "Android surface handle became invalid between availability check and swapchain creation. " +
                            "SurfaceView was likely destroyed concurrently — cannot create the Vulkan swapchain.");

                    swapchain.Source = SwapchainSource.CreateAndroidSurface(surfaceHandleAtCreation, jniEnvHandle);
                    break;
                }
            }

            switch (this.graphicsSurface.Type)
            {
                case GraphicsSurfaceType.OpenGL:
                    var openGLGraphics = (IOpenGLGraphicsSurface)this.graphicsSurface;
                    var openGLInfo = new OpenGLPlatformInfo(
                        openGLContextHandle: openGLGraphics.WindowContext,
                        getProcAddress: openGLGraphics.GetProcAddress,
                        makeCurrent: openGLGraphics.MakeCurrent,
                        getCurrentContext: () => openGLGraphics.CurrentContext,
                        clearCurrentContext: openGLGraphics.ClearCurrent,
                        deleteContext: openGLGraphics.DeleteContext,
                        swapBuffers: openGLGraphics.SwapBuffers,
                        setSyncToVerticalBlank: v => openGLGraphics.VerticalSync = v,
                        setSwapchainFramebuffer: () => OpenGLNative.glBindFramebuffer(FramebufferTarget.Framebuffer, (uint)(openGLGraphics.BackbufferFramebuffer ?? 0)),
                        null);

                    Device = GraphicsDevice.CreateOpenGL(options, openGLInfo, swapchain.Width, swapchain.Height);
                    Device.LogOpenGL(out maxTextureSize);
                    break;

                case GraphicsSurfaceType.Vulkan:
                    // Pass the persisted VkPipelineCache blob through so the driver can skip
                    // recompiling shader pipelines whose SPIR-V was previously seen — meaningfully
                    // cuts cold-start shader-compile cost on Android. The blob is header-validated
                    // by the driver (vendorID/deviceID/UUID), so passing stale or device-mismatched
                    // data is safe — the driver silently discards it.
                    //
                    // Pin the Vk staging-pool sizes explicitly. The fork's backend defaults are
                    // 64 KiB floor / 4 MiB recycle ceiling (vs upstream's vestigial 64 B / 512 B
                    // which effectively bypassed the pool entirely for any realistic UpdateBuffer
                    // size). We declare the same values here so the policy is visible/auditable in
                    // the framework rather than dependent on backend default drift, and so the
                    // floor matches the per-frame upload pattern the framework's
                    // VeldridStagingTexturePool / staging-buffer paths actually generate
                    // (typical glyph atlas tile / SSBO update is well under 64 KiB).
                    var vkOptions = new VulkanDeviceOptions
                    {
                        PipelineCacheData = pipelineCacheData,
                        MinStagingBufferSize = 64 * 1024,
                        MaxStagingBufferSize = 4 * 1024 * 1024,
                    };
                    Device = GraphicsDevice.CreateVulkan(options, swapchain, vkOptions);
                    Device.LogVulkan(out maxTextureSize);
                    break;

                case GraphicsSurfaceType.Direct3D11:
#pragma warning disable CA1416 // D3D11 is only reachable on Windows via the GraphicsSurfaceType switch
                    Device = GraphicsDevice.CreateD3D11(options, swapchain);
                    Device.LogD3D11(out maxTextureSize);
#pragma warning restore CA1416
                    break;

                case GraphicsSurfaceType.Direct3D12:
#pragma warning disable CA1416 // D3D12 is only reachable on Windows via the GraphicsSurfaceType switch
                    Device = GraphicsDevice.CreateD3D12(options, swapchain);
                    Device.LogD3D12(out maxTextureSize);
#pragma warning restore CA1416
                    break;

                case GraphicsSurfaceType.Metal:
                    Device = GraphicsDevice.CreateMetal(options, swapchain);
                    Device.LogMetal(out maxTextureSize);
                    break;

                default:
                    throw new InvalidOperationException();
            }

            Logger.Log($"{nameof(UseStructuredBuffers)}: {UseStructuredBuffers}");

            MaxTextureSize = maxTextureSize;
        }

        /// <summary>
        /// Notifies the device that a new frame has started.
        /// </summary>
        /// <param name="windowSize">The window size.</param>
        public void Resize(Vector2I windowSize)
        {
            if (windowSize != currentWindowSize)
            {
                try
                {
                    Device.ResizeMainWindow((uint)windowSize.X, (uint)windowSize.Y);
                    currentWindowSize = windowSize;
                }
                catch (VeldridException ex) when (isSwapchainSurfaceLost(ex))
                {
                    // Veldrid's Vulkan swapchain throws this when its underlying surface (e.g. the Android
                    // SurfaceView's ANativeWindow) is in an invalid state at the moment we try to recreate it.
                    // Leave currentWindowSize unchanged so the resize is retried on the next frame, by which
                    // point the platform surface has typically stabilised.
                    Logger.Log($"Vulkan swapchain surface lost during resize ({windowSize.X}x{windowSize.Y}); will retry next frame: {ex.Message}", level: LogLevel.Important);
                }
            }
        }

        /// <summary>
        /// Swaps the back buffer with the front buffer to display the new frame.
        /// </summary>
        public void SwapBuffers()
        {
            try
            {
                Device.SwapBuffers();
                consecutiveSwapchainFailures = 0;
            }
            catch (VeldridException ex) when (isSwapchainSurfaceLost(ex))
            {
                // Crash recovery for transient surface-lost on the very first frames after window creation
                // (observed on Android with SDL3 + Vulkan, where the SurfaceView's underlying surface can
                // become invalid between Veldrid initialisation and the first present). Without this guard
                // the VeldridException propagates out of GameHost.DrawFrame and aborts the game thread.
                consecutiveSwapchainFailures++;

                Logger.Log(
                    $"Vulkan swapchain surface lost during SwapBuffers (attempt {consecutiveSwapchainFailures}/{max_consecutive_swapchain_failures}); skipping frame: {ex.Message}",
                    level: LogLevel.Important);

                if (consecutiveSwapchainFailures >= max_consecutive_swapchain_failures)
                {
                    // Genuinely dead device — rethrow as a real crash rather than spin forever.
                    throw;
                }

                // Force the next BeginFrame to call ResizeMainWindow, which asks Veldrid to rebuild the
                // underlying swapchain (which may now succeed if the platform surface has recovered).
                currentWindowSize = default;
            }
        }

        private static bool isSwapchainSurfaceLost(VeldridException ex)
        {
            // Veldrid does not expose a typed exception for VK_ERROR_SURFACE_LOST_KHR / out-of-date surface
            // failures, so match on the message text it produces in VkSwapchain.createSwapchain.
            string message = ex.Message;
            return message.Contains("Swapchain", StringComparison.Ordinal)
                   || message.Contains("surface", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Waits until all renderer commands have been fully executed GPU-side, as signaled by the graphics backend.
        /// </summary>
        /// <remarks>
        /// This is equivalent to a <c>glFinish</c> call.
        /// </remarks>
        public void WaitUntilIdle()
            => Device.WaitForIdle();

        /// <summary>
        /// Returns the current contents of the underlying VkPipelineCache as a serialised blob,
        /// suitable for persisting to disk and feeding back into the constructor on the next launch.
        /// Returns <c>null</c> when the active backend is not Vulkan or the cache is empty.
        /// </summary>
        /// <remarks>
        /// The blob's first bytes are a driver-validated header (vendorID / deviceID / driver UUID),
        /// so it is always safe to round-trip stale data — the driver silently discards mismatched
        /// blobs at create time. Should typically be called once just before disposing the device.
        /// </remarks>
        public byte[]? GetPipelineCacheData()
        {
            if (graphicsSurface.Type != GraphicsSurfaceType.Vulkan)
                return null;

            if (!Device.GetVulkanInfo(out var info))
                return null;

            byte[] data = info.GetPipelineCacheData();
            return data.Length == 0 ? null : data;
        }

        /// <summary>
        /// Waits until the GPU signals that the next frame is ready to be rendered.
        /// </summary>
        public void WaitUntilNextFrameReady()
            => Device.WaitForNextFrameReady();

        /// <summary>
        /// Invoked when the rendering thread is active and commands will be enqueued.
        /// This is mainly required for OpenGL renderers to mark context as current before performing GL calls.
        /// </summary>
        public void MakeCurrent()
        {
            if (graphicsSurface.Type == GraphicsSurfaceType.OpenGL)
            {
                var openGLGraphics = (IOpenGLGraphicsSurface)graphicsSurface;
                openGLGraphics.MakeCurrent(openGLGraphics.WindowContext);
            }
        }

        /// <summary>
        /// Invoked when the rendering thread is suspended and no more commands will be enqueued.
        /// This is mainly required for OpenGL renderers to mark context as current before performing GL calls.
        /// </summary>
        public void ClearCurrent()
        {
            if (graphicsSurface.Type == GraphicsSurfaceType.OpenGL)
            {
                var openGLGraphics = (IOpenGLGraphicsSurface)graphicsSurface;
                openGLGraphics.ClearCurrent();
            }
        }

        /// <summary>
        /// Returns an image containing the current content of the backbuffer, i.e. takes a screenshot.
        /// </summary>
        public unsafe Image<Rgba32> TakeScreenshot()
        {
            var texture = Device.SwapchainFramebuffer.ColorTargets[0].Target;

            switch (graphicsSurface.Type)
            {
                // Veldrid doesn't support copying content from a swapchain framebuffer texture on OpenGL.
                // OpenGL already provides a method for reading pixels directly from the active framebuffer, so let's just use that for now.
                case GraphicsSurfaceType.OpenGL:
                {
                    var pixelData = SixLabors.ImageSharp.Configuration.Default.MemoryAllocator.Allocate<Rgba32>((int)(texture.Width * texture.Height));

                    var info = Device.GetOpenGLInfo();

                    info.ExecuteOnGLThread(() =>
                    {
                        fixed (Rgba32* data = pixelData.Memory.Span)
                            OpenGLNative.glReadPixels(0, 0, texture.Width, texture.Height, GLPixelFormat.Rgba, GLPixelType.UnsignedByte, data);
                    });

                    var glImage = Image.LoadPixelData(pixelData.Memory.Span, (int)texture.Width, (int)texture.Height);
                    glImage.Mutate(i => i.Flip(FlipMode.Vertical));
                    return glImage;
                }

                default:
                    return ExtractTexture<Bgra32>(texture, flipVertical: !Device.IsUvOriginTopLeft);
            }
        }

        public unsafe Image<Rgba32> ExtractTexture<TPixel>(Texture texture, bool flipVertical = false)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            uint width = texture.Width;
            uint height = texture.Height;

            using var staging = Factory.CreateTexture(TextureDescription.Texture2D(width, height, texture.MipLevels, texture.ArrayLayers, texture.Format, TextureUsage.Staging));
            using var commands = Factory.CreateCommandList();
            using var fence = Factory.CreateFence(false);

            commands.Begin();
            commands.CopyTexture(texture, staging);
            commands.End();
            Device.SubmitCommands(commands, fence);

            if (!waitForFence(fence, 5000))
            {
                Logger.Log("Failed to capture framebuffer content within reasonable time.", level: LogLevel.Important);
                return new Image<Rgba32>((int)width, (int)height);
            }

            var resource = Device.Map(staging, MapMode.Read);
            var span = new Span<TPixel>(resource.Data.ToPointer(), (int)(resource.SizeInBytes / Marshal.SizeOf<TPixel>()));

            // on some backends (Direct3D11, in particular), the staging resource data may contain padding at the end of each row for alignment,
            // which means that for the image width, we cannot use the framebuffer's width raw.
            using var image = Image.LoadPixelData(span, (int)(resource.RowPitch / Marshal.SizeOf<TPixel>()), (int)height);

            if (flipVertical)
                image.Mutate(i => i.Flip(FlipMode.Vertical));

            // if the image width doesn't match the framebuffer, it means that we still have padding at the end of each row mentioned above to get rid of.
            // snip it to get a clean image.
            if (image.Width != width)
                image.Mutate(i => i.Crop((int)texture.Width, (int)texture.Height));

            Device.Unmap(staging);

            return image.CloneAs<Rgba32>();
        }

        /// <summary>
        /// Waits for a <see cref="Fence"/> to be signalled.
        /// </summary>
        /// <param name="fence">The fence.</param>
        /// <param name="millisecondsTimeout">The maximum amount of time to wait.</param>
        /// <returns>Whether the fence was signalled.</returns>
        private bool waitForFence(Fence fence, int millisecondsTimeout)
        {
            // todo: Metal doesn't support WaitForFence due to lack of implementation and bugs with supporting MTLSharedEvent.notifyListener,
            // until that is fixed in some way or another, poll on the signal state.
            if (graphicsSurface.Type == GraphicsSurfaceType.Metal)
            {
                const int sleep_time = 10;

                while (!fence.Signaled && (millisecondsTimeout -= sleep_time) > 0)
                    Thread.Sleep(sleep_time);

                return fence.Signaled;
            }

            return Device.WaitForFence(fence, (ulong)(millisecondsTimeout * 1_000_000));
        }
    }
}
