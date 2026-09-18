using PacketWire;

namespace PacketWire.Runtime.Tests;

/// <summary>
/// Verifies encoding, decoding, endianness handling, and capacity boundary validations of <see cref="PacketIntegerCodec"/>.
/// </summary>
public sealed class PacketIntegerCodecTests
{
    /// <summary>
    /// Verifies that <see cref="PacketIntegerCodec.GetByteCount"/> returns the expected byte count for each integer size.
    /// </summary>
    /// <param name="size">The integer size enum value.</param>
    /// <param name="expectedByteCount">The expected byte count.</param>
    [Theory]
    [InlineData(PacketIntegerSize.OneByte, 1)]
    [InlineData(PacketIntegerSize.TwoBytes, 2)]
    [InlineData(PacketIntegerSize.FourBytes, 4)]
    [InlineData(PacketIntegerSize.EightBytes, 8)]
    public void GetByteCountReturnsConfiguredWidth(
        PacketIntegerSize size,
        int expectedByteCount)
    {
        int actualByteCount =
            PacketIntegerCodec.GetByteCount(size);

        Assert.Equal(
            expectedByteCount,
            actualByteCount);
    }

    /// <summary>
    /// Verifies that <see cref="PacketIntegerCodec.GetMaximumValue"/> returns the maximum unsigned representation for each size.
    /// </summary>
    [Fact]
    public void GetMaximumValueReturnsExpectedValues()
    {
        Assert.Equal(
            byte.MaxValue,
            PacketIntegerCodec.GetMaximumValue(
                PacketIntegerSize.OneByte));

        Assert.Equal(
            ushort.MaxValue,
            PacketIntegerCodec.GetMaximumValue(
                PacketIntegerSize.TwoBytes));

        Assert.Equal(
            uint.MaxValue,
            PacketIntegerCodec.GetMaximumValue(
                PacketIntegerSize.FourBytes));

        Assert.Equal(
            ulong.MaxValue,
            PacketIntegerCodec.GetMaximumValue(
                PacketIntegerSize.EightBytes));
    }

