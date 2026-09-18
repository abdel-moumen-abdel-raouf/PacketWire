using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace PacketWire.Generator.Tests;

/// <summary>
/// Verifies fail-closed behavior of protected facades when confronted with malformed protected frames, invalid protector lengths, tampering, and buffer underflows.
/// </summary>
public sealed class PacketWireGeneratedMalformedProtectedPayloadTests
{
    /// <summary>
    /// Verifies that protected deserialization rejects invalid frame lengths, unexpected plaintext sizes, negative lengths from protectors, and protector unprotection exceptions.
    /// </summary>
    [Fact]
    public void ProtectedFacadeRejectsMalformedProtectedPayloadsFailClosed()
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

            [Packet(typeof(ApplicationProtocol), 0x1234, 7)]
            public sealed class ProtectedValidationPacket
            {
                [PacketField(0)]
                public bool Enabled { get; init; }

                [PacketField(1)]
                [Optional]
                public int? OptionalNumber { get; init; }

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
                public static class MalformedProtectedPayloadHarness
                {
                    public static bool ValidateAll()
                    {
                        byte[] plaintext =
                            CreatePlainPayload();

                        if (plaintext.Length != 10)
                        {
                            return false;
                        }

                        byte[] validProtected =
                            BuildProtectedFrame(
                                plaintext);

                        if (!ValidateOriginal(
                                validProtected))
                        {
                            return false;
                        }

                        //
                        // Protected frame contains no transmitted payload.
                        // IdentityProtector exposes zero plaintext bytes.
                        // DTO decoding must fail on the first required field.
                        //
                        byte[] emptyProtectedPayload =
                            BuildProtectedFrame(
                                global::System.Array.Empty<byte>());

                        if (!ExpectBufferFailure(
                                emptyProtectedPayload,
                                new IdentityProtector()))
                        {
                            return false;
                        }

                        //
                        // Plaintext is one byte shorter than the DTO.
                        //
                        byte[] truncatedPlaintext =
                            new byte[
                                plaintext.Length -
                                1];

                        global::System.Array.Copy(
                            plaintext,
                            truncatedPlaintext,
                            truncatedPlaintext.Length);

                        byte[] protectedTruncatedPlaintext =
                            BuildProtectedFrame(
                                truncatedPlaintext);

                        if (!ExpectBufferFailure(
                                protectedTruncatedPlaintext,
                                new IdentityProtector()))
                        {
                            return false;
                        }

                        //
                        // Plaintext has one extra trailing byte.
                        // Generated codec must reject unconsumed plaintext.
                        //
                        byte[] extraPlaintext =
                            new byte[
                                plaintext.Length +
                                1];

                        global::System.Array.Copy(
                            plaintext,
                            extraPlaintext,
                            plaintext.Length);

                        extraPlaintext[^1] =
                            0xEE;

                        byte[] protectedExtraPlaintext =
                            BuildProtectedFrame(
                                extraPlaintext);

                        if (!ExpectBufferFailure(
                                protectedExtraPlaintext,
                                new IdentityProtector()))
                        {
                            return false;
                        }

                        //
                        // First payload byte is the Boolean field.
                        //
                        byte[] invalidBoolean =
                            (byte[])plaintext.Clone();

                        invalidBoolean[0] =
                            0x02;

                        byte[] protectedInvalidBoolean =
                            BuildProtectedFrame(
                                invalidBoolean);

                        if (!ExpectBufferFailure(
                                protectedInvalidBoolean,
                                new IdentityProtector()))
                        {
                            return false;
                        }

                        //
                        // Second payload byte is the optional-presence marker.
                        //
                        byte[] invalidOptionalMarker =
                            (byte[])plaintext.Clone();

                        invalidOptionalMarker[1] =
                            0xFF;

                        byte[] protectedInvalidOptional =
                            BuildProtectedFrame(
                                invalidOptionalMarker);

                        if (!ExpectBufferFailure(
                                protectedInvalidOptional,
                                new IdentityProtector()))
                        {
                            return false;
                        }

                        //
                        // The protector requires a four-byte trailer,
                        // but the transmitted protected payload is only
                        // three bytes long.
                        //
                        byte[] tooShortForProtector =
                            BuildProtectedFrame(
                                new byte[]
                                {
                                    0x01,
                                    0x02,
                                    0x03
                                });

                        if (!ExpectProtectionFailure(
                                tooShortForProtector,
                                new FourByteTrailerProtector()))
                        {
                            return false;
                        }

                        //
                        // Payload is long enough for the protector contract,
                        // but its four-byte authentication trailer is invalid.
                        //
                        byte[] malformedTrailer =
                            BuildProtectedFrame(
                                new byte[]
                                {
                                    0x01,
                                    0x00,

                                    0x00,
                                    0x00,
                                    0x00,
                                    0x00
                                });

                        if (!ExpectProtectionFailure(
                                malformedTrailer,
                                new FourByteTrailerProtector()))
                        {
                            return false;
                        }

                        return
                            ValidateOriginal(
                                validProtected);
                    }

