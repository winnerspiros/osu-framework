// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using osu.Framework.Graphics.Veldrid.Textures;
using Veldrid;

namespace osu.Framework.Graphics.Veldrid.Pipelines
{
    /// <summary>
    /// A non-graphical pipeline that provides a command list and handles basic tasks like uploading textures.
    /// </summary>
    internal class BasicPipeline
    {
        /// <summary>
        /// Invoked when a new execution of this <see cref="BasicPipeline"/> is started.
        /// </summary>
        public event Action<ulong>? ExecutionStarted;

        /// <summary>
        /// Invoked when an execution of this <see cref="BasicPipeline"/> has finished.
        /// </summary>
        public event Action<ulong>? ExecutionFinished;

        /// <summary>
        /// The platform graphics device.
        /// </summary>
        public GraphicsDevice Device
            => device.Device;

        /// <summary>
        /// The platform graphics resource factory.
        /// </summary>
        public ResourceFactory Factory
            => device.Factory;

        /// <summary>
        /// The command list.
        /// </summary>
        public CommandList Commands { get; }

        /// <summary>
        /// The current execution index.
        /// </summary>
        private ulong executionIndex;

        /// <summary>
        /// A list of fences which tracks in-flight executions for the purpose of knowing the last completed execution.
        /// </summary>
        private readonly List<ExecutionCompletionFence> pendingExecutions = new List<ExecutionCompletionFence>();

        /// <summary>
        /// We are using fences every execution. Construction can be expensive, so let's pool some.
        /// </summary>
        private readonly Queue<Fence> fencePool = new Queue<Fence>();

        /// <summary>
        /// Whether any commands have been recorded into this pipeline's command list since the last <see cref="Begin"/> call.
        /// Set this to <c>true</c> whenever commands are recorded outside of <see cref="UpdateTexture{T}"/>.
        /// When <c>false</c> at <see cref="End"/>, the submission is skipped to avoid an unnecessary <c>vkQueueSubmit</c>.
        /// </summary>
        public bool HasPendingWork { get; set; }

        private readonly VeldridDevice device;

        public BasicPipeline(VeldridDevice device)
        {
            this.device = device;
            Commands = device.Factory.CreateCommandList();
        }

        /// <summary>
        /// Begins the pipeline.
        /// </summary>
        public virtual void Begin()
        {
            updatePendingExecutions();
            executionIndex++;
            HasPendingWork = false;

            Commands.Begin();
            ExecutionStarted?.Invoke(executionIndex);
        }

        /// <summary>
        /// Finishes the pipeline and submits it for processing by the device.
        /// </summary>
        public void End()
        {
            Commands.End();

            if (!HasPendingWork)
            {
                // Nothing was recorded — immediately signal completion without a GPU round-trip.
                // This avoids an unnecessary vkQueueSubmit (≈0.3 ms on Adreno) on frames where
                // the buffer-update pipeline has no work (e.g. static scenes with unchanged UBOs).
                ExecutionFinished?.Invoke(executionIndex);
                return;
            }

            if (!fencePool.TryDequeue(out Fence? fence))
                fence = Factory.CreateFence(false);
            pendingExecutions.Add(new ExecutionCompletionFence(fence, executionIndex));

            device.Device.SubmitCommands(Commands, fence);
        }

        private void updatePendingExecutions()
        {
            for (int i = 0; i < pendingExecutions.Count; i++)
            {
                var fence = pendingExecutions[i];

                if (!fence.Fence.Signaled)
                    continue;

                ExecutionFinished?.Invoke(fence.Index);

                Device.ResetFence(fence.Fence);
                fencePool.Enqueue(fence.Fence);
                pendingExecutions.RemoveAt(i--);
            }
        }

