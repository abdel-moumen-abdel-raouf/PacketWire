using PacketWire;

namespace PacketWire.Runtime.Tests;

/// <summary>
/// Verifies primitive and string deserialization, buffer boundary enforcement, and validation rules in <see cref="PacketReader"/>.
/// </summary>
public sealed class PacketReaderTests
{
    /// <summary>
    /// Verifies that reading an unsigned 16-bit integer decodes in little-endian byte order.
    /// </summary>
    [Fact]
    public void ReadUInt16UsesLittleEndianOrder()
    {
        byte[] buffer = { 0x34, 0x12 };

        PacketReader reader = new(
            buffer,
            PacketByteOrder.LittleEndian);

        ushort value = reader.ReadUInt16();

        Assert.Equal((ushort)0x1234, value);
        Assert.Equal(2, reader.ConsumedCount);
        Assert.Equal(0, reader.Remaining);
    }

    /// <summary>
    /// Verifies that reading an unsigned 16-bit integer decodes in big-endian byte order.
    /// </summary>
    [Fact]
    public void ReadUInt16UsesBigEndianOrder()
    {
        byte[] buffer = { 0x12, 0x34 };

        PacketReader reader = new(
            buffer,
            PacketByteOrder.BigEndian);

        ushort value = reader.ReadUInt16();

        Assert.Equal((ushort)0x1234, value);
    }

    /// <summary>
    /// Verifies that reading a 32-bit integer decodes in little-endian byte order.
    /// </summary>
    [Fact]
    public void ReadInt32UsesLittleEndianOrder()
    {
        byte[] buffer = { 0x78, 0x56, 0x34, 0x12 };

        PacketReader reader = new(
            buffer,
            PacketByteOrder.LittleEndian);

        int value = reader.ReadInt32();

        Assert.Equal(0x12345678, value);
    }

    /// <summary>
    /// Verifies that canonical 0 and 1 byte values correctly decode as boolean false and true.
    /// </summary>
    /// <param name="wireValue">The byte value on the wire.</param>
    /// <param name="expected">The expected boolean value.</param>
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void ReadBooleanAcceptsCanonicalValues(
        byte wireValue,
        bool expected)
    {
        byte[] buffer = { wireValue };

        PacketReader reader = new(
            buffer,
            PacketByteOrder.LittleEndian);

        Assert.Equal(expected, reader.ReadBoolean());
    }

    /// <summary>
    /// Verifies that reading a non-canonical boolean byte value (other than 0 or 1) throws <see cref="PacketBufferException"/>.
    /// </summary>
    [Fact]
    public void ReadBooleanRejectsNonCanonicalValue()
    {
        Assert.Throws<PacketBufferException>(
            ReadInvalidBoolean);
    }

    /// <summary>
    /// Verifies that fixed string decoding trims trailing zero padding bytes.
    /// </summary>
    [Fact]
    public void ReadFixedStringRemovesZeroPadding()
    {
        byte[] buffer =
        {
            0x41,
            0x6C,
            0x69,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00
        };

        PacketReader reader = new(
            buffer,
            PacketByteOrder.LittleEndian);

        string value = reader.ReadFixedString(8);

        Assert.Equal("Ali", value);
        Assert.Equal(8, reader.ConsumedCount);
    }

    /// <summary>
    /// Verifies that multibyte UTF-8 characters decode properly in fixed string fields.
    /// </summary>
    [Fact]
    public void ReadFixedStringSupportsMultibyteUtf8Characters()
    {
        byte[] buffer =
        {
            0xD8,
            0xB9,
            0x00,
            0x00
        };

        PacketReader reader = new(
            buffer,
            PacketByteOrder.LittleEndian);

        string value = reader.ReadFixedString(4);

        Assert.Equal("ع", value);
    }

    /// <summary>
    /// Verifies that non-zero bytes occurring after padding has started trigger <see cref="PacketBufferException"/>.
    /// </summary>
    [Fact]
    public void ReadFixedStringRejectsNonZeroDataAfterPaddingStarts()
    {
        Assert.Throws<PacketBufferException>(
            ReadFixedStringWithInvalidPadding);
    }

    /// <summary>
    /// Verifies that malformed UTF-8 sequences trigger <see cref="PacketBufferException"/>.
    /// </summary>
    [Fact]
    public void ReadFixedStringRejectsInvalidUtf8()
    {
        Assert.Throws<PacketBufferException>(
            ReadFixedStringWithInvalidUtf8);
    }

    /// <summary>
    /// Verifies that attempting to read past the end of the available buffer throws <see cref="PacketBufferException"/>.
    /// </summary>
    [Fact]
    public void ReadThrowsWhenBufferDoesNotContainEnoughData()
    {
        Assert.Throws<PacketBufferException>(
            ReadUInt16FromInsufficientBuffer);
    }

    /// <summary>
    /// Verifies that constructing a reader with unspecified byte order throws <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void ConstructorRejectsUnspecifiedByteOrder()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            ConstructReaderWithUnspecifiedByteOrder);
    }

    /// <summary>
    /// Helper decoding a non-canonical boolean value.
    /// </summary>
    private static void ReadInvalidBoolean()
    {
        byte[] buffer = { 2 };

        PacketReader reader = new(
            buffer,
            PacketByteOrder.LittleEndian);

        _ = reader.ReadBoolean();
    }

    /// <summary>
    /// Helper decoding a fixed string with non-zero bytes after zero padding.
    /// </summary>
    private static void ReadFixedStringWithInvalidPadding()
    {
        byte[] buffer =
        {
            0x41,
            0x00,
            0x42,
            0x00
        };

        PacketReader reader = new(
            buffer,
            PacketByteOrder.LittleEndian);

        _ = reader.ReadFixedString(4);
    }

    /// <summary>
    /// Helper decoding a fixed string with invalid UTF-8 bytes.
    /// </summary>
    private static void ReadFixedStringWithInvalidUtf8()
    {
        byte[] buffer =
        {
            0xC3,
            0x28
        };

        PacketReader reader = new(
            buffer,
            PacketByteOrder.LittleEndian);

        _ = reader.ReadFixedString(2);
    }

    /// <summary>
    /// Helper attempting to read a two-byte integer from a one-byte buffer.
    /// </summary>
    private static void ReadUInt16FromInsufficientBuffer()
    {
        byte[] buffer = { 0x01 };

        PacketReader reader = new(
            buffer,
            PacketByteOrder.LittleEndian);

        _ = reader.ReadUInt16();
    }

    /// <summary>
    /// Helper constructing a reader with unspecified byte order.
    /// </summary>
    private static void ConstructReaderWithUnspecifiedByteOrder()
    {
        byte[] buffer = { 0x00 };

        PacketReader reader = new(
            buffer,
            PacketByteOrder.Unspecified);

        _ = reader;
    }
}
