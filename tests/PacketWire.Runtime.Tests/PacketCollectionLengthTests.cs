using PacketWire;

namespace PacketWire.Runtime.Tests;

/// <summary>
/// Verifies length calculation and overflow validation for encoded collection counts.
/// </summary>
public sealed class PacketCollectionLengthTests
{
    /// <summary>
    /// Verifies that <see cref="PacketCollectionCodec.GetEncodedCountLength"/> returns the configured byte width for all supported integer sizes.
    /// </summary>
    /// <param name="size">The collection count integer size.</param>
    /// <param name="expectedLength">The expected byte length.</param>
    [Theory]
    [InlineData(PacketIntegerSize.OneByte, 1)]
    [InlineData(PacketIntegerSize.TwoBytes, 2)]
    [InlineData(PacketIntegerSize.FourBytes, 4)]
    [InlineData(PacketIntegerSize.EightBytes, 8)]
    public void GetEncodedCountLengthReturnsConfiguredWidth(
        PacketIntegerSize size,
        int expectedLength)
    {
        PacketProtocolDefinition definition =
            CreateDefinition(size);

        int length =
            PacketCollectionCodec.GetEncodedCountLength(
                10,
                definition);

        Assert.Equal(
            expectedLength,
            length);
    }

    /// <summary>
    /// Verifies that calculating length for a count exceeding protocol capacity throws <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void GetEncodedCountLengthRejectsProtocolOverflow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            GetLengthForCountLargerThanOneByte);
    }

    /// <summary>
    /// Verifies that calculating length for a count exceeding the contractual maximum throws <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void GetEncodedCountLengthRejectsContractMaximumOverflow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            GetLengthForCountLargerThanContractMaximum);
    }

    /// <summary>
    /// Creates a test protocol definition with the specified collection count size.
    /// </summary>
    /// <param name="countSize">The collection count integer size.</param>
    /// <returns>A configured <see cref="PacketProtocolDefinition"/>.</returns>
    private static PacketProtocolDefinition CreateDefinition(
        PacketIntegerSize countSize)
    {
        return new PacketProtocolDefinition(
            PacketIntegerSize.TwoBytes,
            PacketIntegerSize.TwoBytes,
            countSize,
            PacketByteOrder.LittleEndian);
    }

    /// <summary>
    /// Helper triggering length calculation for an overflowing count on a one-byte protocol.
    /// </summary>
    private static void GetLengthForCountLargerThanOneByte()
    {
        PacketProtocolDefinition definition =
            CreateDefinition(
                PacketIntegerSize.OneByte);

        _ = PacketCollectionCodec.GetEncodedCountLength(
            256,
            definition);
    }

    /// <summary>
    /// Helper triggering length calculation for a count exceeding the specified contractual maximum.
    /// </summary>
    private static void GetLengthForCountLargerThanContractMaximum()
    {
        PacketProtocolDefinition definition =
            CreateDefinition(
                PacketIntegerSize.TwoBytes);

        _ = PacketCollectionCodec.GetEncodedCountLength(
            101,
            definition,
            maximumCount: 100);
    }
}
