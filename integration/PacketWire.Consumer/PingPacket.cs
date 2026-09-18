using PacketWire;

namespace PacketWire.Consumer;

/// <summary>
/// Represents a sample ping packet used in consumer integration tests and protocol framing verifications.
/// </summary>
[Packet(
    typeof(PrimaryProtocol),
    0x1001,
    1)]
public sealed class PingPacket
{
    /// <summary>
    /// Gets the unique identifier of the ping request.
    /// </summary>
    [PacketField(0)]
    public int RequestId { get; init; }

    /// <summary>
    /// Gets the timestamp when the ping request was generated.
    /// </summary>
    [PacketField(1)]
    public long Timestamp { get; init; }

    /// <summary>
    /// Gets a value indicating whether the recipient must send an acknowledgment or reply.
    /// </summary>
    [PacketField(2)]
    public bool RequiresReply { get; init; }
}