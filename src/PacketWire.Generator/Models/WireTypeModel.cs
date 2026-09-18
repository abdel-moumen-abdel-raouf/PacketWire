using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace PacketWire.Generator;

/// <summary>
/// Represents the semantic model of a packet or contract type including all ordered fields to be emitted into its codec.
/// </summary>
internal sealed class WireTypeModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WireTypeModel"/> class.
    /// </summary>
    /// <param name="wireType">The named type symbol for the packet or contract.</param>
    /// <param name="fields">The immutable list of ordered field models for serialization.</param>
    internal WireTypeModel(
        INamedTypeSymbol wireType,
        ImmutableArray<PacketFieldModel> fields)
    {
        WireType = wireType;
        Fields = fields;
    }

    /// <summary>
    /// Gets the type symbol representing the wire type.
    /// </summary>
    internal INamedTypeSymbol WireType { get; }

    /// <summary>
    /// Gets the immutable array of fields belonging to this wire type, ordered by their serialization indices.
    /// </summary>
    internal ImmutableArray<PacketFieldModel> Fields { get; }
}
