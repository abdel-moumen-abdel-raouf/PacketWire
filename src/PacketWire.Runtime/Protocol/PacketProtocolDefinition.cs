namespace PacketWire;

/// <summary>
/// Encapsulates the immutable wire formatting, integer widths, byte endianness, and calculated header layout for a PacketWire protocol.
/// </summary>
/// <remarks>
/// A protocol definition specifies how packet frames are framed on the wire:
/// <c>PacketLength | Flags | PacketCategory | PacketId | Payload</c>.
/// The header length is precomputed based on the configured integer sizes:
/// <c>HeaderLength = LengthSize + 1 (Flags) + CategorySize + IdSize</c>.
/// Maximum numeric values for packet IDs, categories, frame lengths, and collection counts are also derived and enforced.
/// </remarks>
public sealed class PacketProtocolDefinition
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PacketProtocolDefinition"/> class with the specified integer sizes, byte order, and optional category size.
    /// </summary>
    /// <param name="packetIdSize">The wire byte width for packet identifiers.</param>
    /// <param name="packetLengthSize">The wire byte width for full frame lengths.</param>
    /// <param name="collectionCountSize">The wire byte width for collection count headers.</param>
    /// <param name="byteOrder">The byte endianness for multi-byte numeric primitives.</param>
    /// <param name="packetCategorySize">The wire byte width for packet category identifiers. Defaults to <see cref="PacketIntegerSize.OneByte"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when any <see cref="PacketIntegerSize"/> parameter is not <see cref="PacketIntegerSize.OneByte"/>, <see cref="PacketIntegerSize.TwoBytes"/>, <see cref="PacketIntegerSize.FourBytes"/>, or <see cref="PacketIntegerSize.EightBytes"/>,
    /// or when <paramref name="byteOrder"/> is not <see cref="PacketByteOrder.LittleEndian"/> or <see cref="PacketByteOrder.BigEndian"/>.
    /// </exception>
    public PacketProtocolDefinition(
        PacketIntegerSize packetIdSize,
        PacketIntegerSize packetLengthSize,
        PacketIntegerSize collectionCountSize,
        PacketByteOrder byteOrder,
        PacketIntegerSize packetCategorySize = PacketIntegerSize.OneByte)
    {
        ValidateIntegerSize(
            packetIdSize,
            nameof(packetIdSize));

        ValidateIntegerSize(
            packetLengthSize,
            nameof(packetLengthSize));

        ValidateIntegerSize(
            collectionCountSize,
            nameof(collectionCountSize));

        ValidateIntegerSize(
            packetCategorySize,
            nameof(packetCategorySize));

        if (byteOrder is not PacketByteOrder.LittleEndian and
            not PacketByteOrder.BigEndian)
        {
            throw new ArgumentOutOfRangeException(
                nameof(byteOrder),
                byteOrder,
                "The byte order must be LittleEndian or BigEndian.");
        }

        PacketIdSize = packetIdSize;
        PacketLengthSize = packetLengthSize;
        CollectionCountSize = collectionCountSize;
        PacketCategorySize = packetCategorySize;
        ByteOrder = byteOrder;

        HeaderLength =
            PacketIntegerCodec.GetByteCount(PacketLengthSize) +
            1 +
            PacketIntegerCodec.GetByteCount(PacketCategorySize) +
            PacketIntegerCodec.GetByteCount(PacketIdSize);

        MaximumPacketId =
            PacketIntegerCodec.GetMaximumValue(PacketIdSize);

        MaximumPacketCategory =
            PacketIntegerCodec.GetMaximumValue(PacketCategorySize);

        MaximumPacketLength =
            PacketIntegerCodec.GetMaximumValue(PacketLengthSize);

        MaximumCollectionCount =
            PacketIntegerCodec.GetMaximumValue(CollectionCountSize);
    }

    /// <summary>
    /// Gets the wire byte width allocated for packet identifiers.
    /// </summary>
    /// <value>A <see cref="PacketIntegerSize"/> indicating the byte width of packet IDs.</value>
    public PacketIntegerSize PacketIdSize { get; }

    /// <summary>
    /// Gets the wire byte width allocated for full frame lengths.
    /// </summary>
    /// <value>A <see cref="PacketIntegerSize"/> indicating the byte width of frame length headers.</value>
    public PacketIntegerSize PacketLengthSize { get; }

    /// <summary>
    /// Gets the wire byte width allocated for collection item count headers.
    /// </summary>
    /// <value>A <see cref="PacketIntegerSize"/> indicating the byte width of collection count fields.</value>
    public PacketIntegerSize CollectionCountSize { get; }

    /// <summary>
    /// Gets the wire byte width allocated for packet category identifiers.
    /// </summary>
    /// <value>A <see cref="PacketIntegerSize"/> indicating the byte width of category fields.</value>
    public PacketIntegerSize PacketCategorySize { get; }

    /// <summary>
    /// Gets the byte endianness used when encoding and decoding numeric primitives.
    /// </summary>
    /// <value>A <see cref="PacketByteOrder"/> specifying little-endian or big-endian encoding.</value>
    public PacketByteOrder ByteOrder { get; }

    /// <summary>
    /// Gets the precomputed total header length in bytes for frames in this protocol.
    /// </summary>
    /// <value>The exact number of bytes preceding the payload in every frame.</value>
    public int HeaderLength { get; }

    /// <summary>
    /// Gets the maximum permissible numeric value for packet identifiers based on <see cref="PacketIdSize"/>.
    /// </summary>
    /// <value>The maximum unsigned 64-bit integer that fits in the packet ID field.</value>
    public ulong MaximumPacketId { get; }

    /// <summary>
    /// Gets the maximum permissible numeric value for category identifiers based on <see cref="PacketCategorySize"/>.
    /// </summary>
    /// <value>The maximum unsigned 64-bit integer that fits in the category field.</value>
    public ulong MaximumPacketCategory { get; }

    /// <summary>
    /// Gets the maximum permissible total frame length in bytes based on <see cref="PacketLengthSize"/>.
    /// </summary>
    /// <value>The maximum unsigned 64-bit integer representing the maximum transmittable frame size.</value>
    public ulong MaximumPacketLength { get; }

    /// <summary>
    /// Gets the maximum permissible number of items in a collection based on <see cref="CollectionCountSize"/>.
    /// </summary>
    /// <value>The maximum unsigned 64-bit integer that can be represented in collection count headers.</value>
    public ulong MaximumCollectionCount { get; }

    /// <summary>
    /// Validates that the provided <see cref="PacketIntegerSize"/> represents a valid, supported integer size.
    /// </summary>
    /// <param name="size">The integer size to validate.</param>
    /// <param name="parameterName">The name of the parameter being validated.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/> is not 1, 2, 4, or 8 bytes.</exception>
    private static void ValidateIntegerSize(
        PacketIntegerSize size,
        string parameterName)
    {
        if (size is not PacketIntegerSize.OneByte and
            not PacketIntegerSize.TwoBytes and
            not PacketIntegerSize.FourBytes and
            not PacketIntegerSize.EightBytes)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                size,
                "The packet integer size must be OneByte, TwoBytes, FourBytes, or EightBytes.");
        }
    }
}
