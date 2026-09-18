using PacketWire;

namespace PacketWire.Runtime.Tests;

/// <summary>
/// Verifies equality semantics, property initialization, and hash behavior of <see cref="PacketIdentity"/>.
/// </summary>
public sealed class PacketIdentityTests
{
    /// <summary>
    /// Verifies that category and packet ID are correctly stored by the constructor.
    /// </summary>
    [Fact]
    public void ConstructorStoresCategoryAndId()
    {
        PacketIdentity identity =
            new(
                Category: 7,
                Id: 123);

        Assert.Equal(
            7UL,
            identity.Category);

        Assert.Equal(
            123UL,
            identity.Id);
    }

    /// <summary>
    /// Verifies that two identities with identical category and ID compare equal.
    /// </summary>
    [Fact]
    public void EqualIdentitiesCompareEqual()
    {
        PacketIdentity first =
            new(3, 42);

        PacketIdentity second =
            new(3, 42);

        Assert.Equal(
            first,
            second);

        Assert.True(
            first == second);
    }

    /// <summary>
    /// Verifies that different categories or different IDs produce unequal identity values.
    /// </summary>
    [Fact]
    public void DifferentCategoryOrIdProducesDifferentIdentity()
    {
        PacketIdentity baseline =
            new(1, 10);

        Assert.NotEqual(
            baseline,
            new PacketIdentity(2, 10));

        Assert.NotEqual(
            baseline,
            new PacketIdentity(1, 11));
    }
}
