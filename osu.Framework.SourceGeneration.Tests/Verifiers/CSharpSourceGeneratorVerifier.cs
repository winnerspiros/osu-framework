// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using osu.Framework.SourceGeneration.Generators;

namespace osu.Framework.SourceGeneration.Tests.Verifiers
{
    public partial class CSharpSourceGeneratorVerifier<TSourceGenerator>
        where TSourceGenerator : AbstractIncrementalGenerator, new()
    {
        public static async Task VerifyAsync(
            (string filename, string content)[] commonSources,
            (string filename, string content)[] sources,
            (string filename, string content)[] commonGenerated,
            (string filename, string content)[] generated,
            OptimizationLevel optimizationLevel = OptimizationLevel.Release)
        {
            var test = new Test(optimizationLevel);

            foreach (var (filename, content) in commonSources)
                test.TestState.Sources.Add( (filename, SourceText.From(content, Encoding.UTF8)));

            foreach (var (filename, content) in sources)
                test.TestState.Sources.Add( (filename, SourceText.From(content, Encoding.UTF8)));

            foreach (var (filename, content) in commonGenerated)
                test.TestState.GeneratedSources.Add((typeof(TSourceGenerator), filename, SourceText.From(content, Encoding.UTF8)));

            foreach (var (filename, content) in generated)
                test.TestState.GeneratedSources.Add((typeof(TSourceGenerator), filename, SourceText.From(content, Encoding.UTF8)));

            await test.RunAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }
}
