using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace PacketWire.Generator.Tests;

/// <summary>
/// Verifies fail-closed deserialization behavior on malformed plain frames (length smaller than header, incomplete frame, trailing bytes, unrecognized flags).
/// </summary>
public sealed class PacketWireGeneratedMalformedFrameTests
{
    /// <summary>
    /// Verifies that deserializing malformed plain frames throws <see cref="PacketWire.PacketBufferException"/>.
    /// </summary>
    [Fact]
    public void PublicFacadeRejectsMalformedPlainFramesFailClosed()
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
            public sealed class TestPacket
            {
                [PacketField(0)]
                public int Value { get; init; }
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
                public static class MalformedFrameHarness
                {
                    public static bool ValidateAll()
                    {
                        byte[] valid =
                            CreateValidPacket();

                        if (valid.Length != 10)
                        {
                            return false;
                        }

                        if (!ExpectBufferFailure(
                                global::System.Array.Empty<byte>()))
                        {
                            return false;
                        }

                        byte[] truncatedHeader =
                            new byte[
                                global::ApplicationProtocol.Definition.HeaderLength -
                                1];

                        global::System.Array.Copy(
                            valid,
                            truncatedHeader,
                            truncatedHeader.Length);

                        if (!ExpectBufferFailure(
                                truncatedHeader))
                        {
                            return false;
                        }

                        byte[] declaredSmallerThanHeader =
                        [
                            0x05,
                            0x00,

                            0x00,

                            0x03,

                            0x34,
                            0x12
                        ];

                        if (!ExpectBufferFailure(
                                declaredSmallerThanHeader))
                        {
                            return false;
                        }

                        byte[] declaredLongerThanActual =
                            (byte[])valid.Clone();

                        WritePacketLength(
                            declaredLongerThanActual,
                            valid.Length +
                            1);

                        if (!ExpectBufferFailure(
                                declaredLongerThanActual))
                        {
                            return false;
                        }

                        byte[] declaredShorterThanActual =
                            (byte[])valid.Clone();

                        WritePacketLength(
                            declaredShorterThanActual,
                            valid.Length -
                            1);

                        if (!ExpectBufferFailure(
                                declaredShorterThanActual))
                        {
                            return false;
                        }

                        byte[] unknownFlags =
                            (byte[])valid.Clone();

                        unknownFlags[2] =
                            0x80;

                        if (!ExpectBufferFailure(
                                unknownFlags))
                        {
                            return false;
                        }

                        byte[] truncatedPayload =
                            new byte[
                                valid.Length -
                                1];

                        global::System.Array.Copy(
                            valid,
                            truncatedPayload,
                            truncatedPayload.Length);

                        WritePacketLength(
                            truncatedPayload,
                            truncatedPayload.Length);

                        if (!ExpectBufferFailure(
                                truncatedPayload))
                        {
                            return false;
                        }

                        byte[] extraPayload =
                            new byte[
                                valid.Length +
                                1];

                        global::System.Array.Copy(
                            valid,
                            extraPayload,
                            valid.Length);

                        extraPayload[^1] =
                            0xEE;

                        WritePacketLength(
                            extraPayload,
                            extraPayload.Length);

                        if (!ExpectBufferFailure(
                                extraPayload))
                        {
                            return false;
                        }

                        global::TestPacket decoded =
                            global::ApplicationProtocol.Deserialize<global::TestPacket>(
                                valid);

                        return
                            decoded.Value ==
                            0x11223344;
                    }

                    private static byte[] CreateValidPacket()
                    {
                        return
                            global::ApplicationProtocol.Serialize(
                                new global::TestPacket
                                {
                                    Value = 0x11223344
                                });
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
                                "The packet must contain the two-byte length field.",
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
                "PacketWire.Generator.Tests.Dynamic.MalformedFrameHarness",
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