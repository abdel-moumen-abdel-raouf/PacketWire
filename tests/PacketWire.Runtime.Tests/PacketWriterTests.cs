using PacketWire;

namespace PacketWire.Runtime.Tests;

/// <summary>
/// Verifies primitive and string serialization, buffer boundary checks, and endianness handling in <see cref="PacketWriter"/>.
/// </summary>
public sealed class PacketWriterTests
{
    /// <summary>
    /// Verifies that writing an unsigned 16-bit integer encodes in little-endian byte order.
    /// </summary>
    [Fact]
    public void WriteUInt16UsesLittleEndianOrder()
    {
        byte[] buffer = new byte[2];

        PacketWriter writer = new(
            buffer,
            PacketByteOrder.LittleEndian);

        writer.WriteUInt16(0x1234);

        Assert.Equal(
            new byte[] { 0x34, 0x12 },
            buffer);

        Assert.Equal(2, writer.WrittenCount);
        Assert.Equal(0, writer.Remaining);
    }

    /// <summary>
    /// Verifies that writing an unsigned 16-bit integer encodes in big-endian byte order.
    /// </summary>
    [Fact]
    public void WriteUInt16UsesBigEndianOrder()
    {
        byte[] buffer = new byte[2];

        PacketWriter writer = new(
            buffer,
            PacketByteOrder.BigEndian);

        writer.WriteUInt16(0x1234);

        Assert.Equal(
            new byte[] { 0x12, 0x34 },
            buffer);
    }

    /// <summary>
    /// Verifies that writing a 32-bit integer encodes in little-endian byte order.
    /// </summary>
    [Fact]
    public void WriteInt32UsesLittleEndianOrder()
    {
        byte[] buffer = new byte[4];

        PacketWriter writer = new(
            buffer,
            PacketByteOrder.LittleEndian);

        writer.WriteInt32(0x12345678);

        Assert.Equal(
            new byte[] { 0x78, 0x56, 0x34, 0x12 },
            buffer);
    }

    /// <summary>
    /// Verifies that boolean values are encoded as canonical 0 or 1 single bytes.
    /// </summary>
    [Fact]
    public void WriteBooleanUsesSingleCanonicalByte()
    {
        byte[] buffer = new byte[2];

        PacketWriter writer = new(
            buffer,
            PacketByteOrder.LittleEndian);

        writer.WriteBoolean(false);
        writer.WriteBoolean(true);

        Assert.Equal(
            new byte[] { 0x00, 0x01 },
            buffer);
    }

    /// <summary>
    /// Verifies that fixed strings are written in UTF-8 format and zero-padded to the specified byte length.
    /// </summary>
    [Fact]
    public void WriteFixedStringWritesUtf8AndZeroPadding()
    {
        byte[] buffer = new byte[8];

        PacketWriter writer = new(
            buffer,
            PacketByteOrder.LittleEndian);

        writer.WriteFixedString("Ali", 8);

        Assert.Equal(
            new byte[]
            {
                0x41,
                0x6C,
                0x69,
                0x00,
                0x00,
                0x00,
                0x00,
                0x00
            },
            buffer);
    }

    /// <summary>
    /// Verifies that multibyte UTF-8 characters are encoded accurately within fixed string fields.
    /// </summary>
    [Fact]
    public void WriteFixedStringSupportsMultibyteUtf8Characters()
    {
        byte[] buffer = new byte[8];

        PacketWriter writer = new(
            buffer,
            PacketByteOrder.LittleEndian);

        writer.WriteFixedString("ع", 8);

        Assert.Equal(8, writer.WrittenCount);

        Assert.Equal(0xD8, buffer[0]);
        Assert.Equal(0xB9, buffer[1]);

        for (int index = 2; index < buffer.Length; index++)
        {
            Assert.Equal(0, buffer[index]);
        }
    }

    /// <summary>
    /// Verifies that strings whose UTF-8 byte representation exceeds the target field length are rejected.
    /// </summary>
    [Fact]
    public void WriteFixedStringRejectsEncodedValueThatExceedsField()
    {
        Assert.Throws<ArgumentException>(
            WriteFixedStringThatExceedsField);
    }

    /// <summary>
    /// Verifies that strings containing embedded null characters are rejected.
    /// </summary>
    [Fact]
    public void WriteFixedStringRejectsNullCharacter()
    {
        Assert.Throws<ArgumentException>(
            WriteFixedStringContainingNullCharacter);
    }

    /// <summary>
    /// Verifies that attempting to write more bytes than remain in the buffer throws <see cref="PacketBufferException"/>.
    /// </summary>
    [Fact]
    public void WriteThrowsWhenBufferIsTooSmall()
    {
        Assert.Throws<PacketBufferException>(
            WriteUInt16ToInsufficientBuffer);
    }

    /// <summary>
    /// Verifies that constructing a writer with unspecified byte order throws <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void ConstructorRejectsUnspecifiedByteOrder()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            ConstructWriterWithUnspecifiedByteOrder);
    }

    /// <summary>
    /// Helper writing a string that overflows the fixed field length.
    /// </summary>
    private static void WriteFixedStringThatExceedsField()
    {
        byte[] buffer = new byte[2];

        PacketWriter writer = new(
            buffer,
            PacketByteOrder.LittleEndian);

        writer.WriteFixedString("Ali", 2);
    }

    /// <summary>
    /// Helper writing a string containing an embedded null character.
    /// </summary>
    private static void WriteFixedStringContainingNullCharacter()
    {
        byte[] buffer = new byte[8];

        PacketWriter writer = new(
            buffer,
            PacketByteOrder.LittleEndian);

        writer.WriteFixedString("A\0B", 8);
    }

    /// <summary>
    /// Helper writing a two-byte integer into a one-byte buffer.
    /// </summary>
    private static void WriteUInt16ToInsufficientBuffer()
    {
        byte[] buffer = new byte[1];

        PacketWriter writer = new(
            buffer,
            PacketByteOrder.LittleEndian);

        writer.WriteUInt16(1);
    }

    /// <summary>
    /// Helper constructing a writer with unspecified byte order.
    /// </summary>
    private static void ConstructWriterWithUnspecifiedByteOrder()
    {
        byte[] buffer = new byte[1];

        PacketWriter writer = new(
            buffer,
            PacketByteOrder.Unspecified);

        _ = writer;
    }
}
