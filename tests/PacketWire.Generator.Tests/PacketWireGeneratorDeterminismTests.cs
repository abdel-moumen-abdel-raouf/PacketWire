using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace PacketWire.Generator.Tests;

/// <summary>
/// Contains generator tests verifying deterministic code generation across repeated runs,
/// declaration reorderings, and unrelated syntax tree modifications.
/// </summary>
public sealed class PacketWireGeneratorDeterminismTests
{
    /// <summary>
    /// Canonical source code defining a protocol, packets, and shared nested contracts.
    /// </summary>
    private const string CanonicalSource = """
        using PacketWire;

        namespace Sample.Protocol
        {
            [PacketProtocol(
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketByteOrder.LittleEndian)]
            public sealed partial class ApplicationProtocol
            {
            }

            [PacketContract]
            public sealed class SharedContract
            {
                [PacketField(0)]
                public int Code { get; init; }
            }

            [Packet(typeof(ApplicationProtocol), 2, 1)]
            public sealed class SecondPacket
            {
                [PacketField(0)]
                public SharedContract Nested { get; init; } = new();
            }

            [Packet(typeof(ApplicationProtocol), 1, 1)]
            public sealed class FirstPacket
            {
                [PacketField(0)]
                public int Value { get; init; }
            }
        }
        """;

    /// <summary>
    /// Verifies that identical input compilations produce identical generated sources in identical emission order.
    /// </summary>
    [Fact]
    public void IdenticalInputProducesIdenticalGeneratedSourcesInIdenticalOrder()
    {
        GeneratorTestResult first =
            GeneratorTestHost.RunWithOutput(
                CanonicalSource);

        GeneratorTestResult second =
            GeneratorTestHost.RunWithOutput(
                CanonicalSource);

        AssertSuccessfulCompilation(
            first);

        AssertSuccessfulCompilation(
            second);

        Assert.Equal(
            5,
            first.GeneratedSources.Length);

        Assert.Equal(
            first.GeneratedSources.Length,
            second.GeneratedSources.Length);

        AssertUniqueHintNames(
            first);

        AssertUniqueHintNames(
            second);

        string[] firstSnapshot =
            CreateOrderedSnapshot(
                first);

        string[] secondSnapshot =
            CreateOrderedSnapshot(
                second);

        Assert.Equal(
            firstSnapshot,
            secondSnapshot);
    }

    /// <summary>
    /// Verifies that reordering type declarations in the source compilation does not alter
    /// the generated outputs or hint names.
    /// </summary>
    [Fact]
    public void DeclarationReorderingPreservesCanonicalGeneratedOutput()
    {
        const string reorderedSource = """
            using PacketWire;

            namespace Sample.Protocol
            {
                [Packet(typeof(ApplicationProtocol), 1, 1)]
                public sealed class FirstPacket
                {
                    [PacketField(0)]
                    public int Value { get; init; }
                }

                [Packet(typeof(ApplicationProtocol), 2, 1)]
                public sealed class SecondPacket
                {
                    [PacketField(0)]
                    public SharedContract Nested { get; init; } = new();
                }

                [PacketContract]
                public sealed class SharedContract
                {
                    [PacketField(0)]
                    public int Code { get; init; }
                }

                [PacketProtocol(
                    PacketIntegerSize.TwoBytes,
                    PacketIntegerSize.TwoBytes,
                    PacketIntegerSize.TwoBytes,
                    PacketByteOrder.LittleEndian)]
                public sealed partial class ApplicationProtocol
                {
                }
            }
            """;

        GeneratorTestResult canonical =
            GeneratorTestHost.RunWithOutput(
                CanonicalSource);

        GeneratorTestResult reordered =
            GeneratorTestHost.RunWithOutput(
                reorderedSource);

        AssertSuccessfulCompilation(
            canonical);

        AssertSuccessfulCompilation(
            reordered);

        Assert.Equal(
            canonical.GeneratedSources.Length,
            reordered.GeneratedSources.Length);

        AssertUniqueHintNames(
            canonical);

        AssertUniqueHintNames(
            reordered);

        string[] canonicalSnapshot =
            CreateCanonicalSnapshot(
                canonical);

        string[] reorderedSnapshot =
            CreateCanonicalSnapshot(
                reordered);

        Assert.Equal(
            canonicalSnapshot,
            reorderedSnapshot);
    }

