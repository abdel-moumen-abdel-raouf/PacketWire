using PacketWire;

namespace PacketWire.Runtime.Tests;

/// <summary>
/// Verifies header and complete frame encoding, decoding, length validation, and byte ordering in <see cref="PacketFrameCodec"/>.
/// </summary>
public sealed class PacketFrameCodecTests
{
    /// <summary>
    /// Verifies that writing a frame with default category produces the expected byte layout on the wire.
    /// </summary>
    [Fact]
    public void WriteFrameProducesExpectedDefaultCategoryWireLayout()
    {
        PacketProtocolDefinition definition =
            CreateDefaultDefinition();

        byte[] payload =
        {
            0xAA,
            0xBB,
            0xCC
        };

        byte[] frame =
            new byte[9];

        int bytesWritten =
            PacketFrameCodec.WriteFrame(
                frame,
                0x1234,
                payload,
                definition);

        Assert.Equal(
            9,
            bytesWritten);

        Assert.Equal(
            new byte[]
            {
                0x09,
                0x00,

                0x00,

                0x00,

                0x34,
                0x12,

                0xAA,
                0xBB,
                0xCC
            },
            frame);
    }

    /// <summary>
    /// Verifies that writing a frame with an explicit category produces the expected byte layout on the wire.
    /// </summary>
    [Fact]
    public void WriteFrameProducesExpectedExplicitCategoryWireLayout()
    {
        PacketProtocolDefinition definition =
            CreateDefaultDefinition();

        byte[] payload =
        {
            0xAA,
            0xBB,
            0xCC
        };

        byte[] frame =
            new byte[9];

        int bytesWritten =
            PacketFrameCodec.WriteFrame(
                frame,
                packetCategory: 0x7A,
                packetId: 0x1234,
                transmittedPayload: payload,
                definition);

        Assert.Equal(
            9,
            bytesWritten);

        Assert.Equal(
            new byte[]
            {
                0x09,
                0x00,

                0x00,

                0x7A,

                0x34,
                0x12,

                0xAA,
                0xBB,
                0xCC
            },
            frame);
    }

    /// <summary>
    /// Verifies that reading a valid frame extracts the packet category, ID, and payload correctly.
    /// </summary>
    [Fact]
    public void ReadFrameParsesCategoryAndPacketId()
    {
        PacketProtocolDefinition definition =
            CreateDefaultDefinition();

        byte[] frame =
        {
            0x09,
            0x00,

            0x00,

            0x07,

            0x34,
            0x12,

            0xAA,
            0xBB,
            0xCC
        };

        PacketFrameView view =
            PacketFrameCodec.ReadFrame(
                frame,
                definition);

        Assert.Equal(
            9UL,
            view.Header.PacketLength);

        Assert.Equal(
            PacketFrameOptions.None,
            view.Header.Flags);

        Assert.Equal(
            7UL,
            view.Header.PacketCategory);

        Assert.Equal(
            0x1234UL,
            view.Header.PacketId);

        Assert.Equal(
            3,
            view.PayloadLength);

        Assert.True(
            view.Payload.SequenceEqual(
                new byte[]
                {
                    0xAA,
                    0xBB,
                    0xCC
                }));
    }

    /// <summary>
    /// Verifies that an empty payload frame round-trips correctly with default category.
    /// </summary>
    [Fact]
    public void FrameRoundTripsWithEmptyPayloadAndDefaultCategory()
    {
        PacketProtocolDefinition definition =
            CreateDefaultDefinition();

        byte[] frame =
            new byte[definition.HeaderLength];

        int bytesWritten =
            PacketFrameCodec.WriteFrame(
                frame,
                1,
                ReadOnlySpan<byte>.Empty,
                definition);

        Assert.Equal(
            definition.HeaderLength,
            bytesWritten);

        PacketFrameView view =
            PacketFrameCodec.ReadFrame(
                frame,
                definition);

        Assert.Equal(
            (ulong)definition.HeaderLength,
            view.Header.PacketLength);

        Assert.Equal(
            PacketFrameOptions.None,
            view.Header.Flags);

        Assert.Equal(
            0UL,
            view.Header.PacketCategory);

        Assert.Equal(
            1UL,
            view.Header.PacketId);

        Assert.Equal(
            0,
            view.PayloadLength);
    }

