using PacketWire;

namespace PacketWire.Abstractions.Tests;

/// <summary>
/// Verifies argument validation and property initialization of <see cref="FixedStringAttribute"/>.
/// </summary>
public sealed class FixedStringAttributeTests
{
    /// <summary>
    /// Verifies that positive byte lengths are stored correctly.
    /// </summary>
    [Fact]
    public void ConstructorStoresPositiveByteLength()
    {
        FixedStringAttribute attribute = new(32);

        Assert.Equal(32, attribute.ByteLength);
    }

    /// <summary>
    /// Verifies that zero byte length is rejected with <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void ConstructorRejectsZeroByteLength()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new FixedStringAttribute(0));
    }

    /// <summary>
    /// Verifies that negative byte length is rejected with <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void ConstructorRejectsNegativeByteLength()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new FixedStringAttribute(-1));
    }
}
