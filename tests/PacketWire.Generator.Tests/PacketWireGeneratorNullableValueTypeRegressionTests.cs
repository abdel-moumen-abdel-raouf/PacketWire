using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using PacketWire;
using Xunit;

namespace PacketWire.Generator.Tests;

/// <summary>
/// Regression tests verifying that optional nullable value types (<see cref="Nullable{T}"/>)
/// are safely snapshotted once, compile without CS8629 under strict nullability, and preserve
/// protocol wire round-trip fidelity for both null and present states.
/// </summary>
public sealed class PacketWireGeneratorNullableValueTypeRegressionTests
{
    /// <summary>
    /// Shared test source containing a protocol, enum, and packet with optional nullable value-type fields.
    /// </summary>
    private const string NullableProtocolSource = """
        using PacketWire;

        namespace PacketWire.Generator.Tests.Cases
        {
            public enum RegressionTestStatus : byte
            {
                Inactive = 0,
                Active = 1
            }

            [PacketProtocol(
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketByteOrder.BigEndian,
                PacketIntegerSize.OneByte)]
            public sealed partial class RegressionProtocol
            {
            }

            [Packet(typeof(RegressionProtocol), 0x01, 0x0001)]
            public sealed class NullableRegressionPacket
            {
                [PacketField(0)]
                [Optional]
                public int? OptionalNumber { get; init; }

                [PacketField(1)]
                [Optional]
                public RegressionTestStatus? OptionalStatus { get; init; }
            }
        }
        """;

    /// <summary>
    /// Expected serialized frame bytes when optional fields are null.
    /// Total frame length 8 bytes (0x0008), Flags 0x00, Category 0x01, PacketId 0x0001, Payload: Presence 0x00, Presence 0x00.
    /// </summary>
    private static readonly byte[] ExpectedNullFrame =
    [
        0x00, 0x08,
        0x00,
        0x01,
        0x00, 0x01,
        0x00,
        0x00
    ];

    /// <summary>
    /// Expected serialized frame bytes when optional fields are present.
    /// Total frame length 13 bytes (0x000D), Flags 0x00, Category 0x01, PacketId 0x0001, Payload: Presence 0x01, Number 0x11223344, Presence 0x01, Status 0x01.
    /// </summary>
    private static readonly byte[] ExpectedPresentFrame =
    [
        0x00, 0x0D,
        0x00,
        0x01,
        0x00, 0x01,
        0x01,
        0x11, 0x22, 0x33, 0x44,
        0x01,
        0x01
    ];

