using PacketWire;

namespace PacketWire.Abstractions.Tests;

/// <summary>
/// Verifies the numeric representations and stability of the <see cref="PacketByteOrder"/> enumeration.
/// </summary>
public sealed class PacketByteOrderTests
{
    /// <summary>
    /// Verifies that each enum member maps to its expected constant numeric value.
    /// </summary>
    [Fact]
    public void DefinedValuesHaveStableNumericRepresentations()
    {
        Assert.Equal(0, (int)PacketByteOrder.Unspecified);
        Assert.Equal(1, (int)PacketByteOrder.LittleEndian);
        Assert.Equal(2, (int)PacketByteOrder.BigEndian);
    }
}
