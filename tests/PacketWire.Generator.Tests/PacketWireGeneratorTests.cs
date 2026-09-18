using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace PacketWire.Generator.Tests;

/// <summary>
/// Contains generator diagnostic tests verifying basic packet protocol attributes,
/// packet identifier validation, uniqueness constraints, and field ordering.
/// </summary>
public sealed class PacketWireGeneratorTests
{
    /// <summary>
    /// Verifies that a valid packet definition produces no error diagnostics from the generator.
    /// </summary>
    [Fact]
    public void ValidPacketProducesNoGeneratorErrors()
    {
        const string source = """
            using PacketWire;

            [PacketProtocol(
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketByteOrder.LittleEndian)]
            public sealed partial class TestProtocol
            {
            }

            [Packet(typeof(TestProtocol), 1)]
            public sealed class TestPacket
            {
                [PacketField(0)]
                public int Value { get; init; }
            }
            """;

        AssertNoErrors(source);
    }

    /// <summary>
    /// Verifies that referencing a protocol type lacking <c>[PacketProtocol]</c> produces PWG001.
    /// </summary>
    [Fact]
    public void MissingPacketProtocolProducesPWG001()
    {
        const string source = """
            using PacketWire;

            public sealed partial class TestProtocol
            {
            }

            [Packet(typeof(TestProtocol), 1)]
            public sealed class TestPacket
            {
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            GeneratorTestHost.Run(source);

        Assert.Contains(
            diagnostics,
            static diagnostic =>
                diagnostic.Id == "PWG001");
    }

    /// <summary>
    /// Verifies that duplicate packet IDs within the same protocol trigger diagnostic PWG002.
    /// </summary>
    [Fact]
    public void DuplicatePacketIdsProducePWG002()
    {
        const string source = """
            using PacketWire;

            [PacketProtocol(
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketByteOrder.LittleEndian)]
            public sealed partial class TestProtocol
            {
            }

            [Packet(typeof(TestProtocol), 10)]
            public sealed class FirstPacket
            {
            }

            [Packet(typeof(TestProtocol), 10)]
            public sealed class SecondPacket
            {
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            GeneratorTestHost.Run(source);

        Assert.Equal(
            2,
            diagnostics.Count(
                static diagnostic =>
                    diagnostic.Id == "PWG002"));
    }

    /// <summary>
    /// Verifies that declaring a packet ID exceeding the configured protocol ID width produces PWG003.
    /// </summary>
    [Fact]
    public void PacketIdExceedingProtocolWidthProducesPWG003()
    {
        const string source = """
            using PacketWire;

            [PacketProtocol(
                PacketIntegerSize.OneByte,
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketByteOrder.LittleEndian)]
            public sealed partial class TestProtocol
            {
            }

            [Packet(typeof(TestProtocol), 256)]
            public sealed class TestPacket
            {
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            GeneratorTestHost.Run(source);

        Assert.Contains(
            diagnostics,
            static diagnostic =>
                diagnostic.Id == "PWG003");
    }

    /// <summary>
    /// Verifies that duplicate field order indices within a packet model trigger diagnostic PWG004.
    /// </summary>
    [Fact]
    public void DuplicatePacketFieldOrderProducesPWG004()
    {
        const string source = """
            using PacketWire;

            [PacketProtocol(
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketByteOrder.LittleEndian)]
            public sealed partial class TestProtocol
            {
            }

            [Packet(typeof(TestProtocol), 1)]
            public sealed class TestPacket
            {
                [PacketField(0)]
                public int First { get; init; }

                [PacketField(0)]
                public int Second { get; init; }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            GeneratorTestHost.Run(source);

        Assert.Equal(
            2,
            diagnostics.Count(
                static diagnostic =>
                    diagnostic.Id == "PWG004"));
    }

    /// <summary>
    /// Verifies that identical packet IDs across different protocol definitions are permitted.
    /// </summary>
    [Fact]
    public void SamePacketIdAcrossDifferentProtocolsIsAllowed()
    {
        const string source = """
            using PacketWire;

            [PacketProtocol(
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketByteOrder.LittleEndian)]
            public sealed partial class FirstProtocol
            {
            }

            [PacketProtocol(
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketByteOrder.LittleEndian)]
            public sealed partial class SecondProtocol
            {
            }

            [Packet(typeof(FirstProtocol), 1)]
            public sealed class FirstPacket
            {
            }

            [Packet(typeof(SecondProtocol), 1)]
            public sealed class SecondPacket
            {
            }
            """;

        AssertNoErrors(source);
    }

    /// <summary>
    /// Asserts that generator analysis on the given source produces no error-level diagnostics.
    /// </summary>
    /// <param name="source">The source code to compile and analyze.</param>
    private static void AssertNoErrors(
        string source)
    {
        ImmutableArray<Diagnostic> diagnostics =
            GeneratorTestHost.Run(source);

        Assert.DoesNotContain(
            diagnostics,
            static diagnostic =>
                diagnostic.Severity
                == DiagnosticSeverity.Error);
    }
}


