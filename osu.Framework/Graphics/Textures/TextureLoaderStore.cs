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
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Framework.Graphics.Textures
{
    /// <summary>
    /// Handles the parsing of image data from standard image formats into <see cref="TextureUpload"/>s ready for GPU consumption.
    /// </summary>
    public class TextureLoaderStore : IResourceStore<TextureUpload>
    {
        private readonly IResourceStore<byte[]> store;
        private readonly ResourceStore<byte[]> lookupStore;

        public TextureLoaderStore(IResourceStore<byte[]> store)
        {
            this.store = store;
            lookupStore = new ResourceStore<byte[]>(new OptimizedResourceStore(store, OptimizedResourceStore.ImageFallbackRules, CanLoadOptimizedImageFormat));
            lookupStore.AddExtension(@"png");
            lookupStore.AddExtension(@"jpg");
            lookupStore.AddExtension(@"jpeg");
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
        {
            extension = extension.TrimStart('.');

            return Configuration.Default.ImageFormatsManager.ImageFormats
                                .Any(format => format.FileExtensions.Any(fileExtension => fileExtension.Equals(extension, StringComparison.OrdinalIgnoreCase)));
        }

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

            yield return $"{name}.png";
            yield return $"{name}.jpg";
            yield return $"{name}.jpeg";
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
