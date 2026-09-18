using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace PacketWire.Generator.Tests;

/// <summary>
/// Verifies execution, exact wire bytes, round-trip serialization, optional fields, collection limits, and byte ordering in generated codecs.
/// </summary>
public sealed class PacketWireGeneratedCodecExecutionTests
{
    /// <summary>
    /// Expected encoded lengths for permutations of optional reference and value type fields.
    /// </summary>
    private static readonly int[] ExpectedOptionalLengths =
    [
        2,
        6,
        6,
        10
    ];

    /// <summary>
    /// Expected big-endian byte sequence for array encoding tests.
    /// </summary>
    private static readonly byte[] ExpectedBigEndianBytes =
    [
        0x11,
        0x22,
        0x33,
        0x44,
        0x02,
        0x55,
        0x66,
        0x77,
        0x88
    ];

    /// <summary>
    /// Verifies that a complex packet with nested contracts, enums, collections, and primitives serializes to exact expected bytes and round-trips.
    /// </summary>
    [Fact]
    public void ComplexGeneratedCodecProducesExactBytesAndRoundTrips()
    {
        const string source = """
            using System.Collections.Generic;
            using PacketWire;

            public enum TestMode : ushort
            {
                None = 0,
                Active = 0x1234
            }

            [PacketContract]
            public sealed class Address
            {
                [PacketField(0)]
                public int CityId { get; init; }

                [PacketField(1)]
                [FixedString(4)]
                public string Street { get; init; } = string.Empty;
            }

            [PacketContract]
            public sealed class ComplexPacket
            {
                [PacketField(0)]
                public byte Version { get; init; }

                [PacketField(1)]
                [FixedString(8)]
                public string ArabicName { get; init; } = string.Empty;

                [PacketField(2)]
                public TestMode Mode { get; init; }

                [PacketField(3)]
                [Optional]
                public int? OptionalNumber { get; init; }

                [PacketField(4)]
                [Optional]
                [FixedString(6)]
                public string? OptionalAlias { get; init; }

                [PacketField(5)]
                [MaxCount(3)]
                public List<ushort> Values { get; init; } = new();

                [PacketField(6)]
                public Address Address { get; init; } = new();

                [PacketField(7)]
                [MaxCount(2)]
                [FixedString(4)]
                public string[] Tags { get; init; } = [];
            }
            """;

        GeneratorTestResult result =
            GeneratorTestHost.RunWithOutput(source);

        AssertSuccessfulCompilation(result);

        string codecName =
            GetCodecClassName(
                result,
                "ComplexPacket");

        string harnessSource = $$"""
            namespace PacketWire.Generator.Tests.Dynamic
            {
                public static class GeneratedCodecHarness
                {
                    private static global::PacketWire.PacketProtocolDefinition CreateDefinition()
                    {
                        return new global::PacketWire.PacketProtocolDefinition(
                            global::PacketWire.PacketIntegerSize.TwoBytes,
                            global::PacketWire.PacketIntegerSize.TwoBytes,
                            global::PacketWire.PacketIntegerSize.TwoBytes,
                            global::PacketWire.PacketByteOrder.LittleEndian);
                    }

                    public static byte[] Serialize()
                    {
                        global::PacketWire.PacketProtocolDefinition definition =
                            CreateDefinition();

                        global::ComplexPacket value =
                            new global::ComplexPacket
                            {
                                Version = 0x7F,
                                ArabicName = "علي",
                                Mode = global::TestMode.Active,
                                OptionalNumber = 0x01020304,
                                OptionalAlias = null,
                                Values =
                                    new global::System.Collections.Generic.List<ushort>
                                    {
                                        0x1122,
                                        0x3344
                                    },
                                Address =
                                    new global::Address
                                    {
                                        CityId = 0x55667788,
                                        Street = "AB"
                                    },
                                Tags =
                                [
                                    "A",
                                    "BC"
                                ]
                            };

                        int length =
                            global::PacketWire.Generated.{{codecName}}.GetEncodedLength(
                                value,
                                definition);

                        if (length != 41)
                        {
                            throw new global::System.InvalidOperationException(
                                $"Unexpected encoded length: {length}.");
                        }

                        byte[] buffer =
                            new byte[length];

                        global::PacketWire.PacketWriter writer =
                            new global::PacketWire.PacketWriter(
                                buffer,
                                definition.ByteOrder);

                        global::PacketWire.Generated.{{codecName}}.Write(
                            ref writer,
                            value,
                            definition);

                        if (writer.WrittenCount != length)
                        {
                            throw new global::System.InvalidOperationException(
                                $"Written byte count {writer.WrittenCount} does not match encoded length {length}.");
                        }

                        return buffer;
                    }

                    public static bool RoundTrip()
                    {
                        byte[] buffer =
                            Serialize();

                        global::PacketWire.PacketProtocolDefinition definition =
                            CreateDefinition();

                        global::PacketWire.PacketReader reader =
                            new global::PacketWire.PacketReader(
                                buffer,
                                definition.ByteOrder);

                        global::ComplexPacket value =
                            global::PacketWire.Generated.{{codecName}}.Read(
                                ref reader,
                                definition);

                        return
                            reader.Remaining == 0 &&
                            value.Version == 0x7F &&
                            value.ArabicName == "علي" &&
                            value.Mode == global::TestMode.Active &&
                            value.OptionalNumber == 0x01020304 &&
                            value.OptionalAlias is null &&
                            value.Values.Count == 2 &&
                            value.Values[0] == 0x1122 &&
                            value.Values[1] == 0x3344 &&
                            value.Address.CityId == 0x55667788 &&
                            value.Address.Street == "AB" &&
                            value.Tags.Length == 2 &&
                            value.Tags[0] == "A" &&
                            value.Tags[1] == "BC";
                    }
                }
            }
            """;

        byte[] actual =
            GeneratorTestHost.InvokeHarness<byte[]>(
                result,
                harnessSource,
                "PacketWire.Generator.Tests.Dynamic.GeneratedCodecHarness",
                "Serialize");

        byte[] expected =
        [
            0x7F,

            0xD8,
            0xB9,
            0xD9,
            0x84,
            0xD9,
            0x8A,
            0x00,
            0x00,

            0x34,
            0x12,

            0x01,
            0x04,
            0x03,
            0x02,
            0x01,

            0x00,

            0x02,
            0x00,

            0x22,
            0x11,
            0x44,
            0x33,

            0x88,
            0x77,
            0x66,
            0x55,

            0x41,
            0x42,
            0x00,
            0x00,

            0x02,
            0x00,

            0x41,
            0x00,
            0x00,
            0x00,

            0x42,
            0x43,
            0x00,
            0x00
        ];

        Assert.Equal(
            expected,
            actual);

        bool roundTripSucceeded =
            GeneratorTestHost.InvokeHarness<bool>(
                result,
                harnessSource,
                "PacketWire.Generator.Tests.Dynamic.GeneratedCodecHarness",
                "RoundTrip");

        Assert.True(
            roundTripSucceeded);
    }

