namespace PacketWire;

/// <summary>
/// Represents the unique, protocol-scoped composite identity of a packet, consisting of a category and a packet identifier.
/// </summary>
/// <param name="Category">The category partition identifier for the packet.</param>
/// <param name="Id">The numeric packet identifier within the category.</param>
/// <remarks>
/// Packet identity is protocol-scoped. The same category and packet ID values can legitimately coexist in different
/// protocol definitions without ambiguity.
/// </remarks>
public readonly record struct PacketIdentity(
    ulong Category,
    ulong Id);
