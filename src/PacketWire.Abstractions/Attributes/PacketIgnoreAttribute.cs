namespace PacketWire;

/// <summary>
/// Excludes a public property from PacketWire serialization, deserialization, and schema validation.
/// </summary>
/// <remarks>
/// By default, PacketWire requires every public instance property on a packet or contract to have explicit serialization
/// metadata (<see cref="PacketFieldAttribute"/>). Applying <see cref="PacketIgnoreAttribute"/> informs the source generator
/// that the property is intentionally unmapped and should be omitted from the wire representation.
/// </remarks>
[AttributeUsage(
    AttributeTargets.Property,
    AllowMultiple = false,
    Inherited = true)]
public sealed class PacketIgnoreAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PacketIgnoreAttribute"/> class.
    /// </summary>
    public PacketIgnoreAttribute()
    {
    }
}
