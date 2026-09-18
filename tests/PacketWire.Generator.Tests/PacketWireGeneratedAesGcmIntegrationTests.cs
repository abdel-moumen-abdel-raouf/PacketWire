using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace PacketWire.Generator.Tests;

/// <summary>
/// Verifies that generated protocol facades integrate with <see cref="PacketWire.Security.AesGcmPayloadProtector"/> for authenticated encryption and tampering detection.
/// </summary>
public sealed class PacketWireGeneratedAesGcmIntegrationTests
{
    /// <summary>
    /// Source code of the protocol and packet used in tests.
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
        public sealed class SecurePacket
        {
            [PacketField(0)]
            public int Value { get; init; }

            [PacketField(1)]
            [FixedString(4)]
            public string Name { get; init; } = string.Empty;
        }
        """;

    /// <summary>
    /// Verifies that serializing and deserializing with a real AES-GCM protector round-trips correctly and remains backward-compatible with plain frames.
    /// </summary>
    [Fact]
    public void RealAesGcmProtectorRoundTripsThroughGeneratedFacade()
    {
        GeneratorTestResult result =
            CreateResult();

        const string harnessSource = """
            namespace PacketWire.Generator.Tests.Dynamic
            {
                public static class AesGcmRoundTripHarness
                {
                    public static bool Validate()
                    {
                        byte[] key =
                            CreateKey(
                                32,
                                0x10);

                        using global::PacketWire.Security.AesGcmPayloadProtector protector =
                            new(
                                key);

                        global::SecurePacket input =
                            new global::SecurePacket
                            {
                                Value = 0x11223344,
                                Name = "A"
                            };

                        byte[] plain =
                            global::ApplicationProtocol.Serialize(
                                input);

                        if (plain.Length != 14)
                        {
                            return false;
                        }

                        byte[] protectedPacket =
                            global::ApplicationProtocol.Serialize(
                                input,
                                protector);

                        //
                        // Plain payload = 8 bytes.
                        //
                        // AES-GCM protected payload:
                        //   12-byte nonce
                        // +  8-byte ciphertext
                        // + 16-byte authentication tag
                        // = 36 bytes
                        //
                        // Protocol header = 6 bytes.
                        //
                        if (protectedPacket.Length != 42)
                        {
                            return false;
                        }

                        global::PacketWire.PacketFrameView frame =
                            global::PacketWire.PacketFrameCodec.ReadFrame(
                                protectedPacket,
                                global::ApplicationProtocol.Definition);

                        if (frame.Header.Flags !=
                            global::PacketWire.PacketFrameOptions.Protected)
                        {
                            return false;
                        }

                        if (frame.Header.PacketCategory != 3 ||
                            frame.Header.PacketId != 0x1234 ||
                            frame.Header.PacketLength != 42 ||
                            frame.PayloadLength != 36)
                        {
                            return false;
                        }

                        global::SecurePacket restored =
                            global::ApplicationProtocol.Deserialize<global::SecurePacket>(
                                protectedPacket,
                                protector);

                        if (restored.Value != 0x11223344 ||
                            restored.Name != "A")
                        {
                            return false;
                        }

                        //
                        // Passing a protector to a plain packet must remain
                        // backward-compatible. It must not attempt to decrypt
                        // a frame whose Protected option is not set.
                        //
                        global::SecurePacket plainRestored =
                            global::ApplicationProtocol.Deserialize<global::SecurePacket>(
                                plain,
                                protector);

                        return
                            plainRestored.Value == 0x11223344 &&
                            plainRestored.Name == "A";
                    }

