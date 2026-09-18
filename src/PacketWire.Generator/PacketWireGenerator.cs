using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PacketWire.Generator;

/// <summary>
/// Roslyn incremental source generator that analyzes PacketWire contract and protocol declarations, validates wire schemas, and emits serialization codecs, dispatch registries, and facade companions.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class PacketWireGenerator : IIncrementalGenerator
{
    /// <summary>
    /// Configures the incremental generator pipeline to track packet and contract syntax models and register code output generation.
    /// </summary>
    /// <param name="context">The incremental generator initialization context.</param>
    public void Initialize(
        IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<PacketCandidate> packets =
            context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    GeneratorMetadataNames.PacketAttribute,
                    static (node, _) =>
                        node is TypeDeclarationSyntax,
                    static (attributeContext, cancellationToken) =>
                        CreatePacketCandidate(
                            attributeContext,
                            cancellationToken))
                .Where(static candidate => candidate is not null)
                .Select(
                    static (candidate, _) =>
                        candidate!);

        IncrementalValuesProvider<INamedTypeSymbol> contracts =
            context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    GeneratorMetadataNames.PacketContractAttribute,
                    static (node, _) =>
                        node is TypeDeclarationSyntax,
                    static (attributeContext, cancellationToken) =>
                        CreateContractCandidate(
                            attributeContext,
                            cancellationToken));

        IncrementalValueProvider<(
            ImmutableArray<PacketCandidate> Left,
            ImmutableArray<INamedTypeSymbol> Right)> inputs =
                packets
                    .Collect()
                    .Combine(
                        contracts.Collect());

        context.RegisterSourceOutput(
            inputs,
            static (productionContext, input) =>
                Validate(
                    productionContext,
                    input.Left,
                    input.Right));
    }

    /// <summary>
    /// Extracts a <see cref="PacketCandidate"/> from a syntax node decorated with <c>[Packet]</c>.
    /// </summary>
    /// <param name="context">The syntax node and attribute context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A populated candidate if valid; otherwise, <see langword="null"/>.</returns>
    private static PacketCandidate? CreatePacketCandidate(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (context.TargetSymbol is not INamedTypeSymbol packetType ||
            context.Attributes.Length == 0)
        {
            return null;
        }

        AttributeData attribute =
            context.Attributes[0];

        if (attribute.ConstructorArguments.Length < 2 ||
            attribute.ConstructorArguments[0].Value
                is not INamedTypeSymbol protocolType ||
            !TryGetUnsignedValue(
                attribute.ConstructorArguments[1].Value,
                out ulong packetId))
        {
            return null;
        }

        ulong packetCategory = 0;

        if (attribute.ConstructorArguments.Length >= 3 &&
            !TryGetUnsignedValue(
                attribute.ConstructorArguments[2].Value,
                out packetCategory))
        {
            return null;
        }

        return new PacketCandidate(
            packetType,
            protocolType,
            packetId,
            packetCategory,
            GetBestLocation(packetType));
    }

    /// <summary>
    /// Extracts a named type symbol for a nested contract type decorated with <c>[PacketContract]</c>.
    /// </summary>
    /// <param name="context">The syntax node and attribute context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The named type symbol of the contract.</returns>
    private static INamedTypeSymbol CreateContractCandidate(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return (INamedTypeSymbol)context.TargetSymbol;
    }

    /// <summary>
    /// Executes protocol-level, ID uniqueness, and member-level validation across all discovered packet and contract symbols, emitting code when valid.
    /// </summary>
    /// <param name="context">The Roslyn source production context for reporting diagnostics and adding sources.</param>
    /// <param name="packets">The immutable array of packet candidates discovered.</param>
    /// <param name="contracts">The immutable array of contract symbols discovered.</param>
    private static void Validate(
        SourceProductionContext context,
        ImmutableArray<PacketCandidate> packets,
        ImmutableArray<INamedTypeSymbol> contracts)
    {
        ValidateProtocols(
            context,
            packets);

        ValidateDuplicatePacketIds(
            context,
            packets);

        Dictionary<string, INamedTypeSymbol> wireTypes =
            new(StringComparer.Ordinal);

        foreach (PacketCandidate packet in packets)
        {
            AddWireType(
                wireTypes,
                packet.PacketType);
        }

        foreach (INamedTypeSymbol contract in contracts)
        {
            AddWireType(
                wireTypes,
                contract);
        }

        HashSet<string> generatedWireTypeKeys =
            new(StringComparer.Ordinal);

        foreach (INamedTypeSymbol wireType in wireTypes.Values)
        {
            ValidateWireType(
                context,
                wireType);

            bool constructionIsValid =
                WireTypeConstructionValidator.Validate(
                    context,
                    wireType);

            if (!constructionIsValid)
            {
                continue;
            }

            if (!GeneratedWireTypeModelBuilder.TryBuild(
                    wireType,
                    out WireTypeModel? model) ||
                model is null)
            {
                continue;
            }

            context.AddSource(
                GeneratedCodecEmitter.GetHintName(
                    wireType),
                GeneratedCodecEmitter.Emit(
                    model));
            generatedWireTypeKeys.Add(
                wireType.ToDisplayString(
                    SymbolDisplayFormat.FullyQualifiedFormat));
        }

        GeneratedRegistryEmitter.EmitRegistries(
            context,
            packets,
            generatedWireTypeKeys);
        GeneratedProtocolFacadeEmitter.EmitFacades(
            context,
            packets,
            generatedWireTypeKeys);
    }

    /// <summary>
    /// Registers a wire type into the unique wire type dictionary indexed by fully qualified symbol display string.
    /// </summary>
    /// <param name="wireTypes">The dictionary collecting unique wire types.</param>
    /// <param name="wireType">The named type symbol of the wire type to add.</param>
    private static void AddWireType(
        Dictionary<string, INamedTypeSymbol> wireTypes,
        INamedTypeSymbol wireType)
    {
        string key =
            wireType.ToDisplayString(
                SymbolDisplayFormat.FullyQualifiedFormat);

        wireTypes[key] = wireType;
    }

    /// <summary>
    /// Validates that protocol markers referenced by packet candidates are decorated with <c>[PacketProtocol]</c> and obey category/ID range bounds.
    /// </summary>
    /// <param name="context">The Roslyn source production context for reporting diagnostics.</param>
    /// <param name="candidates">The array of packet candidates to validate.</param>
    private static void ValidateProtocols(
        SourceProductionContext context,
        ImmutableArray<PacketCandidate> candidates)
    {
        foreach (PacketCandidate candidate in candidates)
        {
            AttributeData? protocolAttribute =
                FindAttribute(
                    candidate.ProtocolType,
                    GeneratorMetadataNames.PacketProtocolAttribute);

            if (protocolAttribute is null)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        GeneratorDiagnosticDescriptors.MissingPacketProtocol,
                        candidate.Location,
                        candidate.PacketType.Name,
                        candidate.ProtocolType.Name));

                continue;
            }

            if (!TryGetPacketIdMaximum(
                    protocolAttribute,
                    out ulong maximumPacketId))
            {
                continue;
            }

            if (candidate.PacketId > maximumPacketId)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        GeneratorDiagnosticDescriptors.PacketIdExceedsProtocolWidth,
                        candidate.Location,
                        candidate.PacketType.Name,
                        candidate.PacketId,
                        candidate.ProtocolType.Name,
                        maximumPacketId));
            }

            if (!TryGetPacketCategoryMaximum(
                    protocolAttribute,
                    out ulong maximumPacketCategory))
            {
                continue;
            }

            if (candidate.PacketCategory > maximumPacketCategory)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        GeneratorDiagnosticDescriptors.PacketCategoryExceedsProtocolWidth,
                        candidate.Location,
                        candidate.PacketType.Name,
                        candidate.PacketCategory,
                        candidate.ProtocolType.Name,
                        maximumPacketCategory));
            }
        }
    }

    /// <summary>
    /// Checks for duplicate packet category and ID combinations assigned to the same protocol, reporting diagnostics if found.
    /// </summary>
    /// <param name="context">The Roslyn source production context for reporting diagnostics.</param>
    /// <param name="candidates">The array of packet candidates to evaluate.</param>
    private static void ValidateDuplicatePacketIds(
        SourceProductionContext context,
        ImmutableArray<PacketCandidate> candidates)
    {
        Dictionary<string, List<PacketCandidate>> groups =
            new(StringComparer.Ordinal);

        foreach (PacketCandidate candidate in candidates)
        {
            string protocolName =
                candidate.ProtocolType.ToDisplayString(
                    SymbolDisplayFormat.FullyQualifiedFormat);

            string key =
                protocolName +
                "|" +
                candidate.PacketCategory.ToString(
                    CultureInfo.InvariantCulture) +
                "|" +
                candidate.PacketId.ToString(
                    CultureInfo.InvariantCulture);

            if (!groups.TryGetValue(
                    key,
                    out List<PacketCandidate>? group))
            {
                group =
                    new List<PacketCandidate>();

                groups.Add(
                    key,
                    group);
            }

            group.Add(candidate);
        }

        foreach (
            KeyValuePair<string, List<PacketCandidate>> pair
            in groups)
        {
            List<PacketCandidate> group =
                pair.Value;

            if (group.Count < 2)
            {
                continue;
            }

            foreach (PacketCandidate candidate in group)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        GeneratorDiagnosticDescriptors.DuplicatePacketId,
                        candidate.Location,
                        candidate.PacketCategory,
                        candidate.PacketId,
                        candidate.ProtocolType.Name));
            }
        }
    }

    /// <summary>
    /// Inspects properties declared on a packet or contract wire type, validating attribute configurations and building field models.
    /// </summary>
    /// <param name="context">The Roslyn source production context for reporting diagnostics.</param>
    /// <param name="wireType">The named type symbol of the packet or contract being validated.</param>
    private static void ValidateWireType(
        SourceProductionContext context,
        INamedTypeSymbol wireType)
    {
        List<PacketFieldModel> fields =
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
                    ReportInvalidMember(
                        context,
                        wireType,
                        property,
                        "static");
                }

                continue;
            }

            if (property.DeclaredAccessibility
                != Accessibility.Public)
            {
                if (hasSerializationMetadata)
                {
                    ReportInvalidMember(
                        context,
                        wireType,
                        property,
                        "not public");
                }

                continue;
            }

            if (fieldAttribute is null &&
                ignoreAttribute is null)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        GeneratorDiagnosticDescriptors.UnclassifiedProperty,
                        GetBestLocation(property),
                        property.Name,
                        wireType.Name));

                continue;
            }

            if (ignoreAttribute is not null)
            {
                if (hasSerializationMetadata)
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            GeneratorDiagnosticDescriptors.ConflictingPropertyMetadata,
                            GetBestLocation(property),
                            property.Name,
                            wireType.Name));
                }

                continue;
            }

            if (property.IsIndexer)
            {
                ReportInvalidMember(
                    context,
                    wireType,
                    property,
                    "an indexer");

                continue;
            }

            if (!HasAccessibleGetterAndSetter(property))
            {
                ReportInvalidMember(
                    context,
                    wireType,
                    property,
                    "missing a getter and setter or init accessor accessible to generated code");

                continue;
            }

            if (!TryGetIntConstructorArgument(
                    fieldAttribute!,
                    out int order) ||
                order < 0)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        GeneratorDiagnosticDescriptors.InvalidFieldOrder,
                        GetBestLocation(property),
                        property.Name,
                        wireType.Name,
                        TryGetIntConstructorArgument(
                            fieldAttribute!,
                            out int invalidOrder)
                                ? invalidOrder
                                : -1));

                continue;
            }

            PacketFieldModel? field =
                CreateFieldModel(
                    context,
                    wireType,
                    property,
                    order,
                    fixedStringAttribute,
                    optionalAttribute,
                    maxCountAttribute);

            if (field is not null)
            {
                fields.Add(field);
            }
        }

        ValidateDuplicateFieldOrders(
            context,
            wireType,
            fields);
    }

    /// <summary>
    /// Analyzes a property symbol and its serialization attributes to construct a validated <see cref="PacketFieldModel"/>.
    /// </summary>
    /// <param name="context">The Roslyn source production context for reporting diagnostics.</param>
    /// <param name="wireType">The named type symbol of the enclosing packet or contract.</param>
    /// <param name="property">The property symbol being evaluated.</param>
    /// <param name="order">The explicit field serialization order index.</param>
    /// <param name="fixedStringAttribute">The fixed string attribute data if applied; otherwise, <see langword="null"/>.</param>
    /// <param name="optionalAttribute">The optional attribute data if applied; otherwise, <see langword="null"/>.</param>
    /// <param name="maxCountAttribute">The max count attribute data if applied; otherwise, <see langword="null"/>.</param>
    /// <returns>A constructed field model if validation succeeds; otherwise, <see langword="null"/>.</returns>
    private static PacketFieldModel? CreateFieldModel(
        SourceProductionContext context,
        INamedTypeSymbol wireType,
        IPropertySymbol property,
        int order,
        AttributeData? fixedStringAttribute,
        AttributeData? optionalAttribute,
        AttributeData? maxCountAttribute)
    {
        bool isNullable =
            IsNullableType(property.Type);

        bool isOptional =
            optionalAttribute is not null;

        if (isNullable &&
            !isOptional)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    GeneratorDiagnosticDescriptors.OptionalRequired,
                    GetBestLocation(property),
                    property.Name,
                    wireType.Name));

            return null;
        }

        if (!isNullable &&
            isOptional)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    GeneratorDiagnosticDescriptors.OptionalRequiresNullable,
                    GetBestLocation(property),
                    property.Name,
                    wireType.Name));

            return null;
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

        if (maxCountAttribute is not null &&
            (!maximumCount.HasValue ||
             maximumCount.Value < 0))
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    GeneratorDiagnosticDescriptors.InvalidMaximumCount,
                    GetBestLocation(property),
                    property.Name,
                    wireType.Name,
                    maximumCount ?? -1));

            return null;
        }

        if (TryGetCollectionElementType(
                effectiveType,
                out ITypeSymbol? elementType))
        {
            return CreateCollectionFieldModel(
                context,
                wireType,
                property,
                order,
                isOptional,
                fixedStringByteLength,
                maximumCount,
                effectiveType,
                elementType!);
        }

        if (maxCountAttribute is not null)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    GeneratorDiagnosticDescriptors.MaxCountRequiresCollection,
                    GetBestLocation(property),
                    property.Name,
                    wireType.Name));

            return null;
        }

        if (effectiveType.SpecialType
            == SpecialType.System_String)
        {
            if (!ValidateFixedString(
                    context,
                    wireType,
                    property,
                    fixedStringAttribute,
                    fixedStringByteLength))
            {
                return null;
            }

            return new PacketFieldModel(
                property,
                order,
                PacketFieldShape.FixedText,
                isOptional,
                fixedStringByteLength,
                null,
                effectiveType,
                null);
        }

        if (fixedStringAttribute is not null)
        {
            ReportInvalidFixedString(
                context,
                wireType,
                property);

            return null;
        }

        if (IsSupportedPrimitive(effectiveType))
        {
            return new PacketFieldModel(
                property,
                order,
                PacketFieldShape.Scalar,
                isOptional,
                null,
                null,
                effectiveType,
                null);
        }

        if (effectiveType.TypeKind
            == TypeKind.Enum)
        {
            return new PacketFieldModel(
                property,
                order,
                PacketFieldShape.EnumerationValue,
                isOptional,
                null,
                null,
                effectiveType,
                null);
        }

        if (HasPacketContract(effectiveType))
        {
            return new PacketFieldModel(
                property,
                order,
                PacketFieldShape.NestedContract,
                isOptional,
                null,
                null,
                effectiveType,
                null);
        }

        ReportUnsupportedOrMissingContract(
            context,
            wireType,
            property,
            effectiveType);

        return null;
    }

    /// <summary>
    /// Constructs a collection field model for array or list properties, validating element type support and length restrictions.
    /// </summary>
    /// <param name="context">The Roslyn source production context for reporting diagnostics.</param>
    /// <param name="wireType">The named type symbol of the enclosing packet or contract.</param>
    /// <param name="property">The collection property symbol being evaluated.</param>
    /// <param name="order">The explicit field serialization order index.</param>
    /// <param name="isOptional">Indicates whether the collection property is declared as optional.</param>
    /// <param name="fixedStringByteLength">The fixed byte length of strings if the element type is a fixed string.</param>
    /// <param name="maximumCount">The maximum allowed element count constraint if configured.</param>
    /// <param name="collectionType">The overall collection type symbol (array or List{T}).</param>
    /// <param name="elementType">The resolved element type symbol.</param>
    /// <returns>A populated field model if valid; otherwise, <see langword="null"/>.</returns>
    private static PacketFieldModel? CreateCollectionFieldModel(
        SourceProductionContext context,
        INamedTypeSymbol wireType,
        IPropertySymbol property,
        int order,
        bool isOptional,
        int? fixedStringByteLength,
        int? maximumCount,
        ITypeSymbol collectionType,
        ITypeSymbol elementType)
    {
        if (IsNullableType(elementType))
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    GeneratorDiagnosticDescriptors.NullableCollectionElement,
                    GetBestLocation(property),
                    property.Name,
                    wireType.Name,
                    elementType.ToDisplayString()));

            return null;
        }

        ITypeSymbol effectiveElementType =
            UnwrapNullableValueType(
                elementType);

        if (effectiveElementType.SpecialType
            == SpecialType.System_String)
        {
            if (!fixedStringByteLength.HasValue ||
                fixedStringByteLength.Value <= 0)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        fixedStringByteLength.HasValue
                            ? GeneratorDiagnosticDescriptors.InvalidFixedStringMetadata
                            : GeneratorDiagnosticDescriptors.FixedStringRequired,
                        GetBestLocation(property),
                        property.Name,
                        wireType.Name));

                return null;
            }
        }
        else if (fixedStringByteLength.HasValue)
        {
            ReportInvalidFixedString(
                context,
                wireType,
                property);

            return null;
        }

        if (TryGetCollectionElementType(
                effectiveElementType,
                out _))
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    GeneratorDiagnosticDescriptors.UnsupportedFieldType,
                    GetBestLocation(property),
                    property.Name,
                    wireType.Name,
                    elementType.ToDisplayString()));

            return null;
        }

        if (!IsSupportedPrimitive(effectiveElementType) &&
            effectiveElementType.TypeKind != TypeKind.Enum &&
            effectiveElementType.SpecialType != SpecialType.System_String &&
            !HasPacketContract(effectiveElementType))
        {
            ReportUnsupportedOrMissingContract(
                context,
                wireType,
                property,
                effectiveElementType);

            return null;
        }

        return new PacketFieldModel(
            property,
            order,
            PacketFieldShape.Sequence,
            isOptional,
            fixedStringByteLength,
            maximumCount,
            collectionType,
            effectiveElementType);
    }

    /// <summary>
    /// Validates that a string property is decorated with <c>[FixedString]</c> specifying a positive byte length.
    /// </summary>
    /// <param name="context">The Roslyn source production context for reporting diagnostics.</param>
    /// <param name="wireType">The named type symbol of the enclosing packet or contract.</param>
    /// <param name="property">The property symbol representing the string field.</param>
    /// <param name="fixedStringAttribute">The fixed string attribute data if present.</param>
    /// <param name="fixedStringByteLength">The parsed byte length value.</param>
    /// <returns><see langword="true"/> if valid fixed string metadata is present; otherwise, <see langword="false"/>.</returns>
    private static bool ValidateFixedString(
        SourceProductionContext context,
        INamedTypeSymbol wireType,
        IPropertySymbol property,
        AttributeData? fixedStringAttribute,
        int? fixedStringByteLength)
    {
        if (fixedStringAttribute is null)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    GeneratorDiagnosticDescriptors.FixedStringRequired,
                    GetBestLocation(property),
                    property.Name,
                    wireType.Name));

            return false;
        }

        if (!fixedStringByteLength.HasValue ||
            fixedStringByteLength.Value <= 0)
        {
            ReportInvalidFixedString(
                context,
                wireType,
                property);

            return false;
        }

        return true;
    }

    /// <summary>
    /// Checks for duplicate order indices across all fields declared within a wire type, reporting diagnostics for any conflicts.
    /// </summary>
    /// <param name="context">The Roslyn source production context for reporting diagnostics.</param>
    /// <param name="wireType">The named type symbol of the enclosing packet or contract.</param>
    /// <param name="fields">The list of discovered packet field models.</param>
    private static void ValidateDuplicateFieldOrders(
        SourceProductionContext context,
        INamedTypeSymbol wireType,
        List<PacketFieldModel> fields)
    {
        foreach (
            IGrouping<int, PacketFieldModel> group
            in fields.GroupBy(
                static field => field.Order))
        {
            PacketFieldModel[] duplicates =
                group.ToArray();

            if (duplicates.Length < 2)
            {
                continue;
            }

            foreach (PacketFieldModel field in duplicates)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        GeneratorDiagnosticDescriptors.DuplicateFieldOrder,
                        GetBestLocation(field.Property),
                        wireType.Name,
                        field.Order));
            }
        }
    }

    /// <summary>
    /// Reports an invalid member diagnostic when a static or inaccessible property is marked with serialization metadata.
    /// </summary>
    /// <param name="context">The Roslyn source production context for reporting diagnostics.</param>
    /// <param name="wireType">The named type symbol of the enclosing packet or contract.</param>
    /// <param name="property">The invalid property symbol.</param>
    /// <param name="reason">The explanation for member invalidity.</param>
    private static void ReportInvalidMember(
        SourceProductionContext context,
        INamedTypeSymbol wireType,
        IPropertySymbol property,
        string reason)
    {
        context.ReportDiagnostic(
            Diagnostic.Create(
                GeneratorDiagnosticDescriptors.InvalidFieldMember,
                GetBestLocation(property),
                property.Name,
                wireType.Name,
                reason));
    }

    /// <summary>
    /// Reports an invalid fixed string diagnostic when a non-string property or an invalid length argument is decorated with <c>[FixedString]</c>.
    /// </summary>
    /// <param name="context">The Roslyn source production context for reporting diagnostics.</param>
    /// <param name="wireType">The named type symbol of the enclosing packet or contract.</param>
    /// <param name="property">The property symbol with invalid fixed string metadata.</param>
    private static void ReportInvalidFixedString(
        SourceProductionContext context,
        INamedTypeSymbol wireType,
        IPropertySymbol property)
    {
        context.ReportDiagnostic(
            Diagnostic.Create(
                GeneratorDiagnosticDescriptors.InvalidFixedStringMetadata,
                GetBestLocation(property),
                property.Name,
                wireType.Name));
    }

    /// <summary>
    /// Reports a diagnostic indicating that a property's type is neither a supported primitive nor decorated with <c>[PacketContract]</c>.
    /// </summary>
    /// <param name="context">The Roslyn source production context for reporting diagnostics.</param>
    /// <param name="wireType">The named type symbol of the enclosing packet or contract.</param>
    /// <param name="property">The property symbol being diagnosed.</param>
    /// <param name="fieldType">The unsupported field type symbol.</param>
    private static void ReportUnsupportedOrMissingContract(
        SourceProductionContext context,
        INamedTypeSymbol wireType,
        IPropertySymbol property,
        ITypeSymbol fieldType)
    {
        DiagnosticDescriptor descriptor =
            fieldType.Locations.Any(
                static location =>
                    location.IsInSource)
                ? GeneratorDiagnosticDescriptors.NestedContractRequired
                : GeneratorDiagnosticDescriptors.UnsupportedFieldType;

        context.ReportDiagnostic(
            Diagnostic.Create(
                descriptor,
                GetBestLocation(property),
                property.Name,
                wireType.Name,
                fieldType.ToDisplayString()));
    }

    /// <summary>
    /// Verifies that a property exposes both a getter and a setter (or init accessor) accessible to generated code.
    /// </summary>
    /// <param name="property">The property symbol to check.</param>
    /// <returns><see langword="true"/> if both accessors are accessible; otherwise, <see langword="false"/>.</returns>
    private static bool HasAccessibleGetterAndSetter(
        IPropertySymbol property)
    {
        return
            IsAccessibleFromGeneratedCode(
                property.GetMethod) &&
            IsAccessibleFromGeneratedCode(
                property.SetMethod);
    }

    /// <summary>
    /// Checks whether an accessor method is public, internal, or protected internal.
    /// </summary>
    /// <param name="accessor">The method symbol of the property accessor.</param>
    /// <returns><see langword="true"/> if accessible to generated source code; otherwise, <see langword="false"/>.</returns>
    private static bool IsAccessibleFromGeneratedCode(
        IMethodSymbol? accessor)
    {
        if (accessor is null)
        {
            return false;
        }

        return accessor.DeclaredAccessibility
            is Accessibility.Public
            or Accessibility.Internal
            or Accessibility.ProtectedOrInternal;
    }

    /// <summary>
    /// Determines whether the given type symbol represents a supported primitive wire type.
    /// </summary>
    /// <param name="type">The type symbol to check.</param>
    /// <returns><see langword="true"/> if supported primitive; otherwise, <see langword="false"/>.</returns>
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
    /// Resolves the element type of a single-dimensional array or generic <see cref="List{T}"/> collection.
    /// </summary>
    /// <param name="type">The candidate collection type symbol.</param>
    /// <param name="elementType">When this method returns, contains the resolved element type if supported; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the type is a supported 1D array or List{T}; otherwise, <see langword="false"/>.</returns>
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
            namedType.OriginalDefinition.MetadataName == "List`1" &&
            namedType.OriginalDefinition.ContainingNamespace
                .ToDisplayString() == "System.Collections.Generic")
        {
            elementType =
                namedType.TypeArguments[0];

            return true;
        }

        elementType = null;
        return false;
    }

    /// <summary>
    /// Determines whether the given type symbol is nullable (annotated reference type or Nullable{T}).
    /// </summary>
    /// <param name="type">The type symbol to inspect.</param>
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
    /// Unwraps the underlying value type if the symbol is <see cref="Nullable{T}"/>; otherwise returns the original type.
    /// </summary>
    /// <param name="type">The type symbol to unwrap.</param>
    /// <returns>The unwrapped underlying type if nullable value type; otherwise, the original type.</returns>
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
    /// Determines whether the given named type symbol is <see cref="Nullable{T}"/>.
    /// </summary>
    /// <param name="type">The named type symbol to check.</param>
    /// <returns><see langword="true"/> if <see cref="Nullable{T}"/>; otherwise, <see langword="false"/>.</returns>
    private static bool IsNullableValueType(
        INamedTypeSymbol type)
    {
        return
            type.IsGenericType &&
            type.TypeArguments.Length == 1 &&
            type.OriginalDefinition.MetadataName == "Nullable`1" &&
            type.OriginalDefinition.ContainingNamespace
                .ToDisplayString() == "System";
    }

    /// <summary>
    /// Checks whether the specified type symbol has the <c>[PacketContract]</c> attribute applied.
    /// </summary>
    /// <param name="type">The type symbol to inspect.</param>
    /// <returns><see langword="true"/> if marked with <c>PacketContractAttribute</c>; otherwise, <see langword="false"/>.</returns>
    private static bool HasPacketContract(
        ITypeSymbol type)
    {
        return FindAttribute(
            type,
            GeneratorMetadataNames.PacketContractAttribute)
            is not null;
    }

    /// <summary>
    /// Finds the first attribute on a symbol matching the specified fully qualified metadata name.
    /// </summary>
    /// <param name="symbol">The code symbol to inspect.</param>
    /// <param name="metadataName">The fully qualified metadata name of the attribute.</param>
    /// <returns>The matching <see cref="AttributeData"/>, or <see langword="null"/> if not found.</returns>
    private static AttributeData? FindAttribute(
        ISymbol symbol,
        string metadataName)
    {
        foreach (AttributeData attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString()
                == metadataName)
            {
                return attribute;
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts an optional integer constructor argument from an attribute, or returns <see langword="null"/> if absent or invalid.
    /// </summary>
    /// <param name="attribute">The attribute data instance.</param>
    /// <returns>The parsed integer value, or <see langword="null"/>.</returns>
    private static int? GetOptionalIntConstructorArgument(
        AttributeData? attribute)
    {
        if (attribute is null)
        {
            return null;
        }

        return TryGetIntConstructorArgument(
            attribute,
            out int value)
                ? value
                : null;
    }

    /// <summary>
    /// Attempts to extract the first integer constructor argument from an attribute.
    /// </summary>
    /// <param name="attribute">The attribute data instance.</param>
    /// <param name="value">When this method returns, contains the integer value if present.</param>
    /// <returns><see langword="true"/> if an integer argument was extracted; otherwise, <see langword="false"/>.</returns>
    private static bool TryGetIntConstructorArgument(
        AttributeData attribute,
        out int value)
    {
        if (attribute.ConstructorArguments.Length > 0 &&
            attribute.ConstructorArguments[0].Value
                is int intValue)
        {
            value = intValue;
            return true;
        }

        value = 0;
        return false;
    }

    /// <summary>
    /// Determines the maximum allowable packet ID based on the integer size configured on the protocol attribute.
    /// </summary>
    /// <param name="protocolAttribute">The protocol attribute data.</param>
    /// <param name="maximumPacketId">When this method returns, contains the maximum packet ID if resolved.</param>
    /// <returns><see langword="true"/> if resolved; otherwise, <see langword="false"/>.</returns>
    private static bool TryGetPacketIdMaximum(
        AttributeData protocolAttribute,
        out ulong maximumPacketId)
    {
        maximumPacketId = 0;

        if (protocolAttribute.ConstructorArguments.Length == 0 ||
            protocolAttribute.ConstructorArguments[0].Value
                is not int size)
        {
            return false;
        }

        return TryGetMaximumForIntegerSize(
            size,
            out maximumPacketId);
    }

    /// <summary>
    /// Determines the maximum allowable packet category based on the integer size configured on the protocol attribute.
    /// </summary>
    /// <param name="protocolAttribute">The protocol attribute data.</param>
    /// <param name="maximumPacketCategory">When this method returns, contains the maximum category if resolved.</param>
    /// <returns><see langword="true"/> if resolved; otherwise, <see langword="false"/>.</returns>
    private static bool TryGetPacketCategoryMaximum(
        AttributeData protocolAttribute,
        out ulong maximumPacketCategory)
    {
        int size = 1;

        if (protocolAttribute.ConstructorArguments.Length >= 5)
        {
            if (protocolAttribute.ConstructorArguments[4].Value
                is not int configuredSize)
            {
                maximumPacketCategory = 0;
                return false;
            }

            size = configuredSize;
        }

        return TryGetMaximumForIntegerSize(
            size,
            out maximumPacketCategory);
    }

    /// <summary>
    /// Returns the maximum unsigned value that can be represented by a given integer byte width (1, 2, 4, or 8).
    /// </summary>
    /// <param name="size">The byte width.</param>
    /// <param name="maximumValue">When this method returns, contains the maximum value.</param>
    /// <returns><see langword="true"/> if the size is valid; otherwise, <see langword="false"/>.</returns>
    private static bool TryGetMaximumForIntegerSize(
        int size,
        out ulong maximumValue)
    {
        maximumValue =
            size switch
            {
                1 => byte.MaxValue,
                2 => ushort.MaxValue,
                4 => uint.MaxValue,
                8 => ulong.MaxValue,
                _ => 0
            };

        return size is 1 or 2 or 4 or 8;
    }

    /// <summary>
    /// Attempts to convert a boxed numeric constant into a 64-bit unsigned integer value.
    /// </summary>
    /// <param name="value">The boxed numeric object.</param>
    /// <param name="result">When this method returns, contains the unsigned 64-bit integer if conversion succeeded.</param>
    /// <returns><see langword="true"/> if conversion succeeded; otherwise, <see langword="false"/>.</returns>
    private static bool TryGetUnsignedValue(
        object? value,
        out ulong result)
    {
        switch (value)
        {
            case byte byteValue:
                result = byteValue;
                return true;
                
            case ushort unsignedShortValue:
                result = unsignedShortValue;
                return true;

            case uint unsignedIntValue:
                result = unsignedIntValue;
                return true;

            case ulong unsignedLongValue:
                result = unsignedLongValue;
                return true;

            case sbyte signedByteValue
                when signedByteValue >= 0:
                result = (ulong)signedByteValue;
                return true;

            case short shortValue
                when shortValue >= 0:
                result = (ulong)shortValue;
                return true;

            case int intValue
                when intValue >= 0:
                result = (ulong)intValue;
                return true;

            case long longValue
                when longValue >= 0:
                result = (ulong)longValue;
                return true;

            default:
                result = 0;
                return false;
        }
    }

    /// <summary>
    /// Selects the primary source location for reporting a compiler diagnostic on a symbol.
    /// </summary>
    /// <param name="symbol">The symbol to locate.</param>
    /// <returns>The source location if available; otherwise, <see cref="Location.None"/>.</returns>
    private static Location GetBestLocation(
        ISymbol symbol)
    {
        foreach (Location location in symbol.Locations)
        {
            if (location.IsInSource)
            {
                return location;
            }
        }

        return symbol.Locations.FirstOrDefault()
            ?? Location.None;
    }
}





