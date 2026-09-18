using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace PacketWire.Generator.Tests;

/// <summary>
/// Contains generator integration tests verifying security hardening, tampering rejection,
/// and protector contract enforcement for protected protocol facades.
/// </summary>
public sealed class PacketWireGeneratedProtocolProtectionHardeningTests
{
    /// <summary>
    /// Protocol source definition declaring a protocol and packet model for protection hardening tests.
    /// </summary>
    private const string ProtocolSource = """
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
        public sealed class LoginRequest
        {
            [PacketField(0)]
            public int UserId { get; init; }

            [PacketField(1)]
            [FixedString(4)]
            public string Name { get; init; } = string.Empty;
        }
        """;

    /// <summary>
    /// Verifies that protected packet deserialization rejects header tampering, packet identifier modifications,
    /// payload tampering, length mismatches, and protector key mismatches.
    /// </summary>
    [Fact]
    public void ProtectedPacketRejectsHeaderPayloadAndProtectorTampering()
    {
        GeneratorTestResult result =
            GeneratorTestHost.RunWithOutput(
                ProtocolSource);

        AssertSuccessfulCompilation(
            result);

        const string harnessSource = """
            namespace PacketWire.Generator.Tests.Dynamic
            {
                public static class ProtectionTamperingHarness
                {
                    public static bool ValidateAll()
                    {
                        byte[] original =
                            SerializeProtected(
                                0x2A);

                        if (!ValidateOriginal(
                                original,
                                0x2A))
                        {
                            return false;
                        }

                        byte[] categoryTampered =
                            (byte[])original.Clone();

                        categoryTampered[3] ^= 0x01;

                        if (!ExpectProtectionFailure(
                                categoryTampered,
                                0x2A))
                        {
                            return false;
                        }

                        byte[] packetIdTampered =
                            (byte[])original.Clone();

                        packetIdTampered[4] ^= 0x01;

                        if (!ExpectProtectionFailure(
                                packetIdTampered,
                                0x2A))
                        {
                            return false;
                        }

                        byte[] payloadTampered =
                            (byte[])original.Clone();

                        payloadTampered[
                            global::ApplicationProtocol.Definition.HeaderLength] ^= 0x01;

                        if (!ExpectProtectionFailure(
                                payloadTampered,
                                0x2A))
                        {
                            return false;
                        }

                        byte[] expanded =
                            new byte[
                                original.Length +
                                1];

                        global::System.Array.Copy(
                            original,
                            expanded,
                            original.Length);

                        WritePacketLength(
                            expanded,
                            expanded.Length);

                        if (!ExpectProtectionFailure(
                                expanded,
                                0x2A))
                        {
                            return false;
                        }

                        byte[] truncated =
                            new byte[
                                original.Length -
                                1];

                        global::System.Array.Copy(
                            original,
                            truncated,
                            truncated.Length);

                        WritePacketLength(
                            truncated,
                            truncated.Length);

                        if (!ExpectProtectionFailure(
                                truncated,
                                0x2A))
                        {
                            return false;
                        }

                        byte[] clearedProtectedFlag =
                            (byte[])original.Clone();

                        clearedProtectedFlag[2] =
                            0x00;

                        if (!ExpectBufferFailure(
                                clearedProtectedFlag,
                                0x2A))
                        {
                            return false;
                        }

                        byte[] unknownFlag =
                            (byte[])original.Clone();

                        unknownFlag[2] =
                            0x03;

                        if (!ExpectBufferFailure(
                                unknownFlag,
                                0x2A))
                        {
                            return false;
                        }

                        if (!ExpectProtectionFailure(
                                original,
                                0x2B))
                        {
                            return false;
                        }

                        return true;
                    }

                    private static byte[] SerializeProtected(
                        byte key)
                    {
                        return
                            global::ApplicationProtocol.Serialize(
                                new global::LoginRequest
                                {
                                    UserId = 0x11223344,
                                    Name = "A"
                                },
                                new AuthenticatingTestProtector(
                                    key));
                    }

                    private static bool ValidateOriginal(
                        byte[] packet,
                        byte key)
                    {
                        global::LoginRequest result =
                            global::ApplicationProtocol.Deserialize<global::LoginRequest>(
                                packet,
                                new AuthenticatingTestProtector(
                                    key));

                        return
                            result.UserId == 0x11223344 &&
                            result.Name == "A";
                    }

                    private static bool ExpectProtectionFailure(
                        byte[] packet,
                        byte key)
                    {
                        try
                        {
                            _ =
                                global::ApplicationProtocol.Deserialize<global::LoginRequest>(
                                    packet,
                                    new AuthenticatingTestProtector(
                                        key));

                            return false;
                        }
                        catch (global::PacketWire.Security.PayloadProtectionException)
                        {
                            return true;
                        }
                    }

                    private static bool ExpectBufferFailure(
                        byte[] packet,
                        byte key)
                    {
                        try
                        {
                            _ =
                                global::ApplicationProtocol.Deserialize<global::LoginRequest>(
                                    packet,
                                    new AuthenticatingTestProtector(
                                        key));

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
                        packet[0] =
                            (byte)packetLength;

                        packet[1] =
                            (byte)(
                                packetLength >>
                                8);
                    }

                    private sealed class AuthenticatingTestProtector
                        : global::PacketWire.Security.IPayloadProtector
                    {
                        private const int TagLength =
                            4;

                        private readonly byte key;

                        public AuthenticatingTestProtector(
                            byte key)
                        {
                            this.key =
                                key;
                        }

                        public int GetProtectedPayloadLength(
                            int plaintextLength)
                        {
                            global::System.ArgumentOutOfRangeException.ThrowIfNegative(
                                plaintextLength);

                            return checked(
                                plaintextLength +
                                TagLength);
                        }

                        public int GetMaximumPlaintextLength(
                            int protectedPayloadLength)
                        {
                            if (protectedPayloadLength < TagLength)
                            {
                                throw new global::PacketWire.Security.PayloadProtectionException(
                                    "Protected payload is shorter than the authentication tag.");
                            }

                            return
                                protectedPayloadLength -
                                TagLength;
                        }

                        public int Protect(
                            global::System.ReadOnlySpan<byte> plaintext,
                            global::System.ReadOnlySpan<byte> associatedData,
                            global::System.Span<byte> destination)
                        {
                            int requiredLength =
                                GetProtectedPayloadLength(
                                    plaintext.Length);

                            if (destination.Length < requiredLength)
                            {
                                throw new global::System.ArgumentException(
                                    "Destination is too small.",
                                    nameof(destination));
                            }

                            plaintext.CopyTo(
                                destination);

                            uint tag =
                                ComputeTag(
                                    associatedData,
                                    plaintext,
                                    key);

                            WriteUInt32(
                                destination.Slice(
                                    plaintext.Length,
                                    TagLength),
                                tag);

                            return requiredLength;
                        }

                        public int Unprotect(
                            global::System.ReadOnlySpan<byte> protectedPayload,
                            global::System.ReadOnlySpan<byte> associatedData,
                            global::System.Span<byte> destination)
                        {
                            if (protectedPayload.Length < TagLength)
                            {
                                throw new global::PacketWire.Security.PayloadProtectionException(
                                    "Protected payload is malformed.");
                            }

                            int plaintextLength =
                                protectedPayload.Length -
                                TagLength;

                            if (destination.Length < plaintextLength)
                            {
                                throw new global::System.ArgumentException(
                                    "Destination is too small.",
                                    nameof(destination));
                            }

                            global::System.ReadOnlySpan<byte> plaintext =
                                protectedPayload.Slice(
                                    0,
                                    plaintextLength);

                            uint expectedTag =
                                ComputeTag(
                                    associatedData,
                                    plaintext,
                                    key);

                            uint actualTag =
                                ReadUInt32(
                                    protectedPayload.Slice(
                                        plaintextLength,
                                        TagLength));

                            if (actualTag != expectedTag)
                            {
                                throw new global::PacketWire.Security.PayloadProtectionException(
                                    "Protected payload authentication failed.");
                            }

                            plaintext.CopyTo(
                                destination);

                            return plaintextLength;
                        }

                        private static uint ComputeTag(
                            global::System.ReadOnlySpan<byte> associatedData,
                            global::System.ReadOnlySpan<byte> plaintext,
                            byte key)
                        {
                            unchecked
                            {
                                uint hash =
                                    2166136261U;

                                hash ^=
                                    key;

                                hash *=
                                    16777619U;

                                foreach (byte value in associatedData)
                                {
                                    hash ^=
                                        value;

                                    hash *=
                                        16777619U;
                                }

                                foreach (byte value in plaintext)
                                {
                                    hash ^=
                                        value;

                                    hash *=
                                        16777619U;
                                }

                                return hash;
                            }
                        }

                        private static void WriteUInt32(
                            global::System.Span<byte> destination,
                            uint value)
                        {
                            destination[0] =
                                (byte)value;

                            destination[1] =
                                (byte)(
                                    value >>
                                    8);

                            destination[2] =
                                (byte)(
                                    value >>
                                    16);

                            destination[3] =
                                (byte)(
                                    value >>
                                    24);
                        }

                        private static uint ReadUInt32(
                            global::System.ReadOnlySpan<byte> source)
                        {
                            return
                                source[0] |
                                ((uint)source[1] << 8) |
                                ((uint)source[2] << 16) |
                                ((uint)source[3] << 24);
                        }
                    }
                }
            }
            """;

        Assert.True(
            GeneratorTestHost.InvokeHarness<bool>(
                result,
                harnessSource,
                "PacketWire.Generator.Tests.Dynamic.ProtectionTamperingHarness",
                "ValidateAll"));
    }

