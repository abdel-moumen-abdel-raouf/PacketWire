namespace PacketWire;

/// <summary>
/// Specifies the serialization order and inclusion of a property within a PacketWire packet or contract.
/// </summary>
/// <remarks>
/// Properties marked with <see cref="PacketFieldAttribute"/> are serialized strictly in ascending order of their
/// <see cref="Order"/> values. Orders must be unique and non-negative within each contract type. Unmarked public
/// instance properties are rejected at compile time by the source generator unless decorated with <see cref="PacketIgnoreAttribute"/>.
/// </remarks>
[AttributeUsage(
    AttributeTargets.Property,
    AllowMultiple = false,
    Inherited = true)]
public sealed class PacketFieldAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PacketFieldAttribute"/> class with the specified serialization order.
    /// </summary>
    /// <param name="order">The non-negative serialization order index for the property on the wire.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="order"/> is negative.</exception>
    public PacketFieldAttribute(int order)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(order);

        Order = order;
    }

    /// <summary>
    /// Gets the non-negative serialization order index for the property.
    /// </summary>
    /// <value>A non-negative integer indicating the position of this field relative to other fields in the contract.</value>
    public int Order { get; }
}
