using PacketWire.Consumer;

namespace PacketWire.Consumer.Tests;

/// <summary>
/// Verifies end-to-end plain serialization, deserialization, wire layout, and round-tripping for consumer packet types.
/// </summary>
public sealed class PlainConsumerEndToEndTests
{
    /// <summary>
    /// The expected 19-byte wire frame layout for the ping packet.
    /// </summary>
    private static readonly byte[] ExpectedFrame =
    [
        0x13,
        0x00,

        0x00,

        0x01,

        0x01,
        0x10,

        0x44,
        0x33,
        0x22,
        0x11,

        0x08,
        0x07,
        0x06,
        0x05,
        0x04,
        0x03,
        0x02,
        0x01,

        0x01
    ];

    /// <summary>
    /// Verifies that serializing a consumer packet object produces the exact expected raw wire frame.
    /// </summary>
    [Fact]
    public void PublicFacadeSerializesConsumerDtoToExactWireFrame()
    {
        PingPacket packet =
            new()
            {
                RequestId =
                    0x11223344,

                Timestamp =
                    0x0102030405060708,

                RequiresReply =
                    true
            };

        byte[] frame =
            PrimaryProtocol.Serialize(
                packet);

        Assert.Equal(
            19,
            frame.Length);

        Assert.Equal(
            ExpectedFrame,
            frame);

        PacketFrameView parsed =
            PacketFrameCodec.ReadFrame(
                frame,
                PrimaryProtocol.Definition);

        Assert.Equal(
            19UL,
            parsed.Header.PacketLength);

        Assert.Equal(
            PacketFrameOptions.None,
            parsed.Header.Flags);

        Assert.Equal(
            1UL,
            parsed.Header.PacketCategory);

        Assert.Equal(
            0x1001UL,
            parsed.Header.PacketId);

        Assert.Equal(
            13,
            parsed.PayloadLength);
    }

    /// <summary>
    /// Verifies that deserializing a wire frame produces matching packet instances using both typed and untyped overloads.
    /// </summary>
    [Fact]
    public void PublicFacadeDeserializesConsumerFrameTypedAndUntyped()
    {
        object untyped =
            PrimaryProtocol.Deserialize(
                ExpectedFrame);

        PingPacket untypedPacket =
            Assert.IsType<PingPacket>(
                untyped);

        AssertPacket(
            untypedPacket);

        PingPacket typed =
            PrimaryProtocol.Deserialize<PingPacket>(
                ExpectedFrame);

        AssertPacket(
            typed);

        byte[] roundTrip =
            PrimaryProtocol.Serialize(
                typed);

        Assert.Equal(
            ExpectedFrame,
            roundTrip);
    }

    /// <summary>
    /// Asserts that all fields of a deserialized ping packet match the baseline test values.
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
}