    /// <summary>
    /// Verifies that writing and reading frames functions correctly under big-endian protocol configuration.
    /// </summary>
    [Fact]
    public void WriteAndReadFrameSupportBigEndian()
    {
        PacketProtocolDefinition definition = new(
            PacketIntegerSize.TwoBytes,
            PacketIntegerSize.FourBytes,
            PacketIntegerSize.TwoBytes,
            PacketByteOrder.BigEndian);

        byte[] payload =
        {
            0x10,
            0x20
        };

        byte[] frame =
            new byte[10];

        int bytesWritten =
            PacketFrameCodec.WriteFrame(
                frame,
                packetCategory: 3,
                packetId: 0x1234,
                transmittedPayload: payload,
                definition);

        Assert.Equal(
            10,
            bytesWritten);

        Assert.Equal(
            new byte[]
            {
                0x00,
                0x00,
                0x00,
                0x0A,

                0x00,

                0x03,

                0x12,
                0x34,

                0x10,
                0x20
            },
            frame);

        PacketFrameView view =
            PacketFrameCodec.ReadFrame(
                frame,
                definition);

        Assert.Equal(
            10UL,
            view.Header.PacketLength);

        Assert.Equal(
            PacketFrameOptions.None,
            view.Header.Flags);

        Assert.Equal(
            3UL,
            view.Header.PacketCategory);

        Assert.Equal(
            0x1234UL,
            view.Header.PacketId);

        Assert.True(
            view.Payload.SequenceEqual(payload));
    }

    /// <summary>
    /// Verifies that <see cref="PacketFrameCodec.ReadHeader"/> successfully decodes the header prefix from a complete frame.
    /// </summary>
    [Fact]
    public void ReadHeaderCanReadHeaderFromCompleteFrame()
    {
        PacketProtocolDefinition definition =
            CreateDefaultDefinition();

        byte[] frame =
        {
            0x09,
            0x00,

            0x00,

            0x05,

            0x34,
            0x12,

            0xAA,
            0xBB,
            0xCC
        };

        PacketFrameHeader header =
            PacketFrameCodec.ReadHeader(
                frame,
                definition);

        Assert.Equal(
            9UL,
            header.PacketLength);

        Assert.Equal(
            PacketFrameOptions.None,
            header.Flags);

        Assert.Equal(
            5UL,
            header.PacketCategory);

        Assert.Equal(
            0x1234UL,
            header.PacketId);
    }

    /// <summary>
    /// Verifies that declared payload length equals total packet length minus header length.
    /// </summary>
    [Fact]
    public void GetPayloadLengthReturnsDeclaredPayloadLength()
    {
        PacketProtocolDefinition definition =
            CreateDefaultDefinition();

        PacketFrameHeader header = new(
            101,
            0,
            10);

        ulong payloadLength =
            PacketFrameCodec.GetPayloadLength(
                header,
                definition);

        Assert.Equal(
            95UL,
            payloadLength);
    }

    /// <summary>
    /// Verifies that calculated frame length is payload length plus full header length.
    /// </summary>
    [Fact]
    public void CalculateFrameLengthIncludesCompleteHeader()
    {
        PacketProtocolDefinition definition =
            CreateDefaultDefinition();

        ulong frameLength =
            PacketFrameCodec.CalculateFrameLength(
                100,
                definition);

        Assert.Equal(
            106UL,
            frameLength);
    }

    /// <summary>
    /// Verifies that writing a packet ID that exceeds the configured integer width throws <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void WriteFrameRejectsPacketIdThatExceedsConfiguredWidth()
    {
        PacketProtocolDefinition definition = new(
            PacketIntegerSize.OneByte,
            PacketIntegerSize.TwoBytes,
            PacketIntegerSize.TwoBytes,
            PacketByteOrder.LittleEndian);

        byte[] frame =
            new byte[definition.HeaderLength];

        Assert.Throws<ArgumentOutOfRangeException>(
            () => PacketFrameCodec.WriteFrame(
                frame,
                256,
                ReadOnlySpan<byte>.Empty,
                definition));
    }

