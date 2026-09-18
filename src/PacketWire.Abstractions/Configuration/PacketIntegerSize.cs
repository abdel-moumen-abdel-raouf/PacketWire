namespace PacketWire;

/// <summary>
/// Specifies the wire byte width allocated for variable-width packet integer fields (packet IDs, frame lengths, collection counts, and category identifiers).
/// </summary>
public enum PacketIntegerSize
{
    /// <summary>
    /// Integer size is unspecified or invalid.
    /// </summary>
    Unspecified = 0,

    /// <summary>
    /// Encoded as an 8-bit unsigned byte (1 byte on the wire). Maximum value: <see cref="byte.MaxValue"/> (255).
    /// </summary>
    OneByte = 1,

    /// <summary>
    /// Encoded as a 16-bit unsigned integer (2 bytes on the wire). Maximum value: <see cref="ushort.MaxValue"/> (65,535).
    /// </summary>
    TwoBytes = 2,

    /// <summary>
    /// Encoded as a 32-bit unsigned integer (4 bytes on the wire). Maximum value: <see cref="uint.MaxValue"/> (4,294,967,295).
    /// </summary>
    FourBytes = 4,

    /// <summary>
    /// Encoded as a 64-bit unsigned integer (8 bytes on the wire). Maximum value: <see cref="ulong.MaxValue"/> (18,446,744,073,709,551,615).
    /// </summary>
    EightBytes = 8
}
