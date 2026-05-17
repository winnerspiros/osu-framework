// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Framework.Graphics.OpenGL.Buffers
{
    public static class GLBufferFormatExtensions
    {
        public static FramebufferAttachment GetAttachmentType(this RenderbufferInternalFormat format)
        {
            switch (format)
            {
                case RenderbufferInternalFormat.R8:
                case RenderbufferInternalFormat.R8Snorm:
                case RenderbufferInternalFormat.R16F:
                case RenderbufferInternalFormat.R32F:
                case RenderbufferInternalFormat.R8UI:
                case RenderbufferInternalFormat.R8I:
                case RenderbufferInternalFormat.R16UI:
                case RenderbufferInternalFormat.R16I:
                case RenderbufferInternalFormat.R32UI:
                case RenderbufferInternalFormat.R32I:
                case RenderbufferInternalFormat.Rg8:
                case RenderbufferInternalFormat.Rg8Snorm:
                case RenderbufferInternalFormat.Rg16F:
                case RenderbufferInternalFormat.Rg32F:
                case RenderbufferInternalFormat.Rg8UI:
                case RenderbufferInternalFormat.Rg8I:
                case RenderbufferInternalFormat.Rg16UI:
                case RenderbufferInternalFormat.Rg16I:
                case RenderbufferInternalFormat.Rg32UI:
                case RenderbufferInternalFormat.Rg32I:
                case RenderbufferInternalFormat.Rgb8:
                case RenderbufferInternalFormat.Srgb8:
                case RenderbufferInternalFormat.Rgb565:
                case RenderbufferInternalFormat.Rgb8Snorm:
                case RenderbufferInternalFormat.R11Fg11Fb10F:
                case RenderbufferInternalFormat.Rgb9E5:
                case RenderbufferInternalFormat.Rgb16F:
                case RenderbufferInternalFormat.Rgb32F:
                case RenderbufferInternalFormat.Rgb8UI:
                case RenderbufferInternalFormat.Rgb8I:
                case RenderbufferInternalFormat.Rgb16UI:
                case RenderbufferInternalFormat.Rgb16I:
                case RenderbufferInternalFormat.Rgb32UI:
                case RenderbufferInternalFormat.Rgb32I:
                case RenderbufferInternalFormat.Rgba8:
                case RenderbufferInternalFormat.Srgb8Alpha8:
                case RenderbufferInternalFormat.Rgba8Snorm:
                case RenderbufferInternalFormat.Rgb5A1:
                case RenderbufferInternalFormat.Rgba4:
                case RenderbufferInternalFormat.Rgb10A2:
                case RenderbufferInternalFormat.Rgba16F:
                case RenderbufferInternalFormat.Rgba32F:
                case RenderbufferInternalFormat.Rgba8I:
                case RenderbufferInternalFormat.Rgba8UI:
                case RenderbufferInternalFormat.Rgb10A2UI:
                case RenderbufferInternalFormat.Rgba16I:
                case RenderbufferInternalFormat.Rgba16UI:
                case RenderbufferInternalFormat.Rgba32I:
                case RenderbufferInternalFormat.Rgba32UI:
                    return FramebufferAttachment.ColorAttachment0;

                case RenderbufferInternalFormat.DepthComponent16:
                case RenderbufferInternalFormat.DepthComponent24:
                case RenderbufferInternalFormat.DepthComponent32F:
                    return FramebufferAttachment.DepthAttachment;

                case RenderbufferInternalFormat.StencilIndex8:
                    return FramebufferAttachment.StencilAttachment;

                case RenderbufferInternalFormat.Depth24Stencil8:
                case RenderbufferInternalFormat.Depth32FStencil8:
                    return FramebufferAttachment.DepthStencilAttachment;

                default:
                    throw new InvalidOperationException($"{format} is not a valid {nameof(RenderbufferInternalFormat)} type.");
            }
        }

        public static int GetBytesPerPixel(this RenderbufferInternalFormat format)
        {
            // cross-reference: https://www.khronos.org/registry/OpenGL-Refpages/es3.0/html/glTexImage2D.xhtml
            switch (format)
            {
                // GL_RED
                case RenderbufferInternalFormat.R8:
                case RenderbufferInternalFormat.R8Snorm:
                    return 1;

                case RenderbufferInternalFormat.R16F:
                    return 2;

                case RenderbufferInternalFormat.R32F:
                    return 4;

                // GL_RED_INTEGER
                case RenderbufferInternalFormat.R8UI:
                case RenderbufferInternalFormat.R8I:
                    return 1;

                case RenderbufferInternalFormat.R16UI:
                case RenderbufferInternalFormat.R16I:
                    return 2;

                case RenderbufferInternalFormat.R32UI:
                case RenderbufferInternalFormat.R32I:
                    return 4;

                // GL_RG
                case RenderbufferInternalFormat.Rg8:
                case RenderbufferInternalFormat.Rg8Snorm:
                    return 2;

                case RenderbufferInternalFormat.Rg16F:
                    return 4;

                case RenderbufferInternalFormat.Rg32F:
                    return 8;

                // GL_RG_INTEGER
                case RenderbufferInternalFormat.Rg8UI:
                case RenderbufferInternalFormat.Rg8I:
                    return 2;

                case RenderbufferInternalFormat.Rg16UI:
                case RenderbufferInternalFormat.Rg16I:
                    return 4;

                case RenderbufferInternalFormat.Rg32UI:
                case RenderbufferInternalFormat.Rg32I:
                    return 8;

                // GL_RGB
                case RenderbufferInternalFormat.Rgb8:
                case RenderbufferInternalFormat.Srgb8:
                    return 3;

                case RenderbufferInternalFormat.Rgb565:
                    return 2;

                case RenderbufferInternalFormat.Rgb8Snorm:
                    return 3;

                case RenderbufferInternalFormat.R11Fg11Fb10F:
                case RenderbufferInternalFormat.Rgb9E5:
                    return 4;

                case RenderbufferInternalFormat.Rgb16F:
                    return 6;

                case RenderbufferInternalFormat.Rgb32F:
                    return 12;

                // GL_RGB_INTEGER
                case RenderbufferInternalFormat.Rgb8UI:
                case RenderbufferInternalFormat.Rgb8I:
                    return 3;

                case RenderbufferInternalFormat.Rgb16UI:
                case RenderbufferInternalFormat.Rgb16I:
                    return 6;

                case RenderbufferInternalFormat.Rgb32UI:
                case RenderbufferInternalFormat.Rgb32I:
                    return 12;

                // GL_RGBA
                case RenderbufferInternalFormat.Rgba8:
                case RenderbufferInternalFormat.Srgb8Alpha8:
                case RenderbufferInternalFormat.Rgba8Snorm:
                    return 4;

                case RenderbufferInternalFormat.Rgb5A1:
                case RenderbufferInternalFormat.Rgba4:
                    return 2;

                case RenderbufferInternalFormat.Rgb10A2:
                    return 4;

                case RenderbufferInternalFormat.Rgba16F:
                    return 8;

                case RenderbufferInternalFormat.Rgba32F:
                    return 16;

                // GL_RGBA_INTEGER
                case RenderbufferInternalFormat.Rgba8I:
                case RenderbufferInternalFormat.Rgba8UI:
                case RenderbufferInternalFormat.Rgb10A2UI:
                    return 4;

                case RenderbufferInternalFormat.Rgba16I:
                case RenderbufferInternalFormat.Rgba16UI:
                    return 8;

                case RenderbufferInternalFormat.Rgba32I:
                case RenderbufferInternalFormat.Rgba32UI:
                    return 16;

                // GL_DEPTH_COMPONENT
                case RenderbufferInternalFormat.DepthComponent16:
                    return 2;

                case RenderbufferInternalFormat.DepthComponent24:
                    return 3;

                case RenderbufferInternalFormat.DepthComponent32F:
                    return 4;

                // GL_DEPTH_STENCIL
                case RenderbufferInternalFormat.Depth24Stencil8:
                    return 4;

                case RenderbufferInternalFormat.Depth32FStencil8:
                    return 5;

                case RenderbufferInternalFormat.StencilIndex8:
                    return 1;

                default:
                    throw new InvalidOperationException($"{format} is not a valid {nameof(RenderbufferInternalFormat)} type.");
            }
        }
    }
}
