using PacketWire;

namespace PacketWire.Consumer.Beta;

/// <summary>
/// Defines the Beta test protocol facade configuring four-byte length and identity fields, one-byte flags, big-endian byte order, and two-byte category.
/// </summary>
[PacketProtocol(
    PacketIntegerSize.FourBytes,
    PacketIntegerSize.FourBytes,
    PacketIntegerSize.OneByte,
    PacketByteOrder.BigEndian,
    PacketIntegerSize.TwoBytes)]
public sealed partial class ApplicationProtocol
{
}