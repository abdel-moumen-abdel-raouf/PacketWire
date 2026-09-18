using Microsoft.CodeAnalysis;

namespace PacketWire.Generator;

/// <summary>
/// Defines Roslyn diagnostic descriptors emitted by the PacketWire incremental source generator.
/// </summary>
internal static class GeneratorDiagnosticDescriptors
{
    /// <summary>
    /// PWG001: Reported when a packet references a protocol type that is not decorated with <c>PacketProtocolAttribute</c>.
    /// </summary>
    internal static readonly DiagnosticDescriptor MissingPacketProtocol =
        Create(
            "PWG001",
            "Invalid packet protocol",
            "Packet '{0}' references protocol type '{1}', but that type is not decorated with PacketProtocolAttribute");

    /// <summary>
    /// PWG002: Reported when duplicate packet identity (category and packet ID) is declared within the same protocol.
    /// </summary>
    internal static readonly DiagnosticDescriptor DuplicatePacketId =
        Create(
            "PWG002",
            "Duplicate packet identity",
            "Packet category '{0}' and packet ID '{1}' are assigned more than once in protocol '{2}'");

    /// <summary>
    /// PWG003: Reported when a packet ID exceeds the maximum value supported by the protocol's configured packet ID width.
    /// </summary>
    internal static readonly DiagnosticDescriptor PacketIdExceedsProtocolWidth =
        Create(
            "PWG003",
            "Packet ID exceeds protocol capacity",
            "Packet '{0}' uses ID '{1}', but protocol '{2}' supports a maximum packet ID of '{3}'");

    /// <summary>
    /// PWG004: Reported when multiple properties within a contract declare the same serialization order index.
    /// </summary>
    internal static readonly DiagnosticDescriptor DuplicateFieldOrder =
        Create(
            "PWG004",
            "Duplicate packet field order",
            "Wire contract '{0}' declares PacketField order '{1}' more than once");

    /// <summary>
    /// PWG005: Reported when a public instance property on a wire contract lacks both <c>PacketFieldAttribute</c> and <c>PacketIgnoreAttribute</c>.
    /// </summary>
    internal static readonly DiagnosticDescriptor UnclassifiedProperty =
        Create(
            "PWG005",
            "Unclassified packet property",
            "Public instance property '{0}' on wire contract '{1}' must be marked with PacketFieldAttribute or PacketIgnoreAttribute");

    /// <summary>
    /// PWG006: Reported when a property combines <c>PacketIgnoreAttribute</c> with serialization metadata.
    /// </summary>
    internal static readonly DiagnosticDescriptor ConflictingPropertyMetadata =
        Create(
            "PWG006",
            "Conflicting packet property metadata",
            "Property '{0}' on wire contract '{1}' cannot combine PacketIgnoreAttribute with packet field metadata");

    /// <summary>
    /// PWG007: Reported when a property decorated as a packet field has an invalid member shape (e.g. indexer or static).
    /// </summary>
    internal static readonly DiagnosticDescriptor InvalidFieldMember =
        Create(
            "PWG007",
            "Invalid packet field member",
            "Property '{0}' on wire contract '{1}' cannot be used as a packet field because it is {2}");

    /// <summary>
    /// PWG008: Reported when a packet field order index is negative.
    /// </summary>
    internal static readonly DiagnosticDescriptor InvalidFieldOrder =
        Create(
            "PWG008",
            "Invalid packet field order",
            "Packet field '{0}' on wire contract '{1}' has invalid order '{2}' because order must be zero or greater");

    /// <summary>
    /// PWG009: Reported when a packet field uses a CLR type unsupported by PacketWire serialization.
    /// </summary>
    internal static readonly DiagnosticDescriptor UnsupportedFieldType =
        Create(
            "PWG009",
            "Unsupported packet field type",
            "Packet field '{0}' on wire contract '{1}' uses unsupported type '{2}'");

    /// <summary>
    /// PWG010: Reported when a string property lacks the required <c>FixedStringAttribute</c>.
    /// </summary>
    internal static readonly DiagnosticDescriptor FixedStringRequired =
        Create(
            "PWG010",
            "Fixed string metadata required",
            "Packet field '{0}' on wire contract '{1}' requires FixedStringAttribute because it contains a string value");

    /// <summary>
    /// PWG011: Reported when <c>FixedStringAttribute</c> is applied to a non-string property or specifies an invalid byte length.
    /// </summary>
    internal static readonly DiagnosticDescriptor InvalidFixedStringMetadata =
        Create(
            "PWG011",
            "Invalid fixed string metadata",
            "FixedStringAttribute on packet field '{0}' of wire contract '{1}' is only valid for string values and must specify a positive byte length");

    /// <summary>
    /// PWG012: Reported when a nullable property lacks <c>OptionalAttribute</c>.
    /// </summary>
    internal static readonly DiagnosticDescriptor OptionalRequired =
        Create(
            "PWG012",
            "Optional metadata required",
            "Nullable packet field '{0}' on wire contract '{1}' must be marked with OptionalAttribute");

