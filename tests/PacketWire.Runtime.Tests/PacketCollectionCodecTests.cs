using PacketWire;

namespace PacketWire.Runtime.Tests;

/// <summary>
/// Verifies encoding, decoding, capacity limits, and validation rules for collection counts under various protocol configurations.
/// </summary>
public sealed class PacketCollectionCodecTests
{
    /// <summary>
    /// Verifies that collection counts round-trip for all integer sizes in little-endian mode.
    /// </summary>
    [Fact]
    public void CountsRoundTripForAllSupportedSizesInLittleEndianMode()
    {
        AssertRoundTrip(
            200,
            PacketIntegerSize.OneByte,
            PacketByteOrder.LittleEndian);

        AssertRoundTrip(
            50_000,
            PacketIntegerSize.TwoBytes,
            PacketByteOrder.LittleEndian);

        AssertRoundTrip(
            1_000_000,
            PacketIntegerSize.FourBytes,
            PacketByteOrder.LittleEndian);

        AssertRoundTrip(
            1_000_000,
            PacketIntegerSize.EightBytes,
            PacketByteOrder.LittleEndian);
    }

    /// <summary>
    /// Verifies that collection counts round-trip for all integer sizes in big-endian mode.
    /// </summary>
    [Fact]
    public void CountsRoundTripForAllSupportedSizesInBigEndianMode()
    {
        AssertRoundTrip(
            200,
            PacketIntegerSize.OneByte,
            PacketByteOrder.BigEndian);

        AssertRoundTrip(
            50_000,
            PacketIntegerSize.TwoBytes,
            PacketByteOrder.BigEndian);

        AssertRoundTrip(
            1_000_000,
            PacketIntegerSize.FourBytes,
            PacketByteOrder.BigEndian);

        AssertRoundTrip(
            1_000_000,
            PacketIntegerSize.EightBytes,
            PacketByteOrder.BigEndian);
    }

    /// <summary>
    /// Verifies that writing a collection count encodes using the configured two-byte little-endian format.
    /// </summary>
    [Fact]
    public void WriteCountUsesConfiguredLittleEndianWidth()
    {
        PacketProtocolDefinition definition =
            CreateDefinition(
                PacketIntegerSize.TwoBytes,
                PacketByteOrder.LittleEndian);

        byte[] buffer = new byte[2];

        PacketWriter writer = new(
            buffer,
            PacketByteOrder.LittleEndian);

        PacketCollectionCodec.WriteCount(
            ref writer,
            0x1234,
            definition);

        Assert.Equal(
            new byte[]
            {
                0x34,
                0x12
            },
            buffer);
    }

