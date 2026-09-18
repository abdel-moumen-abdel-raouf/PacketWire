using System.Security.Cryptography;
using PacketWire.Security;

namespace PacketWire.Security.AesGcm.Tests;

/// <summary>
/// Verifies encryption, decryption, authentication, and error handling of <see cref="AesGcmPayloadProtector"/>.
/// </summary>
public sealed class AesGcmPayloadProtectorTests
{
    /// <summary>
    /// The standard 12-byte nonce length for AES-GCM.
    /// </summary>
    private const int NonceLength =
        12;

    /// <summary>
    /// The standard 16-byte authentication tag length for AES-GCM.
    /// </summary>
    private const int TagLength =
        16;

    /// <summary>
    /// Sample 128-bit AES key used across test runs.
    /// </summary>
    private static readonly byte[] Key =
    [
        0x00,
        0x01,
        0x02,
        0x03,
        0x04,
        0x05,
        0x06,
        0x07,
        0x08,
        0x09,
        0x0A,
        0x0B,
        0x0C,
        0x0D,
        0x0E,
        0x0F
    ];

    /// <summary>
    /// Sample test plaintext bytes.
    /// </summary>
    private static readonly byte[] Plaintext =
    [
        0x10,
        0x20,
        0x30,
        0x40,
        0x50
    ];

    /// <summary>
    /// Sample associated data representing header framing bytes.
    /// </summary>
    private static readonly byte[] AssociatedData =
    [
        0x09,
        0x00,
        0x01,
        0x03,
        0x34,
        0x12
    ];

    /// <summary>
    /// Verifies that payloads protected by <see cref="AesGcmPayloadProtector"/> can be decrypted directly by the BCL <see cref="System.Security.Cryptography.AesGcm"/> class.
    /// </summary>
    [Fact]
    public void ProtectInteroperatesWithFrameworkAesGcm()
    {
        using AesGcmPayloadProtector protector =
            new(
                Key);

        int protectedLength =
            protector.GetProtectedPayloadLength(
                Plaintext.Length);

        Assert.Equal(
            Plaintext.Length +
            NonceLength +
            TagLength,
            protectedLength);

        byte[] protectedPayload =
            new byte[
                protectedLength];

        int bytesWritten =
            protector.Protect(
                Plaintext,
                AssociatedData,
                protectedPayload);

        Assert.Equal(
            protectedLength,
            bytesWritten);

        ReadOnlySpan<byte> nonce =
            protectedPayload.AsSpan(
                0,
                NonceLength);

        ReadOnlySpan<byte> ciphertext =
            protectedPayload.AsSpan(
                NonceLength,
                Plaintext.Length);

        ReadOnlySpan<byte> tag =
            protectedPayload.AsSpan(
                NonceLength +
                Plaintext.Length,
                TagLength);

        byte[] decrypted =
            new byte[
                Plaintext.Length];

        using System.Security.Cryptography.AesGcm frameworkAesGcm =
            new(
                Key,
                TagLength);

        frameworkAesGcm.Decrypt(
            nonce,
            ciphertext,
            tag,
            decrypted,
            AssociatedData);

        Assert.Equal(
            Plaintext,
            decrypted);
    }

    /// <summary>
    /// Verifies that payloads encrypted by the standard BCL <see cref="System.Security.Cryptography.AesGcm"/> class can be unprotected by <see cref="AesGcmPayloadProtector"/>.
    /// </summary>
    [Fact]
    public void UnprotectInteroperatesWithFrameworkAesGcm()
    {
        byte[] nonce =
        [
            0x00,
            0x11,
            0x22,
            0x33,
            0x44,
            0x55,
            0x66,
            0x77,
            0x88,
            0x99,
            0xAA,
            0xBB
        ];

        byte[] protectedPayload =
            new byte[
                NonceLength +
                Plaintext.Length +
                TagLength];

        nonce.CopyTo(
            protectedPayload,
            0);

        Span<byte> ciphertext =
            protectedPayload.AsSpan(
                NonceLength,
                Plaintext.Length);

        Span<byte> tag =
            protectedPayload.AsSpan(
                NonceLength +
                Plaintext.Length,
                TagLength);

        using (System.Security.Cryptography.AesGcm frameworkAesGcm =
            new(
                Key,
                TagLength))
        {
            frameworkAesGcm.Encrypt(
                nonce,
                Plaintext,
                ciphertext,
                tag,
                AssociatedData);
        }

        using AesGcmPayloadProtector protector =
            new(
                Key);

        int maximumPlaintextLength =
            protector.GetMaximumPlaintextLength(
                protectedPayload.Length);

        Assert.Equal(
            Plaintext.Length,
            maximumPlaintextLength);

        byte[] restored =
            new byte[
                maximumPlaintextLength];

        int bytesWritten =
            protector.Unprotect(
                protectedPayload,
                AssociatedData,
                restored);

        Assert.Equal(
            Plaintext.Length,
            bytesWritten);

        Assert.Equal(
            Plaintext,
            restored);
    }

