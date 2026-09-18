using PacketWire.Security;

namespace PacketWire.Runtime.Tests;

/// <summary>
/// Verifies architectural decoupling, contracts, error propagation, and mock payload protector behavior.
/// </summary>
public sealed class PayloadProtectionAbstractionTests
{
    /// <summary>
    /// Sample test plaintext bytes.
    /// </summary>
    private static readonly byte[] Plaintext =
    [
        0x10,
        0x20,
        0x30
    ];

    /// <summary>
    /// Sample associated data representing header framing bytes.
    /// </summary>
    private static readonly byte[] AssociatedData =
    [
        0xA0,
        0x0F
    ];

    /// <summary>
    /// Modified associated data used to verify authentication failure.
    /// </summary>
    private static readonly byte[] WrongAssociatedData =
    [
        0xA0,
        0x0E
    ];

    /// <summary>
    /// Expected wire bytes produced by <see cref="DeterministicTestProtector"/>.
    /// </summary>
    private static readonly byte[] ExpectedProtectedPayload =
    [
        0xAF,
        0x10,
        0x20,
        0x30,
        0xA5
    ];

    /// <summary>
    /// Verifies that PacketWire.Security.Abstractions has no dependency reference to PacketWire.Runtime.
    /// </summary>
    [Fact]
    public void SecurityAbstractionsAssemblyDoesNotReferenceRuntime()
    {
        var references =
            typeof(IPayloadProtector)
                .Assembly
                .GetReferencedAssemblies();

        Assert.DoesNotContain(
            references,
            static reference =>
                string.Equals(
                    reference.Name,
                    "PacketWire.Runtime",
                    StringComparison.Ordinal));
    }

    /// <summary>
    /// Verifies that the protector contract correctly handles associated data, length queries, and plaintext round-tripping.
    /// </summary>
    [Fact]
    public void ProtectorContractSupportsAssociatedDataAndVariableLengthOutput()
    {
        DeterministicTestProtector protector =
            new();

        int protectedLength =
            protector.GetProtectedPayloadLength(
                Plaintext.Length);

        Assert.Equal(
            5,
            protectedLength);

        byte[] protectedPayload =
            new byte[protectedLength];

        int protectedBytesWritten =
            protector.Protect(
                Plaintext,
                AssociatedData,
                protectedPayload);

        Assert.Equal(
            protectedLength,
            protectedBytesWritten);

        Assert.Equal(
            ExpectedProtectedPayload,
            protectedPayload);

        int maximumPlaintextLength =
            protector.GetMaximumPlaintextLength(
                protectedPayload.Length);

        Assert.Equal(
            Plaintext.Length,
            maximumPlaintextLength);

        byte[] restoredPlaintext =
            new byte[maximumPlaintextLength];

        int plaintextBytesWritten =
            protector.Unprotect(
                protectedPayload,
                AssociatedData,
                restoredPlaintext);

        Assert.Equal(
            Plaintext.Length,
            plaintextBytesWritten);

        Assert.Equal(
            Plaintext,
            restoredPlaintext);
    }

    /// <summary>
    /// Verifies that presenting invalid associated data during unprotection throws <see cref="PayloadProtectionException"/>.
    /// </summary>
    [Fact]
    public void ProtectorCanRejectIncorrectAssociatedData()
    {
        DeterministicTestProtector protector =
            new();

        byte[] protectedPayload =
            new byte[
                protector.GetProtectedPayloadLength(
                    Plaintext.Length)];

        _ =
            protector.Protect(
                Plaintext,
                AssociatedData,
                protectedPayload);

        byte[] plaintextDestination =
            new byte[
                protector.GetMaximumPlaintextLength(
                    protectedPayload.Length)];

        Assert.Throws<PayloadProtectionException>(
            () =>
                protector.Unprotect(
                    protectedPayload,
                    WrongAssociatedData,
                    plaintextDestination));
    }

