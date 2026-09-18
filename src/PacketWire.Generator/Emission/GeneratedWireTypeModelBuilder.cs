using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace PacketWire.Generator;

/// <summary>
/// Extracts, analyzes, and orders properties on a packet or contract symbol to produce a <see cref="WireTypeModel"/>.
/// </summary>
internal static class GeneratedWireTypeModelBuilder
{
    /// <summary>
    /// Attempts to build a complete wire type model for the specified packet or contract type symbol.
    /// </summary>
    /// <param name="wireType">The type symbol to inspect.</param>
    /// <param name="model">When this method returns <see langword="true"/>, contains the constructed <see cref="WireTypeModel"/>; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the wire type model was successfully built; otherwise <see langword="false"/>.</returns>
    internal static bool TryBuild(
        INamedTypeSymbol wireType,
        out WireTypeModel? model)
    {
        List<PacketFieldModel> fields =
            new();

        HashSet<int> orders =
            new();

        foreach (
            IPropertySymbol property
            in wireType
                .GetMembers()
                .OfType<IPropertySymbol>())
        {
            AttributeData? fieldAttribute =
                FindAttribute(
                    property,
                    GeneratorMetadataNames.PacketFieldAttribute);

            AttributeData? ignoreAttribute =
                FindAttribute(
                    property,
                    GeneratorMetadataNames.PacketIgnoreAttribute);

            AttributeData? fixedStringAttribute =
                FindAttribute(
                    property,
                    GeneratorMetadataNames.FixedStringAttribute);

            AttributeData? optionalAttribute =
                FindAttribute(
                    property,
                    GeneratorMetadataNames.OptionalAttribute);

            AttributeData? maxCountAttribute =
                FindAttribute(
                    property,
                    GeneratorMetadataNames.MaxCountAttribute);

            bool hasSerializationMetadata =
                fieldAttribute is not null ||
                fixedStringAttribute is not null ||
                optionalAttribute is not null ||
                maxCountAttribute is not null;

            if (property.IsStatic)
            {
                if (hasSerializationMetadata)
                {
                    model = null;
                    return false;
                }

                continue;
            }

            if (property.DeclaredAccessibility
                != Accessibility.Public)
            {
                if (hasSerializationMetadata)
                {
                    model = null;
                    return false;
                }

                continue;
            }

            if (fieldAttribute is null &&
                ignoreAttribute is null)
            {
                model = null;
                return false;
            }

            if (ignoreAttribute is not null)
            {
                if (hasSerializationMetadata)
                {
                    model = null;
                    return false;
                }

                continue;
            }

            if (property.IsIndexer ||
                !HasAccessibleGetterAndSetter(property) ||
                !TryGetIntConstructorArgument(
                    fieldAttribute!,
                    out int order) ||
                order < 0 ||
                !orders.Add(order))
            {
                model = null;
                return false;
            }

            bool isOptional =
                optionalAttribute is not null;

            bool isNullable =
                IsNullableType(
                    property.Type);

            if (isOptional != isNullable)
            {
                model = null;
                return false;
            }

            ITypeSymbol effectiveType =
                UnwrapNullableValueType(
                    property.Type);

            int? fixedStringByteLength =
                GetOptionalIntConstructorArgument(
                    fixedStringAttribute);

            int? maximumCount =
                GetOptionalIntConstructorArgument(
                    maxCountAttribute);

            if (TryGetCollectionElementType(
                    effectiveType,
                    out ITypeSymbol? elementType))
            {
                if (elementType is null ||
                    IsNullableType(elementType))
                {
                    model = null;
                    return false;
                }

                ITypeSymbol effectiveElementType =
                    UnwrapNullableValueType(
                        elementType);

                if (!IsSupportedCollectionElement(
                        effectiveElementType,
                        fixedStringByteLength))
                {
                    model = null;
                    return false;
                }

                fields.Add(
                    new PacketFieldModel(
                        property,
                        order,
                        PacketFieldShape.Sequence,
                        isOptional,
                        fixedStringByteLength,
                        maximumCount,
                        effectiveType,
                        effectiveElementType));

                continue;
            }

            if (maximumCount.HasValue)
            {
                model = null;
                return false;
            }

            if (effectiveType.SpecialType
                == SpecialType.System_String)
            {
                if (!fixedStringByteLength.HasValue ||
                    fixedStringByteLength.Value <= 0)
                {
                    model = null;
                    return false;
                }

                fields.Add(
                    new PacketFieldModel(
                        property,
                        order,
                        PacketFieldShape.FixedText,
                        isOptional,
                        fixedStringByteLength,
                        null,
                        effectiveType,
                        null));

                continue;
            }

            if (fixedStringByteLength.HasValue)
            {
                model = null;
                return false;
            }

            if (IsSupportedPrimitive(
                    effectiveType))
            {
                fields.Add(
                    new PacketFieldModel(
                        property,
                        order,
                        PacketFieldShape.Scalar,
                        isOptional,
                        null,
                        null,
                        effectiveType,
                        null));

                continue;
            }

            if (effectiveType.TypeKind
                == TypeKind.Enum)
            {
                fields.Add(
                    new PacketFieldModel(
                        property,
                        order,
                        PacketFieldShape.EnumerationValue,
                        isOptional,
                        null,
                        null,
                        effectiveType,
                        null));

                continue;
            }

            if (HasPacketContract(
                    effectiveType))
            {
                fields.Add(
                    new PacketFieldModel(
                        property,
                        order,
                        PacketFieldShape.NestedContract,
                        isOptional,
                        null,
                        null,
                        effectiveType,
                        null));

                continue;
            }

            model = null;
            return false;
        }

        model =
            new WireTypeModel(
                wireType,
                fields
                    .OrderBy(
                        static field =>
                            field.Order)
                    .ToImmutableArray());

        return true;
    }

