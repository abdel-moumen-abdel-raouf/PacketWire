using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace PacketWire.Generator.Tests;

/// <summary>
/// Verifies that generated facades reject non-canonical booleans, invalid optional presence bytes, malformed UTF-8, and dirty fixed-string padding.
/// </summary>
public sealed class PacketWireGeneratedMalformedPrimitiveTests
{
    /// <summary>
    /// Verifies that deserialization throws <see cref="PacketWire.PacketBufferException"/> on corrupted primitive values, invalid boolean bytes, malformed optional presence indicators, and dirty string padding.
    /// </summary>
    [Fact]
    public void PublicFacadeRejectsMalformedPrimitiveOptionalAndStringPayloads()
    {
        const string source = """
            using PacketWire;

            [PacketProtocol(
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketByteOrder.LittleEndian)]
            public sealed partial class ApplicationProtocol
            {
            }

            [Packet(typeof(ApplicationProtocol), 0x1234, 3)]
            public sealed class ValidationPacket
            {
                [PacketField(0)]
                public bool Enabled { get; init; }

                [PacketField(1)]
                [Optional]
                public int? OptionalNumber { get; init; }

                [PacketField(2)]
                [FixedString(4)]
                public string Name { get; init; } = string.Empty;

                [PacketField(3)]
                public int Tail { get; init; }
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
                public static class MalformedPrimitiveHarness
                {
                    public static bool ValidateAll()
                    {
                        byte[] valid =
                            CreateValidPacket();

                        int headerLength =
                            global::ApplicationProtocol
                                .Definition
                                .HeaderLength;

                        if (headerLength != 6)
                        {
                            return false;
                        }

                        if (valid.Length != 20)
                        {
                            return false;
                        }

                        if (!ValidateOriginal(
                                valid))
                        {
                            return false;
                        }

                        //
                        // Payload layout:
                        //
                        // +0  bool
                        // +1  optional presence marker
                        // +2  optional Int32 (4 bytes)
                        // +6  fixed UTF-8 string (4 bytes)
                        // +10 trailing Int32 (4 bytes)
                        //

                        byte[] invalidBoolean =
                            (byte[])valid.Clone();

                        invalidBoolean[
                            headerLength] =
                            0x02;

                        if (!ExpectBufferFailure(
                                invalidBoolean))
                        {
                            return false;
                        }

                        byte[] invalidOptionalMarker =
                            (byte[])valid.Clone();

                        invalidOptionalMarker[
                            headerLength +
                            1] =
                            0xFF;

                        if (!ExpectBufferFailure(
                                invalidOptionalMarker))
                        {
                            return false;
                        }

                        int stringOffset =
                            headerLength +
                            6;

                        byte[] invalidUtf8 =
                            (byte[])valid.Clone();

                        invalidUtf8[
                            stringOffset] =
                            0xC3;

                        invalidUtf8[
                            stringOffset +
                            1] =
                            0x28;

                        invalidUtf8[
                            stringOffset +
                            2] =
                            0x00;

                        invalidUtf8[
                            stringOffset +
                            3] =
                            0x00;

                        if (!ExpectBufferFailure(
                                invalidUtf8))
                        {
                            return false;
                        }

                        byte[] invalidPadding =
                            (byte[])valid.Clone();

                        invalidPadding[
                            stringOffset] =
                            0x41;

                        invalidPadding[
                            stringOffset +
                            1] =
                            0x00;

                        invalidPadding[
                            stringOffset +
                            2] =
                            0x42;

                        invalidPadding[
                            stringOffset +
                            3] =
                            0x00;

                        if (!ExpectBufferFailure(
                                invalidPadding))
                        {
                            return false;
                        }

                        byte[] truncatedTail =
                            new byte[
                                valid.Length -
                                1];

                        global::System.Array.Copy(
                            valid,
                            truncatedTail,
                            truncatedTail.Length);

                        WritePacketLength(
                            truncatedTail,
                            truncatedTail.Length);

                        if (!ExpectBufferFailure(
                                truncatedTail))
                        {
                            return false;
                        }

                        return
                            ValidateOriginal(
                                valid);
                    }

                    private static byte[] CreateValidPacket()
                    {
                        return
                            global::ApplicationProtocol.Serialize(
                                new global::ValidationPacket
                                {
                                    Enabled = true,
                                    OptionalNumber =
                                        0x01020304,
                                    Name = "A",
                                    Tail =
                                        0x11223344
                                });
                    }

                    private static bool ValidateOriginal(
                        byte[] packet)
                    {
                        global::ValidationPacket value =
                            global::ApplicationProtocol.Deserialize<global::ValidationPacket>(
                                packet);

                        return
                            value.Enabled &&
                            value.OptionalNumber ==
                                0x01020304 &&
                            value.Name ==
                                "A" &&
                            value.Tail ==
                                0x11223344;
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

                    private static void WritePacketLength(
                        byte[] packet,
                        int packetLength)
                    {
                        if (packet.Length < 2)
                        {
                            throw new global::System.ArgumentException(
                                "The packet must contain the two-byte packet-length field.",
                                nameof(packet));
                        }

                        if ((uint)packetLength >
                            global::System.UInt16.MaxValue)
                        {
                            throw new global::System.ArgumentOutOfRangeException(
                                nameof(packetLength));
                        }

                        packet[0] =
                            (byte)packetLength;

                        packet[1] =
                            (byte)(
                                packetLength >>
                                8);
                    }
                }
            }
            """;

        Assert.True(
            GeneratorTestHost.InvokeHarness<bool>(
                result,
                harnessSource,
                "PacketWire.Generator.Tests.Dynamic.MalformedPrimitiveHarness",
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