    /// <summary>
    /// Verifies that adding unrelated classes and code to the source compilation does not affect
    /// the emitted PacketWire generated sources.
    /// </summary>
    [Fact]
    public void UnrelatedSourceChangesDoNotAlterGeneratedPacketWireOutput()
    {
        const string sourceWithUnrelatedCode = """
            using PacketWire;

            namespace Sample.Protocol
            {
                // This type is intentionally unrelated to PacketWire.
                public sealed class UnrelatedRuntimeType
                {
                    public int RuntimeValue { get; init; }
                }

                [PacketProtocol(
                    PacketIntegerSize.TwoBytes,
                    PacketIntegerSize.TwoBytes,
                    PacketIntegerSize.TwoBytes,
                    PacketByteOrder.LittleEndian)]
                public sealed partial class ApplicationProtocol
                {
                }

                [PacketContract]
                public sealed class SharedContract
                {
                    [PacketField(0)]
                    public int Code { get; init; }
                }

                [Packet(typeof(ApplicationProtocol), 2, 1)]
                public sealed class SecondPacket
                {
                    [PacketField(0)]
                    public SharedContract Nested { get; init; } = new();
                }

                [Packet(typeof(ApplicationProtocol), 1, 1)]
                public sealed class FirstPacket
                {
                    [PacketField(0)]
                    public int Value { get; init; }
                }
            }
            """;

        GeneratorTestResult canonical =
            GeneratorTestHost.RunWithOutput(
                CanonicalSource);

        GeneratorTestResult withUnrelatedCode =
            GeneratorTestHost.RunWithOutput(
                sourceWithUnrelatedCode);

        AssertSuccessfulCompilation(
            canonical);

        AssertSuccessfulCompilation(
            withUnrelatedCode);

        AssertUniqueHintNames(
            canonical);

        AssertUniqueHintNames(
            withUnrelatedCode);

        string[] canonicalSnapshot =
            CreateCanonicalSnapshot(
                canonical);

        string[] unrelatedSnapshot =
            CreateCanonicalSnapshot(
                withUnrelatedCode);

        Assert.Equal(
            canonicalSnapshot,
            unrelatedSnapshot);
    }

    /// <summary>
    /// Creates an array of serialized snapshot strings preserving the original generation emission order.
    /// </summary>
    /// <param name="result">The generator test result to snapshot.</param>
    /// <returns>An array of snapshot strings containing hint names and source texts.</returns>
    private static string[] CreateOrderedSnapshot(
        GeneratorTestResult result)
    {
        return result.GeneratedSources
            .Select(
                static generated =>
                    CreateSnapshotEntry(
                        generated))
            .ToArray();
    }

    /// <summary>
    /// Creates a canonically sorted array of snapshot strings ordered by hint name.
    /// </summary>
    /// <param name="result">The generator test result to snapshot.</param>
    /// <returns>A sorted array of snapshot strings containing hint names and source texts.</returns>
    private static string[] CreateCanonicalSnapshot(
        GeneratorTestResult result)
    {
        return result.GeneratedSources
            .OrderBy(
                static generated =>
                    generated.HintName,
                StringComparer.Ordinal)
            .Select(
                static generated =>
                    CreateSnapshotEntry(
                        generated))
            .ToArray();
    }

    /// <summary>
    /// Creates a single formatted snapshot entry combining the hint name and generated source text.
    /// </summary>
    /// <param name="generated">The generated source result to format.</param>
    /// <returns>A formatted snapshot representation.</returns>
    private static string CreateSnapshotEntry(
        GeneratedSourceResult generated)
    {
        return
            generated.HintName +
            "\n<<<PACKETWIRE-SOURCE>>>\n" +
            generated.SourceText.ToString();
    }

    /// <summary>
    /// Asserts that all generated hint names are unique and end with the standard <c>.g.cs</c> extension.
    /// </summary>
    /// <param name="result">The generator test result to inspect.</param>
    private static void AssertUniqueHintNames(
        GeneratorTestResult result)
    {
        string[] hintNames =
            result.GeneratedSources
                .Select(
                    static generated =>
                        generated.HintName)
                .ToArray();

        Assert.Equal(
            hintNames.Length,
            hintNames
                .Distinct(
                    StringComparer.Ordinal)
                .Count());

        Assert.All(
            hintNames,
            static hintName =>
                Assert.EndsWith(
                    ".g.cs",
                    hintName,
                    StringComparison.Ordinal));
    }

    /// <summary>
    /// Asserts that code generation and compilation succeeded with no diagnostic errors.
    /// </summary>
    /// <param name="result">The generator test result to inspect.</param>
    private static void AssertSuccessfulCompilation(
        GeneratorTestResult result)
    {
        Assert.DoesNotContain(
            result.GeneratorDiagnostics,
            static diagnostic =>
                diagnostic.Severity
                == DiagnosticSeverity.Error);

        ImmutableArray<Diagnostic> errors =
            result.OutputCompilation
                .GetDiagnostics()
                .Where(
                    static diagnostic =>
                        diagnostic.Severity
                        == DiagnosticSeverity.Error)
                .ToImmutableArray();

        Assert.True(
            errors.IsEmpty,
            string.Join(
                Environment.NewLine,
                errors.Select(
                    static diagnostic =>
                        diagnostic.ToString())));
    }
}