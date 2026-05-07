// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.IO.Stores;

namespace osu.Framework.Tests.IO
{
    [TestFixture]
    public class OptimizedResourceStoreTest
    {
        [Test]
        public void TestImageFallbackPrefersOptimizedAsset()
        {
            using var store = new OptimizedResourceStore(new TestByteResourceStore(new Dictionary<string, byte[]>
            {
                ["Textures/test.avif"] = Encoding.UTF8.GetBytes("avif"),
                ["Textures/test.png"] = Encoding.UTF8.GetBytes("png"),
            }), OptimizedResourceStore.ImageFallbackRules);

            using Stream stream = store.GetStream("Textures/test.png")!;

            Assert.That(readString(stream), Is.EqualTo("avif"));
        }

        [Test]
        public void TestUnsupportedOptimizedImageFallsBackToOriginal()
        {
            using var store = new OptimizedResourceStore(new TestByteResourceStore(new Dictionary<string, byte[]>
            {
                ["Textures/test.avif"] = Encoding.UTF8.GetBytes("avif"),
                ["Textures/test.png"] = Encoding.UTF8.GetBytes("png"),
            }), OptimizedResourceStore.ImageFallbackRules, extension => !extension.Equals("avif", StringComparison.OrdinalIgnoreCase));

            using Stream stream = store.GetStream("Textures/test.png")!;

            Assert.That(readString(stream), Is.EqualTo("png"));
        }

        [Test]
        public void TestAvailableResourcesExposeOriginalAlias()
        {
            using var store = new OptimizedResourceStore(new TestByteResourceStore(new Dictionary<string, byte[]>
            {
                ["Textures/test.avif"] = Array.Empty<byte>(),
            }), OptimizedResourceStore.ImageFallbackRules);

            Assert.That(store.GetAvailableResources(), Contains.Item("Textures/test.avif"));
            Assert.That(store.GetAvailableResources(), Contains.Item("Textures/test.png"));
        }

        [Test]
        public void TestVideoPathFallback()
        {
            string resolved = OptimizedResourceStore.ResolvePath("Videos/clip.mp4",
                path => path.Equals("Videos/clip.webm", StringComparison.Ordinal),
                OptimizedResourceStore.VideoFallbackRules);

            Assert.That(resolved, Is.EqualTo("Videos/clip.webm"));
        }

        private static string readString(Stream stream)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, true, 1024, leaveOpen: true);
            return reader.ReadToEnd();
        }

        private sealed class TestByteResourceStore : IResourceStore<byte[]>
        {
            private readonly IReadOnlyDictionary<string, byte[]> resources;

            public TestByteResourceStore(IReadOnlyDictionary<string, byte[]> resources) => this.resources = resources;

            public byte[] Get(string name) => resources.TryGetValue(name, out byte[] value) ? value : null;

            public Task<byte[]> GetAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult(Get(name));

            public Stream GetStream(string name) => resources.TryGetValue(name, out byte[] value) ? new MemoryStream(value, writable: false) : null;

            public IEnumerable<string> GetAvailableResources() => resources.Keys.ToArray();

            public void Dispose()
            {
            }
        }
    }
}