        /// <summary>
        /// Updates a <see cref="global::Veldrid.Texture"/> with a <paramref name="data"/> at the specified coordinates.
        /// </summary>
        /// <param name="stagingPool">The staging texture pool.</param>
        /// <param name="texture">The <see cref="global::Veldrid.Texture"/> to update.</param>
        /// <param name="x">The X coordinate of the update region.</param>
        /// <param name="y">The Y coordinate of the update region.</param>
        /// <param name="width">The width of the update region.</param>
        /// <param name="height">The height of the update region.</param>
        /// <param name="level">The texture level.</param>
        /// <param name="data">The texture data.</param>
        /// <typeparam name="T">The pixel type.</typeparam>
        public void UpdateTexture<T>(VeldridStagingTexturePool stagingPool, Texture texture, int x, int y, int width, int height, int level, ReadOnlySpan<T> data)
            where T : unmanaged
        {
            // This code is doing the same as the simpler approach of:
            //
            // Device.UpdateTexture(texture, data, (uint)x, (uint)y, 0, (uint)width, (uint)height, 1, (uint)level, 0);
            //
            // Except we are using a staging texture pool to avoid the alloc overhead of each staging texture.
            //
            // Note: this path is intentionally NOT routed through GraphicsDevice.BeginTextureUpdateBatch
            // (the Vk-overridden coalescing API). That batch is designed for callers issuing many
            // device-level UpdateTexture calls without an open command list, where each call would
            // become its own vkQueueSubmit; it Submits its own one-shot CB on Dispose. The framework
            // already coalesces all per-frame texture work into the per-frame CommandList below
            // (Commands.CopyTexture) which submits exactly once at end-of-frame, so wrapping this
            // call site in a TextureUpdateBatch would *add* a second vkQueueSubmit per frame for
            // zero functional gain. The fork-side wins that *do* benefit this path (bumped staging-
            // pool defaults, sync2 / timeline-semaphores, persisted VkPipelineCache) apply
            // automatically through the existing Device.UpdateTexture / Commands.CopyTexture calls.
            var staging = stagingPool.Get(width, height, texture.Format);
            device.Device.UpdateTexture(staging, data, 0, 0, 0, (uint)width, (uint)height, 1, (uint)level, 0);
            Commands.CopyTexture(staging, 0, 0, 0, 0, 0, texture, (uint)x, (uint)y, 0, (uint)level, 0, (uint)width, (uint)height, 1, 1);
            HasPendingWork = true;
        }

        /// <summary>
        /// Updates a <see cref="global::Veldrid.Texture"/> with a <paramref name="data"/> at the specified coordinates.
        /// </summary>
        /// <param name="stagingPool">The staging texture pool.</param>
        /// <param name="texture">The <see cref="global::Veldrid.Texture"/> to update.</param>
        /// <param name="x">The X coordinate of the update region.</param>
        /// <param name="y">The Y coordinate of the update region.</param>
        /// <param name="width">The width of the update region.</param>
        /// <param name="height">The height of the update region.</param>
        /// <param name="level">The texture level.</param>
        /// <param name="data">The texture data.</param>
        /// <param name="rowLengthInBytes">The number of bytes per row of the image to read from <paramref name="data"/>.</param>
        public void UpdateTexture(VeldridStagingTexturePool stagingPool, Texture texture, int x, int y, int width, int height, int level, IntPtr data, int rowLengthInBytes)
        {
            var staging = stagingPool.Get(width, height, texture.Format);

            unsafe
            {
                MappedResource mappedData = Device.Map(staging, MapMode.Write);

                try
                {
                    void* srcPtr = data.ToPointer();
                    void* dstPtr = mappedData.Data.ToPointer();

                    for (int i = 0; i < height; i++)
                    {
                        Unsafe.CopyBlockUnaligned(dstPtr, srcPtr, mappedData.RowPitch);

                        srcPtr = Unsafe.Add<byte>(srcPtr, rowLengthInBytes);
                        dstPtr = Unsafe.Add<byte>(dstPtr, (int)mappedData.RowPitch);
                    }
                }
                finally
                {
                    Device.Unmap(staging);
                }
            }

            Commands.CopyTexture(
                staging, 0, 0, 0, 0, 0,
                texture, (uint)x, (uint)y, 0, (uint)level, 0, (uint)width, (uint)height, 1, 1);
            HasPendingWork = true;
        }

        /// <summary>
        /// Generate mipmaps for the given texture.
        /// </summary>
        /// <param name="texture">The texture.</param>
        public void GenerateMipmaps(VeldridTexture texture)
        {
            var resources = texture.GetResourceList();
            for (int i = 0; i < resources.Count; i++)
                Commands.GenerateMipmaps(resources[i].Texture);
        }

        private readonly record struct ExecutionCompletionFence(Fence Fence, ulong Index);
    }
}