    /// <summary>
    /// Verifies that writing a category exceeding the configured category width throws <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void WriteFrameRejectsCategoryThatExceedsConfiguredWidth()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            WriteFrameWithCategoryLargerThanOneByte);
    }

    /// <summary>
    /// Verifies that calculating frame length for an overflowing payload throws <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void CalculateFrameLengthRejectsLengthThatExceedsConfiguredWidth()
    {
        PacketProtocolDefinition definition = new(
            PacketIntegerSize.OneByte,
            PacketIntegerSize.OneByte,
            PacketIntegerSize.OneByte,
            PacketByteOrder.LittleEndian);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => PacketFrameCodec.CalculateFrameLength(
                253,
                definition));
    }

    /// <summary>
    /// Verifies that reading a header whose declared length is smaller than the header length throws <see cref="PacketBufferException"/>.
    /// </summary>
    [Fact]
    public void ReadHeaderRejectsPacketLengthSmallerThanHeader()
    {
        PacketProtocolDefinition definition =
            CreateDefaultDefinition();

        byte[] header =
        {
            0x05,
            0x00,

            0x00,

            0x00,

            0x01,
            0x00
        };

        Assert.Equal(
            definition.HeaderLength,
            header.Length);

        Assert.Throws<PacketBufferException>(
            () => PacketFrameCodec.ReadHeader(
                header,
                definition));
    }

    /// <summary>
    /// Verifies that reading a frame where the buffer is shorter than the declared length throws <see cref="PacketBufferException"/>.
    /// </summary>
    [Fact]
    public void ReadFrameRejectsIncompleteFrame()
    {
        PacketProtocolDefinition definition =
            CreateDefaultDefinition();

        byte[] frame =
        {
            0x09,
            0x00,

            0x00,

            0x00,

            0x01,
            0x00,

            0xAA,
            0xBB
        };

        Assert.Equal(
            8,
            frame.Length);

        Assert.Throws<PacketBufferException>(
            () => PacketFrameCodec.ReadFrame(
                frame,
                definition));
    }

    /// <summary>
    /// Verifies that reading a frame where trailing bytes follow the declared packet length throws <see cref="PacketBufferException"/>.
    /// </summary>
    [Fact]
    public void ReadFrameRejectsExtraBytesAfterDeclaredFrame()
    {
        PacketProtocolDefinition definition =
            CreateDefaultDefinition();

        byte[] frame =
        {
            0x07,
            0x00,

            0x00,

            0x00,

            0x01,
            0x00,

            0xAA,
            0xBB
        };

        Assert.Equal(
            8,
            frame.Length);

        Assert.Throws<PacketBufferException>(
            () => PacketFrameCodec.ReadFrame(
                frame,
                definition));
    }

    /// <summary>
    /// Verifies that writing a frame into a destination buffer shorter than required throws <see cref="PacketBufferException"/>.
    /// </summary>
    [Fact]
    public void WriteFrameRejectsDestinationThatIsTooSmall()
    {
        PacketProtocolDefinition definition =
            CreateDefaultDefinition();

        byte[] frame =
            new byte[definition.HeaderLength];

        byte[] payload =
        {
            0xAA
        };

        Assert.Throws<PacketBufferException>(
            () => PacketFrameCodec.WriteFrame(
                frame,
                1,
                payload,
                definition));
    }

    /// <summary>
    /// Creates a default protocol definition with two-byte header integer sizes and little-endian ordering.
    /// </summary>
    /// <returns>A default configured <see cref="PacketProtocolDefinition"/>.</returns>
    private static PacketProtocolDefinition CreateDefaultDefinition()
    {
        return new PacketProtocolDefinition(
            PacketIntegerSize.TwoBytes,
            PacketIntegerSize.TwoBytes,
            PacketIntegerSize.TwoBytes,
            PacketByteOrder.LittleEndian);
    }

    /// <summary>
    /// Helper writing a frame with category 256 onto a one-byte category protocol.
    /// </summary>
    private static void WriteFrameWithCategoryLargerThanOneByte()
    {
        PacketProtocolDefinition definition =
            CreateDefaultDefinition();

        byte[] frame =
            new byte[definition.HeaderLength];

        PacketFrameCodec.WriteFrame(
            frame,
            packetCategory: 256,
            packetId: 1,
            transmittedPayload: ReadOnlySpan<byte>.Empty,
            definition);
    }
}