    /// <summary>
    /// Verifies that optional presence flags change the calculated and encoded wire lengths deterministically.
    /// </summary>
    [Fact]
    public void OptionalPresenceChangesEncodedLengthDeterministically()
    {
        const string source = """
            using PacketWire;

            [PacketContract]
            public sealed class OptionalContract
            {
                [PacketField(0)]
                [Optional]
                public int? Number { get; init; }

                [PacketField(1)]
                [Optional]
                [FixedString(4)]
                public string? Name { get; init; }
            }
            """;

        GeneratorTestResult result =
            GeneratorTestHost.RunWithOutput(source);

        AssertSuccessfulCompilation(result);

        string codecName =
            GetCodecClassName(
                result,
                "OptionalContract");

        string harnessSource = $$"""
            namespace PacketWire.Generator.Tests.Dynamic
            {
                public static class OptionalHarness
                {
                    public static int[] GetLengths()
                    {
                        global::PacketWire.PacketProtocolDefinition definition =
                            new global::PacketWire.PacketProtocolDefinition(
                                global::PacketWire.PacketIntegerSize.TwoBytes,
                                global::PacketWire.PacketIntegerSize.TwoBytes,
                                global::PacketWire.PacketIntegerSize.TwoBytes,
                                global::PacketWire.PacketByteOrder.LittleEndian);

                        global::OptionalContract none =
                            new global::OptionalContract();

                        global::OptionalContract number =
                            new global::OptionalContract
                            {
                                Number = 42
                            };

                        global::OptionalContract name =
                            new global::OptionalContract
                            {
                                Name = "A"
                            };

                        global::OptionalContract both =
                            new global::OptionalContract
                            {
                                Number = 42,
                                Name = "A"
                            };

                        return
                        [
                            global::PacketWire.Generated.{{codecName}}.GetEncodedLength(
                                none,
                                definition),

                            global::PacketWire.Generated.{{codecName}}.GetEncodedLength(
                                number,
                                definition),

                            global::PacketWire.Generated.{{codecName}}.GetEncodedLength(
                                name,
                                definition),

                            global::PacketWire.Generated.{{codecName}}.GetEncodedLength(
                                both,
                                definition)
                        ];
                    }
                }
            }
            """;

        int[] lengths =
            GeneratorTestHost.InvokeHarness<int[]>(
                result,
                harnessSource,
                "PacketWire.Generator.Tests.Dynamic.OptionalHarness",
                "GetLengths");

        Assert.Equal(
            ExpectedOptionalLengths,
            lengths);
    }

