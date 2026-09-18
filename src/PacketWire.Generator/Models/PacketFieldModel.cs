using Microsoft.CodeAnalysis;

namespace PacketWire.Generator;

/// <summary>
/// Represents semantic and structural metadata for an individual packet field extracted from a contract property.
/// </summary>
internal sealed class PacketFieldModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PacketFieldModel"/> class.
    /// </summary>
    /// <param name="property">The property symbol representing the field.</param>
    /// <param name="order">The serialization order index.</param>
    /// <param name="shape">The classified structural shape of the field.</param>
    /// <param name="isOptional">Indicates whether the field is marked as optional.</param>
    /// <param name="fixedStringByteLength">The wire byte length for fixed strings, or <see langword="null"/> if not a fixed string.</param>
    /// <param name="maximumCount">The maximum permissible collection element count, or <see langword="null"/> if unconstrained.</param>
    /// <param name="valueType">The resolved type symbol of the property value.</param>
    /// <param name="elementType">The element type symbol if the field is a collection; otherwise <see langword="null"/>.</param>
    internal PacketFieldModel(
        IPropertySymbol property,
        int order,
        PacketFieldShape shape,
        bool isOptional,
        int? fixedStringByteLength,
        int? maximumCount,
        ITypeSymbol valueType,
        ITypeSymbol? elementType)
    {
        Property = property;
        Order = order;
        Shape = shape;
        IsOptional = isOptional;
        FixedStringByteLength = fixedStringByteLength;
        MaximumCount = maximumCount;
        ValueType = valueType;
        ElementType = elementType;
    }

    /// <summary>
    /// Gets the property symbol for this field.
    /// </summary>
    internal IPropertySymbol Property { get; }

    /// <summary>
    /// Gets the serialization order index.
    /// </summary>
    internal int Order { get; }

    /// <summary>
    /// Gets the classified structural shape of this field.
    /// </summary>
    internal PacketFieldShape Shape { get; }

    /// <summary>
    /// Gets a value indicating whether this field is marked optional with <c>OptionalAttribute</c>.
    /// </summary>
    internal bool IsOptional { get; }

    /// <summary>
    /// Gets the declared fixed byte length for string fields, or <see langword="null"/>.
    /// </summary>
    internal int? FixedStringByteLength { get; }

    /// <summary>
    /// Gets the declared maximum item count for collection fields, or <see langword="null"/>.
    /// </summary>
    internal int? MaximumCount { get; }

    /// <summary>
    /// Gets the type symbol of the field's value.
    /// </summary>
    internal ITypeSymbol ValueType { get; }

    /// <summary>
    /// Gets the item type symbol when <see cref="Shape"/> is <see cref="PacketFieldShape.Sequence"/>; otherwise <see langword="null"/>.
    /// </summary>
    internal ITypeSymbol? ElementType { get; }
}
