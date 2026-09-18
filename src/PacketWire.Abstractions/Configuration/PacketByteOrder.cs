namespace PacketWire;

/// <summary>
/// Specifies the byte ordering (endianness) used when encoding and decoding numeric primitives in PacketWire.
/// </summary>
public enum PacketByteOrder
{
    /// <summary>
    /// Byte order is unspecified or invalid.
    /// </summary>
    Unspecified = 0,

    /// <summary>
    /// Least significant byte first (little-endian wire encoding).
    /// </summary>
    LittleEndian = 1,

    /// <summary>
    /// Most significant byte first (network / big-endian wire encoding).
    /// </summary>
    BigEndian = 2
}
