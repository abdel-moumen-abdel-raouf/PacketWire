using PacketWire.Consumer;

namespace PacketWire.Consumer.Tests;

/// <summary>
/// Verifies boundary accessibility, identity resolution, and metadata exposure of generated protocol facades from a consumer test assembly.
/// </summary>
public sealed class ConsumerBoundaryTests
{
    /// <summary>
    /// Verifies that the public protocol definition and identity mapping APIs are accessible across assembly boundaries and expose accurate framing metadata.
    /// </summary>
    [Fact]
    public void GeneratedPublicFacadeIsAvailableFromConsumerAssembly()
    {
        PacketProtocolDefinition definition =
            PrimaryProtocol.Definition;

        PacketIdentity identity =
            PrimaryProtocol.GetIdentity<PingPacket>();

        Assert.Equal(
            6,
            definition.HeaderLength);

        Assert.Equal(
            PacketIntegerSize.TwoBytes,
            definition.PacketLengthSize);

        Assert.Equal(
            PacketIntegerSize.OneByte,
            definition.PacketCategorySize);

        Assert.Equal(
            PacketIntegerSize.TwoBytes,
            definition.PacketIdSize);

        Assert.Equal(
            PacketIntegerSize.TwoBytes,
            definition.CollectionCountSize);

        Assert.Equal(
            PacketByteOrder.LittleEndian,
            definition.ByteOrder);

        Assert.Equal(
            1UL,
            identity.Category);

        Assert.Equal(
            0x1001UL,
            identity.Id);

        Assert.Same(
            typeof(PrimaryProtocol).Assembly,
            typeof(PingPacket).Assembly);
    }
}