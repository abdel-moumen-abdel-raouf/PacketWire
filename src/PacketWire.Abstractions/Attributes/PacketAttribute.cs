namespace PacketWire;

/// <summary>
/// Registers a class or struct as a packet message within a specific PacketWire protocol definition.
/// </summary>
/// <remarks>
/// Packet identity is protocol-scoped and formed by the composite pair of <see cref="Category"/> and <see cref="Id"/>.
/// The same category and packet ID combination can legitimately exist across different protocol types without conflict.
/// The assigned <see cref="Id"/> and <see cref="Category"/> values must not exceed the maximum values permitted by the
/// integer widths declared on the target <see cref="ProtocolType"/>.
/// </remarks>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct,
    AllowMultiple = false,
    Inherited = false)]
public sealed class PacketAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PacketAttribute"/> class with the specified protocol type, packet identifier, and optional category.
    /// </summary>
    /// <param name="protocolType">The type representing the protocol definition, which must be decorated with <see cref="PacketProtocolAttribute"/>.</param>
    /// <param name="id">The unique packet identifier within the protocol and category.</param>
    /// <param name="category">The protocol category under which the packet is partitioned. Defaults to <c>0</c>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="protocolType"/> is <see langword="null"/>.</exception>
    public PacketAttribute(
        Type protocolType,
        ulong id,
        ulong category = 0)
    {
        ArgumentNullException.ThrowIfNull(protocolType);

        ProtocolType = protocolType;
        Id = id;
        Category = category;
    }

    /// <summary>
    /// Gets the protocol definition type to which this packet contract belongs.
    /// </summary>
    /// <value>The <see cref="Type"/> of the owning protocol definition.</value>
    public Type ProtocolType { get; }

    /// <summary>
    /// Gets the numeric packet identifier within the protocol and category.
    /// </summary>
    /// <value>An unsigned 64-bit integer representing the packet identifier on the wire.</value>
    public ulong Id { get; }

    /// <summary>
    /// Gets the category partition to which this packet belongs.
    /// </summary>
    /// <value>An unsigned 64-bit integer representing the category identifier on the wire.</value>
    public ulong Category { get; }
}
