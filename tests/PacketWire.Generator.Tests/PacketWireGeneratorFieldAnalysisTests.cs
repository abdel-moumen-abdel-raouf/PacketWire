using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace PacketWire.Generator.Tests;

/// <summary>
/// Contains generator diagnostic tests verifying field analysis, attribute constraints,
/// supported types, nullability rules, collection limits, and order indexing.
/// </summary>
public sealed class PacketWireGeneratorFieldAnalysisTests
{
    /// <summary>
    /// Verifies that an unclassified public property on a packet produces diagnostic PWG005.
    /// </summary>
    [Fact]
    public void UnclassifiedPublicPacketPropertyProducesPWG005()
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
                public int Value { get; init; }
            }
            """;

        AssertHasDiagnostic(
            source,
            "PWG005");
    }

    /// <summary>
    /// Verifies that marking a non-wire public property with <c>[PacketIgnore]</c> suppresses unclassified property errors.
    /// </summary>
    [Fact]
    public void PacketIgnoreAllowsNonWireProperty()
    {
        const string source = """
            using System;
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

                [PacketIgnore]
                public DateTime RuntimeOnlyValue { get; init; }
            }
            """;

        AssertNoErrors(source);
    }

    /// <summary>
    /// Verifies that applying both <c>[PacketField]</c> and <c>[PacketIgnore]</c> to the same property produces PWG006.
    /// </summary>
    [Fact]
    public void PacketFieldAndPacketIgnoreProducePWG006()
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
                [PacketIgnore]
                public int Value { get; init; }
            }
            """;

        AssertHasDiagnostic(
            source,
            "PWG006");
    }

    /// <summary>
    /// Verifies that applying <c>[PacketField]</c> to a static property produces PWG007.
    /// </summary>
    [Fact]
    public void StaticPacketFieldProducesPWG007()
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
                public static int Value { get; set; }
            }
            """;

        AssertHasDiagnostic(
            source,
            "PWG007");
    }

    /// <summary>
    /// Verifies that applying <c>[PacketField]</c> to a read-only property without a setter/init accessor produces PWG007.
    /// </summary>
    [Fact]
    public void ReadOnlyPacketFieldProducesPWG007()
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
                public int Value { get; }
            }
            """;

        AssertHasDiagnostic(
            source,
            "PWG007");
    }

    /// <summary>
    /// Verifies that specifying a negative field order index produces PWG008.
    /// </summary>
    [Fact]
    public void NegativeFieldOrderProducesPWG008()
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
                [PacketField(-1)]
                public int Value { get; init; }
            }
            """;

        AssertHasDiagnostic(
            source,
            "PWG008");
    }

    /// <summary>
    /// Verifies that using an unsupported .NET framework type as a packet field produces PWG009.
    /// </summary>
    [Fact]
    public void UnsupportedFrameworkTypeProducesPWG009()
    {
        const string source = """
            using System;
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
                public DateTime Value { get; init; }
            }
            """;

        AssertHasDiagnostic(
            source,
            "PWG009");
    }

    /// <summary>
    /// Verifies that a string field without a <c>[FixedString]</c> attribute produces PWG010.
    /// </summary>
    [Fact]
    public void StringWithoutFixedStringProducesPWG010()
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
                public string Value { get; init; }
            }
            """;

        AssertHasDiagnostic(
            source,
            "PWG010");
    }

    /// <summary>
    /// Verifies that specifying an invalid fixed string byte count (e.g. 0) produces PWG011.
    /// </summary>
    [Fact]
    public void InvalidFixedStringLengthProducesPWG011()
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
                [FixedString(0)]
                public string Value { get; init; }
            }
            """;

        AssertHasDiagnostic(
            source,
            "PWG011");
    }

    /// <summary>
    /// Verifies that declaring a nullable field without the <c>[Optional]</c> attribute produces PWG012.
    /// </summary>
    [Fact]
    public void NullableFieldWithoutOptionalProducesPWG012()
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
                [FixedString(32)]
                public string? Value { get; init; }
            }
            """;

        AssertHasDiagnostic(
            source,
            "PWG012");
    }

    /// <summary>
    /// Verifies that applying <c>[Optional]</c> to a non-nullable field produces PWG013.
    /// </summary>
    [Fact]
    public void OptionalOnNonNullableFieldProducesPWG013()
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
                [Optional]
                public int Value { get; init; }
            }
            """;

        AssertHasDiagnostic(
            source,
            "PWG013");
    }

    /// <summary>
    /// Verifies that declaring a custom nested type without <c>[PacketContract]</c> produces PWG014.
    /// </summary>
    [Fact]
    public void CustomNestedTypeWithoutPacketContractProducesPWG014()
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

            public sealed class NestedValue
            {
                public int Value { get; init; }
            }

            [Packet(typeof(TestProtocol), 1)]
            public sealed class TestPacket
            {
                [PacketField(0)]
                public NestedValue Value { get; init; }
            }
            """;

        AssertHasDiagnostic(
            source,
            "PWG014");
    }

    /// <summary>
    /// Verifies that applying <c>[MaxCount]</c> to a scalar (non-collection) field produces PWG015.
    /// </summary>
    [Fact]
    public void MaxCountOnScalarProducesPWG015()
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
                [MaxCount(10)]
                public int Value { get; init; }
            }
            """;

        AssertHasDiagnostic(
            source,
            "PWG015");
    }

    /// <summary>
    /// Verifies that specifying a negative max count in <c>[MaxCount]</c> produces PWG016.
    /// </summary>
    [Fact]
    public void NegativeMaxCountProducesPWG016()
    {
        const string source = """
            using System.Collections.Generic;
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
                [MaxCount(-1)]
                public List<int> Values { get; init; }
            }
            """;

        AssertHasDiagnostic(
            source,
            "PWG016");
    }

    /// <summary>
    /// Verifies that collections with nullable element types produce PWG017.
    /// </summary>
    [Fact]
    public void NullableCollectionElementProducesPWG017()
    {
        const string source = """
            using System.Collections.Generic;
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
                [FixedString(32)]
                public List<string?> Values { get; init; }
            }
            """;

        AssertHasDiagnostic(
            source,
            "PWG017");
    }

    /// <summary>
    /// Verifies that field rules are equally enforced within nested types decorated with <c>[PacketContract]</c>.
    /// </summary>
    [Fact]
    public void PacketContractFieldsAreValidated()
    {
        const string source = """
            using PacketWire;

            [PacketContract]
            public sealed class NestedContract
            {
                public int Value { get; init; }
            }
            """;

        AssertHasDiagnostic(
            source,
            "PWG005");
    }

    /// <summary>
    /// Verifies that duplicate field order indices within a <c>[PacketContract]</c> produce PWG004.
    /// </summary>
    [Fact]
    public void DuplicateFieldOrderInsidePacketContractProducesPWG004()
    {
        const string source = """
            using PacketWire;

            [PacketContract]
            public sealed class NestedContract
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
    /// Verifies that a packet containing the full supported matrix of primitives, enums, fixed strings,
    /// collections, optional fields, and nested contracts compiles with zero generator errors.
    /// </summary>
    [Fact]
    public void SupportedFieldMatrixProducesNoGeneratorErrors()
    {
        const string source = """
            using System;
            using System.Collections.Generic;
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

            [PacketContract]
            public sealed class NestedContract
            {
                [PacketField(0)]
                public int Id { get; init; }

                [PacketField(1)]
                [FixedString(16)]
                public string Name { get; init; }
            }

            [Packet(typeof(TestProtocol), 1)]
            public sealed class TestPacket
            {
                [PacketField(0)]
                public byte ByteValue { get; init; }

                [PacketField(1)]
                public sbyte SignedByteValue { get; init; }

                [PacketField(2)]
                public short ShortValue { get; init; }

                [PacketField(3)]
                public ushort UnsignedShortValue { get; init; }

                [PacketField(4)]
                public int IntValue { get; init; }

                [PacketField(5)]
                public uint UnsignedIntValue { get; init; }

                [PacketField(6)]
                public long LongValue { get; init; }

                [PacketField(7)]
                public ulong UnsignedLongValue { get; init; }

                [PacketField(8)]
                public float SingleValue { get; init; }

                [PacketField(9)]
                public double DoubleValue { get; init; }

                [PacketField(10)]
                public bool BooleanValue { get; init; }

                [PacketField(11)]
                public TestMode Mode { get; init; }

                [PacketField(12)]
                [FixedString(32)]
                public string Name { get; init; }

                [PacketField(13)]
                [MaxCount(100)]
                public List<int> Numbers { get; init; }

                [PacketField(14)]
                [MaxCount(10)]
                [FixedString(16)]
                public string[] Tags { get; init; }

                [PacketField(15)]
                public NestedContract Nested { get; init; }

                [PacketField(16)]
                [Optional]
                public int? OptionalNumber { get; init; }

                [PacketField(17)]
                [Optional]
                [FixedString(32)]
                public string? OptionalName { get; init; }

                [PacketField(18)]
                [Optional]
                [MaxCount(50)]
                public List<int>? OptionalNumbers { get; init; }

                [PacketIgnore]
                public DateTime RuntimeOnlyValue { get; init; }

                public static int StaticValue { get; set; }
            }
            """;

        AssertNoErrors(source);
    }

    /// <summary>
    /// Asserts that generator analysis on the given source produces a diagnostic with the specified ID.
    /// </summary>
    /// <param name="source">The source code to compile and analyze.</param>
    /// <param name="diagnosticId">The expected diagnostic identifier.</param>
    private static void AssertHasDiagnostic(
        string source,
        string diagnosticId)
    {
        ImmutableArray<Diagnostic> diagnostics =
            GeneratorTestHost.Run(source);

        Assert.Contains(
            diagnostics,
            diagnostic =>
                diagnostic.Id == diagnosticId);
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