    /// <summary>
    /// TEST 1: Verifies that optional nullable value types are snapshotted into local variables,
    /// compile cleanly under strict nullability without CS8629, and do not use null-forgiving operators.
    /// </summary>
    [Fact]
    public void NullableValueTypeCompilationRegressionEmitsSnapshotWithoutCS8629()
    {
        GeneratorTestResult result =
            GeneratorTestHost.RunWithOutput(NullableProtocolSource);

        ImmutableArray<Diagnostic> diagnostics =
            result.OutputCompilation.GetDiagnostics();

        Assert.DoesNotContain(
            diagnostics,
            static diagnostic =>
                diagnostic.Id == "CS8629");

        Assert.DoesNotContain(
            diagnostics,
            static diagnostic =>
                diagnostic.Severity == DiagnosticSeverity.Error);

        Assert.DoesNotContain(
            diagnostics,
            static diagnostic =>
                diagnostic.Severity == DiagnosticSeverity.Warning);

        Assert.DoesNotContain(
            result.GeneratorDiagnostics,
            static diagnostic =>
                diagnostic.Severity == DiagnosticSeverity.Error);

        GeneratedSourceResult generated =
            Assert.Single(
                result.GeneratedSources.Where(
                    static source =>
                        source.HintName.Contains("NullableRegressionPacket", StringComparison.Ordinal)));

        string generatedText =
            generated.SourceText.ToString();

        Assert.Contains(
            "int? __optional_0 = value.@OptionalNumber;",
            generatedText,
            StringComparison.Ordinal);

        Assert.Contains(
            "bool __present_0 = __optional_0.HasValue;",
            generatedText,
            StringComparison.Ordinal);

        Assert.Contains(
            "global::PacketWire.PacketPresenceCodec.Write(ref writer, __present_0);",
            generatedText,
            StringComparison.Ordinal);

        Assert.Contains(
            "if (__optional_0.HasValue)",
            generatedText,
            StringComparison.Ordinal);

        Assert.Contains(
            "writer.WriteInt32(__optional_0.Value);",
            generatedText,
            StringComparison.Ordinal);

        Assert.Contains(
            "__optional_1 = value.@OptionalStatus;",
            generatedText,
            StringComparison.Ordinal);

        Assert.Contains(
            "bool __present_1 = __optional_1.HasValue;",
            generatedText,
            StringComparison.Ordinal);

        Assert.Contains(
            "global::PacketWire.PacketPresenceCodec.Write(ref writer, __present_1);",
            generatedText,
            StringComparison.Ordinal);

        Assert.Contains(
            "if (__optional_1.HasValue)",
            generatedText,
            StringComparison.Ordinal);

        Assert.Contains(
            "writer.WriteByte((byte)__optional_1.Value);",
            generatedText,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "!.Value",
            generatedText,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "OptionalNumber!",
            generatedText,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "OptionalStatus!",
            generatedText,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// TEST 2: Verifies wire round-trip fidelity for nullable value types in both null and present states,
    /// proving identical wire presence byte encoding and exact property reconstruction.
    /// </summary>
    [Fact]
    public void NullableValueTypeWireRoundTripPreservesValueAndNullSemantics()
    {
        GeneratorTestResult result =
            GeneratorTestHost.RunWithOutput(NullableProtocolSource);

        const string harnessSource = """
            namespace PacketWire.Generator.Tests.Dynamic
            {
                public static class NullableRoundTripHarness
                {
                    public static byte[] SerializeNull()
                    {
                        return global::PacketWire.Generator.Tests.Cases.RegressionProtocol.Serialize(
                            new global::PacketWire.Generator.Tests.Cases.NullableRegressionPacket
                            {
                                OptionalNumber = null,
                                OptionalStatus = null
                            });
                    }

                    public static bool RoundTripNull()
                    {
                        byte[] bytes = SerializeNull();
                        global::PacketWire.Generator.Tests.Cases.NullableRegressionPacket packet =
                            global::PacketWire.Generator.Tests.Cases.RegressionProtocol.Deserialize<global::PacketWire.Generator.Tests.Cases.NullableRegressionPacket>(bytes);

                        return packet.OptionalNumber is null && packet.OptionalStatus is null;
                    }

                    public static byte[] SerializePresent()
                    {
                        return global::PacketWire.Generator.Tests.Cases.RegressionProtocol.Serialize(
                            new global::PacketWire.Generator.Tests.Cases.NullableRegressionPacket
                            {
                                OptionalNumber = 0x11223344,
                                OptionalStatus = global::PacketWire.Generator.Tests.Cases.RegressionTestStatus.Active
                            });
                    }

                    public static bool RoundTripPresent()
                    {
                        byte[] bytes = SerializePresent();
                        global::PacketWire.Generator.Tests.Cases.NullableRegressionPacket packet =
                            global::PacketWire.Generator.Tests.Cases.RegressionProtocol.Deserialize<global::PacketWire.Generator.Tests.Cases.NullableRegressionPacket>(bytes);

                        return packet.OptionalNumber == 0x11223344 &&
                               packet.OptionalStatus == global::PacketWire.Generator.Tests.Cases.RegressionTestStatus.Active;
                    }
                }
            }
            """;

        byte[] nullBytes =
            GeneratorTestHost.InvokeHarness<byte[]>(
                result,
                harnessSource,
                "PacketWire.Generator.Tests.Dynamic.NullableRoundTripHarness",
                "SerializeNull");

        Assert.Equal(
            ExpectedNullFrame,
            nullBytes);

        bool nullRoundTrip =
            GeneratorTestHost.InvokeHarness<bool>(
                result,
                harnessSource,
                "PacketWire.Generator.Tests.Dynamic.NullableRoundTripHarness",
                "RoundTripNull");

        Assert.True(nullRoundTrip);

        byte[] presentBytes =
            GeneratorTestHost.InvokeHarness<byte[]>(
                result,
                harnessSource,
                "PacketWire.Generator.Tests.Dynamic.NullableRoundTripHarness",
                "SerializePresent");

        Assert.Equal(
            ExpectedPresentFrame,
            presentBytes);

        bool presentRoundTrip =
            GeneratorTestHost.InvokeHarness<bool>(
                result,
                harnessSource,
                "PacketWire.Generator.Tests.Dynamic.NullableRoundTripHarness",
                "RoundTripPresent");

        Assert.True(presentRoundTrip);
    }
}
