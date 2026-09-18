using PacketWire;

namespace PacketWire.Runtime.Tests;

/// <summary>
/// Verifies property initialization and error message formatting of <see cref="PacketProtectionRequiredException"/>.
/// </summary>
public sealed class PacketProtectionRequiredExceptionTests
{
    /// <summary>
    /// Verifies that constructing the exception with frame options stores the options and includes the enum name in the message.
    /// </summary>
    [Fact]
    public void OptionsConstructorStoresFrameOptions()
    {
        PacketProtectionRequiredException exception =
            new(
                PacketFrameOptions.Protected);

        Assert.Equal(
            PacketFrameOptions.Protected,
            exception.Options);

        Assert.Contains(
            nameof(PacketFrameOptions.Protected),
            exception.Message,
            StringComparison.Ordinal);
    }
}