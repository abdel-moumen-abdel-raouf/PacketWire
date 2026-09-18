using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace PacketWire.Generator.Tests;

/// <summary>
/// Contains generator diagnostic tests verifying packet category semantics, uniqueness rules,
/// default category assignments, and category width boundary validations.
/// </summary>
public sealed class PacketWireGeneratorCategoryTests
{
    /// <summary>
    /// Verifies that identical packet IDs across different categories within the same protocol are permitted.
    /// </summary>
    [Fact]
    public void SamePacketIdInDifferentCategoriesIsAllowed()
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

            [Packet(typeof(TestProtocol), 1, 1)]
            public sealed class AuthenticationPacket
            {
            }

            [Packet(typeof(TestProtocol), 1, 2)]
            public sealed class SecurityPacket
            {
            }
            """;

        AssertNoErrors(source);
    }

    /// <summary>
    /// Verifies that duplicate packet ID and category pairs within the same protocol trigger diagnostic PWG002.
    /// </summary>
    [Fact]
    public void DuplicateCategoryAndPacketIdProducePWG002()
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

            [Packet(typeof(TestProtocol), 10, 3)]
            public sealed class FirstPacket
            {
            }

            [Packet(typeof(TestProtocol), 10, 3)]
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
    /// Verifies that omitting the category parameter causes a packet to default to category zero,
    /// triggering collision diagnostic PWG002 if an explicit category 0 packet shares the ID.
    /// </summary>
    [Fact]
    public void OmittedCategoryUsesDefaultCategoryZero()
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

            [Packet(typeof(TestProtocol), 10, 0)]
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
    /// Verifies that declaring a packet category exceeding the protocol's configured category integer width
    /// triggers diagnostic PWG018.
    /// </summary>
    [Fact]
    public void CategoryExceedingProtocolWidthProducesPWG018()
    {
        const string source = """
            using PacketWire;

            [PacketProtocol(
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketByteOrder.LittleEndian,
                PacketIntegerSize.OneByte)]
            public sealed partial class TestProtocol
            {
            }

            [Packet(typeof(TestProtocol), 1, 256)]
            public sealed class TestPacket
            {
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            GeneratorTestHost.Run(source);

        Assert.Contains(
            diagnostics,
            static diagnostic =>
                diagnostic.Id == "PWG018");
    }

    /// <summary>
    /// Verifies that configuring a larger category integer width allows higher category integer values
    /// without diagnostics.
    /// </summary>
    [Fact]
    public void LargerConfiguredCategoryWidthAllowsLargerCategory()
    {
        const string source = """
            using PacketWire;

            [PacketProtocol(
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketByteOrder.LittleEndian,
                PacketIntegerSize.TwoBytes)]
            public sealed partial class TestProtocol
            {
            }

            [Packet(typeof(TestProtocol), 1, 500)]
            public sealed class TestPacket
            {
            }
            """;

        AssertNoErrors(source);
    }

    /// <summary>
    /// Asserts that generator analysis on the given source text produces no error diagnostics.
    /// </summary>
    /// <param name="source">The C# source code to compile and analyze.</param>
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