                    private static byte[] CreatePlainPayload()
                    {
                        byte[] frame =
                            global::ApplicationProtocol.Serialize(
                                new global::ProtectedValidationPacket
                                {
                                    Enabled = true,
                                    OptionalNumber =
                                        0x01020304,
                                    Name = "A"
                                });

                        int headerLength =
                            global::ApplicationProtocol
                                .Definition
                                .HeaderLength;

                        int payloadLength =
                            frame.Length -
                            headerLength;

                        byte[] payload =
                            new byte[
                                payloadLength];

                        global::System.Array.Copy(
                            frame,
                            headerLength,
                            payload,
                            0,
                            payloadLength);

                        return payload;
                    }

                    private static byte[] BuildProtectedFrame(
                        byte[] transmittedPayload)
                    {
                        global::PacketWire.PacketProtocolDefinition definition =
                            global::ApplicationProtocol.Definition;

                        ulong frameLength =
                            global::PacketWire.PacketFrameCodec.CalculateFrameLength(
                                transmittedPayload.Length,
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
                                global::PacketWire.PacketFrameOptions.Protected,
                                7,
                                0x1234,
                                transmittedPayload,
                                definition);

                        if (bytesWritten !=
                            frame.Length)
                        {
                            throw new global::System.InvalidOperationException(
                                "Test protected frame length mismatch.");
                        }

                        return frame;
                    }

                    private static bool ValidateOriginal(
                        byte[] packet)
                    {
                        global::ProtectedValidationPacket value =
                            global::ApplicationProtocol.Deserialize<global::ProtectedValidationPacket>(
                                packet,
                                new IdentityProtector());

                        return
                            value.Enabled &&
                            value.OptionalNumber ==
                                0x01020304 &&
                            value.Name ==
                                "A";
                    }

                    private static bool ExpectBufferFailure(
                        byte[] packet,
                        global::PacketWire.Security.IPayloadProtector protector)
                    {
                        try
                        {
                            _ =
                                global::ApplicationProtocol.Deserialize(
                                    packet,
                                    protector);

                            return false;
                        }
                        catch (global::PacketWire.PacketBufferException)
                        {
                            return true;
                        }
                    }

                    private static bool ExpectProtectionFailure(
                        byte[] packet,
                        global::PacketWire.Security.IPayloadProtector protector)
                    {
                        try
                        {
                            _ =
                                global::ApplicationProtocol.Deserialize(
                                    packet,
                                    protector);

                            return false;
                        }
                        catch (global::PacketWire.Security.PayloadProtectionException)
                        {
                            return true;
                        }
                    }

