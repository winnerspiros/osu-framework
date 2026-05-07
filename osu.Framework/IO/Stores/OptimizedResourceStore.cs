// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace osu.Framework.IO.Stores
{
    /// <summary>
    /// A resource store wrapper that can transparently redirect lookups for original assets to more compact equivalents.
    /// </summary>
    public class OptimizedResourceStore : IResourceStore<byte[]>
    {
        public sealed class FallbackRule
        {
            public string SourceExtension { get; }

            public IReadOnlyList<string> LookupExtensions { get; }

            public FallbackRule(string sourceExtension, params string[] lookupExtensions)
            {
                SourceExtension = normaliseExtension(sourceExtension);
                LookupExtensions = lookupExtensions.Select(normaliseExtension).ToArray();
            }
        }

        public static IReadOnlyList<FallbackRule> ImageFallbackRules { get; } = new[]
        {
            new FallbackRule("png", "avif", "webp", "png"),
            new FallbackRule("jpg", "avif", "webp", "jpg"),
            new FallbackRule("jpeg", "avif", "webp", "jpeg"),
        };

        public static IReadOnlyList<FallbackRule> AudioFallbackRules { get; } = new[]
        {
            new FallbackRule("wav", "ogg", "wav"),
            new FallbackRule("mp3", "ogg", "mp3"),
        };

        public static IReadOnlyList<FallbackRule> VideoFallbackRules { get; } = new[]
        {
            new FallbackRule("mp4", "webm", "mp4"),
        };

        public static IReadOnlyList<FallbackRule> DefaultFallbackRules { get; } = ImageFallbackRules.Concat(AudioFallbackRules).Concat(VideoFallbackRules).ToArray();

        private readonly IResourceStore<byte[]> store;
        private readonly Dictionary<string, string[]> lookupExtensionsBySourceExtension;
        private readonly Dictionary<string, string[]> aliasExtensionsByAvailableExtension;
        private readonly Func<string, bool> isExtensionSupported;

        public OptimizedResourceStore(IResourceStore<byte[]> store, IEnumerable<FallbackRule> fallbackRules = null, Func<string, bool> isExtensionSupported = null)
        {
            ArgumentNullException.ThrowIfNull(store);

            this.store = store;
            this.isExtensionSupported = isExtensionSupported ?? (_ => true);

            fallbackRules ??= Enumerable.Empty<FallbackRule>();

            lookupExtensionsBySourceExtension = fallbackRules.GroupBy(rule => rule.SourceExtension, StringComparer.OrdinalIgnoreCase)
                                                              .ToDictionary(group => group.Key,
                                                                            group => group.SelectMany(rule => rule.LookupExtensions).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                                                                            StringComparer.OrdinalIgnoreCase);

            aliasExtensionsByAvailableExtension = fallbackRules.SelectMany(rule => rule.LookupExtensions
                                                                              .Where(extension => !string.Equals(extension, rule.SourceExtension, StringComparison.OrdinalIgnoreCase))
                                                                              .Select(extension => new KeyValuePair<string, string>(extension, rule.SourceExtension)))
                                                              .GroupBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                                                              .ToDictionary(group => group.Key,
                                                                            group => group.Select(pair => pair.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                                                                            StringComparer.OrdinalIgnoreCase);
        }

        public byte[] Get(string name)
        {
            this.LogIfNonBackgroundThread(name);

            foreach (string lookupName in enumerateLookupNames(name))
            {
                byte[] result = store.Get(lookupName);
                if (result != null)
                    return result;
            }

            return null;
        }

        public async Task<byte[]> GetAsync(string name, CancellationToken cancellationToken = default)
        {
            this.LogIfNonBackgroundThread(name);

            foreach (string lookupName in enumerateLookupNames(name))
            {
                byte[] result = await store.GetAsync(lookupName, cancellationToken).ConfigureAwait(false);
                if (result != null)
                    return result;
            }

            return null;
        }

        public Stream GetStream(string name)
        {
            this.LogIfNonBackgroundThread(name);

            foreach (string lookupName in enumerateLookupNames(name))
            {
                Stream result = store.GetStream(lookupName);
                if (result != null)
                    return result;
            }

            return null;
        }

        public IEnumerable<string> GetAvailableResources()
        {
            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string resource in store.GetAvailableResources())
            {
                if (yielded.Add(resource))
                    yield return resource;

                string extension = normaliseExtension(Path.GetExtension(resource));

                if (!aliasExtensionsByAvailableExtension.TryGetValue(extension, out string[] sourceExtensions) || !isExtensionSupported(extension))
                    continue;

                foreach (string sourceExtension in sourceExtensions)
                {
                    string alias = changeExtension(resource, sourceExtension);

                    if (yielded.Add(alias))
                        yield return alias;
                }
            }
        }

        public void Dispose() => store.Dispose();

        public static string ResolvePath(string path, Func<string, bool> exists, IEnumerable<FallbackRule> fallbackRules, Func<string, bool> isExtensionSupported = null)
        {
            if (path == null)
                return null;

            ArgumentNullException.ThrowIfNull(exists);

            foreach (string candidate in enumerateLookupNames(path, buildLookupMap(fallbackRules), isExtensionSupported ?? (_ => true)))
            {
                if (exists(candidate))
                    return candidate;
            }

            return path;
        }

        public static IEnumerable<string> EnumerateLookupNames(string name, IEnumerable<FallbackRule> fallbackRules, Func<string, bool> isExtensionSupported = null) =>
            enumerateLookupNames(name, buildLookupMap(fallbackRules), isExtensionSupported ?? (_ => true));

        private IEnumerable<string> enumerateLookupNames(string name) => enumerateLookupNames(name, lookupExtensionsBySourceExtension, isExtensionSupported);

        private static IEnumerable<string> enumerateLookupNames(string name, IReadOnlyDictionary<string, string[]> lookupExtensionsBySourceExtension, Func<string, bool> isExtensionSupported)
        {
            if (name == null)
                yield break;

            string extension = normaliseExtension(Path.GetExtension(name));

            if (string.IsNullOrEmpty(extension) || !lookupExtensionsBySourceExtension.TryGetValue(extension, out string[] lookupExtensions))
            {
                yield return name;

                yield break;
            }

            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string lookupExtension in lookupExtensions)
            {
                bool isOriginalExtension = string.Equals(lookupExtension, extension, StringComparison.OrdinalIgnoreCase);

                if (!isOriginalExtension && !isExtensionSupported(lookupExtension))
                    continue;

                string candidate = changeExtension(name, lookupExtension);

                if (yielded.Add(candidate))
                    yield return candidate;
            }
        }

        private static Dictionary<string, string[]> buildLookupMap(IEnumerable<FallbackRule> fallbackRules) =>
            (fallbackRules ?? Enumerable.Empty<FallbackRule>()).GroupBy(rule => rule.SourceExtension, StringComparer.OrdinalIgnoreCase)
                                                             .ToDictionary(group => group.Key,
                                                                           group => group.SelectMany(rule => rule.LookupExtensions).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                                                                           StringComparer.OrdinalIgnoreCase);

        private static string changeExtension(string name, string extension) =>
            Path.ChangeExtension(name, normaliseExtension(extension));

        private static string normaliseExtension(string extension) =>
            extension?.TrimStart('.').ToLowerInvariant();
    }
}
