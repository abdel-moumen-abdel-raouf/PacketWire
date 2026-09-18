namespace PacketWire;

/// <summary>
/// Provides low-level encoding, decoding, and validation for binary packet wire frames and headers.
/// </summary>
/// <remarks>
/// <para>
/// PacketWire wire frames have the layout:
/// <c>PacketLength | Flags | PacketCategory | PacketId | Payload</c>.
/// The header fields precede the payload and are encoded according to the protocol's configured
/// <see cref="PacketProtocolDefinition"/> (integer sizes and byte endianness).
/// </para>
/// <para>
/// All framing operations are strictly fail-closed: unsupported flag bits, invalid lengths, truncated buffers,
/// and trailing bytes beyond the declared frame length are rejected immediately with exceptions.
/// </para>
/// </remarks>
public static class PacketFrameCodec
{
    /// <summary>
    /// Bitmask defining all recognized packet frame option flags supported by the current runtime version.
    /// </summary>
    private const byte SupportedFlagsMask =
        (byte)PacketFrameOptions.Protected;

    /// <summary>
    /// Calculates the total transmitted frame length in bytes (header length plus payload length) for a given payload.
    /// </summary>
    /// <param name="payloadLength">The length of the payload in bytes. Must be non-negative.</param>
    /// <param name="definition">The protocol definition providing header length and maximum frame capacity.</param>
    /// <returns>The total frame length in bytes as an unsigned 64-bit integer.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="definition"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="payloadLength"/> is negative or the resulting frame length exceeds
    /// <see cref="PacketProtocolDefinition.MaximumPacketLength"/>.
    /// </exception>
    public static ulong CalculateFrameLength(
        int payloadLength,
        PacketProtocolDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        ArgumentOutOfRangeException.ThrowIfNegative(
            payloadLength);

        ulong frameLength =
            (ulong)definition.HeaderLength +
            (ulong)payloadLength;

        if (frameLength > definition.MaximumPacketLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(payloadLength),
                payloadLength,
                $"The resulting packet length {frameLength} exceeds the protocol maximum packet length {definition.MaximumPacketLength}.");
        }