                    private static byte[] CreateKey(
                        int length,
                        byte seed)
                    {
                        byte[] key =
                            new byte[length];

                        for (
                            int index = 0;
                            index < key.Length;
                            index++)
                        {
                            key[index] =
                                unchecked(
                                    (byte)(
                                        seed +
                                        index));
                        }

                        return key;
                    }
                }
            }
            """;

        Assert.True(
            GeneratorTestHost.InvokeHarness<bool>(
                result,
                harnessSource,
                "PacketWire.Generator.Tests.Dynamic.AesGcmRoundTripHarness",
                "Validate"));
    }

    /// <summary>
    /// Verifies that tampering with authenticated associated data, nonce, ciphertext, tag, or using the wrong key causes authentication failure.
    /// </summary>
    [Fact]
    public void RealAesGcmProtectorAuthenticatesHeaderPayloadAndKey()
    {
        GeneratorTestResult result =
            CreateResult();

        const string harnessSource = """
            namespace PacketWire.Generator.Tests.Dynamic
            {
                public static class AesGcmTamperingHarness
                {
                    public static bool Validate()
                    {
                        byte[] key =
                            CreateKey(
                                24,
                                0x20);

                        byte[] original;

                        using (
                            global::PacketWire.Security.AesGcmPayloadProtector protector =
                                new(
                                    key))
                        {
                            original =
                                global::ApplicationProtocol.Serialize(
                                    new global::SecurePacket
                                    {
                                        Value = 0x11223344,
                                        Name = "A"
                                    },
                                    protector);
                        }

                        int headerLength =
                            global::ApplicationProtocol
                                .Definition
                                .HeaderLength;

                        if (headerLength != 6)
                        {
                            return false;
                        }

                        //
                        // Category is authenticated as Associated Data.
                        //
                        byte[] categoryTampered =
                            (byte[])original.Clone();

                        categoryTampered[3] ^=
                            0x01;

                        if (!ExpectProtectionFailure(
                                categoryTampered,
                                key))
                        {
                            return false;
                        }

                        //
                        // Packet ID is authenticated as Associated Data.
                        //
                        byte[] idTampered =
                            (byte[])original.Clone();

                        idTampered[4] ^=
                            0x01;

                        if (!ExpectProtectionFailure(
                                idTampered,
                                key))
                        {
                            return false;
                        }

                        //
                        // Nonce starts immediately after the header.
                        //
                        byte[] nonceTampered =
                            (byte[])original.Clone();

                        nonceTampered[
                            headerLength] ^=
                            0x01;

                        if (!ExpectProtectionFailure(
                                nonceTampered,
                                key))
                        {
                            return false;
                        }

                        //
                        // Ciphertext begins after the 12-byte nonce.
                        //
                        byte[] ciphertextTampered =
                            (byte[])original.Clone();

                        ciphertextTampered[
                            headerLength +
                            12] ^=
                            0x01;

                        if (!ExpectProtectionFailure(
                                ciphertextTampered,
                                key))
                        {
                            return false;
                        }

                        //
                        // Authentication tag occupies the final 16 bytes.
                        //
                        byte[] tagTampered =
                            (byte[])original.Clone();

                        tagTampered[^1] ^=
                            0x01;

                        if (!ExpectProtectionFailure(
                                tagTampered,
                                key))
                        {
                            return false;
                        }

                        //
                        // Same packet, different key.
                        //
                        byte[] wrongKey =
                            (byte[])key.Clone();

                        wrongKey[0] ^=
                            0x80;

                        if (!ExpectProtectionFailure(
                                original,
                                wrongKey))
                        {
                            return false;
                        }

                        using global::PacketWire.Security.AesGcmPayloadProtector controlProtector =
                            new(
                                key);

                        global::SecurePacket control =
                            global::ApplicationProtocol.Deserialize<global::SecurePacket>(
                                original,
                                controlProtector);

                        return
                            control.Value == 0x11223344 &&
                            control.Name == "A";
                    }

                    private static bool ExpectProtectionFailure(
                        byte[] packet,
                        byte[] key)
                    {
                        try
                        {
                            using global::PacketWire.Security.AesGcmPayloadProtector protector =
                                new(
                                    key);

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

                    private static byte[] CreateKey(
                        int length,
                        byte seed)
                    {
                        byte[] key =
                            new byte[length];

                        for (
                            int index = 0;
                            index < key.Length;
                            index++)
                        {
                            key[index] =
                                unchecked(
                                    (byte)(
                                        seed +
                                        index));
                        }

                        return key;
                    }
                }
            }
            """;

        Assert.True(
            GeneratorTestHost.InvokeHarness<bool>(
                result,
                harnessSource,
                "PacketWire.Generator.Tests.Dynamic.AesGcmTamperingHarness",
                "Validate"));
    }

    /// <summary>
    /// Compiles the test protocol source and asserts no compilation errors.
    /// </summary>
    /// <returns>A successful <see cref="GeneratorTestResult"/>.</returns>
    private static GeneratorTestResult CreateResult()
    {
        GeneratorTestResult result =
            GeneratorTestHost.RunWithOutput(
                ProtocolSource);

        AssertSuccessfulCompilation(
            result);

        return result;
    }

    /// <summary>
    /// Asserts that compilation produced no diagnostics or errors.
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