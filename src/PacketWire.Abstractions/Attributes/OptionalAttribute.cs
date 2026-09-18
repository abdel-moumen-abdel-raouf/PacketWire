namespace PacketWire;

/// <summary>
/// Marks a nullable property as an optional field in a PacketWire packet or contract.
/// </summary>
/// <remarks>
/// Optional fields are preceded on the wire by an explicit 1-byte presence marker (<c>0x01</c> for present, <c>0x00</c>
/// for absent). Any other value on the wire fails closed with a buffer exception. When present, the underlying value
/// is serialized immediately after the marker; when absent, no payload bytes are written or read for that field.
/// Properties decorated with this attribute must have a nullable reference or value type.
/// </remarks>
[AttributeUsage(
    AttributeTargets.Property,
    AllowMultiple = false,
    Inherited = true)]
public sealed class OptionalAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OptionalAttribute"/> class.
    /// </summary>
    public OptionalAttribute()
    {
    }
}
