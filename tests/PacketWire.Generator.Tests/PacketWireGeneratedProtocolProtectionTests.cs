using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace PacketWire.Generator.Tests;

/// <summary>
/// Contains generator tests verifying that protected protocol facades utilize the packet frame header
/// as authenticated associated data during encryption and decryption, and correctly handle round-trips.
/// </summary>
public sealed class PacketWireGeneratedProtocolProtectionTests
{
    /// <summary>
    /// The expected protected binary frame byte sequence for the test login request.
    /// </summary>
    private static readonly byte[] ExpectedProtectedPacket =
    [
        0x10,
        0x00,

        0x01,

        0x03,

        0x34,
        0x12,

        0x34,

        0x44,
        0x33,
        0x22,
        0x11,

        0x41,
        0x00,
        0x00,
        0x00,

        0xA5
    ];

    /// <summary>
    /// Verifies that a generated protected facade authenticates the packet header as associated data,
    /// round-trips serialized models, and rejects unprotected deserialization when protection is expected.
    /// </summary>
    [Fact]
    public void ProtectedFacadeUsesHeaderAsAssociatedDataAndRoundTrips()
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
            public sealed class LoginRequest
            {
                [PacketField(0)]
                public int UserId { get; init; }

                [PacketField(1)]
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
                public static class ProtectionHarness
                {
                    public static byte[] SerializeProtected()
                    {
                        return global::ApplicationProtocol.Serialize(
                            new global::LoginRequest
                            {
                                UserId = 0x11223344,
                                Name = "A"
                            },
                            new DeterministicProtector());
                    }

                    public static bool ValidateBehavior()
                    {
                        byte[] protectedPacket =
                            SerializeProtected();

                        global::LoginRequest protectedLogin =
                            global::ApplicationProtocol.Deserialize<global::LoginRequest>(
                                protectedPacket,
                                new DeterministicProtector());

                        if (protectedLogin.UserId != 0x11223344 ||
                            protectedLogin.Name != "A")
                        {
                            return false;
                        }

                        try
                        {
                            _ =
                                global::ApplicationProtocol.Deserialize(
                                    protectedPacket);

                            return false;
                        }
                        catch (global::PacketWire.PacketProtectionRequiredException exception)
                        {
                            if ((exception.Options &
                                 global::PacketWire.PacketFrameOptions.Protected) == 0)
                            {
                                return false;
                            }
                        }

                        byte[] plainPacket =
                            global::ApplicationProtocol.Serialize(
                                new global::LoginRequest
                                {
                                    UserId = 123,
                                    Name = "B"
                                });

                        global::LoginRequest plainLogin =
                            global::ApplicationProtocol.Deserialize<global::LoginRequest>(
                                plainPacket,
                                new ThrowingProtector());

                        return
                            plainLogin.UserId == 123 &&
                            plainLogin.Name == "B";
                    }

                    private sealed class DeterministicProtector
                        : global::PacketWire.Security.IPayloadProtector
                    {
                        private const byte Trailer =
                            0xA5;

                        public int GetProtectedPayloadLength(
                            int plaintextLength)
                        {
                            global::System.ArgumentOutOfRangeException.ThrowIfNegative(
                                plaintextLength);

                            return checked(
                                plaintextLength +
                                2);
                        }

                        public int GetMaximumPlaintextLength(
                            int protectedPayloadLength)
                        {
                            if (protectedPayloadLength < 2)
                            {
                                throw new global::System.ArgumentOutOfRangeException(
                                    nameof(protectedPayloadLength));
                            }

                            return
                                protectedPayloadLength -
                                2;
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

                            destination[0] =
                                CalculateAssociatedDataMarker(
                                    associatedData);

                            plaintext.CopyTo(
                                destination.Slice(
                                    1,
                                    plaintext.Length));

                            destination[requiredLength - 1] =
                                Trailer;

                            return requiredLength;
                        }

                        public int Unprotect(
                            global::System.ReadOnlySpan<byte> protectedPayload,
                            global::System.ReadOnlySpan<byte> associatedData,
                            global::System.Span<byte> destination)
                        {
                            if (protectedPayload.Length < 2)
                            {
                                throw new global::PacketWire.Security.PayloadProtectionException(
                                    "Malformed protected payload.");
                            }

                            int plaintextLength =
                                protectedPayload.Length -
                                2;

                            if (destination.Length < plaintextLength)
                            {
                                throw new global::System.ArgumentException(
                                    "Destination is too small.",
                                    nameof(destination));
                            }

                            byte expectedMarker =
                                CalculateAssociatedDataMarker(
                                    associatedData);

                            if (protectedPayload[0] != expectedMarker)
                            {
                                throw new global::PacketWire.Security.PayloadProtectionException(
                                    "Associated-data verification failed.");
                            }

                            if (protectedPayload[^1] != Trailer)
                            {
                                throw new global::PacketWire.Security.PayloadProtectionException(
                                    "Protected-payload verification failed.");
                            }

                            protectedPayload
                                .Slice(
                                    1,
                                    plaintextLength)
                                .CopyTo(destination);

                            return plaintextLength;
                        }

                        private static byte CalculateAssociatedDataMarker(
                            global::System.ReadOnlySpan<byte> associatedData)
                        {
                            byte marker =
                                0;

                            foreach (byte value in associatedData)
                            {
                                marker ^= value;
                            }

                            return marker;
                        }
                    }

                    private sealed class ThrowingProtector
                        : global::PacketWire.Security.IPayloadProtector
                    {
                        public int GetProtectedPayloadLength(
                            int plaintextLength)
                        {
                            throw new global::System.InvalidOperationException(
                                "The protector must not be used for a plain packet.");
                        }

                        public int GetMaximumPlaintextLength(
                            int protectedPayloadLength)
                        {
                            throw new global::System.InvalidOperationException(
                                "The protector must not be used for a plain packet.");
                        }

                        public int Protect(
                            global::System.ReadOnlySpan<byte> plaintext,
                            global::System.ReadOnlySpan<byte> associatedData,
                            global::System.Span<byte> destination)
                        {
                            throw new global::System.InvalidOperationException(
                                "The protector must not be used for a plain packet.");
                        }

                        public int Unprotect(
                            global::System.ReadOnlySpan<byte> protectedPayload,
                            global::System.ReadOnlySpan<byte> associatedData,
                            global::System.Span<byte> destination)
                        {
                            throw new global::System.InvalidOperationException(
                                "The protector must not be used for a plain packet.");
                        }
                    }
                }
            }
            """;

        byte[] protectedPacket =
            GeneratorTestHost.InvokeHarness<byte[]>(
                result,
                harnessSource,
                "PacketWire.Generator.Tests.Dynamic.ProtectionHarness",
                "SerializeProtected");

        Assert.Equal(
            ExpectedProtectedPacket,
            protectedPacket);

        Assert.True(
            GeneratorTestHost.InvokeHarness<bool>(
                result,
                harnessSource,
                "PacketWire.Generator.Tests.Dynamic.ProtectionHarness",
                "ValidateBehavior"));
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