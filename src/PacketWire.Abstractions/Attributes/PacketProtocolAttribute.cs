namespace PacketWire;

/// <summary>
/// Configures binary wire format framing, integer field widths, and byte endianness for a PacketWire protocol definition.
/// </summary>
/// <remarks>
/// Applying this attribute to a partial class designates it as a protocol definition facade. The source generator
/// emits high-performance dispatch, serialization, and deserialization methods on the decorated class according to the
/// integer widths and endianness specified here. Wire frames follow the structural layout:
/// <c>PacketLength | Flags | PacketCategory | PacketId | Payload</c>.
/// </remarks>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct,
    AllowMultiple = false,
    Inherited = false)]
public sealed class PacketProtocolAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PacketProtocolAttribute"/> class with the specified integer sizes, byte order, and optional category size.
    /// </summary>
    /// <param name="packetIdSize">The wire byte width for the packet identifier field (<c>1</c>, <c>2</c>, <c>4</c>, or <c>8</c> bytes).</param>
    /// <param name="packetLengthSize">The wire byte width for the full frame length field (<c>1</c>, <c>2</c>, <c>4</c>, or <c>8</c> bytes).</param>
    /// <param name="collectionCountSize">The wire byte width used for collection item count headers (<c>1</c>, <c>2</c>, <c>4</c>, or <c>8</c> bytes).</param>
    /// <param name="byteOrder">The byte endianness (<see cref="PacketByteOrder.LittleEndian"/> or <see cref="PacketByteOrder.BigEndian"/>) for all wire integers.</param>
    /// <param name="packetCategorySize">The wire byte width for the packet category field (<c>1</c>, <c>2</c>, <c>4</c>, or <c>8</c> bytes). Defaults to <see cref="PacketIntegerSize.OneByte"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when any <see cref="PacketIntegerSize"/> parameter is not a defined non-zero size (<see cref="PacketIntegerSize.OneByte"/>, <see cref="PacketIntegerSize.TwoBytes"/>, <see cref="PacketIntegerSize.FourBytes"/>, or <see cref="PacketIntegerSize.EightBytes"/>),
    /// or when <paramref name="byteOrder"/> is not <see cref="PacketByteOrder.LittleEndian"/> or <see cref="PacketByteOrder.BigEndian"/>.
    /// </exception>
    public PacketProtocolAttribute(
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
                nameof(byteOrder));
        }

        PacketIdSize = packetIdSize;
        PacketLengthSize = packetLengthSize;
        CollectionCountSize = collectionCountSize;
        PacketCategorySize = packetCategorySize;
        ByteOrder = byteOrder;
    }

    /// <summary>
    /// Gets the wire byte width allocated for packet identifiers in this protocol.
    /// </summary>
    /// <value>A <see cref="PacketIntegerSize"/> indicating whether packet IDs are 1, 2, 4, or 8 bytes.</value>
    public PacketIntegerSize PacketIdSize { get; }

    /// <summary>
    /// Gets the wire byte width allocated for the total packet length field in frame headers.
    /// </summary>
    /// <value>A <see cref="PacketIntegerSize"/> indicating whether total frame lengths are 1, 2, 4, or 8 bytes.</value>
    public PacketIntegerSize PacketLengthSize { get; }

    /// <summary>
    /// Gets the wire byte width allocated for collection element counts in serialized payloads.
    /// </summary>
    /// <value>A <see cref="PacketIntegerSize"/> indicating whether collection count headers are 1, 2, 4, or 8 bytes.</value>
    public PacketIntegerSize CollectionCountSize { get; }

    /// <summary>
    /// Gets the wire byte width allocated for the packet category partition field in frame headers.
    /// </summary>
    /// <value>A <see cref="PacketIntegerSize"/> indicating whether category IDs are 1, 2, 4, or 8 bytes.</value>
    public PacketIntegerSize PacketCategorySize { get; }

    /// <summary>
    /// Gets the endianness used for multi-byte numeric fields serialized under this protocol.
    /// </summary>
    /// <value>A <see cref="PacketByteOrder"/> value specifying little-endian or big-endian encoding.</value>
    public PacketByteOrder ByteOrder { get; }

    /// <summary>
    /// Validates that the provided <see cref="PacketIntegerSize"/> represents a supported integer width.
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
