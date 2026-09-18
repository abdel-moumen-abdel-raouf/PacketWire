using PacketWire;

namespace PacketWire.Abstractions.Tests;

/// <summary>
/// Verifies initialization and validation behavior of <see cref="MaxCountAttribute"/>.
/// </summary>
public sealed class MaxCountAttributeTests
{
    /// <summary>
    /// Verifies that zero is accepted as a valid maximum count.
    /// </summary>
    [Fact]
    public void ConstructorAllowsZeroMaximumCount()
    {
        MaxCountAttribute attribute = new(0);

        Assert.Equal(0, attribute.MaxCount);
    }

    /// <summary>
    /// Verifies that positive counts are stored correctly.
    /// </summary>
    [Fact]
    public void ConstructorStoresPositiveMaximumCount()
    {
        MaxCountAttribute attribute = new(500);

        Assert.Equal(500, attribute.MaxCount);
    }

    /// <summary>
    /// Verifies that negative counts are rejected with <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void ConstructorRejectsNegativeMaximumCount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new MaxCountAttribute(-1));
    }
}