    /// <summary>
    /// Verifies that <see cref="PacketWire.MaxCountAttribute"/> is enforced during both length calculation and reading.
    /// </summary>
    [Fact]
    public void MaxCountIsEnforcedDuringLengthCalculationAndRead()
    {
        const string source = """
            using System.Collections.Generic;
            using PacketWire;

            [PacketContract]
            public sealed class LimitedContract
            {
                [PacketField(0)]
                [MaxCount(2)]
                public List<int> Values { get; init; } = new();
            }
            """;

        GeneratorTestResult result =
            GeneratorTestHost.RunWithOutput(source);

        AssertSuccessfulCompilation(result);

        string codecName =
            GetCodecClassName(
                result,
                "LimitedContract");

        string harnessSource = $$"""
            namespace PacketWire.Generator.Tests.Dynamic
            {
                public static class MaxCountHarness
                {
                    private static global::PacketWire.PacketProtocolDefinition CreateDefinition()
                    {
                        return new global::PacketWire.PacketProtocolDefinition(
                            global::PacketWire.PacketIntegerSize.TwoBytes,
                            global::PacketWire.PacketIntegerSize.TwoBytes,
                            global::PacketWire.PacketIntegerSize.TwoBytes,
                            global::PacketWire.PacketByteOrder.LittleEndian);
                    }

                    public static bool LengthRejectsOversizedCollection()
                    {
                        global::LimitedContract value =
                            new global::LimitedContract
                            {
                                Values =
                                    new global::System.Collections.Generic.List<int>
                                    {
                                        1,
                                        2,
                                        3
                                    }
                            };

                        try
                        {
                            _ =
                                global::PacketWire.Generated.{{codecName}}.GetEncodedLength(
                                    value,
                                    CreateDefinition());

                            return false;
                        }
                        catch (global::System.ArgumentOutOfRangeException)
                        {
                            return true;
                        }
                    }

                    public static bool ReadRejectsOversizedCollection()
                    {
                        byte[] malformed =
                        [
                            0x03,
                            0x00
                        ];

                        global::PacketWire.PacketProtocolDefinition definition =
                            CreateDefinition();

                        global::PacketWire.PacketReader reader =
                            new global::PacketWire.PacketReader(
                                malformed,
                                definition.ByteOrder);

                        try
                        {
                            _ =
                                global::PacketWire.Generated.{{codecName}}.Read(
                                    ref reader,
                                    definition);

                            return false;
                        }
                        catch (global::PacketWire.PacketBufferException)
                        {
                            return true;
                        }
                    }
                }
            }
            """;

        bool lengthRejected =
            GeneratorTestHost.InvokeHarness<bool>(
                result,
                harnessSource,
                "PacketWire.Generator.Tests.Dynamic.MaxCountHarness",
                "LengthRejectsOversizedCollection");

        bool readRejected =
            GeneratorTestHost.InvokeHarness<bool>(
                result,
                harnessSource,
                "PacketWire.Generator.Tests.Dynamic.MaxCountHarness",
                "ReadRejectsOversizedCollection");

        Assert.True(lengthRejected);
        Assert.True(readRejected);
    }

