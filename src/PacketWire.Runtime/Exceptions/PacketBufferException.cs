namespace PacketWire;

/// <summary>
/// Exception thrown when a packet buffer is corrupted, truncated, contains unexpected trailing bytes, or violates wire-format constraints.
/// </summary>
/// <remarks>
/// This exception occurs during binary serialization or deserialization when buffer boundaries are exceeded,
/// fixed strings contain non-zero padding, optional flags contain values other than 0 or 1, collection counts exceed limits,
/// or wire frame length headers do not match the actual buffer length.
/// </remarks>
public sealed class PacketBufferException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PacketBufferException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the buffer violation.</param>
    public PacketBufferException(string message)
        : base(message)
    {
    }
}