    /// <summary>
    /// Verifies that calling <see cref="PacketIntegerCodec.GetByteCount"/> with <see cref="PacketIntegerSize.Unspecified"/> throws <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void GetByteCountRejectsUnspecifiedSize()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PacketIntegerCodec.GetByteCount(
                PacketIntegerSize.Unspecified));
    }

    /// <summary>
    /// Verifies that calling <see cref="PacketIntegerCodec.GetByteCount"/> with an undefined enum value throws <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void GetByteCountRejectsUndefinedSize()
    {
        PacketIntegerSize undefinedSize =
            (PacketIntegerSize)3;

        Assert.Throws<ArgumentOutOfRangeException>(
            () => PacketIntegerCodec.GetByteCount(
                undefinedSize));
    }

    /// <summary>
    /// Verifies that a two-byte integer is serialized in little-endian order.
    /// </summary>
    [Fact]
    public void WriteUsesLittleEndianOrderForTwoBytes()
    {
        byte[] buffer = new byte[2];

        PacketWriter writer = new(
            buffer,
            PacketByteOrder.LittleEndian);

        PacketIntegerCodec.Write(
            ref writer,
            0x1234,
            PacketIntegerSize.TwoBytes);

        Assert.Equal(
            new byte[]
            {
                0x34,
                0x12
            },
            buffer);
    }

    /// <summary>
    /// Verifies that a two-byte integer is serialized in big-endian order.
    /// </summary>
    [Fact]
    public void WriteUsesBigEndianOrderForTwoBytes()
    {
        byte[] buffer = new byte[2];

        PacketWriter writer = new(
            buffer,
            PacketByteOrder.BigEndian);

        PacketIntegerCodec.Write(
            ref writer,
            0x1234,
            PacketIntegerSize.TwoBytes);

        Assert.Equal(
            new byte[]
            {
                0x12,
                0x34
            },
            buffer);
    }

    /// <summary>
    /// Verifies that a four-byte integer is serialized in little-endian order.
    /// </summary>
    [Fact]
    public void WriteUsesLittleEndianOrderForFourBytes()
    {
        byte[] buffer = new byte[4];

        PacketWriter writer = new(
            buffer,
            PacketByteOrder.LittleEndian);

        PacketIntegerCodec.Write(
            ref writer,
            0x12345678,
            PacketIntegerSize.FourBytes);

        Assert.Equal(
            new byte[]
            {
                0x78,
                0x56,
                0x34,
                0x12
            },
            buffer);
    }

    /// <summary>
    /// Verifies that a four-byte integer is serialized in big-endian order.
    /// </summary>
    [Fact]
    public void WriteUsesBigEndianOrderForFourBytes()
    {
        byte[] buffer = new byte[4];

        PacketWriter writer = new(
            buffer,
            PacketByteOrder.BigEndian);

        PacketIntegerCodec.Write(
            ref writer,
            0x12345678,
            PacketIntegerSize.FourBytes);

        Assert.Equal(
            new byte[]
            {
                0x12,
                0x34,
                0x56,
                0x78
            },
            buffer);
    }

    /// <summary>
    /// Verifies that integer values round-trip for all sizes in little-endian mode.
    /// </summary>
    [Fact]
    public void ValuesRoundTripForAllSupportedSizesInLittleEndianMode()
    {
        AssertRoundTrip(
            byte.MaxValue,
            PacketIntegerSize.OneByte,
            PacketByteOrder.LittleEndian);

        AssertRoundTrip(
            ushort.MaxValue,
            PacketIntegerSize.TwoBytes,
            PacketByteOrder.LittleEndian);

        AssertRoundTrip(
            uint.MaxValue,
            PacketIntegerSize.FourBytes,
            PacketByteOrder.LittleEndian);

        AssertRoundTrip(
            ulong.MaxValue,
            PacketIntegerSize.EightBytes,
            PacketByteOrder.LittleEndian);
    }

    /// <summary>
    /// Verifies that integer values round-trip for all sizes in big-endian mode.
    /// </summary>
    [Fact]
    public void ValuesRoundTripForAllSupportedSizesInBigEndianMode()
    {
        AssertRoundTrip(
            byte.MaxValue,
            PacketIntegerSize.OneByte,
            PacketByteOrder.BigEndian);

        AssertRoundTrip(
            ushort.MaxValue,
            PacketIntegerSize.TwoBytes,
            PacketByteOrder.BigEndian);

        AssertRoundTrip(
            uint.MaxValue,
            PacketIntegerSize.FourBytes,
            PacketByteOrder.BigEndian);

        AssertRoundTrip(
            ulong.MaxValue,
            PacketIntegerSize.EightBytes,
            PacketByteOrder.BigEndian);
    }

    /// <summary>
    /// Verifies that writing a value exceeding 1 byte throws <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void WriteRejectsValueLargerThanOneByte()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            WriteValueLargerThanOneByte);
    }

    /// <summary>
    /// Verifies that writing a value exceeding 2 bytes throws <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void WriteRejectsValueLargerThanTwoBytes()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            WriteValueLargerThanTwoBytes);
    }

    /// <summary>
    /// Verifies that writing a value exceeding 4 bytes throws <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void WriteRejectsValueLargerThanFourBytes()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            WriteValueLargerThanFourBytes);
    }

    /// <summary>
    /// Verifies that reading with unspecified integer size throws <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void ReadRejectsUnspecifiedSize()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            ReadUsingUnspecifiedSize);
    }

    /// <summary>
    /// Helper asserting serialization and deserialization round-trip for an integer.
    /// </summary>
    /// <param name="value">The integer value to round-trip.</param>
    /// <param name="size">The target integer byte size.</param>
    /// <param name="byteOrder">The target byte order.</param>
    private static void AssertRoundTrip(
        ulong value,
        PacketIntegerSize size,
        PacketByteOrder byteOrder)
    {
        int byteCount =
            PacketIntegerCodec.GetByteCount(size);

        byte[] buffer =
            new byte[byteCount];

        PacketWriter writer = new(
            buffer,
            byteOrder);

        PacketIntegerCodec.Write(
            ref writer,
            value,
            size);

        Assert.Equal(
            byteCount,
            writer.WrittenCount);

        PacketReader reader = new(
            buffer,
            byteOrder);

        ulong actualValue =
            PacketIntegerCodec.Read(
                ref reader,
                size);

        Assert.Equal(
            value,
            actualValue);

        Assert.Equal(
            byteCount,
            reader.ConsumedCount);

        Assert.Equal(
            0,
            reader.Remaining);
    }

    /// <summary>
    /// Helper writing 256 into a one-byte target.
    /// </summary>
    private static void WriteValueLargerThanOneByte()
    {
        byte[] buffer = new byte[1];

        PacketWriter writer = new(
            buffer,
            PacketByteOrder.LittleEndian);

        PacketIntegerCodec.Write(
            ref writer,
            256,
            PacketIntegerSize.OneByte);
    }

    /// <summary>
    /// Helper writing 65,536 into a two-byte target.
    /// </summary>
    private static void WriteValueLargerThanTwoBytes()
    {
        byte[] buffer = new byte[2];

        PacketWriter writer = new(
            buffer,
            PacketByteOrder.LittleEndian);

        PacketIntegerCodec.Write(
            ref writer,
            65_536,
            PacketIntegerSize.TwoBytes);
    }

    /// <summary>
    /// Helper writing 4,294,967,296 into a four-byte target.
    /// </summary>
    private static void WriteValueLargerThanFourBytes()
    {
        byte[] buffer = new byte[4];

        PacketWriter writer = new(
            buffer,
            PacketByteOrder.LittleEndian);

        PacketIntegerCodec.Write(
            ref writer,
            4_294_967_296,
            PacketIntegerSize.FourBytes);
    }

    /// <summary>
    /// Helper attempting to read using unspecified integer size.
    /// </summary>
    private static void ReadUsingUnspecifiedSize()
    {
        byte[] buffer = new byte[1];

        PacketReader reader = new(
            buffer,
            PacketByteOrder.LittleEndian);

        _ = PacketIntegerCodec.Read(
            ref reader,
            PacketIntegerSize.Unspecified);
    }
}
