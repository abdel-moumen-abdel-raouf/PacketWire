namespace PacketWire.Security;

/// <summary>
/// Defines the contract for cryptographic payload protection (encryption and authentication) and unprotection (decryption and verification) in PacketWire.
/// </summary>
/// <remarks>
/// PacketWire frames carrying protected payloads have the <c>PacketFrameOptions.Protected</c> flag set in their header.
/// Implementations of this interface provide authenticated encryption with associated data (AEAD). The clear protocol frame header
/// (<c>PacketLength | Flags | PacketCategory | PacketId</c>) is supplied as <c>associatedData</c> during both protection and unprotection,
/// ensuring that packet identity and framing parameters cannot be tampered with or transposed without invalidating authentication.
/// </remarks>
public interface IPayloadProtector
{
    /// <summary>
    /// Calculates the exact byte length required to store a protected payload (ciphertext plus cryptographic overhead such as nonces and authentication tags) for the given plaintext length.
    /// </summary>
    /// <param name="plaintextLength">The length of the unencrypted payload in bytes. Must be non-negative.</param>
    /// <returns>The total number of bytes required to store the protected payload.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="plaintextLength"/> is negative.</exception>
    int GetProtectedPayloadLength(
        int plaintextLength);

    /// <summary>
    /// Calculates the maximum plaintext byte length that can be recovered from a protected payload of the specified length.
    /// </summary>
    /// <param name="protectedPayloadLength">The byte length of the protected payload received on the wire.</param>
    /// <returns>The maximum plaintext length in bytes after removing cryptographic overhead.</returns>
    /// <exception cref="PayloadProtectionException">Thrown when <paramref name="protectedPayloadLength"/> is smaller than the minimum required cryptographic overhead.</exception>
    int GetMaximumPlaintextLength(
        int protectedPayloadLength);

    /// <summary>
    /// Encrypts and authenticates the supplied plaintext payload, writing the resulting protected payload (nonce, ciphertext, tag) to <paramref name="destination"/>.
    /// </summary>
    /// <param name="plaintext">The unencrypted payload bytes to protect.</param>
    /// <param name="associatedData">Unencrypted header bytes to authenticate alongside the ciphertext (typically the clear frame header).</param>
    /// <param name="destination">The destination span that receives the protected payload.</param>
    /// <returns>The number of bytes written to <paramref name="destination"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="destination"/> is too small to receive the protected payload.</exception>
    /// <exception cref="PayloadProtectionException">Thrown when cryptographic protection or encryption fails.</exception>
    int Protect(
        ReadOnlySpan<byte> plaintext,
        ReadOnlySpan<byte> associatedData,
        Span<byte> destination);

    /// <summary>
    /// Authenticates and decrypts the supplied protected payload, writing the recovered plaintext to <paramref name="destination"/>.
    /// </summary>
    /// <param name="protectedPayload">The protected payload bytes (nonce, ciphertext, tag) received on the wire.</param>
    /// <param name="associatedData">Associated data verified against the cryptographic tag (typically the clear frame header).</param>
    /// <param name="destination">The destination span that receives the recovered plaintext payload.</param>
    /// <returns>The number of plaintext bytes written to <paramref name="destination"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="destination"/> is too small to receive the recovered plaintext.</exception>
    /// <exception cref="PayloadProtectionException">Thrown when cryptographic authentication fails, indicating corrupted data, altered ciphertext, or modified associated data.</exception>
    int Unprotect(
        ReadOnlySpan<byte> protectedPayload,
        ReadOnlySpan<byte> associatedData,
        Span<byte> destination);
}