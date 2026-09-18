namespace PacketWire;

/// <summary>
/// Exception thrown during frame dispatch or deserialization when a packet identity (category and packet ID) is not registered in the target protocol.
/// </summary>
/// <remarks>
/// Packet identity is protocol-scoped. If a frame arriving on the wire carries a <see cref="PacketIdentity"/> that has no
/// corresponding registered packet type in the protocol facade or registry, this exception is thrown fail-closed.
/// </remarks>
public sealed class PacketIdentityNotRegisteredException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PacketIdentityNotRegisteredException"/> class with the unregistered packet identity.
    /// </summary>
    /// <param name="identity">The unknown packet identity received from the wire frame header.</param>
    public PacketIdentityNotRegisteredException(
        PacketIdentity identity)
        : base(
            $"Packet identity Category={identity.Category}, Id={identity.Id} is not registered in the selected protocol.")
    {
        Identity = identity;
    }

    /// <summary>
    /// Gets the unregistered packet identity that caused the exception.
    /// </summary>
    /// <value>A <see cref="PacketIdentity"/> containing the category and ID that failed resolution.</value>
    public PacketIdentity Identity { get; }
}