    /// <summary>
    /// Verifies that writing a negative collection count throws <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void WriteCountRejectsNegativeCount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            WriteNegativeCount);
    }

    /// <summary>
    /// Verifies that writing a count exceeding protocol capacity throws <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void WriteCountRejectsCountThatExceedsProtocolCapacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            WriteCountLargerThanOneByte);
    }

    /// <summary>
    /// Verifies that writing a count exceeding the contractual maximum throws <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void WriteCountRejectsCountThatExceedsContractMaximum()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            WriteCountLargerThanContractMaximum);
    }

    /// <summary>
    /// Verifies that passing a negative maximum count to write throws <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void WriteCountRejectsNegativeMaximumCount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            WriteUsingNegativeMaximumCount);
    }

    /// <summary>
    /// Verifies that reading a count exceeding the contract maximum throws <see cref="PacketBufferException"/>.
    /// </summary>
    [Fact]
    public void ReadCountRejectsCountThatExceedsContractMaximum()
    {
        Assert.Throws<PacketBufferException>(
            ReadCountLargerThanContractMaximum);
    }

    /// <summary>
    /// Verifies that reading a count exceeding <see cref="int.MaxValue"/> throws <see cref="PacketBufferException"/>.
    /// </summary>
    [Fact]
    public void ReadCountRejectsCountThatExceedsInt32Capacity()
    {
        Assert.Throws<PacketBufferException>(
            ReadCountLargerThanInt32);
    }

    /// <summary>
    /// Verifies that passing a negative maximum count to read throws <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void ReadCountRejectsNegativeMaximumCount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            ReadUsingNegativeMaximumCount);
    }

    /// <summary>
    /// Helper verifying write and read round-trip for a specific count, integer size, and byte order.
    /// </summary>
    /// <param name="count">The collection item count.</param>
    /// <param name="countSize">The configured integer width.</param>
    /// <param name="byteOrder">The configured byte order.</param>
    private static void AssertRoundTrip(
        int count,
        PacketIntegerSize countSize,
        PacketByteOrder byteOrder)
    {
        PacketProtocolDefinition definition =
            CreateDefinition(
                countSize,
                byteOrder);

        int byteCount =
            PacketIntegerCodec.GetByteCount(
                countSize);

        byte[] buffer =
            new byte[byteCount];

        PacketWriter writer = new(
            buffer,
            byteOrder);

        PacketCollectionCodec.WriteCount(
            ref writer,
            count,
            definition);

        Assert.Equal(
            byteCount,
            writer.WrittenCount);

        PacketReader reader = new(
            buffer,
            byteOrder);

        int actualCount =
            PacketCollectionCodec.ReadCount(
                ref reader,
                definition);

        Assert.Equal(
            count,
            actualCount);

        Assert.Equal(
            byteCount,
            reader.ConsumedCount);

        Assert.Equal(
            0,
            reader.Remaining);
    }

    /// <summary>
    /// Creates a test protocol definition with the specified count size and byte order.
    /// </summary>
    /// <param name="collectionCountSize">The collection count integer size.</param>
    /// <param name="byteOrder">The byte order.</param>
    /// <returns>A configured <see cref="PacketProtocolDefinition"/>.</returns>
    private static PacketProtocolDefinition CreateDefinition(
        PacketIntegerSize collectionCountSize,
        PacketByteOrder byteOrder)
    {
        return new PacketProtocolDefinition(
            PacketIntegerSize.TwoBytes,
            PacketIntegerSize.TwoBytes,
            collectionCountSize,
            byteOrder);
    }

    /// <summary>
    /// Helper writing a negative count to trigger an exception.
    /// </summary>
    private static void WriteNegativeCount()
    {
        PacketProtocolDefinition definition =
            CreateDefinition(
                PacketIntegerSize.TwoBytes,
                PacketByteOrder.LittleEndian);

        byte[] buffer = new byte[2];

        PacketWriter writer = new(
            buffer,
            PacketByteOrder.LittleEndian);

        PacketCollectionCodec.WriteCount(
            ref writer,
            -1,
            definition);
    }

    /// <summary>
    /// Helper writing a count that exceeds a one-byte integer size limit.
    /// </summary>
    private static void WriteCountLargerThanOneByte()
    {
        PacketProtocolDefinition definition =
            CreateDefinition(
                PacketIntegerSize.OneByte,
                PacketByteOrder.LittleEndian);

        byte[] buffer = new byte[1];

        PacketWriter writer = new(
            buffer,
            PacketByteOrder.LittleEndian);

        PacketCollectionCodec.WriteCount(
            ref writer,
            256,
            definition);
    }

    /// <summary>
    /// Helper writing a count that exceeds the specified contractual maximum.
    /// </summary>
    private static void WriteCountLargerThanContractMaximum()
    {
        PacketProtocolDefinition definition =
            CreateDefinition(
                PacketIntegerSize.TwoBytes,
                PacketByteOrder.LittleEndian);

        byte[] buffer = new byte[2];

        PacketWriter writer = new(
            buffer,
            PacketByteOrder.LittleEndian);

        PacketCollectionCodec.WriteCount(
            ref writer,
            101,
            definition,
            maximumCount: 100);
    }

    /// <summary>
    /// Helper writing a count using a negative contractual maximum.
    /// </summary>
    private static void WriteUsingNegativeMaximumCount()
    {
        PacketProtocolDefinition definition =
            CreateDefinition(
                PacketIntegerSize.TwoBytes,
                PacketByteOrder.LittleEndian);

        byte[] buffer = new byte[2];

        PacketWriter writer = new(
            buffer,
            PacketByteOrder.LittleEndian);

        PacketCollectionCodec.WriteCount(
            ref writer,
            0,
            definition,
            maximumCount: -1);
    }

    /// <summary>
    /// Helper reading a count that exceeds the contractual maximum.
    /// </summary>
    private static void ReadCountLargerThanContractMaximum()
    {
        PacketProtocolDefinition definition =
            CreateDefinition(
                PacketIntegerSize.TwoBytes,
                PacketByteOrder.LittleEndian);

        byte[] buffer =
        {
            0x65,
            0x00
        };

        PacketReader reader = new(
            buffer,
            PacketByteOrder.LittleEndian);

        _ = PacketCollectionCodec.ReadCount(
            ref reader,
            definition,
            maximumCount: 100);
    }

    /// <summary>
    /// Helper reading a count encoded in 8 bytes that exceeds Int32 max value.
    /// </summary>
    private static void ReadCountLargerThanInt32()
    {
        PacketProtocolDefinition definition =
            CreateDefinition(
                PacketIntegerSize.EightBytes,
                PacketByteOrder.LittleEndian);

        byte[] buffer = new byte[8];

        PacketWriter writer = new(
            buffer,
            PacketByteOrder.LittleEndian);

        PacketIntegerCodec.Write(
            ref writer,
            (ulong)int.MaxValue + 1UL,
            PacketIntegerSize.EightBytes);

        PacketReader reader = new(
            buffer,
            PacketByteOrder.LittleEndian);

        _ = PacketCollectionCodec.ReadCount(
            ref reader,
            definition);
    }

    /// <summary>
    /// Helper reading a count using a negative contractual maximum.
    /// </summary>
    private static void ReadUsingNegativeMaximumCount()
    {
        PacketProtocolDefinition definition =
            CreateDefinition(
                PacketIntegerSize.TwoBytes,
                PacketByteOrder.LittleEndian);

        byte[] buffer =
        {
            0x00,
            0x00
        };

        PacketReader reader = new(
            buffer,
            PacketByteOrder.LittleEndian);

        _ = PacketCollectionCodec.ReadCount(
            ref reader,
            definition,
            maximumCount: -1);
    }
}
