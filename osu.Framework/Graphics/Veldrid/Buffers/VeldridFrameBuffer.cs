// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.Veldrid.Textures;
using System.Numerics;
using Veldrid;
using Texture = Veldrid.Texture;

namespace osu.Framework.Graphics.Veldrid.Buffers
{
    internal class VeldridFrameBuffer : IVeldridFrameBuffer
    {
        public osu.Framework.Graphics.Textures.Texture Texture { get; }

        public Framebuffer Framebuffer { get; private set; }

        private readonly VeldridRenderer renderer;
        private readonly PixelFormat? depthFormat;
        private readonly VeldridTexture colourTarget;
        private readonly bool externalColourTarget;
        private readonly int mipLevel;
        private Texture? depthTarget;

        public Vector2 Size
        {
            get;
            set
            {
                if (value == field)
                    return;

                field = value;

                colourTarget.Width = (int)Math.Ceiling(value.X);
                colourTarget.Height = (int)Math.Ceiling(value.Y);
                colourTarget.SetData(new TextureUpload());
                colourTarget.Upload();

                recreateResources();
            }
        } = Vector2.One;

        public VeldridFrameBuffer(VeldridRenderer renderer, PixelFormat[]? formats = null, SamplerFilter filteringMode = SamplerFilter.MinLinearMagLinearMipLinear)
        {
            // todo: we probably want the arguments separated to "PixelFormat[] colorFormats, PixelFormat depthFormat".
            if (formats?.Length > 1)
                throw new ArgumentException("Veldrid framebuffer cannot contain more than one depth target.");

            this.renderer = renderer;

            depthFormat = formats?[0];

            colourTarget = new FrameBufferTexture(renderer, filteringMode);
            Texture = renderer.CreateTexture(colourTarget);

            recreateResources();
        }

        internal VeldridFrameBuffer(VeldridRenderer renderer, VeldridTexture colourTarget, int mipLevel)
        {
            this.renderer = renderer;
            this.colourTarget = colourTarget;
            this.mipLevel = mipLevel;

            Texture = renderer.CreateTexture(colourTarget);
            externalColourTarget = true;

            recreateResources();
        }

        [MemberNotNull(nameof(Framebuffer))]
        private void recreateResources()
        {
            // The texture is created once and resized internally, so it should not be deleted.
            DeleteResources(false);

            if (depthFormat is PixelFormat depth)
            {
                // TextureUsage.Transient tells the Vulkan backend to allocate with
                // VK_IMAGE_USAGE_TRANSIENT_ATTACHMENT_BIT + LAZILY_ALLOCATED memory on
                // tile-based GPUs (Adreno, Mali, PowerVR). Combined with the DontCare storeOp
                // the Veldrid fork applies to transient depth attachments, this keeps depth
                // entirely in tile RAM — zero DRAM allocation and no tile→DRAM writeback.
                // Other backends (OpenGL, D3D11, D3D12, Metal) silently ignore the flag.
                var depthDescription = TextureDescription.Texture2D((uint)colourTarget.Width, (uint)colourTarget.Height, 1, 1, depth, TextureUsage.DepthStencil | TextureUsage.Transient);
                depthTarget = renderer.Factory.CreateTexture(ref depthDescription);
            }

            FramebufferDescription description = new FramebufferDescription
            {
                ColorTargets = new[] { new FramebufferAttachmentDescription(colourTarget.GetResourceList().Single().Texture, 0, (uint)mipLevel) },
                DepthTarget = depthTarget == null ? null : new FramebufferAttachmentDescription(depthTarget, 0)
            };

            Framebuffer = renderer.Factory.CreateFramebuffer(ref description);

            // Check if we need to rebind this framebuffer as a result of recreating it.
            if (renderer.IsFrameBufferBound(this))
            {
                Unbind();
                Bind();
            }
        }

        /// <summary>
        /// Deletes the resources of this frame buffer.
        /// </summary>
        /// <param name="deleteTexture">Whether the texture should also be deleted.</param>
        public void DeleteResources(bool deleteTexture)
        {
            if (deleteTexture && !externalColourTarget)
                colourTarget.Dispose();

            if (Framebuffer.IsNotNull())
                Framebuffer.Dispose();

            depthTarget?.Dispose();
        }

        public void Bind() => renderer.BindFrameBuffer(this);
        public void Unbind() => renderer.UnbindFrameBuffer(this);

        ~VeldridFrameBuffer()
        {
            renderer.ScheduleDisposal(b => b.Dispose(false), this);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private bool isDisposed;

        protected void Dispose(bool disposing)
        {
            if (isDisposed)
                return;

            renderer.DeleteFrameBuffer(this);
            isDisposed = true;
        }

        private class FrameBufferTexture : VeldridTexture
        {
            public FrameBufferTexture(VeldridRenderer renderer, SamplerFilter filteringMode = SamplerFilter.MinLinearMagLinearMipLinear)
                : base(renderer, 1, 1, true, filteringMode)
            {
                BypassTextureUploadQueueing = true;

                SetData(new TextureUpload());
                Upload();
            }

            public override int Width
            {
                get => base.Width;
                set => base.Width = Math.Clamp(value, 1, Renderer.MaxTextureSize);
            }

            public override int Height
            {
                get => base.Height;
                set => base.Height = Math.Clamp(value, 1, Renderer.MaxTextureSize);
            }
        }
    }
}
