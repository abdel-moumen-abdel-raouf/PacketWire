using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace PacketWire.Generator.Tests;

/// <summary>
/// Verifies that generated facades and codecs robustly reject truncated frames, malformed headers, trailing payloads, and invalid identities under adversarial inputs.
/// </summary>
public sealed class PacketWireGeneratedAdversarialRegressionTests
{
    /// <summary>
    /// Verifies that deserializing any strict truncated prefix or frame with unexpected trailing payload bytes throws <see cref="PacketWire.PacketBufferException"/>.
    /// </summary>
    [Fact]
    public void PublicFacadeRejectsAllTruncatedPrefixesAndTrailingPayloadBytes()
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
            public sealed class RegressionPacket
            {
                [PacketField(0)]
                public int Value { get; init; }

                [PacketField(1)]
                public bool Enabled { get; init; }

                [PacketField(2)]
                [FixedString(4)]
                public string Name { get; init; } = string.Empty;
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
                public static class AdversarialPrefixHarness
                {
                    public static bool ValidateAll()
                    {
                        byte[] valid =
                            global::ApplicationProtocol.Serialize(
                                new global::RegressionPacket
                                {
                                    Value = 0x11223344,
                                    Enabled = true,
                                    Name = "A"
                                });

                        if (valid.Length != 15)
                        {
                            return false;
                        }

                        global::RegressionPacket original =
                            global::ApplicationProtocol.Deserialize<global::RegressionPacket>(
                                valid);

                        if (original.Value != 0x11223344 ||
                            !original.Enabled ||
                            original.Name != "A")
                        {
                            return false;
                        }

                        //
                        // Test every possible strict prefix:
                        //
                        // 0 bytes
                        // 1 byte
                        // ...
                        // valid.Length - 1 bytes
                        //
                        // Once the two-byte PacketLength field exists,
                        // rewrite it to match the supplied prefix so that
                        // the test progresses beyond frame-length checking
                        // and exercises header/payload decoding as deeply
                        // as the available bytes allow.
                        //
                        for (
                            int length = 0;
                            length < valid.Length;
                            length++)
                        {
                            byte[] truncated =
                                new byte[length];

                            if (length > 0)
                            {
                                global::System.Array.Copy(
                                    valid,
                                    truncated,
                                    length);
                            }

                            if (length >= 2)
                            {
                                WriteTwoBytePacketLength(
                                    truncated,
                                    length);
                            }

                            if (!ExpectBufferFailure(
                                    truncated))
                            {
                                return false;
                            }
                        }

                        //
                        // The valid DTO consumes exactly its payload.
                        // Any declared trailing payload must therefore fail.
                        //
                        for (
                            int extraLength = 1;
                            extraLength <= 4;
                            extraLength++)
                        {
                            byte[] extended =
                                new byte[
                                    valid.Length +
                                    extraLength];

                            global::System.Array.Copy(
                                valid,
                                extended,
                                valid.Length);

                            for (
                                int index = valid.Length;
                                index < extended.Length;
                                index++)
                            {
                                extended[index] =
                                    0xEE;
                            }

                            WriteTwoBytePacketLength(
                                extended,
                                extended.Length);

                            if (!ExpectBufferFailure(
                                    extended))
                            {
                                return false;
                            }
                        }

                        global::RegressionPacket finalControl =
                            global::ApplicationProtocol.Deserialize<global::RegressionPacket>(
                                valid);

                        return
                            finalControl.Value == 0x11223344 &&
                            finalControl.Enabled &&
                            finalControl.Name == "A";
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

                    private static void WriteTwoBytePacketLength(
                        byte[] packet,
                        int packetLength)
                    {
                        if (packet.Length < 2)
                        {
                            throw new global::System.ArgumentException(
                                "The packet does not contain its two-byte length field.",
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
                "PacketWire.Generator.Tests.Dynamic.AdversarialPrefixHarness",
                "ValidateAll"));
    }

    /// <summary>
    /// Verifies fail-closed behavior across diverse framing configurations (compact 1-byte vs wide 4-byte big-endian).
    /// </summary>
    [Fact]
    public void MalformedHeadersFailClosedAcrossLengthWidthsAndByteOrders()
    {
        const string source = """
            using PacketWire;

            [PacketProtocol(
                PacketIntegerSize.OneByte,
                PacketIntegerSize.OneByte,
                PacketIntegerSize.OneByte,
                PacketByteOrder.LittleEndian,
                PacketIntegerSize.OneByte)]
            public sealed partial class CompactProtocol
            {
            }

            [Packet(typeof(CompactProtocol), 0x22, 0x11)]
            public sealed class CompactPacket
            {
                [PacketField(0)]
                public ushort Value { get; init; }
            }

            [PacketProtocol(
                PacketIntegerSize.FourBytes,
                PacketIntegerSize.FourBytes,
                PacketIntegerSize.TwoBytes,
                PacketByteOrder.BigEndian,
                PacketIntegerSize.TwoBytes)]
            public sealed partial class WideProtocol
            {
            }

            [Packet(typeof(WideProtocol), 0x11223344, 0x5566)]
            public sealed class WidePacket
            {
                [PacketField(0)]
                public ushort Value { get; init; }
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
                public static class CrossProtocolMalformedHarness
                {
                    public static bool ValidateAll()
                    {
                        byte[] compact =
                            global::CompactProtocol.Serialize(
                                new global::CompactPacket
                                {
                                    Value = 0x1234
                                });

                        byte[] wide =
                            global::WideProtocol.Serialize(
                                new global::WidePacket
                                {
                                    Value = 0x1234
                                });

                        if (global::CompactProtocol.Definition.HeaderLength != 4 ||
                            compact.Length != 6)
                        {
                            return false;
                        }

                        if (global::WideProtocol.Definition.HeaderLength != 11 ||
                            wide.Length != 13)
                        {
                            return false;
                        }

                        global::CompactPacket compactControl =
                            global::CompactProtocol.Deserialize<global::CompactPacket>(
                                compact);

                        global::WidePacket wideControl =
                            global::WideProtocol.Deserialize<global::WidePacket>(
                                wide);

                        if (compactControl.Value != 0x1234 ||
                            wideControl.Value != 0x1234)
                        {
                            return false;
                        }

                        //
                        // One-byte little-endian PacketLength.
                        //
                        byte[] compactLengthTooLarge =
                            (byte[])compact.Clone();

                        compactLengthTooLarge[0] =
                            (byte)(
                                compact.Length +
                                1);

                        if (!ExpectCompactBufferFailure(
                                compactLengthTooLarge))
                        {
                            return false;
                        }

                        byte[] compactLengthBelowHeader =
                            (byte[])compact.Clone();

                        compactLengthBelowHeader[0] =
                            0x03;

                        if (!ExpectCompactBufferFailure(
                                compactLengthBelowHeader))
                        {
                            return false;
                        }

                        //
                        // With a one-byte PacketLength field,
                        // Flags starts at offset 1.
                        //
                        byte[] compactUnknownFlags =
                            (byte[])compact.Clone();

                        compactUnknownFlags[1] =
                            0x80;

                        if (!ExpectCompactBufferFailure(
                                compactUnknownFlags))
                        {
                            return false;
                        }

                        //
                        // Compact header:
                        //
                        // length   offset 0
                        // flags    offset 1
                        // category offset 2
                        // id       offset 3
                        //
                        byte[] compactUnknownIdentity =
                            (byte[])compact.Clone();

                        compactUnknownIdentity[3] =
                            0x23;

                        if (!ExpectCompactIdentityFailure(
                                compactUnknownIdentity))
                        {
                            return false;
                        }

                        //
                        // Four-byte big-endian PacketLength.
                        //
                        byte[] wideLengthTooLarge =
                            (byte[])wide.Clone();

                        WriteFourByteBigEndianLength(
                            wideLengthTooLarge,
                            wide.Length +
                            1);

                        if (!ExpectWideBufferFailure(
                                wideLengthTooLarge))
                        {
                            return false;
                        }

                        byte[] wideLengthBelowHeader =
                            (byte[])wide.Clone();

                        WriteFourByteBigEndianLength(
                            wideLengthBelowHeader,
                            10);

                        if (!ExpectWideBufferFailure(
                                wideLengthBelowHeader))
                        {
                            return false;
                        }

                        //
                        // With a four-byte PacketLength field,
                        // Flags starts at offset 4.
                        //
                        byte[] wideUnknownFlags =
                            (byte[])wide.Clone();

                        wideUnknownFlags[4] =
                            0x80;

                        if (!ExpectWideBufferFailure(
                                wideUnknownFlags))
                        {
                            return false;
                        }

                        //
                        // Wide header:
                        //
                        // PacketLength  offsets 0..3
                        // Flags         offset 4
                        // Category      offsets 5..6
                        // PacketId      offsets 7..10
                        //
                        byte[] wideUnknownIdentity =
                            (byte[])wide.Clone();

                        wideUnknownIdentity[10] ^=
                            0x01;

                        if (!ExpectWideIdentityFailure(
                                wideUnknownIdentity))
                        {
                            return false;
                        }

                        global::CompactPacket finalCompactControl =
                            global::CompactProtocol.Deserialize<global::CompactPacket>(
                                compact);

                        global::WidePacket finalWideControl =
                            global::WideProtocol.Deserialize<global::WidePacket>(
                                wide);

                        return
                            finalCompactControl.Value == 0x1234 &&
                            finalWideControl.Value == 0x1234;
                    }

                    private static bool ExpectCompactBufferFailure(
                        byte[] packet)
                    {
                        try
                        {
                            _ =
                                global::CompactProtocol.Deserialize(
                                    packet);

                            return false;
                        }
                        catch (global::PacketWire.PacketBufferException)
                        {
                            return true;
                        }
                    }

                    private static bool ExpectWideBufferFailure(
                        byte[] packet)
                    {
                        try
                        {
                            _ =
                                global::WideProtocol.Deserialize(
                                    packet);

                            return false;
                        }
                        catch (global::PacketWire.PacketBufferException)
                        {
                            return true;
                        }
                    }

                    private static bool ExpectCompactIdentityFailure(
                        byte[] packet)
                    {
                        try
                        {
                            _ =
                                global::CompactProtocol.Deserialize(
                                    packet);

                            return false;
                        }
                        catch (global::PacketWire.PacketIdentityNotRegisteredException)
                        {
                            return true;
                        }
                    }

                    private static bool ExpectWideIdentityFailure(
                        byte[] packet)
                    {
                        try
                        {
                            _ =
                                global::WideProtocol.Deserialize(
                                    packet);

                            return false;
                        }
                        catch (global::PacketWire.PacketIdentityNotRegisteredException)
                        {
                            return true;
                        }
                    }

                    private static void WriteFourByteBigEndianLength(
                        byte[] packet,
                        int packetLength)
                    {
                        if (packet.Length < 4)
                        {
                            throw new global::System.ArgumentException(
                                "The packet does not contain its four-byte length field.",
                                nameof(packet));
                        }

                        if (packetLength < 0)
                        {
                            throw new global::System.ArgumentOutOfRangeException(
                                nameof(packetLength));
                        }

                        uint value =
                            (uint)packetLength;

                        packet[0] =
                            (byte)(
                                value >>
                                24);

                        packet[1] =
                            (byte)(
                                value >>
                                16);

                        packet[2] =
                            (byte)(
                                value >>
                                8);

                        packet[3] =
                            (byte)value;
                    }
                }
            }
            """;

        Assert.True(
            GeneratorTestHost.InvokeHarness<bool>(
                result,
                harnessSource,
                "PacketWire.Generator.Tests.Dynamic.CrossProtocolMalformedHarness",
                "ValidateAll"));
    }

    /// <summary>
    /// Asserts that generator diagnostics and output compilation contain no compilation errors.
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