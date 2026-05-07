// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using SixLabors.ImageSharp;

namespace osu.Framework.Tests.IO
{
    [TestFixture]
    public class TextureLoaderStoreFallbackTest
    {
        [Test]
        public void TestDecodeFailureFallsBackToOriginalImage()
        {
            var innerStore = new TestByteResourceStore(new Dictionary<string, byte[]>
            {
                ["Textures/test.avif"] = new byte[] { 1 },
                ["Textures/test.png"] = new byte[] { 2 },
            });

            using var store = new TestTextureLoaderStore(innerStore);
            using TextureUpload upload = store.Get("Textures/test.png")!;

            Assert.That(upload, Is.Not.Null);
            Assert.That(store.AttemptedLookups, Is.EqualTo(new[] { "Textures/test.avif", "Textures/test.png" }));
        }

        [Test]
        public void TestExtensionlessLookupStillFindsTexture()
        {
            var innerStore = new TestByteResourceStore(new Dictionary<string, byte[]>
            {
                ["Textures/test.png"] = new byte[] { 2 },
            });

            using var store = new TestTextureLoaderStore(innerStore);
            using TextureUpload upload = store.Get("Textures/test")!;

            Assert.That(upload, Is.Not.Null);
            Assert.That(store.AttemptedLookups, Contains.Item("Textures/test.png"));
        }

        private sealed class TestTextureLoaderStore : TextureLoaderStore
        {
            public readonly List<string> AttemptedLookups = new List<string>();

            public TestTextureLoaderStore(IResourceStore<byte[]> store)
                : base(store)
            {
            }

            protected override Image<TPixel> ImageFromStream<TPixel>(Stream stream)
            {
                int marker = stream.ReadByte();
                AttemptedLookups.Add(marker == 1 ? "Textures/test.avif" : "Textures/test.png");

                if (marker == 1)
                    throw new InvalidDataException("Simulated unsupported optimized format.");

                return new Image<TPixel>(1, 1);
            }

            protected override bool CanLoadOptimizedImageFormat(string extension) => true;
        }

        private sealed class TestByteResourceStore : IResourceStore<byte[]>
        {
            private readonly IReadOnlyDictionary<string, byte[]> resources;

            public TestByteResourceStore(IReadOnlyDictionary<string, byte[]> resources) => this.resources = resources;

            public byte[] Get(string name) => resources.GetValueOrDefault(name);

            public Task<byte[]> GetAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult(Get(name));

            public Stream GetStream(string name) => resources.TryGetValue(name, out byte[] value) ? new MemoryStream(value, writable: false) : null;

            public IEnumerable<string> GetAvailableResources() => resources.Keys.ToArray();

            public void Dispose()
            {
            }
        }
    }
}
