using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace PacketWire.Generator.Tests;

/// <summary>
/// Contains generator tests verifying compilation and emission of advanced codec patterns,
/// including optional primitives, fixed strings, collections, and nested contracts.
/// </summary>
public sealed class PacketWireGeneratorAdvancedCodecEmissionTests
{
    /// <summary>
    /// Verifies that optional scalar and string fields generate valid, compilable codec emission
    /// using presence codecs.
    /// </summary>
    [Fact]
    public void OptionalScalarAndStringGenerateCompilableCodec()
    {
        const string source = """
            using PacketWire;

            [PacketContract]
            public sealed class TestContract
            {
                [PacketField(0)]
                [Optional]
                public int? Number { get; init; }

                [PacketField(1)]
                [Optional]
                [FixedString(32)]
                public string? Name { get; init; }
            }
            """;

        GeneratorTestResult result =
            GeneratorTestHost.RunWithOutput(source);

        AssertSuccessfulCompilation(result);

        GeneratedSourceResult generated =
            Assert.Single(
                result.GeneratedSources);

        string text =
            generated.SourceText.ToString();

        Assert.Contains(
            "PacketPresenceCodec.Write",
            text,
            StringComparison.Ordinal);

        Assert.Contains(
            "PacketPresenceCodec.Read",
            text,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that array and list collection fields generate valid, compilable codec emission
    /// using collection codecs.
    /// </summary>
    [Fact]
    public void ArraysAndListsGenerateCompilableCodec()
    {
        const string source = """
            using System.Collections.Generic;
            using PacketWire;

            [PacketContract]
            public sealed class TestContract
            {
                [PacketField(0)]
                [MaxCount(100)]
                public List<int> Numbers { get; init; } = new();

                [PacketField(1)]
                [MaxCount(10)]
                [FixedString(16)]
                public string[] Names { get; init; } = [];
            }
            """;

        GeneratorTestResult result =
            GeneratorTestHost.RunWithOutput(source);

        AssertSuccessfulCompilation(result);

        GeneratedSourceResult generated =
            Assert.Single(
                result.GeneratedSources);

        string text =
            generated.SourceText.ToString();

        Assert.Contains(
            "PacketCollectionCodec.WriteCount",
            text,
            StringComparison.Ordinal);

        Assert.Contains(
            "PacketCollectionCodec.ReadCount",
            text,
            StringComparison.Ordinal);

        Assert.Contains(
            "PacketCollectionCodec.GetEncodedCountLength",
            text,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that nested packet contracts generate separate, compilable codecs for each contract type.
    /// </summary>
    [Fact]
    public void NestedPacketContractGeneratesCompilableCodecs()
    {
        const string source = """
            using PacketWire;

            [PacketContract]
            public sealed class Address
            {
                [PacketField(0)]
                public int CityId { get; init; }

                [PacketField(1)]
                [FixedString(32)]
                public string Street { get; init; } = string.Empty;
            }

            [PacketContract]
            public sealed class Customer
            {
                [PacketField(0)]
                public int Id { get; init; }

                [PacketField(1)]
                public Address Address { get; init; } = new();
            }
            """;

        GeneratorTestResult result =
            GeneratorTestHost.RunWithOutput(source);

        AssertSuccessfulCompilation(result);

        Assert.Equal(
            2,
            result.GeneratedSources.Length);
    }

    /// <summary>
    /// Verifies that optional nested packet contracts generate compilable codecs handling nullable nested types.
    /// </summary>
    [Fact]
    public void OptionalNestedPacketContractGeneratesCompilableCodecs()
    {
        const string source = """
            using PacketWire;

            [PacketContract]
            public sealed class Address
            {
                [PacketField(0)]
                public int CityId { get; init; }
            }

            [PacketContract]
            public sealed class Customer
            {
                [PacketField(0)]
                [Optional]
                public Address? Address { get; init; }
            }
            """;

        GeneratorTestResult result =
            GeneratorTestHost.RunWithOutput(source);

        AssertSuccessfulCompilation(result);

        Assert.Equal(
            2,
            result.GeneratedSources.Length);
    }

    /// <summary>
    /// Verifies that collections containing nested packet contracts generate valid, compilable codecs.
    /// </summary>
    [Fact]
    public void CollectionOfNestedContractsGeneratesCompilableCodecs()
    {
        const string source = """
            using System.Collections.Generic;
            using PacketWire;

            [PacketContract]
            public sealed class Item
            {
                [PacketField(0)]
                public int Id { get; init; }
            }

            [PacketContract]
            public sealed class Inventory
            {
                [PacketField(0)]
                [MaxCount(100)]
                public List<Item> Items { get; init; } = new();
            }
            """;

        GeneratorTestResult result =
            GeneratorTestHost.RunWithOutput(source);

        AssertSuccessfulCompilation(result);

        Assert.Equal(
            2,
            result.GeneratedSources.Length);
    }

    /// <summary>
    /// Verifies that optional collections generate valid, compilable codecs combining presence and collection logic.
    /// </summary>
    [Fact]
    public void OptionalCollectionGeneratesCompilableCodec()
    {
        const string source = """
            using System.Collections.Generic;
            using PacketWire;

            [PacketContract]
            public sealed class TestContract
            {
                [PacketField(0)]
                [Optional]
                [MaxCount(50)]
                public List<int>? Values { get; init; }
            }
            """;

        GeneratorTestResult result =
            GeneratorTestHost.RunWithOutput(source);

        AssertSuccessfulCompilation(result);

        Assert.Single(
            result.GeneratedSources);
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
