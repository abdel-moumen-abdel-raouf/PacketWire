using Microsoft.CodeAnalysis;

namespace PacketWire.Generator;

/// <summary>
/// Represents a packet type candidate discovered during source analysis decorated with <c>PacketAttribute</c>.
/// </summary>
internal sealed class PacketCandidate
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PacketCandidate"/> class.
    /// </summary>
    /// <param name="packetType">The symbol representing the packet class or struct.</param>
    /// <param name="protocolType">The symbol representing the associated protocol definition.</param>
    /// <param name="packetId">The packet identifier parsed from the attribute.</param>
    /// <param name="packetCategory">The packet category partition parsed from the attribute.</param>
    /// <param name="location">The source code location for diagnostic reporting.</param>
    internal PacketCandidate(
        INamedTypeSymbol packetType,
        INamedTypeSymbol protocolType,
        ulong packetId,
        ulong packetCategory,
        Location location)
    {
        PacketType = packetType;
        ProtocolType = protocolType;
        PacketId = packetId;
        PacketCategory = packetCategory;
        Location = location;
    }

    /// <summary>
    /// Gets the symbol for the packet type.
    /// </summary>
    internal INamedTypeSymbol PacketType { get; }

    /// <summary>
    /// Gets the symbol for the protocol definition class.
    /// </summary>
    internal INamedTypeSymbol ProtocolType { get; }

    /// <summary>
    /// Gets the numeric packet identifier.
    /// </summary>
    internal ulong PacketId { get; }

    /// <summary>
    /// Gets the category partition identifier.
    /// </summary>
    internal ulong PacketCategory { get; }

    /// <summary>
    /// Gets the primary source code location of the packet declaration.
    /// </summary>
    internal Location Location { get; }
}
