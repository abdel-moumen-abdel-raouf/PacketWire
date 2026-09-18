using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace PacketWire.Generator.Tests;

/// <summary>
/// Verifies that generated facades reject malformed collections, count mismatches, truncated item payloads, and nested contract parsing failures.
/// </summary>
public sealed class PacketWireGeneratedMalformedCollectionTests
{
    /// <summary>
    /// Verifies that deserialization throws <see cref="PacketWire.PacketBufferException"/> when collection counts claim more items than payload bytes allow or nested contracts are malformed.
    /// </summary>
    [Fact]
    public void PublicFacadeRejectsMalformedCollectionsAndNestedContracts()
    {
        const string source = """
            using System.Collections.Generic;
            using PacketWire;

            [PacketProtocol(
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketByteOrder.LittleEndian)]
            public sealed partial class ApplicationProtocol
            {
            }

            [Packet(typeof(ApplicationProtocol), 1, 7)]
            public sealed class ArrayPacket
            {
                [PacketField(0)]
                [MaxCount(2)]
                public int[] Values { get; init; } = [];
            }

            [Packet(typeof(ApplicationProtocol), 2, 7)]
            public sealed class ListPacket
            {
                [PacketField(0)]
                [MaxCount(2)]
                public List<ushort> Values { get; init; } = new();
            }

            [PacketContract]
            public sealed class NestedValue
            {
                [PacketField(0)]
                public int Code { get; init; }

                [PacketField(1)]
                [FixedString(4)]
                public string Name { get; init; } = string.Empty;
            }

            [Packet(typeof(ApplicationProtocol), 3, 7)]
            public sealed class NestedPacket
            {
                [PacketField(0)]
                public NestedValue Value { get; init; } = new();
            }
            """;

        GeneratorTestResult result =
            GeneratorTestHost.RunWithOutput(
                source);

        AssertSuccessfulCompilation(
            result);

        const string harnessSource = """
            namespace PacketWire.Generator.Tests.Dynamic
            {
                public static class MalformedCollectionHarness
                {
                    public static bool ValidateAll()
                    {
                        //
                        // Array count = 3, while [MaxCount(2)].
                        //
                        byte[] oversizedArrayCount =
                            BuildFrame(
                                7,
                                1,
                                new byte[]
                                {
                                    0x03,
                                    0x00
                                });

                        if (!ExpectBufferFailure(
                                oversizedArrayCount))
                        {
                            return false;
                        }

                        //
                        // List count = 3, while [MaxCount(2)].
                        //
                        byte[] oversizedListCount =
                            BuildFrame(
                                7,
                                2,
                                new byte[]
                                {
                                    0x03,
                                    0x00
                                });

                        if (!ExpectBufferFailure(
                                oversizedListCount))
                        {
                            return false;
                        }

                        //
                        // Array claims two Int32 elements,
                        // but only one Int32 follows the count.
                        //
                        byte[] truncatedArrayElements =
                            BuildFrame(
                                7,
                                1,
                                new byte[]
                                {
                                    0x02,
                                    0x00,

                                    0x44,
                                    0x33,
                                    0x22,
                                    0x11
                                });

                        if (!ExpectBufferFailure(
                                truncatedArrayElements))
                        {
                            return false;
                        }

                        //
                        // List claims two UInt16 elements,
                        // but only one UInt16 follows the count.
                        //
                        byte[] truncatedListElements =
                            BuildFrame(
                                7,
                                2,
                                new byte[]
                                {
                                    0x02,
                                    0x00,

                                    0x22,
                                    0x11
                                });

                        if (!ExpectBufferFailure(
                                truncatedListElements))
                        {
                            return false;
                        }

                        //
                        // NestedValue requires:
                        //
                        // Int32 Code      = 4 bytes
                        // FixedString(4)  = 4 bytes
                        //
                        // Supply only seven payload bytes.
                        //
                        byte[] truncatedNestedContract =
                            BuildFrame(
                                7,
                                3,
                                new byte[]
                                {
                                    0x44,
                                    0x33,
                                    0x22,
                                    0x11,

                                    0x41,
                                    0x00,
                                    0x00
                                });

                        if (!ExpectBufferFailure(
                                truncatedNestedContract))
                        {
                            return false;
                        }

                        //
                        // Valid nested contract control case.
                        //
                        byte[] validNestedContract =
                            BuildFrame(
                                7,
                                3,
                                new byte[]
                                {
                                    0x44,
                                    0x33,
                                    0x22,
                                    0x11,

                                    0x41,
                                    0x00,
                                    0x00,
                                    0x00
                                });

                        global::NestedPacket nested =
                            global::ApplicationProtocol.Deserialize<global::NestedPacket>(
                                validNestedContract);

                        if (nested.Value.Code !=
                            0x11223344)
                        {
                            return false;
                        }

                        if (nested.Value.Name !=
                            "A")
                        {
                            return false;
                        }

                        //
                        // Valid array control case.
                        //
                        byte[] validArray =
                            global::ApplicationProtocol.Serialize(
                                new global::ArrayPacket
                                {
                                    Values =
                                    [
                                        1,
                                        2
                                    ]
                                });

                        global::ArrayPacket decodedArray =
                            global::ApplicationProtocol.Deserialize<global::ArrayPacket>(
                                validArray);

                        if (decodedArray.Values.Length != 2 ||
                            decodedArray.Values[0] != 1 ||
                            decodedArray.Values[1] != 2)
                        {
                            return false;
                        }

                        //
                        // Valid list control case.
                        //
                        byte[] validList =
                            global::ApplicationProtocol.Serialize(
                                new global::ListPacket
                                {
                                    Values =
                                        new global::System.Collections.Generic.List<ushort>
                                        {
                                            0x1122,
                                            0x3344
                                        }
                                });

                        global::ListPacket decodedList =
                            global::ApplicationProtocol.Deserialize<global::ListPacket>(
                                validList);

                        return
                            decodedList.Values.Count == 2 &&
                            decodedList.Values[0] == 0x1122 &&
                            decodedList.Values[1] == 0x3344;
                    }

                    private static byte[] BuildFrame(
                        ulong category,
                        ulong id,
                        byte[] payload)
                    {
                        global::PacketWire.PacketProtocolDefinition definition =
                            global::ApplicationProtocol.Definition;

                        ulong frameLength =
                            global::PacketWire.PacketFrameCodec.CalculateFrameLength(
                                payload.Length,
                                definition);

                        if (frameLength >
                            int.MaxValue)
                        {
                            throw new global::System.InvalidOperationException(
                                "Test frame is unexpectedly too large.");
                        }

                        byte[] frame =
                            new byte[
                                (int)frameLength];

                        int bytesWritten =
                            global::PacketWire.PacketFrameCodec.WriteFrame(
                                frame,
                                category,
                                id,
                                payload,
                                definition);

                        if (bytesWritten !=
                            frame.Length)
                        {
                            throw new global::System.InvalidOperationException(
                                "Test frame length mismatch.");
                        }

                        return frame;
                    }

                    private static bool ExpectBufferFailure(
                        byte[] packet)
                    {
                        try
                        {
                            _ =
                                global::ApplicationProtocol.Deserialize(
                                    packet);

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

        Assert.True(
            GeneratorTestHost.InvokeHarness<bool>(
                result,
                harnessSource,
                "PacketWire.Generator.Tests.Dynamic.MalformedCollectionHarness",
                "ValidateAll"));
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