    /// <summary>
    /// Verifies that <see cref="PayloadProtectionException"/> supports default, message, and inner-exception constructor overloads.
    /// </summary>
    [Fact]
    public void PayloadProtectionExceptionSupportsStandardConstructors()
    {
        PayloadProtectionException defaultException =
            new();

        PayloadProtectionException messageException =
            new(
                "Protection failed.");

        InvalidOperationException innerException =
            new(
                "Inner failure.");

        PayloadProtectionException wrappedException =
            new(
                "Protection failed.",
                innerException);

        Assert.NotNull(
            defaultException.Message);

        Assert.Equal(
            "Protection failed.",
            messageException.Message);

        Assert.Equal(
            "Protection failed.",
            wrappedException.Message);

        Assert.Same(
            innerException,
            wrappedException.InnerException);
    }

    /// <summary>
    /// Deterministic test implementation of <see cref="IPayloadProtector"/> for testing associated data markers and framing.
    /// </summary>
    private sealed class DeterministicTestProtector
        : IPayloadProtector
    {
        /// <summary>
        /// Constant trailing sentinel byte appended to protected payloads.
        /// </summary>
        private const byte Trailer =
            0xA5;

        /// <summary>
        /// Calculates the protected payload length given the plaintext length.
        /// </summary>
        /// <param name="plaintextLength">The length of plaintext in bytes.</param>
        /// <returns>The total protected payload length.</returns>
        public int GetProtectedPayloadLength(
            int plaintextLength)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(
                plaintextLength);

            return checked(
                plaintextLength +
                2);
        }

        /// <summary>
        /// Calculates the maximum plaintext length from the protected payload length.
        /// </summary>
        /// <param name="protectedPayloadLength">The protected payload byte length.</param>
        /// <returns>The maximum plaintext length in bytes.</returns>
        public int GetMaximumPlaintextLength(
            int protectedPayloadLength)
        {
            if (protectedPayloadLength < 2)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(protectedPayloadLength),
                    protectedPayloadLength,
                    "The protected payload must contain at least two bytes.");
            }

            return
                protectedPayloadLength -
                2;
        }

        /// <summary>
        /// Protects plaintext by prefixing an associated data marker byte and appending a trailing sentinel byte.
        /// </summary>
        /// <param name="plaintext">The plaintext bytes to protect.</param>
        /// <param name="associatedData">The associated framing data to authenticate.</param>
        /// <param name="destination">The destination buffer for protected bytes.</param>
        /// <returns>The number of bytes written to the destination.</returns>
        public int Protect(
            ReadOnlySpan<byte> plaintext,
            ReadOnlySpan<byte> associatedData,
            Span<byte> destination)
        {
            int requiredLength =
                GetProtectedPayloadLength(
                    plaintext.Length);

            if (destination.Length < requiredLength)
            {
                throw new ArgumentException(
                    "The destination buffer is too small.",
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

        /// <summary>
        /// Unprotects a protected payload by validating the associated data marker and trailing sentinel byte.
        /// </summary>
        /// <param name="protectedPayload">The protected payload bytes.</param>
        /// <param name="associatedData">The associated framing data.</param>
        /// <param name="destination">The destination buffer for decrypted plaintext.</param>
        /// <returns>The number of plaintext bytes written.</returns>
        public int Unprotect(
            ReadOnlySpan<byte> protectedPayload,
            ReadOnlySpan<byte> associatedData,
            Span<byte> destination)
        {
            if (protectedPayload.Length < 2)
            {
                throw new PayloadProtectionException(
                    "The protected payload is malformed.");
            }

            int plaintextLength =
                protectedPayload.Length -
                2;

            if (destination.Length < plaintextLength)
            {
                throw new ArgumentException(
                    "The destination buffer is too small.",
                    nameof(destination));
            }

            byte expectedMarker =
                CalculateAssociatedDataMarker(
                    associatedData);

            if (protectedPayload[0] != expectedMarker)
            {
                throw new PayloadProtectionException(
                    "Associated-data verification failed.");
            }

            if (protectedPayload[^1] != Trailer)
            {
                throw new PayloadProtectionException(
                    "Protected-payload verification failed.");
            }

            protectedPayload
                .Slice(
                    1,
                    plaintextLength)
                .CopyTo(destination);

            return plaintextLength;
        }

        /// <summary>
        /// Computes a single-byte checksum marker from associated data bytes.
        /// </summary>
        /// <param name="associatedData">The associated data bytes.</param>
        /// <returns>The XOR checksum marker.</returns>
        private static byte CalculateAssociatedDataMarker(
            ReadOnlySpan<byte> associatedData)
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
}