using PacketWire;
using AlphaProtocol = PacketWire.Consumer.Alpha.ApplicationProtocol;
using AlphaPacket = PacketWire.Consumer.Alpha.MessagePacket;
using BetaProtocol = PacketWire.Consumer.Beta.ApplicationProtocol;
using BetaPacket = PacketWire.Consumer.Beta.MessagePacket;

namespace PacketWire.Consumer.Tests;

/// <summary>
/// Verifies isolation between multiple independent protocol definitions sharing identical numeric identities but distinct framing, integer widths, and endianness.
/// </summary>
public sealed class MultiProtocolConsumerIsolationTests
{
    /// <summary>
    /// Expected wire bytes for the Alpha protocol message packet frame.
    /// </summary>
    private static readonly byte[] ExpectedAlphaFrame =
    [
        0x0A,
        0x00,

        0x00,

        0x07,

        0x22,
        0x22,

        0x44,
        0x33,
        0x22,
        0x11
    ];

    /// <summary>
    /// Expected wire bytes for the Beta protocol message packet frame.
    /// </summary>
    private static readonly byte[] ExpectedBetaFrame =
    [
        0x00,
        0x00,
        0x00,
        0x0F,

        0x00,

        0x00,
        0x07,

        0x00,
        0x00,
        0x22,
        0x22,

        0x11,
        0x22,
        0x33,
        0x44
    ];

    /// <summary>
    /// Verifies that multiple protocols maintain completely independent type-to-identity mappings and distinct protocol definitions even when sharing numeric IDs.
    /// </summary>
    [Fact]
    public void SameCompositeIdentityRemainsIsolatedPerConsumerProtocol()
    {
        PacketIdentity alphaIdentity =
            AlphaProtocol.GetIdentity<AlphaPacket>();

        PacketIdentity betaIdentity =
            BetaProtocol.GetIdentity<BetaPacket>();

        Assert.Equal(
            new PacketIdentity(
                7,
                0x2222),
            alphaIdentity);

        Assert.Equal(
            new PacketIdentity(
                7,
                0x2222),
            betaIdentity);

        Assert.False(
            AlphaProtocol.TryGetIdentity<BetaPacket>(
                out PacketIdentity alphaCrossIdentity));

        Assert.Equal(
            default,
            alphaCrossIdentity);

        Assert.False(
            BetaProtocol.TryGetIdentity<AlphaPacket>(
                out PacketIdentity betaCrossIdentity));

        Assert.Equal(
            default,
            betaCrossIdentity);

        PacketProtocolDefinition alphaDefinition =
            AlphaProtocol.Definition;

        PacketProtocolDefinition betaDefinition =
            BetaProtocol.Definition;

        Assert.Equal(
            6,
            alphaDefinition.HeaderLength);

        Assert.Equal(
            PacketByteOrder.LittleEndian,
            alphaDefinition.ByteOrder);

        Assert.Equal(
            PacketIntegerSize.TwoBytes,
            alphaDefinition.PacketLengthSize);

        Assert.Equal(
            PacketIntegerSize.OneByte,
            alphaDefinition.PacketCategorySize);

        Assert.Equal(
            PacketIntegerSize.TwoBytes,
            alphaDefinition.PacketIdSize);

        Assert.Equal(
            11,
            betaDefinition.HeaderLength);

        Assert.Equal(
            PacketByteOrder.BigEndian,
            betaDefinition.ByteOrder);

        Assert.Equal(
            PacketIntegerSize.FourBytes,
            betaDefinition.PacketLengthSize);

        Assert.Equal(
            PacketIntegerSize.TwoBytes,
            betaDefinition.PacketCategorySize);

        Assert.Equal(
            PacketIntegerSize.FourBytes,
            betaDefinition.PacketIdSize);
    }

    /// <summary>
    /// Verifies that each protocol serializes and deserializes its registered packets using its own configuration and rejects cross-protocol frames.
    /// </summary>
    [Fact]
    public void ConsumerProtocolsSerializeAndDeserializeUsingIndependentWireDefinitions()
    {
        AlphaPacket alphaInput =
            new()
            {
                Value =
                    0x11223344
            };

        BetaPacket betaInput =
            new()
            {
                Value =
                    0x11223344
            };

        byte[] alphaFrame =
            AlphaProtocol.Serialize(
                alphaInput);

        byte[] betaFrame =
            BetaProtocol.Serialize(
                betaInput);

        Assert.Equal(
            ExpectedAlphaFrame,
            alphaFrame);

        Assert.Equal(
            ExpectedBetaFrame,
            betaFrame);

        PacketFrameView alphaParsed =
            PacketFrameCodec.ReadFrame(
                alphaFrame,
                AlphaProtocol.Definition);

        Assert.Equal(
            10UL,
            alphaParsed.Header.PacketLength);

        Assert.Equal(
            7UL,
            alphaParsed.Header.PacketCategory);

        Assert.Equal(
            0x2222UL,
            alphaParsed.Header.PacketId);

        Assert.Equal(
            4,
            alphaParsed.PayloadLength);

        PacketFrameView betaParsed =
            PacketFrameCodec.ReadFrame(
                betaFrame,
                BetaProtocol.Definition);

        Assert.Equal(
            15UL,
            betaParsed.Header.PacketLength);

        Assert.Equal(
            7UL,
            betaParsed.Header.PacketCategory);

        Assert.Equal(
            0x2222UL,
            betaParsed.Header.PacketId);

        Assert.Equal(
            4,
            betaParsed.PayloadLength);

        AlphaPacket alphaRestored =
            AlphaProtocol.Deserialize<AlphaPacket>(
                alphaFrame);

        BetaPacket betaRestored =
            BetaProtocol.Deserialize<BetaPacket>(
                betaFrame);

        Assert.Equal(
            0x11223344,
            alphaRestored.Value);

        Assert.Equal(
            0x11223344,
            betaRestored.Value);

        Assert.Throws<PacketBufferException>(
            () =>
                BetaProtocol.Deserialize<BetaPacket>(
                    alphaFrame));

        Assert.Throws<PacketBufferException>(
            () =>
                AlphaProtocol.Deserialize<AlphaPacket>(
                    betaFrame));
    }
}