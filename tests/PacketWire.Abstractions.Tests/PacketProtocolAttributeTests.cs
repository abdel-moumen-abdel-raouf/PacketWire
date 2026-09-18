using PacketWire;

namespace PacketWire.Abstractions.Tests;

/// <summary>
/// Verifies initialization and argument validation for <see cref="PacketProtocolAttribute"/>.
/// </summary>
public sealed class PacketProtocolAttributeTests
{
    /// <summary>
    /// Verifies that valid protocol configuration parameters are correctly stored with default one-byte category size.
    /// </summary>
    [Fact]
    public void ConstructorStoresValidProtocolConfiguration()
    {
        PacketProtocolAttribute attribute = new(
            PacketIntegerSize.TwoBytes,
            PacketIntegerSize.FourBytes,
            PacketIntegerSize.TwoBytes,
            PacketByteOrder.LittleEndian);

        Assert.Equal(
            PacketIntegerSize.TwoBytes,
            attribute.PacketIdSize);

        Assert.Equal(
            PacketIntegerSize.FourBytes,
            attribute.PacketLengthSize);

        Assert.Equal(
            PacketIntegerSize.TwoBytes,
            attribute.CollectionCountSize);

        Assert.Equal(
            PacketIntegerSize.OneByte,
            attribute.PacketCategorySize);

        Assert.Equal(
            PacketByteOrder.LittleEndian,
            attribute.ByteOrder);
    }

    /// <summary>
    /// Verifies that an explicitly specified packet category size is stored correctly.
    /// </summary>
    [Fact]
    public void ConstructorStoresExplicitPacketCategorySize()
    {
        PacketProtocolAttribute attribute = new(
            PacketIntegerSize.TwoBytes,
            PacketIntegerSize.FourBytes,
            PacketIntegerSize.TwoBytes,
            PacketByteOrder.BigEndian,
            PacketIntegerSize.FourBytes);

        Assert.Equal(
            PacketIntegerSize.FourBytes,
            attribute.PacketCategorySize);
    }

    /// <summary>
    /// Verifies that passing an unspecified packet ID size throws <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void ConstructorRejectsUnspecifiedPacketIdSize()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PacketProtocolAttribute(
                PacketIntegerSize.Unspecified,
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketByteOrder.LittleEndian));
    }

    /// <summary>
    /// Verifies that passing an unspecified packet length size throws <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void ConstructorRejectsUnspecifiedPacketLengthSize()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PacketProtocolAttribute(
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.Unspecified,
                PacketIntegerSize.TwoBytes,
                PacketByteOrder.LittleEndian));
    }

    /// <summary>
    /// Verifies that passing an unspecified collection count size throws <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void ConstructorRejectsUnspecifiedCollectionCountSize()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PacketProtocolAttribute(
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.Unspecified,
                PacketByteOrder.LittleEndian));
    }

    /// <summary>
    /// Verifies that passing an unspecified category size throws <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void ConstructorRejectsUnspecifiedCategorySize()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PacketProtocolAttribute(
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketByteOrder.LittleEndian,
                PacketIntegerSize.Unspecified));
    }

    /// <summary>
    /// Verifies that passing an unspecified byte order throws <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void ConstructorRejectsUnspecifiedByteOrder()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PacketProtocolAttribute(
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketByteOrder.Unspecified));
    }

    /// <summary>
    /// Verifies that passing an undefined numeric enum value for integer size throws <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void ConstructorRejectsUndefinedPacketIntegerSize()
    {
        PacketIntegerSize undefinedSize =
            (PacketIntegerSize)3;

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PacketProtocolAttribute(
                undefinedSize,
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketByteOrder.LittleEndian));
    }

    /// <summary>
    /// Verifies that passing an undefined numeric enum value for byte order throws <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void ConstructorRejectsUndefinedByteOrder()
    {
        PacketByteOrder undefinedByteOrder =
            (PacketByteOrder)99;

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PacketProtocolAttribute(
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                undefinedByteOrder));
    }
}
