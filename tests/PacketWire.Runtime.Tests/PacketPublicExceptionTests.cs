using PacketWire;

namespace PacketWire.Runtime.Tests;

/// <summary>
/// Verifies property assignments and state retention of public PacketWire exception classes.
/// </summary>
public sealed class PacketPublicExceptionTests
{
    /// <summary>
    /// Verifies that <see cref="PacketTypeNotRegisteredException"/> preserves the unregistered packet <see cref="Type"/>.
    /// </summary>
    [Fact]
    public void PacketTypeNotRegisteredExceptionStoresType()
    {
        PacketTypeNotRegisteredException exception =
            new(typeof(string));

        Assert.Equal(
            typeof(string),
            exception.PacketType);
    }

    /// <summary>
    /// Verifies that <see cref="PacketIdentityNotRegisteredException"/> preserves the unregistered <see cref="PacketIdentity"/>.
    /// </summary>
    [Fact]
    public void PacketIdentityNotRegisteredExceptionStoresIdentity()
    {
        PacketIdentity identity =
            new(3, 100);

        PacketIdentityNotRegisteredException exception =
            new(identity);

        Assert.Equal(
            identity,
            exception.Identity);
    }

    /// <summary>
    /// Verifies that <see cref="PacketTypeMismatchException"/> preserves both the expected and actual packet types.
    /// </summary>
    [Fact]
    public void PacketTypeMismatchExceptionStoresExpectedAndActualTypes()
    {
        PacketTypeMismatchException exception =
            new(
                typeof(string),
                typeof(int));

        Assert.Equal(
            typeof(string),
            exception.ExpectedType);

        Assert.Equal(
            typeof(int),
            exception.ActualType);
    }
}