    /// <summary>
    /// Verifies that modifying ciphertext, nonce, tag, or associated data causes authentication failure throwing <see cref="PayloadProtectionException"/>.
    /// </summary>
    [Fact]
    public void AuthenticationRejectsTamperingAndWrongAssociatedData()
    {
        using AesGcmPayloadProtector protector =
            new(
                Key);

        byte[] protectedPayload =
            new byte[
                protector.GetProtectedPayloadLength(
                    Plaintext.Length)];

        _ =
            protector.Protect(
                Plaintext,
                AssociatedData,
                protectedPayload);

        byte[] destination =
            new byte[
                Plaintext.Length];

        byte[] nonceTampered =
            (byte[])protectedPayload.Clone();

        nonceTampered[0] ^=
            0x01;

        Assert.Throws<PayloadProtectionException>(
            () =>
                protector.Unprotect(
                    nonceTampered,
                    AssociatedData,
                    destination));

        byte[] ciphertextTampered =
            (byte[])protectedPayload.Clone();

        ciphertextTampered[
            NonceLength] ^=
            0x01;

        Assert.Throws<PayloadProtectionException>(
            () =>
                protector.Unprotect(
                    ciphertextTampered,
                    AssociatedData,
                    destination));

        byte[] tagTampered =
            (byte[])protectedPayload.Clone();

        tagTampered[^1] ^=
            0x01;

        Assert.Throws<PayloadProtectionException>(
            () =>
                protector.Unprotect(
                    tagTampered,
                    AssociatedData,
                    destination));

        byte[] wrongAssociatedData =
            (byte[])AssociatedData.Clone();

        wrongAssociatedData[0] ^=
            0x01;

        Assert.Throws<PayloadProtectionException>(
            () =>
                protector.Unprotect(
                    protectedPayload,
                    wrongAssociatedData,
                    destination));
    }

    /// <summary>
    /// Verifies that protected payloads smaller than nonce plus tag length are rejected.
    /// </summary>
    [Fact]
    public void TooShortProtectedPayloadIsRejected()
    {
        using AesGcmPayloadProtector protector =
            new(
                Key);

        byte[] malformed =
            new byte[
                NonceLength +
                TagLength -
                1];

        Assert.Throws<PayloadProtectionException>(
            () =>
                protector.GetMaximumPlaintextLength(
                    malformed.Length));

        Assert.Throws<PayloadProtectionException>(
            () =>
                protector.Unprotect(
                    malformed,
                    AssociatedData,
                    Array.Empty<byte>()));
    }

    /// <summary>
    /// Verifies that passing undersized destination spans throws <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void SmallDestinationBuffersAreRejected()
    {
        using AesGcmPayloadProtector protector =
            new(
                Key);

        int protectedLength =
            protector.GetProtectedPayloadLength(
                Plaintext.Length);

        byte[] tooSmallProtectedDestination =
            new byte[
                protectedLength -
                1];

        Assert.Throws<ArgumentException>(
            () =>
                protector.Protect(
                    Plaintext,
                    AssociatedData,
                    tooSmallProtectedDestination));

        byte[] protectedPayload =
            new byte[
                protectedLength];

        _ =
            protector.Protect(
                Plaintext,
                AssociatedData,
                protectedPayload);

        byte[] tooSmallPlaintextDestination =
            new byte[
                Plaintext.Length -
                1];

        Assert.Throws<ArgumentException>(
            () =>
                protector.Unprotect(
                    protectedPayload,
                    AssociatedData,
                    tooSmallPlaintextDestination));
    }

    /// <summary>
    /// Verifies that key sizes other than 128, 192, and 256 bits are rejected during construction.
    /// </summary>
    [Fact]
    public void InvalidAesKeyLengthsAreRejected()
    {
        int[] invalidLengths =
        [
            0,
            1,
            15,
            17,
            23,
            25,
            31,
            33
        ];

        foreach (int length in invalidLengths)
        {
            byte[] invalidKey =
                new byte[length];

            Assert.Throws<ArgumentException>(
                () =>
                    new AesGcmPayloadProtector(
                        invalidKey));
        }
    }

    /// <summary>
    /// Verifies that methods on a disposed protector instance throw <see cref="ObjectDisposedException"/>.
    /// </summary>
    [Fact]
    public void DisposedProtectorRejectsFurtherUse()
    {
        AesGcmPayloadProtector protector =
            new(
                Key);

        protector.Dispose();

        Assert.Throws<ObjectDisposedException>(
            () =>
                protector.GetProtectedPayloadLength(
                    Plaintext.Length));

        Assert.Throws<ObjectDisposedException>(
            () =>
                protector.GetMaximumPlaintextLength(
                    NonceLength +
                    TagLength));

        byte[] destination =
            new byte[
                Plaintext.Length +
                NonceLength +
                TagLength];

        Assert.Throws<ObjectDisposedException>(
            () =>
                protector.Protect(
                    Plaintext,
                    AssociatedData,
                    destination));

        Assert.Throws<ObjectDisposedException>(
            () =>
                protector.Unprotect(
                    destination,
                    AssociatedData,
                    new byte[
                        Plaintext.Length]));
    }
}