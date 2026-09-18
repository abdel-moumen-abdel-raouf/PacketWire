using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace PacketWire.Generator.Tests;

/// <summary>
/// Contains generator tests verifying compilable codec emission for primitive fields, enums,
/// fixed strings, structs, collections, and contract shape constraints.
/// </summary>
public sealed class PacketWireGeneratorCodecEmissionTests
{
    /// <summary>
    /// Verifies that a packet containing primitives, enums, and fixed strings generates valid,
    /// compilable codec methods with appropriate reader and writer invocations.
    /// </summary>
    [Fact]
    public void PrimitiveEnumAndFixedStringPacketGeneratesCompilableCodec()
    {
        const string source = """
            using PacketWire;

            public enum TestMode : byte
            {
                First = 0,
                Second = 1
            }

            [PacketProtocol(
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketByteOrder.LittleEndian)]
            public sealed partial class TestProtocol
            {
            }

            [Packet(typeof(TestProtocol), 1, 2)]
            public sealed class TestPacket
            {
                [PacketField(0)]
                public int Id { get; init; }

                [PacketField(1)]
                public ushort Level { get; init; }

                [PacketField(2)]
                public bool Enabled { get; init; }

                [PacketField(3)]
                public TestMode Mode { get; init; }

                [PacketField(4)]
                [FixedString(32)]
                public string Name { get; init; } = string.Empty;
            }
            """;

        GeneratorTestResult result =
            GeneratorTestHost.RunWithOutput(source);

        AssertNoGeneratorErrors(result);
        AssertOutputCompilationHasNoErrors(result);

        GeneratedSourceResult generated =
            Assert.Single(
                result.GeneratedSources.Where(
                    static generatedSource =>
                        !generatedSource.HintName.StartsWith(
                            "PacketWire.Registry.",
                            StringComparison.Ordinal) &&
                        !generatedSource.HintName.StartsWith(
                            "PacketWire.ProtocolFacade.",
                            StringComparison.Ordinal)));

        string generatedText =
            generated.SourceText.ToString();

        Assert.Contains(
            "internal static int GetEncodedLength",
            generatedText,
            StringComparison.Ordinal);

        Assert.Contains(
            "internal static void Write",
            generatedText,
            StringComparison.Ordinal);

        Assert.Contains(
            "Read(",
            generatedText,
            StringComparison.Ordinal);

        Assert.Contains(
            "writer.WriteInt32(value.@Id);",
            generatedText,
            StringComparison.Ordinal);

        Assert.Contains(
            "writer.WriteUInt16(value.@Level);",
            generatedText,
            StringComparison.Ordinal);

        Assert.Contains(
            "writer.WriteBoolean(value.@Enabled);",
            generatedText,
            StringComparison.Ordinal);

        Assert.Contains(
            "writer.WriteByte((byte)value.@Mode);",
            generatedText,
            StringComparison.Ordinal);

        Assert.Contains(
            "writer.WriteFixedString(value.@Name, 32);",
            generatedText,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that value-type struct contracts generate valid, compilable codecs.
    /// </summary>
    [Fact]
    public void PrimitiveStructGeneratesCompilableCodec()
    {
        const string source = """
            using PacketWire;

            [PacketContract]
            public struct TestContract
            {
                [PacketField(0)]
                public long Value { get; init; }

                [PacketField(1)]
                public double Amount { get; init; }
            }
            """;

        GeneratorTestResult result =
            GeneratorTestHost.RunWithOutput(source);

        AssertNoGeneratorErrors(result);
        AssertOutputCompilationHasNoErrors(result);

        Assert.Single(
            result.GeneratedSources);
    }

    /// <summary>
    /// Verifies that a contract class lacking an accessible parameterless constructor triggers diagnostic PWG019.
    /// </summary>
    [Fact]
    public void ClassWithoutAccessibleParameterlessConstructorProducesPWG019()
    {
        const string source = """
            using PacketWire;

            [PacketContract]
            public sealed class TestContract
            {
                public TestContract(int value)
                {
                    Value = value;
                }

                [PacketField(0)]
                public int Value { get; init; }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            GeneratorTestHost.Run(source);

        Assert.Contains(
            diagnostics,
            static diagnostic =>
                diagnostic.Id == "PWG019");
    }

    /// <summary>
    /// Verifies that an abstract wire contract triggers diagnostic PWG019.
    /// </summary>
    [Fact]
    public void AbstractWireContractProducesPWG019()
    {
        const string source = """
            using PacketWire;

            [PacketContract]
            public abstract class TestContract
            {
                [PacketField(0)]
                public int Value { get; init; }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            GeneratorTestHost.Run(source);

        Assert.Contains(
            diagnostics,
            static diagnostic =>
                diagnostic.Id == "PWG019");
    }

    /// <summary>
    /// Verifies that contract inheritance from a custom base class triggers diagnostic PWG020.
    /// </summary>
    [Fact]
    public void WireContractInheritanceProducesPWG020()
    {
        const string source = """
            using PacketWire;

            public class BaseContract
            {
            }

            [PacketContract]
            public sealed class TestContract : BaseContract
            {
                [PacketField(0)]
                public int Value { get; init; }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            GeneratorTestHost.Run(source);

        Assert.Contains(
            diagnostics,
            static diagnostic =>
                diagnostic.Id == "PWG020");
    }

    /// <summary>
    /// Verifies that a contract containing a collection field generates a valid, compilable codec.
    /// </summary>
    [Fact]
    public void CollectionFieldGeneratesCompilableCodec()
    {
        const string source = """
            using System.Collections.Generic;
            using PacketWire;

            [PacketContract]
            public sealed class TestContract
            {
                [PacketField(0)]
                public List<int> Values { get; init; } = new();
            }
            """;

        GeneratorTestResult result =
            GeneratorTestHost.RunWithOutput(source);

        AssertNoGeneratorErrors(result);
        AssertOutputCompilationHasNoErrors(result);

        Assert.Single(
            result.GeneratedSources);
    }

    /// <summary>
    /// Asserts that the generator execution produced no error-level diagnostics.
    /// </summary>
    /// <param name="result">The generator test result to inspect.</param>
    private static void AssertNoGeneratorErrors(
        GeneratorTestResult result)
    {
        Assert.DoesNotContain(
            result.GeneratorDiagnostics,
            static diagnostic =>
                diagnostic.Severity
                == DiagnosticSeverity.Error);
    }

    /// <summary>
    /// Asserts that the output compilation produced by the generator has no error-level compiler diagnostics.
    /// </summary>
    /// <param name="result">The generator test result to inspect.</param>
    private static void AssertOutputCompilationHasNoErrors(
        GeneratorTestResult result)
    {
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





