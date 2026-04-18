// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics.OpenGL.Buffers;
using osu.Framework.Graphics.Rendering.Vertices;
using osuTK.Graphics.ES30;

namespace osu.Framework.Graphics.OpenGL.Batches
{
    internal class GLQuadBatch<T> : GLVertexBatch<T>
        where T : unmanaged, IEquatable<T>, IVertex
    {
        public GLQuadBatch(GLRenderer renderer, int size, int maxBuffers)
            : base(renderer, size, maxBuffers)
        {
        }

        // On mobile tile-based GPUs, StreamDraw hints to the driver that buffer data is transient
        // and only used for one draw call, enabling more efficient memory handling.
        protected override GLVertexBuffer<T> CreateVertexBuffer(GLRenderer renderer)
            => new GLQuadBuffer<T>(renderer, Size, RuntimeInfo.IsMobile ? BufferUsageHint.StreamDraw : BufferUsageHint.DynamicDraw);
    }
}
