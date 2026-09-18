using System.Security.Cryptography;

namespace PacketWire.Security;

/// <summary>
/// Provides authenticated encryption and decryption (AEAD) for PacketWire packet payloads using the AES-GCM algorithm.
/// </summary>
/// <remarks>
/// <para>
/// This protector uses the AES algorithm in Galois/Counter Mode (AES-GCM) with a 12-byte (96-bit) randomly generated nonce
/// and a 16-byte (128-bit) authentication tag. The protected wire payload format is:
/// <c>[12-byte Nonce] [Ciphertext] [16-byte Tag]</c>.
/// Total cryptographic overhead is exactly 28 bytes (<see cref="ProtectionOverhead"/>).
/// </para>
/// <para>
/// When protecting or unprotecting a frame, the clear protocol frame header (<c>PacketLength | Flags | PacketCategory | PacketId</c>)
/// is passed as authenticated associated data (AAD). This binds packet identity and framing metadata directly to the ciphertext,
/// preventing frame modification, splicing, or category/ID spoofing.
/// </para>
/// <para>
/// Cryptographic operations on the underlying <see cref="AesGcm"/> instance are synchronized across threads using an internal
/// locking mechanism, making protect and unprotect invocations thread-safe.
/// </para>
/// <para>
/// On cryptographic authentication failure, destination buffers are immediately wiped (<see cref="Span{T}.Clear()"/>)
/// before throwing <see cref="PayloadProtectionException"/> to prevent leaking partial plaintext or unauthenticated data.
/// </para>
/// </remarks>
public sealed class AesGcmPayloadProtector
    : IPayloadProtector,
      IDisposable
{
    /// <summary>
    /// The size, in bytes, of the initialization vector (nonce) used by AES-GCM (12 bytes / 96 bits).
    /// </summary>
    private const int NonceLength =
        12;

    /// <summary>
    /// The size, in bytes, of the authentication tag produced and verified by AES-GCM (16 bytes / 128 bits).
    /// </summary>
    private const int TagLength =
        16;

    /// <summary>
    /// The total cryptographic overhead added to protected payloads, comprising nonce length plus tag length (28 bytes).
    /// </summary>
    private const int ProtectionOverhead =
        NonceLength +
        TagLength;

    /// <summary>
    /// The underlying platform AES-GCM cryptographic primitive instance.
    /// </summary>
    private readonly AesGcm aesGcm;

    /// <summary>
    /// Synchronization object used to ensure thread safety across concurrent encryption and decryption operations.
    /// </summary>
    private readonly object synchronizationRoot =
        new();

    /// <summary>
    /// Indicates whether this protector instance has been disposed.
    /// </summary>
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="AesGcmPayloadProtector"/> class with the specified symmetric key.
    /// </summary>
    /// <param name="key">The AES key. Must be exactly 16 bytes (AES-128), 24 bytes (AES-192), or 32 bytes (AES-256).</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> does not contain exactly 16, 24, or 32 bytes.</exception>
    public AesGcmPayloadProtector(
        ReadOnlySpan<byte> key)
    {
        if (key.Length is not 16 and
            not 24 and
            not 32)
        {
            throw new ArgumentException(
                "AES-GCM keys must contain exactly 16, 24, or 32 bytes.",
                nameof(key));
        }

        aesGcm =
            new AesGcm(
                key,
                TagLength);
    }

    /// <summary>
    /// Calculates the exact byte length of the protected payload for the specified plaintext length.
    /// </summary>
    /// <param name="plaintextLength">The byte length of the unencrypted payload. Must be non-negative.</param>
    /// <returns>The required protected payload length in bytes (plaintext length plus 28 bytes of overhead).</returns>
    /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="plaintextLength"/> is negative.</exception>
    /// <exception cref="OverflowException">Thrown when the resulting length exceeds <see cref="int.MaxValue"/>.</exception>
    public int GetProtectedPayloadLength(
        int plaintextLength)
    {
        ThrowIfDisposed();

        ArgumentOutOfRangeException.ThrowIfNegative(
            plaintextLength);

        return checked(
            plaintextLength +
            ProtectionOverhead);
    }

    /// <summary>
    /// Calculates the maximum plaintext byte length that can be recovered from the specified protected payload length.
    /// </summary>
    /// <param name="protectedPayloadLength">The byte length of the protected payload.</param>
    /// <returns>The recovered plaintext length in bytes (protected payload length minus 28 bytes of overhead).</returns>
    /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed.</exception>
    /// <exception cref="PayloadProtectionException">Thrown when <paramref name="protectedPayloadLength"/> is less than the 28-byte minimum overhead.</exception>
    public int GetMaximumPlaintextLength(
        int protectedPayloadLength)
    {
        ThrowIfDisposed();

        if (protectedPayloadLength <
            ProtectionOverhead)
        {
            throw new PayloadProtectionException(
                $"An AES-GCM protected payload requires at least {ProtectionOverhead} bytes.");
        }

        return
            protectedPayloadLength -
            ProtectionOverhead;
    }

    /// <summary>
    /// Encrypts and authenticates the supplied plaintext payload, writing the nonce, ciphertext, and tag into <paramref name="destination"/>.
    /// </summary>
    /// <param name="plaintext">The unencrypted payload bytes to protect.</param>
    /// <param name="associatedData">Associated data to authenticate alongside the ciphertext (typically the clear frame header).</param>
    /// <param name="destination">The destination buffer that receives the protected payload (12-byte nonce, ciphertext, 16-byte tag).</param>
    /// <returns>The total number of protected bytes written to <paramref name="destination"/>.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="destination"/> has insufficient capacity to receive the protected payload.</exception>
    /// <exception cref="PayloadProtectionException">Thrown when encryption or authentication fails.</exception>
    public int Protect(
        ReadOnlySpan<byte> plaintext,
        ReadOnlySpan<byte> associatedData,
        Span<byte> destination)
    {
        ThrowIfDisposed();

        int requiredLength =
            GetProtectedPayloadLength(
                plaintext.Length);

        if (destination.Length <
            requiredLength)
        {
            throw new ArgumentException(
                $"The destination requires at least {requiredLength} bytes.",
                nameof(destination));
        }

        Span<byte> nonce =
            destination.Slice(
                0,
                NonceLength);

        Span<byte> ciphertext =
            destination.Slice(
                NonceLength,
                plaintext.Length);

        Span<byte> tag =
            destination.Slice(
                NonceLength +
                plaintext.Length,
                TagLength);

        RandomNumberGenerator.Fill(
            nonce);

        try
        {
            lock (synchronizationRoot)
            {
                ThrowIfDisposed();

                aesGcm.Encrypt(
                    nonce,
                    plaintext,
                    ciphertext,
                    tag,
                    associatedData);
            }
        }
        catch (CryptographicException exception)
        {
            destination
                .Slice(
                    0,
                    requiredLength)
                .Clear();

            throw new PayloadProtectionException(
                "AES-GCM payload protection failed.",
                exception);
        }

        return requiredLength;
    }

    /// <summary>
    /// Authenticates and decrypts the protected payload, writing the recovered plaintext to <paramref name="destination"/>.
    /// </summary>
    /// <param name="protectedPayload">The protected payload bytes containing nonce, ciphertext, and authentication tag.</param>
    /// <param name="associatedData">The associated data to verify against the authentication tag (typically the clear frame header).</param>
    /// <param name="destination">The destination buffer that receives the decrypted plaintext payload.</param>
    /// <returns>The number of plaintext bytes written to <paramref name="destination"/>.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="destination"/> has insufficient capacity for the plaintext.</exception>
    /// <exception cref="PayloadProtectionException">Thrown when authentication or decryption fails due to modified ciphertext, invalid tag, or tampered associated data.</exception>
    public int Unprotect(
        ReadOnlySpan<byte> protectedPayload,
        ReadOnlySpan<byte> associatedData,
        Span<byte> destination)
    {
        ThrowIfDisposed();

        int plaintextLength =
            GetMaximumPlaintextLength(
                protectedPayload.Length);

        if (destination.Length <
            plaintextLength)
        {
            throw new ArgumentException(
                $"The destination requires at least {plaintextLength} bytes.",
                nameof(destination));
        }

        ReadOnlySpan<byte> nonce =
            protectedPayload.Slice(
                0,
                NonceLength);

        ReadOnlySpan<byte> ciphertext =
            protectedPayload.Slice(
                NonceLength,
                plaintextLength);

        ReadOnlySpan<byte> tag =
            protectedPayload.Slice(
                NonceLength +
                plaintextLength,
                TagLength);

        try
        {
            lock (synchronizationRoot)
            {
                ThrowIfDisposed();

                aesGcm.Decrypt(
                    nonce,
                    ciphertext,
                    tag,
                    destination.Slice(
                        0,
                        plaintextLength),
                    associatedData);
            }
        }
        catch (CryptographicException exception)
        {
            destination
                .Slice(
                    0,
                    plaintextLength)
                .Clear();

            throw new PayloadProtectionException(
                "AES-GCM payload authentication failed.",
                exception);
        }

        return plaintextLength;
    }

    /// <summary>
    /// Releases all resources used by the underlying <see cref="AesGcm"/> cryptographic instance.
    /// </summary>
    public void Dispose()
    {
        lock (synchronizationRoot)
        {
            if (!disposed)
            {
                aesGcm.Dispose();
                disposed = true;
            }
        }

        GC.SuppressFinalize(
            this);
    }

    /// <summary>
    /// Throws an <see cref="ObjectDisposedException"/> if the protector has already been disposed.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when <see cref="disposed"/> is <see langword="true"/>.</exception>
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            disposed,
            this);
    }
}