    /// <summary>
    /// Determines whether the specified element type is supported as an item in a serialized collection.
    /// </summary>
    /// <param name="elementType">The element type symbol.</param>
    /// <param name="fixedStringByteLength">Configured fixed string byte length, if applicable.</param>
    /// <returns><see langword="true"/> if supported; otherwise, <see langword="false"/>.</returns>
    private static bool IsSupportedCollectionElement(
        ITypeSymbol elementType,
        int? fixedStringByteLength)
    {
        if (TryGetCollectionElementType(
                elementType,
                out _))
        {
            return false;
        }

        if (elementType.SpecialType
            == SpecialType.System_String)
        {
            return
                fixedStringByteLength.HasValue &&
                fixedStringByteLength.Value > 0;
        }

        if (fixedStringByteLength.HasValue)
        {
            return false;
        }

        return
            IsSupportedPrimitive(elementType) ||
            elementType.TypeKind == TypeKind.Enum ||
            HasPacketContract(elementType);
    }

    /// <summary>
    /// Checks whether the property has accessible getter and setter accessors.
    /// </summary>
    /// <param name="property">The property symbol to inspect.</param>
    /// <returns><see langword="true"/> if both accessors are accessible; otherwise, <see langword="false"/>.</returns>
    private static bool HasAccessibleGetterAndSetter(
        IPropertySymbol property)
    {
        return
            IsAccessible(property.GetMethod) &&
            IsAccessible(property.SetMethod);
    }

    /// <summary>
    /// Checks whether an accessor method is accessible to generated code.
    /// </summary>
    /// <param name="accessor">The method symbol representing the getter or setter.</param>
    /// <returns><see langword="true"/> if public, internal, or protected internal; otherwise, <see langword="false"/>.</returns>
    private static bool IsAccessible(
        IMethodSymbol? accessor)
    {
        return accessor is not null &&
            accessor.DeclaredAccessibility
                is Accessibility.Public
                or Accessibility.Internal
                or Accessibility.ProtectedOrInternal;
    }

    /// <summary>
    /// Checks whether the type symbol represents one of the supported scalar primitives.
    /// </summary>
    /// <param name="type">The type symbol to check.</param>
    /// <returns><see langword="true"/> if primitive; otherwise, <see langword="false"/>.</returns>
    private static bool IsSupportedPrimitive(
        ITypeSymbol type)
    {
        return type.SpecialType
            is SpecialType.System_Byte
            or SpecialType.System_SByte
            or SpecialType.System_Int16
            or SpecialType.System_UInt16
            or SpecialType.System_Int32
            or SpecialType.System_UInt32
            or SpecialType.System_Int64
            or SpecialType.System_UInt64
            or SpecialType.System_Single
            or SpecialType.System_Double
            or SpecialType.System_Boolean;
    }

    /// <summary>
    /// Attempts to extract the element type symbol if the given type is a 1-dimensional array or a generic <see cref="List{T}"/>.
    /// </summary>
    /// <param name="type">The type symbol to inspect.</param>
    /// <param name="elementType">When this method returns <see langword="true"/>, receives the element type symbol.</param>
    /// <returns><see langword="true"/> if the type is a recognized collection; otherwise, <see langword="false"/>.</returns>
    private static bool TryGetCollectionElementType(
        ITypeSymbol type,
        out ITypeSymbol? elementType)
    {
        if (type is IArrayTypeSymbol arrayType &&
            arrayType.Rank == 1)
        {
            elementType =
                arrayType.ElementType;

            return true;
        }

        if (type is INamedTypeSymbol namedType &&
            namedType.IsGenericType &&
            namedType.TypeArguments.Length == 1 &&
            namedType.OriginalDefinition.MetadataName
                == "List`1" &&
            namedType.OriginalDefinition.ContainingNamespace
                .ToDisplayString()
                == "System.Collections.Generic")
        {
            elementType =
                namedType.TypeArguments[0];

            return true;
        }

        elementType = null;
        return false;
    }

