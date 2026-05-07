// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;
using osu.Framework.Development;
using osu.Framework.Extensions.ImageExtensions;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osuTK.Graphics;
using osuTK.Graphics.ES30;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Framework.Graphics.OpenGL.Textures
{
    internal class GLTexture : INativeTexture
    {
        protected readonly GLRenderer Renderer;
        private readonly Lock uploadQueueLock = new Lock();
        private readonly Queue<ITextureUpload> uploadQueue = new Queue<ITextureUpload>();

        IRenderer INativeTexture.Renderer => Renderer;

        public string Identifier
        {
            get
            {
                if (!Available || textureId == 0)
                    return "-";

                return textureId.ToString();
            }
        }

        public int MaxSize => Renderer.MaxTextureSize;

        public virtual int Width { get; set; }
        public virtual int Height { get; set; }
        public virtual int GetByteSize() => Width * Height * 4;
        public bool Available { get; private set; } = true;

        public int? MipLevel
        {
            get;
            set
            {
                field = value;
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinLod, field ?? 0);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLod, field ?? IRenderer.MAX_MIPMAP_LEVELS);
            }
        }

        ulong INativeTexture.TotalBindCount { get; set; }

        public bool BypassTextureUploadQueueing { get; set; }

        private int internalWidth;
        private int internalHeight;
        private bool manualMipmaps;

        private readonly List<RectangleI> uploadedRegions = new List<RectangleI>();

        private readonly All filteringMode;
        private readonly Color4? initialisationColour;

        /// <summary>
        /// Creates a new <see cref="GLTexture"/>.
        /// </summary>
        /// <param name="renderer"></param>
        /// <param name="width">The width of the texture.</param>
        /// <param name="height">The height of the texture.</param>
        /// <param name="manualMipmaps">Whether manual mipmaps will be uploaded to the texture. If false, the texture will compute mipmaps automatically.</param>
        /// <param name="filteringMode">The filtering mode.</param>
        /// <param name="initialisationColour">The colour to initialise texture levels with (in the case of sub region initial uploads). If null, no initialisation is provided out-of-the-box.</param>
        public GLTexture(GLRenderer renderer, int width, int height, bool manualMipmaps = false, All filteringMode = All.Linear, Color4? initialisationColour = null)
        {
            Renderer = renderer;
            Width = width;
            Height = height;

            this.manualMipmaps = manualMipmaps;
            this.filteringMode = filteringMode;
            this.initialisationColour = initialisationColour;
        }

        #region Disposal

        private bool isDisposed;

        public void Dispose()
        {
            if (isDisposed)
                return;

            isDisposed = true;

            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~GLTexture()
        {
            Dispose(false);
        }

        protected virtual void Dispose(bool isDisposing)
        {
            Renderer.ScheduleDisposal(texture =>
            {
                while (texture.tryGetNextUpload(out var upload))
                    upload.Dispose();

                int disposableId = texture.textureId;

                if (disposableId <= 0)
                    return;

                GL.DeleteTextures(1, new[] { disposableId });

                texture.memoryLease?.Dispose();

                texture.textureId = 0;
                texture.Available = false;
            }, this);
        }

        #endregion

        #region Memory Tracking

        private List<long> levelMemoryUsage = new List<long>();

        private NativeMemoryTracker.NativeMemoryLease memoryLease;

        private void updateMemoryUsage(int level, long newUsage)
        {
            levelMemoryUsage ??= new List<long>();

            while (level >= levelMemoryUsage.Count)
                levelMemoryUsage.Add(0);

            levelMemoryUsage[level] = newUsage;

            memoryLease?.Dispose();
            memoryLease = NativeMemoryTracker.AddMemory(this, getMemoryUsage());
        }

        private long getMemoryUsage()
        {
            long usage = 0;

            for (int i = 0; i < levelMemoryUsage.Count; i++)
                usage += levelMemoryUsage[i];

            return usage;
        }

        #endregion

        private int textureId;

        public virtual int TextureId
        {
            get
            {
                ObjectDisposedException.ThrowIf(!Available, this);

                return textureId;
            }
        }

        public void FlushUploads()
        {
            while (tryGetNextUpload(out var upload))
                upload.Dispose();
        }

        public void SetData(ITextureUpload upload)
        {
            lock (uploadQueueLock)
            {
                if (uploadQueue.Count >= 100 && uploadQueue.Count % 100 == 0)
                    Logger.Log($"Texture {Identifier}'s upload queue is large ({uploadQueue.Count})");

                bool requireUpload = uploadQueue.Count == 0;
                uploadQueue.Enqueue(upload);

                if (requireUpload && !BypassTextureUploadQueueing)
                    Renderer.EnqueueTextureUpload(this);
            }
        }

        public unsafe bool Upload()
        {
            if (!Available)
                return false;

            uploadedRegions.Clear();

            // We should never run raw OGL calls on another thread than the main thread due to race conditions.
            ThreadSafety.EnsureDrawThread();

            while (tryGetNextUpload(out ITextureUpload upload))
            {
                using (upload)
                {
                    fixed (Rgba32* ptr = upload.Data)
                        DoUpload(upload, (IntPtr)ptr);

                    uploadedRegions.Add(upload.Bounds);
                }
            }

            #region Mipmap generation

            if (uploadedRegions.Count != 0 && !manualMipmaps)
            {
                GL.Hint(HintTarget.GenerateMipmapHint, HintMode.Nicest);
                GL.GenerateMipmap(TextureTarget.Texture2D);
            }

            #endregion

            return uploadedRegions.Count != 0;
        }

        public bool UploadComplete
        {
            get
            {
                lock (uploadQueueLock)
                    return uploadQueue.Count == 0;
            }
        }

        /// <summary>
        /// Whether the texture is currently queued for upload.
        /// </summary>
        public bool IsQueuedForUpload { get; set; }

        private bool tryGetNextUpload(out ITextureUpload upload)
        {
            lock (uploadQueueLock)
            {
                if (uploadQueue.Count == 0)
                {
                    upload = null;
                    return false;
                }

                upload = uploadQueue.Dequeue();
                return true;
            }
        }

        protected virtual void DoUpload(ITextureUpload upload, IntPtr dataPointer)
        {
            // Do we need to generate a new texture?
            if (textureId <= 0 || internalWidth != Width || internalHeight != Height)
            {
                internalWidth = Width;
                internalHeight = Height;

                // We only need to generate a new texture if we don't have one already. Otherwise just re-use the current one.
                if (textureId <= 0)
                {
                    int[] textures = new int[1];
                    GL.GenTextures(1, textures);

                    textureId = textures[0];

                    Renderer.BindTexture(this);

                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBaseLevel, 0);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, IRenderer.MAX_MIPMAP_LEVELS);

                    // These shouldn't be required, but on some older Intel drivers the MAX_LOD chosen by the shader isn't clamped to the MAX_LEVEL from above, resulting in disappearing textures.
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinLod, 0);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLod, IRenderer.MAX_MIPMAP_LEVELS);

                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter,
                        (int)(manualMipmaps ? filteringMode : filteringMode == All.Linear ? All.LinearMipmapLinear : All.Nearest));
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)filteringMode);

                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
                }
                else
                    Renderer.BindTexture(this);

                if ((Width == upload.Bounds.Width && Height == upload.Bounds.Height) || dataPointer == IntPtr.Zero)
                {
                    updateMemoryUsage(upload.Level, (long)Width * Height * 4);
                    GL.TexImage2D(TextureTarget2d.Texture2D, upload.Level, TextureComponentCount.Rgba8, Width, Height, 0, upload.Format, PixelType.UnsignedByte, dataPointer);
                }
                else
                {
                    initializeLevel(upload.Level, Width, Height);

                    GL.TexSubImage2D(TextureTarget2d.Texture2D, upload.Level, upload.Bounds.X, upload.Bounds.Y, upload.Bounds.Width, upload.Bounds.Height, upload.Format,
                        PixelType.UnsignedByte, dataPointer);
                }
            }
            // Just update content of the current texture
            else if (dataPointer != IntPtr.Zero)
            {
                Renderer.BindTexture(this);

                if (!manualMipmaps && upload.Level > 0)
                {
                    //allocate mipmap levels
                    int level = 1;
                    int d = 2;

                    while (Width / d > 0)
                    {
                        initializeLevel(level, Width / d, Height / d);
                        level++;
                        d *= 2;
                    }

                    manualMipmaps = true;
                }

                int div = (int)Math.Pow(2, upload.Level);

                GL.TexSubImage2D(TextureTarget2d.Texture2D, upload.Level, upload.Bounds.X / div, upload.Bounds.Y / div, upload.Bounds.Width / div, upload.Bounds.Height / div,
                    upload.Format, PixelType.UnsignedByte, dataPointer);
            }
        }

        private void initializeLevel(int level, int width, int height)
        {
            if (initialisationColour == null)
                return;

            var colour = initialisationColour.Value;
            bool isTransparentBlack = colour.R == 0 && colour.G == 0 && colour.B == 0 && colour.A == 0;

            if (isTransparentBlack)
            {
                // For transparent black, pass a null data pointer to let the driver zero the memory.
                // This avoids a CPU-side image allocation and memcpy, which is significant on mobile.
                updateMemoryUsage(level, (long)width * height * 4);
                GL.TexImage2D(TextureTarget2d.Texture2D, level, TextureComponentCount.Rgba8, width, height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
                return;
            }

            var rgbaColour = new Rgba32(new Vector4(colour.R, colour.G, colour.B, colour.A));

            using var image = new Image<Rgba32>(width, height, rgbaColour);

            using (var pixels = image.CreateReadOnlyPixelSpan())
            {
                updateMemoryUsage(level, (long)width * height * 4);
                GL.TexImage2D(TextureTarget2d.Texture2D, level, TextureComponentCount.Rgba8, width, height, 0, PixelFormat.Rgba, PixelType.UnsignedByte,
                    ref MemoryMarshal.GetReference(pixels.Span));
            }
        }
    }
}
