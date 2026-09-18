namespace PacketWire;

/// <summary>
/// Provides a zero-copy stack-only view over a decoded packet frame, separating its parsed header from its payload slice.
/// </summary>
public readonly ref struct PacketFrameView
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PacketFrameView"/> struct with the specified header and payload span.
    /// </summary>
    /// <param name="header">The parsed frame header fields.</param>
    /// <param name="payload">The slice of the buffer containing the packet payload bytes.</param>
    public PacketFrameView(
        PacketFrameHeader header,
        ReadOnlySpan<byte> payload)
    {
        Header = header;
        Payload = payload;
    }

    /// <summary>
    /// Gets the parsed frame header containing length, flags, category, and packet ID.
    /// </summary>
    /// <value>A <see cref="PacketFrameHeader"/> containing header metadata.</value>
    public PacketFrameHeader Header { get; }

    /// <summary>
    /// Gets the contiguous read-only span containing the packet payload bytes (excluding the header).
    /// </summary>
    /// <value>A <see cref="ReadOnlySpan{T}"/> representing the payload portion of the frame.</value>
    public ReadOnlySpan<byte> Payload { get; }

    /// <summary>
    /// Gets the length, in bytes, of the payload span.
    /// </summary>
    /// <value>The number of bytes in <see cref="Payload"/>.</value>
    public int PayloadLength => Payload.Length;
}
