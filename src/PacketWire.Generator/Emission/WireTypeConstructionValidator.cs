using System.Linq;
using Microsoft.CodeAnalysis;

namespace PacketWire.Generator;

/// <summary>
/// Validates that declared wire contract types (packets and nested contracts) can be constructed by source-generated codecs.
/// </summary>
internal static class WireTypeConstructionValidator
{
    /// <summary>
    /// Validates constructibility, accessibility, inheritance, and constructor presence for the given wire type symbol.
    /// </summary>
    /// <param name="context">The source production context for reporting diagnostics.</param>
    /// <param name="wireType">The named type symbol to validate.</param>
    /// <returns><see langword="true"/> if the wire type is valid and constructible; otherwise, <see langword="false"/>.</returns>
    internal static bool Validate(
        SourceProductionContext context,
        INamedTypeSymbol wireType)
    {
        bool isValid = true;

        if (!IsTypeAccessible(wireType))
        {
            ReportCannotConstruct(
                context,
                wireType,
                "the type or one of its containing types is not accessible to generated code");

            isValid = false;
        }

        if (IsGenericOrNestedInGenericType(wireType))
        {
            ReportCannotConstruct(
                context,
                wireType,
                "generic wire contract types are not supported");

            isValid = false;
        }

        if (wireType.TypeKind == TypeKind.Struct)
        {
            if (wireType.IsRefLikeType)
            {
                ReportCannotConstruct(
                    context,
                    wireType,
                    "ref struct wire contracts are not supported");

                isValid = false;
            }

            return isValid;
        }

        if (wireType.TypeKind != TypeKind.Class)
        {
            ReportCannotConstruct(
                context,
                wireType,
                "the wire contract is neither a class nor a struct");

            return false;
        }

        if (wireType.IsStatic)
        {
            ReportCannotConstruct(
                context,
                wireType,
                "static classes cannot represent packet instances");

            isValid = false;
        }

        if (wireType.IsAbstract)
        {
            ReportCannotConstruct(
                context,
                wireType,
                "abstract classes cannot be instantiated");

            isValid = false;
        }

        if (wireType.BaseType is INamedTypeSymbol baseType &&
            baseType.SpecialType != SpecialType.System_Object)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    GeneratorDiagnosticDescriptors.WireContractInheritanceUnsupported,
                    GetBestLocation(wireType),
                    wireType.Name,
                    baseType.ToDisplayString()));

            isValid = false;
        }

        bool hasAccessibleParameterlessConstructor =
            wireType.InstanceConstructors.Any(
                static constructor =>
                    constructor.Parameters.Length == 0 &&
                    IsAccessibleFromGeneratedCode(
                        constructor.DeclaredAccessibility));

        if (!hasAccessibleParameterlessConstructor)
        {
            ReportCannotConstruct(
                context,
                wireType,
                "it does not expose an accessible parameterless constructor");

            isValid = false;
        }

        return isValid;
    }

    /// <summary>
    /// Checks whether the wire type or any containing type is generic.
    /// </summary>
    /// <param name="wireType">The type symbol to inspect.</param>
    /// <returns><see langword="true"/> if generic; otherwise, <see langword="false"/>.</returns>
    private static bool IsGenericOrNestedInGenericType(
        INamedTypeSymbol wireType)
    {
        INamedTypeSymbol? current = wireType;

        while (current is not null)
        {
            if (current.IsGenericType)
            {
                return true;
            }

            current = current.ContainingType;
        }

        return false;
    }

    /// <summary>
    /// Checks whether the wire type and all containing types have sufficient accessibility for generated code.
    /// </summary>
    /// <param name="wireType">The type symbol to check.</param>
    /// <returns><see langword="true"/> if accessible; otherwise, <see langword="false"/>.</returns>
    private static bool IsTypeAccessible(
        INamedTypeSymbol wireType)
    {
        INamedTypeSymbol? current = wireType;

        while (current is not null)
        {
            if (!IsAccessibleFromGeneratedCode(
                    current.DeclaredAccessibility))
            {
                return false;
            }

            current = current.ContainingType;
        }

        return true;
    }

    /// <summary>
    /// Determines whether the declared accessibility permits instantiation from generated code in the same assembly.
    /// </summary>
    /// <param name="accessibility">The declared accessibility.</param>
    /// <returns><see langword="true"/> if public, internal, or protected internal; otherwise, <see langword="false"/>.</returns>
    private static bool IsAccessibleFromGeneratedCode(
        Accessibility accessibility)
    {
        return accessibility
            is Accessibility.Public
            or Accessibility.Internal
            or Accessibility.ProtectedOrInternal;
    }

    /// <summary>
    /// Reports a PWG019 diagnostic stating that the wire contract cannot be constructed.
    /// </summary>
    /// <param name="context">The production context for reporting.</param>
    /// <param name="wireType">The invalid type symbol.</param>
    /// <param name="reason">The specific reason why construction is prohibited.</param>
    private static void ReportCannotConstruct(
        SourceProductionContext context,
        INamedTypeSymbol wireType,
        string reason)
    {
        context.ReportDiagnostic(
            Diagnostic.Create(
                GeneratorDiagnosticDescriptors.WireContractCannotBeConstructed,
                GetBestLocation(wireType),
                wireType.Name,
                reason));
    }

    /// <summary>
    /// Resolves the most specific source location for diagnostic reporting.
    /// </summary>
    /// <param name="symbol">The symbol to locate.</param>
    /// <returns>The source location, or <see cref="Location.None"/>.</returns>
    private static Location GetBestLocation(
        ISymbol symbol)
    {
        return symbol.Locations.FirstOrDefault(
                   static location => location.IsInSource)
            ?? symbol.Locations.FirstOrDefault()
            ?? Location.None;
    }
}
