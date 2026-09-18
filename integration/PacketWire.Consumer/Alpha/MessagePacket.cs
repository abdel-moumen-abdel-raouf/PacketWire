using PacketWire;

namespace PacketWire.Consumer.Alpha;

/// <summary>
/// Represents a message packet payload registered under the Alpha protocol.
/// </summary>
[Packet(
    typeof(ApplicationProtocol),
    0x2222,
    7)]
public sealed class MessagePacket
{
    /// <summary>
    /// Gets the payload integer value.
    /// </summary>
    [PacketField(0)]
    public int Value { get; init; }
}