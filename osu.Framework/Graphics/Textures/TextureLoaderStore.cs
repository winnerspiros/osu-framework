// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.IO.Stores;
using ImageSharpConfiguration = SixLabors.ImageSharp.Configuration;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Framework.Graphics.Textures
{
    /// <summary>
    /// Handles the parsing of image data from standard image formats into <see cref="TextureUpload"/>s ready for GPU consumption.
    /// </summary>
    public class TextureLoaderStore : IResourceStore<TextureUpload>
    {
        private static readonly string[] base_lookup_extensions = { "png", "jpg", "jpeg" };

        private readonly IResourceStore<byte[]> store;
        private readonly ResourceStore<byte[]> lookupStore;
        private readonly HashSet<string> supportedImageExtensions;

        public TextureLoaderStore(IResourceStore<byte[]> store)
        {
            this.store = store;
            supportedImageExtensions = ImageSharpConfiguration.Default.ImageFormatsManager.ImageFormats
                                                 .SelectMany(format => format.FileExtensions)
                                                 .ToHashSet(StringComparer.OrdinalIgnoreCase);

            lookupStore = new ResourceStore<byte[]>(new OptimizedResourceStore(store, OptimizedResourceStore.ImageFallbackRules, CanLoadOptimizedImageFormat));

            foreach (string extension in base_lookup_extensions)
                lookupStore.AddExtension(extension);
        }

        public Task<TextureUpload> GetAsync(string name, CancellationToken cancellationToken = default) =>
            Task.Run(() => Get(name), cancellationToken);

        public TextureUpload Get(string name)
        {
            if (name == null)
                return null;

            foreach (string lookupName in getLookupNames(name))
            {
                try
                {
                    using (var stream = store.GetStream(lookupName))
                    {
                        if (stream != null)
                            return new TextureUpload(ImageFromStream<Rgba32>(stream));
                    }
                }
                catch
                {
                }
            }

            return null;
        }

        public Stream GetStream(string name) => lookupStore.GetStream(name);

        protected virtual Image<TPixel> ImageFromStream<TPixel>(Stream stream) where TPixel : unmanaged, IPixel<TPixel>
            => TextureUpload.LoadFromStream<TPixel>(stream);

        protected virtual bool CanLoadOptimizedImageFormat(string extension)
            => supportedImageExtensions.Contains(extension.TrimStart('.'));

        private IEnumerable<string> getLookupNames(string name)
        {
            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string baseLookupName in enumerateBaseLookupNames(name))
            {
                foreach (string lookupName in OptimizedResourceStore.EnumerateLookupNames(baseLookupName, OptimizedResourceStore.ImageFallbackRules, CanLoadOptimizedImageFormat))
                {
                    if (yielded.Add(lookupName))
                        yield return lookupName;
                }
            }
        }

        private static IEnumerable<string> enumerateBaseLookupNames(string name)
        {
            yield return name;

            if (!string.IsNullOrEmpty(Path.GetExtension(name)))
                yield break;

            foreach (string extension in base_lookup_extensions)
                yield return $"{name}.{extension}";
        }

        public IEnumerable<string> GetAvailableResources() => lookupStore.GetAvailableResources();

        #region IDisposable Support

        private bool isDisposed;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (isDisposed)
                return;

            lookupStore.Dispose();

            isDisposed = true;
        }

        #endregion
    }
}
