using PacketWire;

namespace PacketWire.Abstractions.Tests;

/// <summary>
/// Verifies initialization and validation behavior of <see cref="PacketAttribute"/>.
/// </summary>
public sealed class PacketAttributeTests
{
    /// <summary>
    /// Verifies that the protocol type, packet ID, and default category are assigned correctly.
    /// </summary>
    [Fact]
    public void ConstructorStoresProtocolTypePacketIdAndDefaultCategory()
    {
        PacketAttribute attribute = new(
            typeof(TestProtocol),
            123UL);

        Assert.Equal(
            typeof(TestProtocol),
            attribute.ProtocolType);

        Assert.Equal(
            123UL,
            attribute.Id);

        Assert.Equal(
            0UL,
            attribute.Category);
    }

    /// <summary>
    /// Verifies that an explicit category value is preserved.
    /// </summary>
    [Fact]
    public void ConstructorStoresExplicitCategory()
    {
        PacketAttribute attribute = new(
            typeof(TestProtocol),
            123UL,
            7UL);

        Assert.Equal(
            7UL,
            attribute.Category);
    }

    /// <summary>
    /// Verifies that maximum 64-bit integer values are accepted for packet ID and category metadata.
    /// </summary>
    [Fact]
    public void ConstructorAllowsMaximumPacketIdAndCategoryMetadataValues()
    {
        PacketAttribute attribute = new(
            typeof(TestProtocol),
            ulong.MaxValue,
            ulong.MaxValue);

        Assert.Equal(
            ulong.MaxValue,
            attribute.Id);

        Assert.Equal(
            ulong.MaxValue,
            attribute.Category);
    }

    /// <summary>
    /// Verifies that passing a null protocol type throws <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void ConstructorRejectsNullProtocolType()
    {
        Assert.Throws<ArgumentNullException>(
            () => new PacketAttribute(
                null!,
                1UL));
    }

    /// <summary>
    /// Mock protocol type used to construct test attribute instances.
    /// </summary>
    private sealed class TestProtocol
    {
    }
}
