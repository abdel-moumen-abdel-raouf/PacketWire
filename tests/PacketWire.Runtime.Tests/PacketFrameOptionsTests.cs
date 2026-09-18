using PacketWire;

namespace PacketWire.Runtime.Tests;

/// <summary>
/// Verifies header flags encoding, decoding, round-tripping, and validation of <see cref="PacketFrameOptions"/>.
/// </summary>
public sealed class PacketFrameOptionsTests
{
    /// <summary>
    /// The expected raw wire bytes for an 8-byte protected frame.
    /// </summary>
    private static readonly byte[] ExpectedProtectedFrame =
    [
        0x08,
        0x00,

        0x01,

        0x03,

        0x34,
        0x12,

        0xAA,
        0xBB
    ];

    /// <summary>
    /// Verifies that protocol header length accounts for the 1-byte flags field.
    /// </summary>
    [Fact]
    public void HeaderLengthIncludesOneFlagsByte()
    {
        PacketProtocolDefinition definition =
            CreateDefinition();

        Assert.Equal(
            6,
            definition.HeaderLength);
    }

    /// <summary>
    /// Verifies that writing a frame using the overload without options defaults to <see cref="PacketFrameOptions.None"/>.
    /// </summary>
    [Fact]
    public void LegacyWriteFrameOverloadWritesNoneFlag()
    {
        PacketProtocolDefinition definition =
            CreateDefinition();

        byte[] frame =
            new byte[
                (int)PacketFrameCodec.CalculateFrameLength(
                    0,
                    definition)];

        int written =
            PacketFrameCodec.WriteFrame(
                frame,
                3,
                0x1234,
                ReadOnlySpan<byte>.Empty,
                definition);

        Assert.Equal(
            definition.HeaderLength,
            written);

        PacketFrameHeader header =
            PacketFrameCodec.ReadHeader(
                frame,
                definition);

        Assert.Equal(
            PacketFrameOptions.None,
            header.Flags);

        Assert.Equal(
            3UL,
            header.PacketCategory);

        Assert.Equal(
            0x1234UL,
            header.PacketId);
    }

    /// <summary>
    /// Verifies that setting <see cref="PacketFrameOptions.Protected"/> round-trips correctly through header encoding and parsing.
    /// </summary>
    [Fact]
    public void ProtectedFlagRoundTripsThroughFrame()
    {
        PacketProtocolDefinition definition =
            CreateDefinition();

        byte[] payload =
        [
            0xAA,
            0xBB
        ];

        byte[] frame =
            new byte[
                (int)PacketFrameCodec.CalculateFrameLength(
                    payload.Length,
                    definition)];

        int written =
            PacketFrameCodec.WriteFrame(
                frame,
                PacketFrameOptions.Protected,
                3,
                0x1234,
                payload,
                definition);

        Assert.Equal(
            ExpectedProtectedFrame.Length,
            written);

        Assert.Equal(
            ExpectedProtectedFrame,
            frame);

        PacketFrameView parsed =
            PacketFrameCodec.ReadFrame(
                frame,
                definition);

        Assert.Equal(
            PacketFrameOptions.Protected,
            parsed.Header.Flags);

        Assert.Equal(
            3UL,
            parsed.Header.PacketCategory);

        Assert.Equal(
            0x1234UL,
            parsed.Header.PacketId);

        Assert.Equal(
            2,
            parsed.PayloadLength);

        Assert.Equal(
            0xAA,
            parsed.Payload[0]);

        Assert.Equal(
            0xBB,
            parsed.Payload[1]);
    }

    /// <summary>
    /// Verifies that passing undefined flag bits to <c>PacketFrameCodec.WriteHeader</c> throws <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void WriteRejectsUnknownFlagBits()
    {
        PacketProtocolDefinition definition =
            CreateDefinition();

        byte[] header =
            new byte[definition.HeaderLength];

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                PacketFrameCodec.WriteHeader(
                    header,
                    (ulong)definition.HeaderLength,
                    (PacketFrameOptions)0x80,
                    3,
                    0x1234,
                    definition));
    }

    /// <summary>
    /// Verifies that reading a header containing undefined flag bits throws <see cref="PacketBufferException"/>.
    /// </summary>
    [Fact]
    public void ReadRejectsUnknownFlagBits()
    {
        PacketProtocolDefinition definition =
            CreateDefinition();

        byte[] header =
        [
            0x06,
            0x00,

            0x80,

            0x03,

            0x34,
            0x12
        ];

        Assert.Throws<PacketBufferException>(
            () =>
                PacketFrameCodec.ReadHeader(
                    header,
                    definition));
    }

    /// <summary>
    /// Creates a test protocol definition for options testing.
    /// </summary>
    /// <returns>A configured <see cref="PacketProtocolDefinition"/>.</returns>
    private static PacketProtocolDefinition CreateDefinition()
    {
        return new PacketProtocolDefinition(
            PacketIntegerSize.TwoBytes,
            PacketIntegerSize.TwoBytes,
            PacketIntegerSize.TwoBytes,
            PacketByteOrder.LittleEndian);
    }
}