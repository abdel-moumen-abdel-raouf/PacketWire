using PacketWire;

namespace PacketWire.Consumer;

/// <summary>
/// Defines the primary protocol facade used in integration tests, configuring two-byte header fields and little-endian wire framing.
/// </summary>
[PacketProtocol(
    PacketIntegerSize.TwoBytes,
    PacketIntegerSize.TwoBytes,
    PacketIntegerSize.TwoBytes,
    PacketByteOrder.LittleEndian)]
public sealed partial class PrimaryProtocol
{
}