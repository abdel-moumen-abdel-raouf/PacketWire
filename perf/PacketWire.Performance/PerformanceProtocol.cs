using PacketWire;

namespace PacketWire.Performance;

/// <summary>
/// Defines the protocol facade used for benchmark measurements with two-byte header fields and little-endian byte order.
/// </summary>
[PacketProtocol(
    PacketIntegerSize.TwoBytes,
    PacketIntegerSize.TwoBytes,
    PacketIntegerSize.TwoBytes,
    PacketByteOrder.LittleEndian)]
internal sealed partial class PerformanceProtocol
{
}

/// <summary>
/// Represents a sample packet used in throughput and allocation benchmarks.
/// </summary>
[Packet(
    typeof(PerformanceProtocol),
    0x1234,
    3)]
internal sealed class PerformancePacket
{
    /// <summary>
    /// Gets the 32-bit integer payload value.
    /// </summary>
    [PacketField(0)]
    public int Number { get; init; }

    /// <summary>
    /// Gets the 64-bit sequence counter payload value.
    /// </summary>
    [PacketField(1)]
    public long Sequence { get; init; }

    /// <summary>
    /// Gets a value indicating whether the packet payload feature is enabled.
    /// </summary>
    [PacketField(2)]
    public bool Enabled { get; init; }
}