    /// <summary>
    /// Verifies that protected facade serialization and deserialization reject invalid protector contract behaviors,
    /// such as negative or oversized lengths and short writes.
    /// </summary>
    [Fact]
    public void ProtectedFacadeRejectsProtectorLengthContractViolations()
    {
        GeneratorTestResult result =
            GeneratorTestHost.RunWithOutput(
                ProtocolSource);

        AssertSuccessfulCompilation(
            result);

        const string harnessSource = """
            namespace PacketWire.Generator.Tests.Dynamic
            {
                public static class ProtectionContractHarness
                {
                    public static bool ValidateAll()
                    {
                        if (!ExpectSerializeFailure(
                                new NegativeProtectedLengthProtector()))
                        {
                            return false;
                        }

                        if (!ExpectSerializeFailure(
                                new ShortWriteProtector()))
                        {
                            return false;
                        }

                        byte[] packet =
                            global::ApplicationProtocol.Serialize(
                                CreatePacket(),
                                new IdentityProtector());

                        if (!ExpectDeserializeFailure(
                                packet,
                                new NegativeMaximumLengthProtector()))
                        {
                            return false;
                        }

                        if (!ExpectDeserializeFailure(
                                packet,
                                new NegativePlaintextLengthProtector()))
                        {
                            return false;
                        }

                        if (!ExpectDeserializeFailure(
                                packet,
                                new OversizedPlaintextLengthProtector()))
                        {
                            return false;
                        }

                        return true;
                    }

                    private static global::LoginRequest CreatePacket()
                    {
                        return
                            new global::LoginRequest
                            {
                                UserId = 123,
                                Name = "A"
                            };
                    }

                    private static bool ExpectSerializeFailure(
                        global::PacketWire.Security.IPayloadProtector protector)
                    {
                        try
                        {
                            _ =
                                global::ApplicationProtocol.Serialize(
                                    CreatePacket(),
                                    protector);

                            return false;
                        }
                        catch (global::System.InvalidOperationException)
                        {
                            return true;
                        }
                    }

                    private static bool ExpectDeserializeFailure(
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
                        catch (global::System.InvalidOperationException)
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
                            return plaintextLength;
                        }

                        public int GetMaximumPlaintextLength(
                            int protectedPayloadLength)
                        {
                            return protectedPayloadLength;
                        }

                        public int Protect(
                            global::System.ReadOnlySpan<byte> plaintext,
                            global::System.ReadOnlySpan<byte> associatedData,
                            global::System.Span<byte> destination)
                        {
                            plaintext.CopyTo(
                                destination);

                            return plaintext.Length;
                        }

                        public int Unprotect(
                            global::System.ReadOnlySpan<byte> protectedPayload,
                            global::System.ReadOnlySpan<byte> associatedData,
                            global::System.Span<byte> destination)
                        {
                            protectedPayload.CopyTo(
                                destination);

                            return protectedPayload.Length;
                        }
                    }

                    private sealed class NegativeProtectedLengthProtector
                        : global::PacketWire.Security.IPayloadProtector
                    {
                        public int GetProtectedPayloadLength(
                            int plaintextLength)
                        {
                            return -1;
                        }

                        public int GetMaximumPlaintextLength(
                            int protectedPayloadLength)
                        {
                            throw new global::System.NotSupportedException();
                        }

                        public int Protect(
                            global::System.ReadOnlySpan<byte> plaintext,
                            global::System.ReadOnlySpan<byte> associatedData,
                            global::System.Span<byte> destination)
                        {
                            throw new global::System.NotSupportedException();
                        }

                        public int Unprotect(
                            global::System.ReadOnlySpan<byte> protectedPayload,
                            global::System.ReadOnlySpan<byte> associatedData,
                            global::System.Span<byte> destination)
                        {
                            throw new global::System.NotSupportedException();
                        }
                    }

                    private sealed class ShortWriteProtector
                        : global::PacketWire.Security.IPayloadProtector
                    {
                        public int GetProtectedPayloadLength(
                            int plaintextLength)
                        {
                            return checked(
                                plaintextLength +
                                1);
                        }

                        public int GetMaximumPlaintextLength(
                            int protectedPayloadLength)
                        {
                            throw new global::System.NotSupportedException();
                        }

                        public int Protect(
                            global::System.ReadOnlySpan<byte> plaintext,
                            global::System.ReadOnlySpan<byte> associatedData,
                            global::System.Span<byte> destination)
                        {
                            return plaintext.Length;
                        }

                        public int Unprotect(
                            global::System.ReadOnlySpan<byte> protectedPayload,
                            global::System.ReadOnlySpan<byte> associatedData,
                            global::System.Span<byte> destination)
                        {
                            throw new global::System.NotSupportedException();
                        }
                    }

                    private sealed class NegativeMaximumLengthProtector
                        : global::PacketWire.Security.IPayloadProtector
                    {
                        public int GetProtectedPayloadLength(
                            int plaintextLength)
                        {
                            throw new global::System.NotSupportedException();
                        }

                        public int GetMaximumPlaintextLength(
                            int protectedPayloadLength)
                        {
                            return -1;
                        }

                        public int Protect(
                            global::System.ReadOnlySpan<byte> plaintext,
                            global::System.ReadOnlySpan<byte> associatedData,
                            global::System.Span<byte> destination)
                        {
                            throw new global::System.NotSupportedException();
                        }

                        public int Unprotect(
                            global::System.ReadOnlySpan<byte> protectedPayload,
                            global::System.ReadOnlySpan<byte> associatedData,
                            global::System.Span<byte> destination)
                        {
                            throw new global::System.NotSupportedException();
                        }
                    }

                    private sealed class NegativePlaintextLengthProtector
                        : global::PacketWire.Security.IPayloadProtector
                    {
                        public int GetProtectedPayloadLength(
                            int plaintextLength)
                        {
                            throw new global::System.NotSupportedException();
                        }

                        public int GetMaximumPlaintextLength(
                            int protectedPayloadLength)
                        {
                            return protectedPayloadLength;
                        }

                        public int Protect(
                            global::System.ReadOnlySpan<byte> plaintext,
                            global::System.ReadOnlySpan<byte> associatedData,
                            global::System.Span<byte> destination)
                        {
                            throw new global::System.NotSupportedException();
                        }

                        public int Unprotect(
                            global::System.ReadOnlySpan<byte> protectedPayload,
                            global::System.ReadOnlySpan<byte> associatedData,
                            global::System.Span<byte> destination)
                        {
                            return -1;
                        }
                    }

                    private sealed class OversizedPlaintextLengthProtector
                        : global::PacketWire.Security.IPayloadProtector
                    {
                        public int GetProtectedPayloadLength(
                            int plaintextLength)
                        {
                            throw new global::System.NotSupportedException();
                        }

                        public int GetMaximumPlaintextLength(
                            int protectedPayloadLength)
                        {
                            return protectedPayloadLength;
                        }

                        public int Protect(
                            global::System.ReadOnlySpan<byte> plaintext,
                            global::System.ReadOnlySpan<byte> associatedData,
                            global::System.Span<byte> destination)
                        {
                            throw new global::System.NotSupportedException();
                        }

                        public int Unprotect(
                            global::System.ReadOnlySpan<byte> protectedPayload,
                            global::System.ReadOnlySpan<byte> associatedData,
                            global::System.Span<byte> destination)
                        {
                            return checked(
                                destination.Length +
                                1);
                        }
                    }
                }
            }
            """;

        Assert.True(
            GeneratorTestHost.InvokeHarness<bool>(
                result,
                harnessSource,
                "PacketWire.Generator.Tests.Dynamic.ProtectionContractHarness",
                "ValidateAll"));
    }

    /// <summary>
    /// Asserts that code generation and compilation succeeded with no diagnostic errors.
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