    /// <summary>
    /// PWG013: Reported when <c>OptionalAttribute</c> is applied to a non-nullable property.
    /// </summary>
    internal static readonly DiagnosticDescriptor OptionalRequiresNullable =
        Create(
            "PWG013",
            "Optional field must be nullable",
            "Packet field '{0}' on wire contract '{1}' is marked with OptionalAttribute but its type is not nullable");

    /// <summary>
    /// PWG014: Reported when a complex nested property type is not decorated with <c>PacketContractAttribute</c>.
    /// </summary>
    internal static readonly DiagnosticDescriptor NestedContractRequired =
        Create(
            "PWG014",
            "Nested packet contract required",
            "Packet field '{0}' on wire contract '{1}' uses nested type '{2}', which must be decorated with PacketContractAttribute");

    /// <summary>
    /// PWG015: Reported when <c>MaxCountAttribute</c> is applied to a non-collection property.
    /// </summary>
    internal static readonly DiagnosticDescriptor MaxCountRequiresCollection =
        Create(
            "PWG015",
            "Maximum count requires a collection",
            "MaxCountAttribute on packet field '{0}' of wire contract '{1}' is only valid for collection fields");

    /// <summary>
    /// PWG016: Reported when <c>MaxCountAttribute</c> specifies a negative maximum count.
    /// </summary>
    internal static readonly DiagnosticDescriptor InvalidMaximumCount =
        Create(
            "PWG016",
            "Invalid maximum collection count",
            "Packet field '{0}' on wire contract '{1}' has invalid maximum count '{2}' because maximum count must be zero or greater");

    /// <summary>
    /// PWG017: Reported when a collection property uses a nullable element type.
    /// </summary>
    internal static readonly DiagnosticDescriptor NullableCollectionElement =
        Create(
            "PWG017",
            "Nullable collection element is unsupported",
            "Collection packet field '{0}' on wire contract '{1}' uses nullable element type '{2}', which is unsupported because collection elements do not have per-item Optional metadata");

    /// <summary>
    /// PWG018: Reported when a packet category exceeds the maximum value supported by the protocol's configured category width.
    /// </summary>
    internal static readonly DiagnosticDescriptor PacketCategoryExceedsProtocolWidth =
        Create(
            "PWG018",
            "Packet category exceeds protocol capacity",
            "Packet '{0}' uses category '{1}', but protocol '{2}' supports a maximum packet category of '{3}'");

    /// <summary>
    /// PWG019: Reported when a wire contract cannot be instantiated by generated codecs (e.g. missing accessible parameterless constructor or abstract).
    /// </summary>
    internal static readonly DiagnosticDescriptor WireContractCannotBeConstructed =
        Create(
            "PWG019",
            "Wire contract cannot be constructed",
            "Wire contract '{0}' cannot be constructed by generated codecs because {1}");

    /// <summary>
    /// PWG020: Reported when a wire contract inherits from another class other than <see cref="object"/> or <see cref="System.ValueType"/>.
    /// </summary>
    internal static readonly DiagnosticDescriptor WireContractInheritanceUnsupported =
        Create(
            "PWG020",
            "Wire contract inheritance is unsupported",
            "Wire contract '{0}' derives from '{1}', but wire contract inheritance is not supported");

    /// <summary>
    /// PWG021: Reported when a class decorated with <c>PacketProtocolAttribute</c> is not declared with the <c>partial</c> modifier.
    /// </summary>
    internal static readonly DiagnosticDescriptor PacketProtocolMustBePartial =
        Create(
            "PWG021",
            "Packet protocol must be partial",
            "Packet protocol '{0}' must be declared partial so PacketWire can generate its public protocol facade");

    /// <summary>
    /// PWG022: Reported when a protocol declaration is nested, generic, or not a class.
    /// </summary>
    internal static readonly DiagnosticDescriptor UnsupportedPacketProtocolDeclaration =
        Create(
            "PWG022",
            "Unsupported packet protocol declaration",
            "Packet protocol '{0}' must be a top-level non-generic class");

    /// <summary>
    /// Helper method for creating diagnostic descriptors with standard generator metadata.
    /// </summary>
    /// <param name="id">The unique diagnostic identifier (e.g. <c>PWG001</c>).</param>
    /// <param name="title">A short summary of the diagnostic.</param>
    /// <param name="messageFormat">The format string for diagnostic error messages.</param>
    /// <returns>A configured <see cref="DiagnosticDescriptor"/>.</returns>
    private static DiagnosticDescriptor Create(
        string id,
        string title,
        string messageFormat)
    {
        return new DiagnosticDescriptor(
            id,
            title,
            messageFormat,
            "PacketWire.Generator",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);
    }
}
