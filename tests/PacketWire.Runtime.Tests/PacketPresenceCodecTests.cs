using PacketWire;

namespace PacketWire.Runtime.Tests;

/// <summary>
/// Verifies encoding and decoding of nullable presence marker flags in <see cref="PacketPresenceCodec"/>.
/// </summary>
public sealed class PacketPresenceCodecTests
{
    /// <summary>
    /// Verifies that serializing false produces a zero byte.
    /// </summary>
    [Fact]
    public void WriteFalseProducesZero()
    {
        byte[] buffer = new byte[1];

        PacketWriter writer = new(
            buffer,
            PacketByteOrder.LittleEndian);

        PacketPresenceCodec.Write(
            ref writer,
            false);

        Assert.Equal(
            (byte)0,
            buffer[0]);

        Assert.Equal(
            1,
            writer.WrittenCount);
    }

    /// <summary>
    /// Verifies that serializing true produces a one byte.
    /// </summary>
    [Fact]
    public void WriteTrueProducesOne()
    {
        byte[] buffer = new byte[1];

        PacketWriter writer = new(
            buffer,
            PacketByteOrder.LittleEndian);

        PacketPresenceCodec.Write(
            ref writer,
            true);

        Assert.Equal(
            (byte)1,
            buffer[0]);

        Assert.Equal(
            1,
            writer.WrittenCount);
    }

    /// <summary>
    /// Verifies that reading a zero byte returns false.
    /// </summary>
    [Fact]
    public void ReadZeroReturnsFalse()
    {
        byte[] buffer = { 0 };

        PacketReader reader = new(
            buffer,
            PacketByteOrder.LittleEndian);

        bool value =
            PacketPresenceCodec.Read(
                ref reader);

        Assert.False(value);
        Assert.Equal(1, reader.ConsumedCount);
    }

    /// <summary>
    /// Verifies that reading a one byte returns true.
    /// </summary>
    [Fact]
    public void ReadOneReturnsTrue()
    {
        byte[] buffer = { 1 };

        PacketReader reader = new(
            buffer,
            PacketByteOrder.LittleEndian);

        bool value =
            PacketPresenceCodec.Read(
                ref reader);

        Assert.True(value);
        Assert.Equal(1, reader.ConsumedCount);
    }

    /// <summary>
    /// Verifies that reading a byte value other than 0 or 1 throws <see cref="PacketBufferException"/>.
    /// </summary>
    [Fact]
    public void ReadRejectsNonCanonicalValue()
    {
        Assert.Throws<PacketBufferException>(
            ReadInvalidPresenceValue);
    }

    /// <summary>
    /// Helper attempting to decode an invalid presence byte value (2).
    /// </summary>
    private static void ReadInvalidPresenceValue()
    {
        byte[] buffer = { 2 };

        PacketReader reader = new(
            buffer,
            PacketByteOrder.LittleEndian);

        _ = PacketPresenceCodec.Read(
            ref reader);
    }
}