        return frameLength;
    }

    /// <summary>
    /// Encodes a plain packet header with default category (<c>0</c>) and no flags into the destination buffer.
    /// </summary>
    /// <param name="destination">The destination buffer to receive the encoded header.</param>
    /// <param name="packetLength">The total length of the packet frame in bytes.</param>
    /// <param name="packetId">The numeric packet identifier.</param>
    /// <param name="definition">The protocol definition specifying field widths and byte endianness.</param>
    /// <returns>The total number of header bytes written to <paramref name="destination"/>.</returns>
    public static int WriteHeader(
        Span<byte> destination,
        ulong packetLength,
        ulong packetId,
        PacketProtocolDefinition definition)
    {
        return WriteHeader(
            destination,
            packetLength,
            PacketFrameOptions.None,
            0,
            packetId,
            definition);
    }

    /// <summary>
    /// Encodes a plain packet header with the specified category and no flags into the destination buffer.
    /// </summary>
    /// <param name="destination">The destination buffer to receive the encoded header.</param>
    /// <param name="packetLength">The total length of the packet frame in bytes.</param>
    /// <param name="packetCategory">The category partition identifier for the packet.</param>
    /// <param name="packetId">The numeric packet identifier.</param>
    /// <param name="definition">The protocol definition specifying field widths and byte endianness.</param>
    /// <returns>The total number of header bytes written to <paramref name="destination"/>.</returns>
    public static int WriteHeader(
        Span<byte> destination,
        ulong packetLength,
        ulong packetCategory,
        ulong packetId,
        PacketProtocolDefinition definition)
    {
        return WriteHeader(
            destination,
            packetLength,
            PacketFrameOptions.None,
            packetCategory,
            packetId,
            definition);
    }

    /// <summary>
    /// Encodes a packet header with the specified flags and default category (<c>0</c>) into the destination buffer.
    /// </summary>
    /// <param name="destination">The destination buffer to receive the encoded header.</param>
    /// <param name="packetLength">The total length of the packet frame in bytes.</param>
    /// <param name="flags">The frame option flags to encode.</param>
    /// <param name="packetId">The numeric packet identifier.</param>
    /// <param name="definition">The protocol definition specifying field widths and byte endianness.</param>
    /// <returns>The total number of header bytes written to <paramref name="destination"/>.</returns>
    public static int WriteHeader(
        Span<byte> destination,
        ulong packetLength,
        PacketFrameOptions flags,
        ulong packetId,
        PacketProtocolDefinition definition)
    {
        return WriteHeader(
            destination,
            packetLength,
            flags,
            0,
            packetId,
            definition);
    }

    /// <summary>
    /// Encodes a complete packet header with explicit flags, category, and packet identifier into the destination buffer.
    /// </summary>
    /// <param name="destination">The destination buffer to receive the encoded header.</param>
    /// <param name="packetLength">The total length of the packet frame in bytes.</param>
    /// <param name="flags">The frame option flags to encode.</param>
    /// <param name="packetCategory">The category partition identifier for the packet.</param>
    /// <param name="packetId">The numeric packet identifier.</param>
    /// <param name="definition">The protocol definition specifying field widths and byte endianness.</param>
    /// <returns>The total number of header bytes written to <paramref name="destination"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="definition"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="flags"/> contains unsupported bits, <paramref name="packetLength"/> is invalid,
    /// or <paramref name="packetCategory"/> or <paramref name="packetId"/> exceeds protocol limits.
    /// </exception>
    /// <exception cref="InvalidOperationException">Thrown when the written byte count does not match <see cref="PacketProtocolDefinition.HeaderLength"/>.</exception>
    public static int WriteHeader(
        Span<byte> destination,
        ulong packetLength,
        PacketFrameOptions flags,
        ulong packetCategory,
        ulong packetId,
        PacketProtocolDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        ValidateFlagsForWrite(
            flags);

        ValidatePacketLengthForWrite(
            packetLength,
            definition);

        if (packetCategory > definition.MaximumPacketCategory)
        {
            throw new ArgumentOutOfRangeException(
                nameof(packetCategory),
                packetCategory,
                $"Packet category exceeds the protocol maximum value {definition.MaximumPacketCategory}.");
        }

        if (packetId > definition.MaximumPacketId)
        {
            throw new ArgumentOutOfRangeException(
                nameof(packetId),
                packetId,
                $"Packet ID exceeds the protocol maximum value {definition.MaximumPacketId}.");
        }

        PacketWriter writer =
            new(
                destination,
                definition.ByteOrder);

        PacketIntegerCodec.Write(
            ref writer,
            packetLength,
            definition.PacketLengthSize);

        writer.WriteByte(
            (byte)flags);

        PacketIntegerCodec.Write(
            ref writer,
            packetCategory,
            definition.PacketCategorySize);

        PacketIntegerCodec.Write(
            ref writer,
            packetId,
            definition.PacketIdSize);

        if (writer.WrittenCount != definition.HeaderLength)
        {
            throw new InvalidOperationException(
                $"Packet header generation wrote {writer.WrittenCount} bytes but the protocol header length is {definition.HeaderLength}.");
        }

        return writer.WrittenCount;
    }

    /// <summary>
    /// Parses and validates a packet frame header from the specified source span.
    /// </summary>
    /// <param name="source">The source byte span containing the header.</param>
    /// <param name="definition">The protocol definition providing expected field widths and byte endianness.</param>
    /// <returns>A <see cref="PacketFrameHeader"/> containing the decoded and validated header values.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="definition"/> is <see langword="null"/>.</exception>
    /// <exception cref="PacketBufferException">
    /// Thrown when <paramref name="source"/> contains fewer bytes than <see cref="PacketProtocolDefinition.HeaderLength"/>,
    /// flags contain unsupported bits, or length/category/ID values exceed protocol limits.
    /// </exception>
    public static PacketFrameHeader ReadHeader(
        ReadOnlySpan<byte> source,
        PacketProtocolDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (source.Length < definition.HeaderLength)
        {
            throw new PacketBufferException(
                $"The packet header requires {definition.HeaderLength} bytes, but only {source.Length} bytes are available.");
        }

        PacketReader reader =
            new(
                source,
                definition.ByteOrder);

        ulong packetLength =
            PacketIntegerCodec.Read(
                ref reader,
                definition.PacketLengthSize);

        byte rawFlags =
            reader.ReadByte();

        PacketFrameOptions flags =
            ValidateFlagsFromWire(
                rawFlags);

        ulong packetCategory =
            PacketIntegerCodec.Read(
                ref reader,
                definition.PacketCategorySize);

        ulong packetId =
            PacketIntegerCodec.Read(
                ref reader,
                definition.PacketIdSize);

        ValidatePacketLengthFromWire(
            packetLength,
            definition);

        if (packetCategory > definition.MaximumPacketCategory)
        {
            throw new PacketBufferException(
                $"Packet category {packetCategory} exceeds the protocol maximum value {definition.MaximumPacketCategory}.");
        }

        if (packetId > definition.MaximumPacketId)
        {
            throw new PacketBufferException(
                $"Packet ID {packetId} exceeds the protocol maximum value {definition.MaximumPacketId}.");
        }

        return new PacketFrameHeader(
            packetLength,
            flags,
            packetCategory,
            packetId);
    }

    /// <summary>
    /// Computes the payload length in bytes by subtracting the protocol header length from the declared frame length.
    /// </summary>
    /// <param name="header">The parsed packet frame header.</param>
    /// <param name="definition">The protocol definition providing the header length.</param>
    /// <returns>The payload length in bytes.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="definition"/> is <see langword="null"/>.</exception>
    /// <exception cref="PacketBufferException">Thrown when the declared frame length is smaller than the header length or exceeds protocol limits.</exception>
    public static ulong GetPayloadLength(
        PacketFrameHeader header,
        PacketProtocolDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        ValidatePacketLengthFromWire(
            header.PacketLength,
            definition);

        return
            header.PacketLength -
            (ulong)definition.HeaderLength;
    }

    /// <summary>
    /// Encodes a complete plain packet frame (header and payload) with default category (<c>0</c>) into the destination buffer.
    /// </summary>
    /// <param name="destination">The destination buffer to receive the complete framed packet.</param>
    /// <param name="packetId">The numeric packet identifier.</param>
    /// <param name="transmittedPayload">The raw payload bytes to encapsulate.</param>
    /// <param name="definition">The protocol definition specifying framing layout and endianness.</param>
    /// <returns>The total number of frame bytes written to <paramref name="destination"/>.</returns>
    public static int WriteFrame(
        Span<byte> destination,
        ulong packetId,
        ReadOnlySpan<byte> transmittedPayload,
        PacketProtocolDefinition definition)
    {
        return WriteFrame(
            destination,
            PacketFrameOptions.None,
            0,
            packetId,
            transmittedPayload,
            definition);
    }

    /// <summary>
    /// Encodes a complete plain packet frame (header and payload) with the specified category into the destination buffer.
    /// </summary>
    /// <param name="destination">The destination buffer to receive the complete framed packet.</param>
    /// <param name="packetCategory">The category partition identifier for the packet.</param>
    /// <param name="packetId">The numeric packet identifier.</param>
    /// <param name="transmittedPayload">The raw payload bytes to encapsulate.</param>
    /// <param name="definition">The protocol definition specifying framing layout and endianness.</param>
    /// <returns>The total number of frame bytes written to <paramref name="destination"/>.</returns>
    public static int WriteFrame(
        Span<byte> destination,
        ulong packetCategory,
        ulong packetId,
        ReadOnlySpan<byte> transmittedPayload,
        PacketProtocolDefinition definition)
    {
        return WriteFrame(
            destination,
            PacketFrameOptions.None,
            packetCategory,
            packetId,
            transmittedPayload,
            definition);
    }

    /// <summary>
    /// Encodes a complete packet frame (header and payload) with explicit flags and default category (<c>0</c>) into the destination buffer.
    /// </summary>
    /// <param name="destination">The destination buffer to receive the complete framed packet.</param>
    /// <param name="flags">The frame option flags to encode.</param>
    /// <param name="packetId">The numeric packet identifier.</param>
    /// <param name="transmittedPayload">The raw payload bytes to encapsulate.</param>
    /// <param name="definition">The protocol definition specifying framing layout and endianness.</param>
    /// <returns>The total number of frame bytes written to <paramref name="destination"/>.</returns>
    public static int WriteFrame(
        Span<byte> destination,
        PacketFrameOptions flags,
        ulong packetId,
        ReadOnlySpan<byte> transmittedPayload,
        PacketProtocolDefinition definition)
    {
        return WriteFrame(
            destination,
            flags,
            0,
            packetId,
            transmittedPayload,
            definition);
    }

    /// <summary>
    /// Encodes a complete packet frame (header and payload) with explicit flags, category, and packet identifier into the destination buffer.
    /// </summary>
    /// <param name="destination">The destination buffer to receive the complete framed packet.</param>
    /// <param name="flags">The frame option flags to encode.</param>
    /// <param name="packetCategory">The category partition identifier for the packet.</param>
    /// <param name="packetId">The numeric packet identifier.</param>
    /// <param name="transmittedPayload">The raw payload bytes to encapsulate.</param>
    /// <param name="definition">The protocol definition specifying framing layout and endianness.</param>
    /// <returns>The total number of frame bytes written to <paramref name="destination"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="definition"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="flags"/>, <paramref name="packetCategory"/>, <paramref name="packetId"/>, or the resulting
    /// frame length violates protocol boundaries or exceeds <see cref="int.MaxValue"/>.
    /// </exception>
    /// <exception cref="PacketBufferException">Thrown when <paramref name="destination"/> has insufficient capacity.</exception>
    public static int WriteFrame(
        Span<byte> destination,
        PacketFrameOptions flags,
        ulong packetCategory,
        ulong packetId,
        ReadOnlySpan<byte> transmittedPayload,
        PacketProtocolDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        ValidateFlagsForWrite(
            flags);

        if (packetCategory > definition.MaximumPacketCategory)
        {
            throw new ArgumentOutOfRangeException(
                nameof(packetCategory),
                packetCategory,
                $"Packet category exceeds the protocol maximum value {definition.MaximumPacketCategory}.");
        }

        if (packetId > definition.MaximumPacketId)
        {
            throw new ArgumentOutOfRangeException(
                nameof(packetId),
                packetId,
                $"Packet ID exceeds the protocol maximum value {definition.MaximumPacketId}.");
        }

        ulong frameLengthValue =
            CalculateFrameLength(
                transmittedPayload.Length,
                definition);

        if (frameLengthValue > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(transmittedPayload),
                $"The resulting frame length {frameLengthValue} exceeds the maximum managed span length supported by PacketWire.");
        }

        int frameLength =
            (int)frameLengthValue;

        if (destination.Length < frameLength)
        {
            throw new PacketBufferException(
                $"The destination buffer requires at least {frameLength} bytes, but only {destination.Length} bytes are available.");
        }

        _ =
            WriteHeader(
                destination,
                frameLengthValue,
                flags,
                packetCategory,
                packetId,
                definition);

        transmittedPayload.CopyTo(
            destination.Slice(
                definition.HeaderLength,
                transmittedPayload.Length));

        return frameLength;
    }

    /// <summary>
    /// Decodes and validates a complete packet frame from the source buffer, returning a zero-copy view of the header and payload.
    /// </summary>
    /// <param name="source">The source byte span containing exactly one complete packet frame.</param>
    /// <param name="definition">The protocol definition providing framing rules.</param>
    /// <returns>A <see cref="PacketFrameView"/> exposing the parsed header and sliced payload span.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="definition"/> is <see langword="null"/>.</exception>
    /// <exception cref="PacketBufferException">
    /// Thrown when <paramref name="source"/> does not contain a complete frame, contains trailing bytes beyond the declared
    /// frame length, contains invalid flags, or violates length constraints.
    /// </exception>
    public static PacketFrameView ReadFrame(
        ReadOnlySpan<byte> source,
        PacketProtocolDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        PacketFrameHeader header =
            ReadHeader(
                source,
                definition);

        if (header.PacketLength > int.MaxValue)
        {
            throw new PacketBufferException(
                $"The declared packet length {header.PacketLength} exceeds the maximum managed span length supported by PacketWire.");
        }

        int packetLength =
            (int)header.PacketLength;

        if (source.Length != packetLength)
        {
            throw new PacketBufferException(
                $"The packet declares {packetLength} bytes, but the supplied frame contains {source.Length} bytes.");
        }

        ulong payloadLengthValue =
            GetPayloadLength(
                header,
                definition);

        if (payloadLengthValue > int.MaxValue)
        {
            throw new PacketBufferException(
                $"The packet payload length {payloadLengthValue} exceeds the maximum managed span length supported by PacketWire.");
        }

        int payloadLength =
            (int)payloadLengthValue;

        ReadOnlySpan<byte> payload =
            source.Slice(
                definition.HeaderLength,
                payloadLength);

        return new PacketFrameView(
            header,
            payload);
    }

    /// <summary>
    /// Validates that the provided frame flags contain only recognized flag bits before writing to the wire.
    /// </summary>
    /// <param name="flags">The frame options flags to validate.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="flags"/> contains unassigned or unsupported bits.</exception>
    private static void ValidateFlagsForWrite(
        PacketFrameOptions flags)
    {
        byte rawFlags =
            (byte)flags;

        if ((rawFlags & ~SupportedFlagsMask) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(flags),
                flags,
                $"Packet frame flags value 0x{rawFlags:X2} contains unsupported flag bits.");
        }
    }

    /// <summary>
    /// Validates raw flag bytes read from the wire, failing closed if any unsupported bits are set.
    /// </summary>
    /// <param name="rawFlags">The raw byte received from the wire.</param>
    /// <returns>The validated <see cref="PacketFrameOptions"/> value.</returns>
    /// <exception cref="PacketBufferException">Thrown when <paramref name="rawFlags"/> contains unsupported flag bits.</exception>
    private static PacketFrameOptions ValidateFlagsFromWire(
        byte rawFlags)
    {
        if ((rawFlags & ~SupportedFlagsMask) != 0)
        {
            throw new PacketBufferException(
                $"Packet frame flags value 0x{rawFlags:X2} contains unsupported flag bits.");
        }

        return (PacketFrameOptions)rawFlags;
    }

    /// <summary>
    /// Validates that a frame length to be written meets minimum header length and maximum protocol length constraints.
    /// </summary>
    /// <param name="packetLength">The frame length to validate.</param>
    /// <param name="definition">The protocol definition providing length boundaries.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="packetLength"/> is smaller than the header or exceeds protocol limits.</exception>
    private static void ValidatePacketLengthForWrite(
        ulong packetLength,
        PacketProtocolDefinition definition)
    {
        if (packetLength < (ulong)definition.HeaderLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(packetLength),
                packetLength,
                $"Packet length cannot be smaller than the protocol header length {definition.HeaderLength}.");
        }

        if (packetLength > definition.MaximumPacketLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(packetLength),
                packetLength,
                $"Packet length exceeds the protocol maximum value {definition.MaximumPacketLength}.");
        }
    }

    /// <summary>
    /// Validates that a frame length read from the wire meets minimum header length and maximum protocol length constraints.
    /// </summary>
    /// <param name="packetLength">The frame length read from the wire.</param>
    /// <param name="definition">The protocol definition providing length boundaries.</param>
    /// <exception cref="PacketBufferException">Thrown when <paramref name="packetLength"/> is smaller than the header or exceeds protocol limits.</exception>
    private static void ValidatePacketLengthFromWire(
        ulong packetLength,
        PacketProtocolDefinition definition)
    {
        if (packetLength < (ulong)definition.HeaderLength)
        {
            throw new PacketBufferException(
                $"The declared packet length {packetLength} is smaller than the protocol header length {definition.HeaderLength}.");
        }

        if (packetLength > definition.MaximumPacketLength)
        {
            throw new PacketBufferException(
                $"The declared packet length {packetLength} exceeds the protocol maximum value {definition.MaximumPacketLength}.");
        }
    }
}