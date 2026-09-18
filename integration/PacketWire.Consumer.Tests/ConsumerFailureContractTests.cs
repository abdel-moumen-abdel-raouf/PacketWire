using PacketWire;
using PacketWire.Consumer;
using PacketWire.Security;
using AlphaPacket = PacketWire.Consumer.Alpha.MessagePacket;

namespace PacketWire.Consumer.Tests;

/// <summary>
/// Verifies that serialization and deserialization APIs enforce registration boundaries, type mismatches, protection checks, and frame truncation errors.
/// </summary>
public sealed class ConsumerFailureContractTests
{
    /// <summary>
    /// Sample 256-bit cryptographic key used for payload protector tests.
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
    /// Verifies that attempting to serialize an unregistered type throws <see cref="PacketTypeNotRegisteredException"/>.
    /// </summary>
    [Fact]
    public void SerializeUnknownConsumerTypeThrowsPacketTypeNotRegisteredException()
    {
        PacketTypeNotRegisteredException exception =
            Assert.Throws<PacketTypeNotRegisteredException>(
                static () =>
                {
                    _ =
                        PrimaryProtocol.Serialize(
                            "not-a-packet");
                });

        Assert.Equal(
            typeof(string),
            exception.PacketType);
    }

    /// <summary>
    /// Verifies that attempting to deserialize a frame with an unrecognized identity throws <see cref="PacketIdentityNotRegisteredException"/>.
    /// </summary>
    [Fact]
    public void DeserializeUnknownConsumerIdentityThrowsPacketIdentityNotRegisteredException()
    {
        byte[] frame =
        [
            0x06,
            0x00,

            0x00,

            0x09,

            0x99,
            0x99
        ];

        PacketIdentityNotRegisteredException exception =
            Assert.Throws<PacketIdentityNotRegisteredException>(
                () =>
                {
                    _ =
                        PrimaryProtocol.Deserialize(
                            frame);
                });

        Assert.Equal(
            new PacketIdentity(
                9,
                0x9999),
            exception.Identity);
    }

    /// <summary>
    /// Verifies that generic deserialization expecting a specific packet type throws <see cref="PacketTypeMismatchException"/> when receiving a different registered type.
    /// </summary>
    [Fact]
    public void TypedDeserializeRejectsDifferentConsumerDtoType()
    {
        byte[] frame =
            PrimaryProtocol.Serialize(
                CreatePingPacket());

        PacketTypeMismatchException exception =
            Assert.Throws<PacketTypeMismatchException>(
                () =>
                {
                    _ =
                        PrimaryProtocol.Deserialize<AlphaPacket>(
                            frame);
                });

        Assert.Equal(
            typeof(AlphaPacket),
            exception.ExpectedType);

        Assert.Equal(
            typeof(PingPacket),
            exception.ActualType);
    }

    /// <summary>
    /// Verifies that plain deserialization overloads reject protected frames by throwing <see cref="PacketProtectionRequiredException"/>.
    /// </summary>
    [Fact]
    public void PlainDeserializeRejectsProtectedConsumerFrame()
    {
        using AesGcmPayloadProtector protector =
            new(
                Key);

        byte[] protectedFrame =
            PrimaryProtocol.Serialize(
                CreatePingPacket(),
                protector);

        PacketProtectionRequiredException exception =
            Assert.Throws<PacketProtectionRequiredException>(
                () =>
                {
                    _ =
                        PrimaryProtocol.Deserialize<PingPacket>(
                            protectedFrame);
                });

        Assert.Equal(
            PacketFrameOptions.Protected,
            exception.Options);
    }

    /// <summary>
    /// Verifies that attempting to deserialize a frame whose byte count is smaller than the declared header length throws <see cref="PacketBufferException"/>.
    /// </summary>
    [Fact]
    public void TruncatedConsumerFrameThrowsPacketBufferException()
    {
        byte[] completeFrame =
            PrimaryProtocol.Serialize(
                CreatePingPacket());

        byte[] truncatedFrame =
            completeFrame[..^1];

        Assert.Equal(
            19,
            completeFrame.Length);

        Assert.Equal(
            18,
            truncatedFrame.Length);

        _ =
            Assert.Throws<PacketBufferException>(
                () =>
                {
                    _ =
                        PrimaryProtocol.Deserialize<PingPacket>(
                            truncatedFrame);
                });
    }

    /// <summary>
    /// Creates a test ping packet initialized with deterministic values.
    /// </summary>
    /// <returns>A populated <see cref="PingPacket"/> instance.</returns>
    private static PingPacket CreatePingPacket()
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
}