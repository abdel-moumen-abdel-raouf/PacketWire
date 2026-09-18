using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace PacketWire.Generator.Tests;

/// <summary>
/// Holds the output compilation, emitted diagnostics, and generated source files from a generator test run.
/// </summary>
internal sealed class GeneratorTestResult
{
    /// <summary>
    /// Initializes a new instance of <see cref="GeneratorTestResult"/>.
    /// </summary>
    /// <param name="outputCompilation">The compilation including all generated syntax trees.</param>
    /// <param name="generatorDiagnostics">The diagnostics reported by the source generator.</param>
    /// <param name="generatedSources">The collection of generated source files.</param>
    internal GeneratorTestResult(
        Compilation outputCompilation,
        ImmutableArray<Diagnostic> generatorDiagnostics,
        ImmutableArray<GeneratedSourceResult> generatedSources)
    {
        OutputCompilation = outputCompilation;
        GeneratorDiagnostics = generatorDiagnostics;
        GeneratedSources = generatedSources;
    }

    /// <summary>
    /// Gets the compilation including all generated syntax trees.
    /// </summary>
    internal Compilation OutputCompilation { get; }

    /// <summary>
    /// Gets the diagnostics reported by the source generator.
    /// </summary>
    internal ImmutableArray<Diagnostic> GeneratorDiagnostics { get; }

    /// <summary>
    /// Gets the collection of generated source files.
    /// </summary>
    internal ImmutableArray<GeneratedSourceResult> GeneratedSources { get; }
}
