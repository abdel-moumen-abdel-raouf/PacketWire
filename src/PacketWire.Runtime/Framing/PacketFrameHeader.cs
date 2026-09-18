namespace PacketWire;

/// <summary>
/// Represents the parsed framing header fields for a PacketWire packet frame.
/// </summary>
/// <param name="PacketLength">The total length of the frame in bytes, including header and payload.</param>
/// <param name="Flags">Bitwise frame options controlling security and encoding features (e.g. <see cref="PacketFrameOptions.Protected"/>).</param>
/// <param name="PacketCategory">The protocol category identifier partitioning the packet namespace.</param>
/// <param name="PacketId">The unique packet identifier within the protocol category.</param>
public readonly record struct PacketFrameHeader(
    ulong PacketLength,
    PacketFrameOptions Flags,
    ulong PacketCategory,
    ulong PacketId)
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PacketFrameHeader"/> struct with default (<see cref="PacketFrameOptions.None"/>) frame options.
    /// </summary>
    /// <param name="packetLength">The total length of the frame in bytes, including header and payload.</param>
    /// <param name="packetCategory">The protocol category identifier partitioning the packet namespace.</param>
    /// <param name="packetId">The unique packet identifier within the protocol category.</param>
    public PacketFrameHeader(
        ulong packetLength,
        ulong packetCategory,
        ulong packetId)
        : this(
            packetLength,
            PacketFrameOptions.None,
            packetCategory,
            packetId)
    {
    }

    /// <summary>
    /// Deconstructs the frame header into its length, category, and packet identifier components.
    /// </summary>
    /// <param name="packetLength">Receives the total frame length in bytes.</param>
    /// <param name="packetCategory">Receives the protocol category identifier.</param>
    /// <param name="packetId">Receives the packet identifier.</param>
    public void Deconstruct(
        out ulong packetLength,
        out ulong packetCategory,
        out ulong packetId)
    {
        packetLength = PacketLength;
        packetCategory = PacketCategory;
        packetId = PacketId;
    }
}