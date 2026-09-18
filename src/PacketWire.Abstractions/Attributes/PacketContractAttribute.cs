namespace PacketWire;

/// <summary>
/// Designates a nested complex type (class or struct) as a reusable PacketWire wire contract.
/// </summary>
/// <remarks>
/// Types decorated with <see cref="PacketContractAttribute"/> can be embedded as properties inside packets
/// or other contracts. All serialized properties on the contract must be ordered using <see cref="PacketFieldAttribute"/>
/// or explicitly excluded with <see cref="PacketIgnoreAttribute"/>. Unlike root packets, nested contracts do not have
/// independent packet identities or framing headers; their payload is serialized inline.
/// </remarks>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct,
    AllowMultiple = false,
    Inherited = false)]
public sealed class PacketContractAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PacketContractAttribute"/> class.
    /// </summary>
    public PacketContractAttribute()
    {
    }
}
