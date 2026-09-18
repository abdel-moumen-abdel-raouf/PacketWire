using PacketWire;

namespace PacketWire.Consumer.Alpha;

/// <summary>
/// Defines the Alpha test protocol facade with distinct framing sizes including a one-byte flags field and two-byte identifiers.
/// </summary>
[PacketProtocol(
    PacketIntegerSize.TwoBytes,
    PacketIntegerSize.TwoBytes,
    PacketIntegerSize.TwoBytes,
    PacketByteOrder.LittleEndian,
    PacketIntegerSize.OneByte)]
public sealed partial class ApplicationProtocol
{
}