using PacketWire;

namespace PacketWire.Abstractions.Tests;

/// <summary>
/// Verifies the numeric representations and byte widths corresponding to the <see cref="PacketIntegerSize"/> enumeration.
/// </summary>
public sealed class PacketIntegerSizeTests
{
    /// <summary>
    /// Verifies that each enum member maps to its expected byte count numeric value.
    /// </summary>
    [Fact]
    public void DefinedValuesHaveStableNumericRepresentations()
    {
        Assert.Equal(0, (int)PacketIntegerSize.Unspecified);
        Assert.Equal(1, (int)PacketIntegerSize.OneByte);
        Assert.Equal(2, (int)PacketIntegerSize.TwoBytes);
        Assert.Equal(4, (int)PacketIntegerSize.FourBytes);
        Assert.Equal(8, (int)PacketIntegerSize.EightBytes);
    }
}