    /// <summary>
    /// Verifies that generated array serialization respects big-endian protocol configuration.
    /// </summary>
    [Fact]
    public void GeneratedArrayCodecRespectsBigEndianProtocol()
    {
        const string source = """
            using PacketWire;

            [PacketContract]
            public sealed class BigEndianContract
            {
                [PacketField(0)]
                public uint Header { get; init; }

                [PacketField(1)]
                [MaxCount(3)]
                public ushort[] Values { get; init; } = [];
            }
            """;

        GeneratorTestResult result =
            GeneratorTestHost.RunWithOutput(source);

        AssertSuccessfulCompilation(result);

        string codecName =
            GetCodecClassName(
                result,
                "BigEndianContract");

        string harnessSource = $$"""
            namespace PacketWire.Generator.Tests.Dynamic
            {
                public static class BigEndianHarness
                {
                    private static global::PacketWire.PacketProtocolDefinition CreateDefinition()
                    {
                        return new global::PacketWire.PacketProtocolDefinition(
                            global::PacketWire.PacketIntegerSize.TwoBytes,
                            global::PacketWire.PacketIntegerSize.TwoBytes,
                            global::PacketWire.PacketIntegerSize.OneByte,
                            global::PacketWire.PacketByteOrder.BigEndian);
                    }

                    public static byte[] Serialize()
                    {
                        global::BigEndianContract value =
                            new global::BigEndianContract
                            {
                                Header = 0x11223344,
                                Values =
                                [
                                    0x5566,
                                    0x7788
                                ]
                            };

                        global::PacketWire.PacketProtocolDefinition definition =
                            CreateDefinition();

                        int length =
                            global::PacketWire.Generated.{{codecName}}.GetEncodedLength(
                                value,
                                definition);

                        byte[] buffer =
                            new byte[length];

                        global::PacketWire.PacketWriter writer =
                            new global::PacketWire.PacketWriter(
                                buffer,
                                definition.ByteOrder);

                        global::PacketWire.Generated.{{codecName}}.Write(
                            ref writer,
                            value,
                            definition);

                        return buffer;
                    }

                    public static bool RoundTrip()
                    {
                        byte[] buffer =
                            Serialize();

                        global::PacketWire.PacketProtocolDefinition definition =
                            CreateDefinition();

                        global::PacketWire.PacketReader reader =
                            new global::PacketWire.PacketReader(
                                buffer,
                                definition.ByteOrder);

                        global::BigEndianContract value =
                            global::PacketWire.Generated.{{codecName}}.Read(
                                ref reader,
                                definition);

                        return
                            reader.Remaining == 0 &&
                            value.Header == 0x11223344 &&
                            value.Values.Length == 2 &&
                            value.Values[0] == 0x5566 &&
                            value.Values[1] == 0x7788;
                    }
                }
            }
            """;

        byte[] bytes =
            GeneratorTestHost.InvokeHarness<byte[]>(
                result,
                harnessSource,
                "PacketWire.Generator.Tests.Dynamic.BigEndianHarness",
                "Serialize");

        Assert.Equal(
            ExpectedBigEndianBytes,
            bytes);

        Assert.True(
            GeneratorTestHost.InvokeHarness<bool>(
                result,
                harnessSource,
                "PacketWire.Generator.Tests.Dynamic.BigEndianHarness",
                "RoundTrip"));
    }

    /// <summary>
    /// Extracts the internal generated codec class name for the specified wire type.
    /// </summary>
    /// <param name="result">The generator test result.</param>
    /// <param name="wireTypeName">The wire type name.</param>
    /// <returns>The generated codec class name.</returns>
    private static string GetCodecClassName(
        GeneratorTestResult result,
        string wireTypeName)
    {
        string typeMarker =
            $"global::{wireTypeName} value";

        GeneratedSourceResult generated =
            Assert.Single(
                result.GeneratedSources.Where(
                    generatedSource =>
                        generatedSource
                            .SourceText
                            .ToString()
                            .Contains(
                                typeMarker,
                                StringComparison.Ordinal)));

        string generatedText =
            generated.SourceText.ToString();

        const string classMarker =
            "internal static class ";

        int classMarkerIndex =
            generatedText.IndexOf(
                classMarker,
                StringComparison.Ordinal);

        Assert.True(
            classMarkerIndex >= 0);

        int classNameStart =
            classMarkerIndex +
            classMarker.Length;

        int classNameEnd =
            generatedText.IndexOf(
                '\n',
                classNameStart);

        Assert.True(
            classNameEnd > classNameStart);

        return generatedText[
                classNameStart..
                classNameEnd]
            .Trim();
    }

    /// <summary>
    /// Asserts that generator diagnostics and output compilation contain no compilation errors.
    /// </summary>
    /// <param name="result">The generator test result.</param>
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