    /// <summary>
    /// Determines whether the type is a nullable reference type or a <see cref="System.Nullable{T}"/> value type.
    /// </summary>
    /// <param name="type">The type symbol to check.</param>
    /// <returns><see langword="true"/> if nullable; otherwise, <see langword="false"/>.</returns>
    private static bool IsNullableType(
        ITypeSymbol type)
    {
        if (type.IsReferenceType)
        {
            return type.NullableAnnotation
                == NullableAnnotation.Annotated;
        }

        return type is INamedTypeSymbol namedType &&
            IsNullableValueType(namedType);
    }

    /// <summary>
    /// Unwraps the underlying value type if the symbol is a <see cref="System.Nullable{T}"/>.
    /// </summary>
    /// <param name="type">The type symbol.</param>
    /// <returns>The unwrapped underlying type, or the original type if not <see cref="System.Nullable{T}"/>.</returns>
    private static ITypeSymbol UnwrapNullableValueType(
        ITypeSymbol type)
    {
        if (type is INamedTypeSymbol namedType &&
            IsNullableValueType(namedType))
        {
            return namedType.TypeArguments[0];
        }

        return type;
    }

    /// <summary>
    /// Checks whether the type is an instance of <see cref="System.Nullable{T}"/>.
    /// </summary>
    /// <param name="type">The named type symbol to check.</param>
    /// <returns><see langword="true"/> if <see cref="System.Nullable{T}"/>; otherwise, <see langword="false"/>.</returns>
    private static bool IsNullableValueType(
        INamedTypeSymbol type)
    {
        return
            type.IsGenericType &&
            type.TypeArguments.Length == 1 &&
            type.OriginalDefinition.MetadataName
                == "Nullable`1" &&
            type.OriginalDefinition.ContainingNamespace
                .ToDisplayString()
                == "System";
    }

    /// <summary>
    /// Checks whether the type symbol is decorated with <c>PacketContractAttribute</c>.
    /// </summary>
    /// <param name="type">The type symbol to check.</param>
    /// <returns><see langword="true"/> if decorated; otherwise, <see langword="false"/>.</returns>
    private static bool HasPacketContract(
        ITypeSymbol type)
    {
        return FindAttribute(
            type,
            GeneratorMetadataNames.PacketContractAttribute)
            is not null;
    }

    /// <summary>
    /// Finds the first attribute on the symbol matching the given metadata name.
    /// </summary>
    /// <param name="symbol">The symbol to inspect.</param>
    /// <param name="metadataName">The fully qualified metadata name of the attribute.</param>
    /// <returns>The matching <see cref="AttributeData"/>, or <see langword="null"/>.</returns>
    private static AttributeData? FindAttribute(
        ISymbol symbol,
        string metadataName)
    {
        return symbol
            .GetAttributes()
            .FirstOrDefault(
                attribute =>
                    attribute.AttributeClass
                        ?.ToDisplayString()
                    == metadataName);
    }

    /// <summary>
    /// Retrieves an optional integer constructor argument from an attribute, if present.
    /// </summary>
    /// <param name="attribute">The attribute data.</param>
    /// <returns>The integer argument value, or <see langword="null"/>.</returns>
    private static int? GetOptionalIntConstructorArgument(
        AttributeData? attribute)
    {
        return TryGetIntConstructorArgument(
            attribute,
            out int value)
                ? value
                : null;
    }

    /// <summary>
    /// Attempts to extract an integer constructor argument from an attribute.
    /// </summary>
    /// <param name="attribute">The attribute data to inspect.</param>
    /// <param name="value">When this method returns <see langword="true"/>, receives the integer value.</param>
    /// <returns><see langword="true"/> if an integer argument was extracted; otherwise, <see langword="false"/>.</returns>
    private static bool TryGetIntConstructorArgument(
        AttributeData? attribute,
        out int value)
    {
        if (attribute is not null &&
            attribute.ConstructorArguments.Length > 0 &&
            attribute.ConstructorArguments[0].Value
                is int intValue)
        {
            value = intValue;
            return true;
        }

        value = 0;
        return false;
    }
}
