using PacketWire;

namespace PacketWire.Abstractions.Tests;

/// <summary>
/// Verifies initialization and validation behavior of <see cref="PacketFieldAttribute"/>.
/// </summary>
public sealed class PacketFieldAttributeTests
{
    /// <summary>
    /// Verifies that zero is accepted and stored as a field order index.
    /// </summary>
    [Fact]
    public void ConstructorStoresZeroOrder()
    {
        PacketFieldAttribute attribute = new(0);

        Assert.Equal(0, attribute.Order);
    }

    /// <summary>
    /// Verifies that positive field order indices are stored correctly.
    /// </summary>
    [Fact]
    public void ConstructorStoresPositiveOrder()
    {
        PacketFieldAttribute attribute = new(25);

        Assert.Equal(25, attribute.Order);
    }

    /// <summary>
    /// Verifies that negative field order indices are rejected with <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void ConstructorRejectsNegativeOrder()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PacketFieldAttribute(-1));
    }
}
