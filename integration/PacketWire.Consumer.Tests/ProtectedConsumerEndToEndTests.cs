using PacketWire;
using PacketWire.Consumer;
using PacketWire.Security;

namespace PacketWire.Consumer.Tests;

/// <summary>
/// Verifies end-to-end encrypted and authenticated packet serialization, framing, and decryption using <see cref="AesGcmPayloadProtector"/>.
/// </summary>
public sealed class ProtectedConsumerEndToEndTests
{
    /// <summary>
    /// Sample 256-bit cryptographic key used for authenticated encryption.
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
        0x0F,
        0x10,
        0x11,
        0x12,
        0x13,
        0x14,
        0x15,
        0x16,
        0x17,
        0x18,
        0x19,
        0x1A,
        0x1B,
        0x1C,
        0x1D,
        0x1E,
        0x1F
    ];

    /// <summary>
    /// Verifies that serializing and deserializing consumer packets with an AES-GCM protector round-trips correctly through both typed and untyped overloads.
    /// </summary>
    [Fact]
    public void RealAesGcmProtectorRoundTripsConsumerDtoThroughPublicFacade()
    {
        using AesGcmPayloadProtector protector =
            new(
                Key);

        PingPacket input =
            CreatePacket();

        byte[] protectedFrame =
            PrimaryProtocol.Serialize(
                input,
                protector);

        Assert.Equal(
            47,
            protectedFrame.Length);

        PacketFrameView parsed =
            PacketFrameCodec.ReadFrame(
                protectedFrame,
                PrimaryProtocol.Definition);

        Assert.Equal(
            47UL,
            parsed.Header.PacketLength);

        Assert.Equal(
            PacketFrameOptions.Protected,
            parsed.Header.Flags);

        Assert.Equal(
            1UL,
            parsed.Header.PacketCategory);

        Assert.Equal(
            0x1001UL,
            parsed.Header.PacketId);

        Assert.Equal(
            41,
            parsed.PayloadLength);

        PingPacket typed =
            PrimaryProtocol.Deserialize<PingPacket>(
                protectedFrame,
                protector);

        AssertPacket(
            typed);

        object untyped =
            PrimaryProtocol.Deserialize(
                protectedFrame,
                protector);

        PingPacket untypedPacket =
            Assert.IsType<PingPacket>(
                untyped);

        AssertPacket(
            untypedPacket);
    }

    /// <summary>
    /// Verifies that protected deserialization overloads transparently accept plain (unprotected) frames without error.
    /// </summary>
    [Fact]
    public void ProtectedDeserializeOverloadAcceptsPlainConsumerFrame()
    {
        using AesGcmPayloadProtector protector =
            new(
                Key);

        PingPacket input =
            CreatePacket();

        byte[] plainFrame =
            PrimaryProtocol.Serialize(
                input);

        Assert.Equal(
            19,
            plainFrame.Length);

        PingPacket restored =
            PrimaryProtocol.Deserialize<PingPacket>(
                plainFrame,
                protector);

        AssertPacket(
            restored);
    }

    /// <summary>
    /// Verifies that altering header fields, nonce, ciphertext, authentication tag, or key results in an authentication failure.
    /// </summary>
    [Fact]
    public void ProtectedConsumerFramesAuthenticateHeaderPayloadAndKey()
    {
        using AesGcmPayloadProtector protector =
            new(
                Key);

        byte[] frame =
            PrimaryProtocol.Serialize(
                CreatePacket(),
                protector);

        const int headerLength =
            6;

        //
        // Clear header is authenticated as Associated Data.
        //

        byte[] categoryTampered =
            (byte[])frame.Clone();

        categoryTampered[3] ^=
            0x01;

        AssertProtectionFailure(
            categoryTampered,
            protector);

        byte[] packetIdTampered =
            (byte[])frame.Clone();

        packetIdTampered[4] ^=
            0x01;

        AssertProtectionFailure(
            packetIdTampered,
            protector);

        //
        // AES-GCM protected payload format:
        //
        // nonce      = 12 bytes
        // ciphertext = 13 bytes
        // tag        = 16 bytes
        //

        byte[] nonceTampered =
            (byte[])frame.Clone();

        nonceTampered[headerLength] ^=
            0x01;

        AssertProtectionFailure(
            nonceTampered,
            protector);

        byte[] ciphertextTampered =
            (byte[])frame.Clone();

        ciphertextTampered[
            headerLength +
            12] ^=
            0x01;

        AssertProtectionFailure(
            ciphertextTampered,
            protector);

        byte[] tagTampered =
            (byte[])frame.Clone();

        tagTampered[^1] ^=
            0x01;

        AssertProtectionFailure(
            tagTampered,
            protector);

        byte[] wrongKey =
            (byte[])Key.Clone();

        wrongKey[0] ^=
            0xFF;

        using AesGcmPayloadProtector wrongProtector =
            new(
                wrongKey);

        AssertProtectionFailure(
            frame,
            wrongProtector);

        //
        // Control: original packet remains valid after all failures.
        //

        PingPacket restored =
            PrimaryProtocol.Deserialize<PingPacket>(
                frame,
                protector);

        AssertPacket(
            restored);
    }

    /// <summary>
    /// Creates a sample ping packet for protected framing tests.
    /// </summary>
    /// <returns>A populated <see cref="PingPacket"/> instance.</returns>
    private static PingPacket CreatePacket()
    {
        return new PingPacket
        {
            RequestId =
                0x11223344,

            Timestamp =
                0x0102030405060708,

            RequiresReply =
                true
        };
    }

    /// <summary>
    /// Asserts that a restored packet contains matching field values.
    /// </summary>
    /// <param name="packet">The packet instance to validate.</param>
    private static void AssertPacket(
        PingPacket packet)
    {
        Assert.Equal(
            0x11223344,
            packet.RequestId);

        Assert.Equal(
            0x0102030405060708,
            packet.Timestamp);

        Assert.True(
            packet.RequiresReply);
    }

    /// <summary>
    /// Asserts that attempting to deserialize the specified frame throws <see cref="PayloadProtectionException"/>.
    /// </summary>
    /// <param name="frame">The frame bytes to deserialize.</param>
    /// <param name="protector">The payload protector to use.</param>
    private static void AssertProtectionFailure(
        byte[] frame,
        AesGcmPayloadProtector protector)
    {
        Assert.Throws<PayloadProtectionException>(
            () =>
                PrimaryProtocol.Deserialize<PingPacket>(
                    frame,
                    protector));
    }
}