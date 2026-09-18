using PacketWire;

namespace PacketWire.Runtime.Tests;

/// <summary>
/// Verifies constructor initialization, header length calculations, capacity limits, and validation rules in <see cref="PacketProtocolDefinition"/>.
/// </summary>
public sealed class PacketProtocolDefinitionTests
{
    /// <summary>
    /// Verifies that constructor parameters are stored and derived properties (such as header length and maximum capacities) are calculated correctly.
    /// </summary>
    [Fact]
    public void ConstructorStoresProtocolConfiguration()
    {
        PacketProtocolDefinition definition = new(
            PacketIntegerSize.TwoBytes,
            PacketIntegerSize.FourBytes,
            PacketIntegerSize.TwoBytes,
            PacketByteOrder.LittleEndian);

        Assert.Equal(
            PacketIntegerSize.TwoBytes,
            definition.PacketIdSize);

        Assert.Equal(
            PacketIntegerSize.FourBytes,
            definition.PacketLengthSize);

        Assert.Equal(
            PacketIntegerSize.TwoBytes,
            definition.CollectionCountSize);

        Assert.Equal(
            PacketIntegerSize.OneByte,
            definition.PacketCategorySize);

        Assert.Equal(
            PacketByteOrder.LittleEndian,
            definition.ByteOrder);

        Assert.Equal(
            8,
            definition.HeaderLength);

        Assert.Equal(
            ushort.MaxValue,
            definition.MaximumPacketId);

        Assert.Equal(
            byte.MaxValue,
            definition.MaximumPacketCategory);

        Assert.Equal(
            uint.MaxValue,
            definition.MaximumPacketLength);

        Assert.Equal(
            ushort.MaxValue,
            definition.MaximumCollectionCount);
    }

    /// <summary>
    /// Verifies that configuring an explicit packet category size adjusts the total header length and maximum category capacity.
    /// </summary>
    [Fact]
    public void ExplicitCategorySizeChangesHeaderAndCapacity()
    {
        PacketProtocolDefinition definition = new(
            PacketIntegerSize.TwoBytes,
            PacketIntegerSize.TwoBytes,
            PacketIntegerSize.TwoBytes,
            PacketByteOrder.LittleEndian,
            PacketIntegerSize.FourBytes);

        Assert.Equal(
            9,
            definition.HeaderLength);

        Assert.Equal(
            uint.MaxValue,
            definition.MaximumPacketCategory);
    }

    /// <summary>
    /// Verifies that two-byte packet length and ID sizes produce a 6-byte header length.
    /// </summary>
    [Fact]
    public void DefaultTwoBytePacketLengthAndIdProduceSixByteHeader()
    {
        PacketProtocolDefinition definition = new(
            PacketIntegerSize.TwoBytes,
            PacketIntegerSize.TwoBytes,
            PacketIntegerSize.TwoBytes,
            PacketByteOrder.LittleEndian);

        Assert.Equal(
            6,
            definition.HeaderLength);
    }

    /// <summary>
    /// Verifies that passing unspecified packet ID size throws <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void ConstructorRejectsUnspecifiedPacketIdSize()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PacketProtocolDefinition(
                PacketIntegerSize.Unspecified,
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketByteOrder.LittleEndian));
    }

    /// <summary>
    /// Verifies that passing unspecified packet length size throws <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void ConstructorRejectsUnspecifiedPacketLengthSize()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PacketProtocolDefinition(
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.Unspecified,
                PacketIntegerSize.TwoBytes,
                PacketByteOrder.LittleEndian));
    }

    /// <summary>
    /// Verifies that passing unspecified collection count size throws <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void ConstructorRejectsUnspecifiedCollectionCountSize()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PacketProtocolDefinition(
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.Unspecified,
                PacketByteOrder.LittleEndian));
    }

    /// <summary>
    /// Verifies that passing unspecified packet category size throws <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void ConstructorRejectsUnspecifiedPacketCategorySize()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PacketProtocolDefinition(
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketByteOrder.LittleEndian,
                PacketIntegerSize.Unspecified));
    }

    /// <summary>
    /// Verifies that passing unspecified byte order throws <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void ConstructorRejectsUnspecifiedByteOrder()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PacketProtocolDefinition(
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketByteOrder.Unspecified));
    }
}