                    private sealed class IdentityProtector
                        : global::PacketWire.Security.IPayloadProtector
                    {
                        public int GetProtectedPayloadLength(
                            int plaintextLength)
                        {
                            global::System.ArgumentOutOfRangeException.ThrowIfNegative(
                                plaintextLength);

                            return plaintextLength;
                        }

                        public int GetMaximumPlaintextLength(
                            int protectedPayloadLength)
                        {
                            global::System.ArgumentOutOfRangeException.ThrowIfNegative(
                                protectedPayloadLength);

                            return protectedPayloadLength;
                        }

                        public int Protect(
                            global::System.ReadOnlySpan<byte> plaintext,
                            global::System.ReadOnlySpan<byte> associatedData,
                            global::System.Span<byte> destination)
                        {
                            if (destination.Length <
                                plaintext.Length)
                            {
                                throw new global::System.ArgumentException(
                                    "Destination is too small.",
                                    nameof(destination));
                            }

                            plaintext.CopyTo(
                                destination);

                            return plaintext.Length;
                        }

                        public int Unprotect(
                            global::System.ReadOnlySpan<byte> protectedPayload,
                            global::System.ReadOnlySpan<byte> associatedData,
                            global::System.Span<byte> destination)
                        {
                            if (destination.Length <
                                protectedPayload.Length)
                            {
                                throw new global::System.ArgumentException(
                                    "Destination is too small.",
                                    nameof(destination));
                            }

                            protectedPayload.CopyTo(
                                destination);

                            return protectedPayload.Length;
                        }
                    }

                    private sealed class FourByteTrailerProtector
                        : global::PacketWire.Security.IPayloadProtector
                    {
                        private const int TrailerLength =
                            4;

                        private const byte TrailerValue =
                            0xA5;

                        public int GetProtectedPayloadLength(
                            int plaintextLength)
                        {
                            global::System.ArgumentOutOfRangeException.ThrowIfNegative(
                                plaintextLength);

                            return checked(
                                plaintextLength +
                                TrailerLength);
                        }

                        public int GetMaximumPlaintextLength(
                            int protectedPayloadLength)
                        {
                            if (protectedPayloadLength <
                                TrailerLength)
                            {
                                throw new global::PacketWire.Security.PayloadProtectionException(
                                    "Protected payload is shorter than the required trailer.");
                            }

                            return
                                protectedPayloadLength -
                                TrailerLength;
                        }

                        public int Protect(
                            global::System.ReadOnlySpan<byte> plaintext,
                            global::System.ReadOnlySpan<byte> associatedData,
                            global::System.Span<byte> destination)
                        {
                            int requiredLength =
                                GetProtectedPayloadLength(
                                    plaintext.Length);

                            if (destination.Length <
                                requiredLength)
                            {
                                throw new global::System.ArgumentException(
                                    "Destination is too small.",
                                    nameof(destination));
                            }

                            plaintext.CopyTo(
                                destination);

                            global::System.Span<byte> trailer =
                                destination.Slice(
                                    plaintext.Length,
                                    TrailerLength);

                            trailer.Fill(
                                TrailerValue);

                            return requiredLength;
                        }

                        public int Unprotect(
                            global::System.ReadOnlySpan<byte> protectedPayload,
                            global::System.ReadOnlySpan<byte> associatedData,
                            global::System.Span<byte> destination)
                        {
                            int plaintextLength =
                                GetMaximumPlaintextLength(
                                    protectedPayload.Length);

                            if (destination.Length <
                                plaintextLength)
                            {
                                throw new global::System.ArgumentException(
                                    "Destination is too small.",
                                    nameof(destination));
                            }

                            global::System.ReadOnlySpan<byte> trailer =
                                protectedPayload.Slice(
                                    plaintextLength,
                                    TrailerLength);

                            for (
                                int index = 0;
                                index < trailer.Length;
                                index++)
                            {
                                if (trailer[index] !=
                                    TrailerValue)
                                {
                                    throw new global::PacketWire.Security.PayloadProtectionException(
                                        "Protected payload trailer validation failed.");
                                }
                            }

                            protectedPayload
                                .Slice(
                                    0,
                                    plaintextLength)
                                .CopyTo(destination);

                            return plaintextLength;
                        }
                    }
                }
            }
            """;

        Assert.True(
            GeneratorTestHost.InvokeHarness<bool>(
                result,
                harnessSource,
                "PacketWire.Generator.Tests.Dynamic.MalformedProtectedPayloadHarness",
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