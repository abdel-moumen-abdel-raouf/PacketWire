using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PacketWire.Generator;

/// <summary>
/// Emits strongly typed, static or partial protocol facade companion classes containing high-level serialization and identity APIs.
/// </summary>
internal static partial class GeneratedProtocolFacadeEmitter
{
    /// <summary>
    /// Validates and generates companion facade partial classes for all protocols represented among discovered packet candidates.
    /// </summary>
    /// <param name="context">The Roslyn source production context for reporting diagnostics and adding sources.</param>
    /// <param name="packets">The immutable array of candidate packet types discovered during compilation.</param>
    /// <param name="generatedWireTypeKeys">The set of successfully generated wire type symbol keys.</param>
    internal static void EmitFacades(
        SourceProductionContext context,
        ImmutableArray<PacketCandidate> packets,
        HashSet<string> generatedWireTypeKeys)
    {
        IEnumerable<IGrouping<string, PacketCandidate>> groups =
            packets
                .GroupBy(
                    static packet =>
                        GetSymbolKey(
                            packet.ProtocolType))
                .OrderBy(
                    static group =>
                        group.Key,
                    StringComparer.Ordinal);

        foreach (
            IGrouping<string, PacketCandidate> group
            in groups)
        {
            PacketCandidate[] entries =
                group
                    .Where(
                        packet =>
                            generatedWireTypeKeys.Contains(
                                GetSymbolKey(
                                    packet.PacketType)))
                    .OrderBy(
                        static packet =>
                            packet.PacketCategory)
                    .ThenBy(
                        static packet =>
                            packet.PacketId)
                    .ToArray();

            if (entries.Length == 0)
            {
                continue;
            }

            INamedTypeSymbol protocolType =
                entries[0].ProtocolType;

            if (!ValidateProtocolType(
                    context,
                    protocolType))
            {
                continue;
            }

            AttributeData? protocolAttribute =
                FindProtocolAttribute(
                    protocolType);

            if (protocolAttribute is null)
            {
                continue;
            }

            if (!TryReadProtocolConfiguration(
                    protocolAttribute,
                    out ProtocolConfiguration configuration))
            {
                continue;
            }

            context.AddSource(
                GetHintName(protocolType),
                Emit(
                    protocolType,
                    configuration));
        }
    }

    /// <summary>
    /// Verifies that the candidate protocol type symbol is a non-generic, non-record, top-level partial class.
    /// </summary>
    /// <param name="context">The Roslyn source production context for reporting diagnostics.</param>
    /// <param name="protocolType">The named type symbol representing the protocol facade class.</param>
    /// <returns><see langword="true"/> if the protocol declaration satisfies all constraints; otherwise, <see langword="false"/>.</returns>
    private static bool ValidateProtocolType(
        SourceProductionContext context,
        INamedTypeSymbol protocolType)
    {
        if (protocolType.TypeKind != TypeKind.Class ||
            protocolType.ContainingType is not null ||
            protocolType.IsGenericType ||
            protocolType.IsRecord)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    GeneratorDiagnosticDescriptors.UnsupportedPacketProtocolDeclaration,
                    GetBestLocation(protocolType),
                    protocolType.Name));

            return false;
        }

        bool isPartial =
            protocolType
                .DeclaringSyntaxReferences
                .Select(
                    static syntaxReference =>
                        syntaxReference.GetSyntax())
                .OfType<TypeDeclarationSyntax>()
                .Any(
                    static declaration =>
                        declaration.Modifiers.Any(
                            SyntaxKind.PartialKeyword));

        if (!isPartial)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    GeneratorDiagnosticDescriptors.PacketProtocolMustBePartial,
                    GetBestLocation(protocolType),
                    protocolType.Name));

            return false;
        }

        return true;
    }

    /// <summary>
    /// Emits the complete C# source text of the partial protocol facade class containing definition, identity, and serialization APIs.
    /// </summary>
    /// <param name="protocolType">The named type symbol representing the protocol facade class.</param>
    /// <param name="configuration">The configuration parameters declared via the protocol attribute.</param>
    /// <returns>A <see cref="SourceText"/> containing the generated facade source code.</returns>
    private static SourceText Emit(
        INamedTypeSymbol protocolType,
        ProtocolConfiguration configuration)
    {
        StringBuilder builder =
            new();

        string? namespaceName =
            protocolType.ContainingNamespace.IsGlobalNamespace
                ? null
                : protocolType.ContainingNamespace.ToDisplayString();

        string registryClassName =
            GeneratedRegistryEmitter.GetRegistryClassName(
                protocolType);

        builder.AppendLine("// <auto-generated/>");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();

        if (namespaceName is not null)
        {
            builder.Append("namespace ");
            builder.Append(namespaceName);
            builder.AppendLine();
            builder.AppendLine("{");
        }

        string indent =
            namespaceName is null
                ? string.Empty
                : "    ";

        builder.Append(indent);

        AppendAccessibility(
            builder,
            protocolType.DeclaredAccessibility);

        if (protocolType.IsStatic)
        {
            builder.Append("static ");
        }
        else
        {
            if (protocolType.IsAbstract)
            {
                builder.Append("abstract ");
            }

            if (protocolType.IsSealed)
            {
                builder.Append("sealed ");
            }
        }

        builder.Append("partial class ");
        builder.Append(protocolType.Name);
        builder.AppendLine();

        builder.Append(indent);
        builder.AppendLine("{");

        string memberIndent =
            indent + "    ";

        string bodyIndent =
            memberIndent + "    ";

        EmitDefinition(
            builder,
            memberIndent,
            configuration);

        builder.AppendLine();

        EmitIdentityMethods(
            builder,
            memberIndent,
            bodyIndent,
            registryClassName);

        builder.AppendLine();

        EmitSerialize(
            builder,
            memberIndent,
            bodyIndent,
            registryClassName);

        builder.AppendLine();

        EmitProtectedSerialize(
            builder,
            memberIndent,
            bodyIndent,
            registryClassName);

        builder.AppendLine();

        EmitDeserialize(
            builder,
            memberIndent,
            bodyIndent,
            registryClassName);

        builder.AppendLine();

        EmitProtectedDeserialize(
            builder,
            memberIndent,
            bodyIndent,
            registryClassName);

        builder.Append(indent);
        builder.AppendLine("}");

        if (namespaceName is not null)
        {
            builder.AppendLine("}");
        }

        return SourceText.From(
            builder.ToString(),
            Encoding.UTF8);
    }

    /// <summary>
    /// Emits the cached <c>PacketProtocolDefinition</c> field and public <c>Definition</c> property.
    /// </summary>
    /// <param name="builder">The <see cref="StringBuilder"/> accumulating generated source code.</param>
    /// <param name="indent">The indentation string for member declarations.</param>
    /// <param name="configuration">The protocol configuration settings.</param>
    private static void EmitDefinition(
        StringBuilder builder,
        string indent,
        ProtocolConfiguration configuration)
    {
        builder.Append(indent);

        builder.AppendLine(
            "private static readonly global::PacketWire.PacketProtocolDefinition __packetWireDefinition =");

        builder.Append(indent);
        builder.AppendLine("    new(");

        builder.Append(indent);
        builder.Append("        ");

        builder.Append(
            GetIntegerSizeExpression(
                configuration.PacketIdSize));

        builder.AppendLine(",");

        builder.Append(indent);
        builder.Append("        ");

        builder.Append(
            GetIntegerSizeExpression(
                configuration.PacketLengthSize));

        builder.AppendLine(",");

        builder.Append(indent);
        builder.Append("        ");

        builder.Append(
            GetIntegerSizeExpression(
                configuration.CollectionCountSize));

        builder.AppendLine(",");

        builder.Append(indent);
        builder.Append("        ");

        builder.Append(
            GetByteOrderExpression(
                configuration.ByteOrder));

        builder.AppendLine(",");

        builder.Append(indent);
        builder.Append("        ");

        builder.Append(
            GetIntegerSizeExpression(
                configuration.PacketCategorySize));

        builder.AppendLine(");");
        builder.AppendLine();

        builder.Append(indent);
        builder.AppendLine("/// <summary>");
        builder.Append(indent);
        builder.AppendLine("/// Gets the immutable protocol definition governing packet framing and serialization rules.");
        builder.Append(indent);
        builder.AppendLine("/// </summary>");
        builder.Append(indent);

        builder.AppendLine(
            "public static global::PacketWire.PacketProtocolDefinition Definition => __packetWireDefinition;");
    }

    /// <summary>
    /// Emits the public <c>TryGetIdentity</c> and <c>GetIdentity</c> facade methods and their generic counterparts.
    /// </summary>
    /// <param name="builder">The <see cref="StringBuilder"/> accumulating generated source code.</param>
    /// <param name="indent">The indentation string for member declarations.</param>
    /// <param name="bodyIndent">The indentation string for method bodies.</param>
    /// <param name="registryClassName">The name of the protocol's internal registry class.</param>
    private static void EmitIdentityMethods(
        StringBuilder builder,
        string indent,
        string bodyIndent,
        string registryClassName)
    {
        builder.Append(indent);
        builder.AppendLine("/// <summary>");
        builder.Append(indent);
        builder.AppendLine("/// Attempts to resolve the wire identity for the specified packet runtime type.");
        builder.Append(indent);
        builder.AppendLine("/// </summary>");
        builder.Append(indent);
        builder.AppendLine("/// <param name=\"packetType\">The runtime type of the packet candidate.</param>");
        builder.Append(indent);
        builder.AppendLine("/// <param name=\"identity\">When this method returns, contains the resolved packet identity if registered; otherwise, default.</param>");
        builder.Append(indent);
        builder.AppendLine("/// <returns><see langword=\"true\"/> if the packet type is registered in this protocol; otherwise, <see langword=\"false\"/>.</returns>");
        builder.Append(indent);

        builder.AppendLine(
            "public static bool TryGetIdentity(");

        builder.Append(indent);

        builder.AppendLine(
            "    global::System.Type packetType,");

        builder.Append(indent);

        builder.AppendLine(
            "    out global::PacketWire.PacketIdentity identity)");

        builder.Append(indent);
        builder.AppendLine("{");

        builder.Append(bodyIndent);

        builder.Append(
            "return global::PacketWire.Generated.");

        builder.Append(registryClassName);

        builder.AppendLine(
            ".TryGetIdentity(packetType, out identity);");

        builder.Append(indent);
        builder.AppendLine("}");
        builder.AppendLine();

        builder.Append(indent);
        builder.AppendLine("/// <summary>");
        builder.Append(indent);
        builder.AppendLine("/// Resolves the wire identity for the specified packet runtime type, throwing if not registered.");
        builder.Append(indent);
        builder.AppendLine("/// </summary>");
        builder.Append(indent);
        builder.AppendLine("/// <param name=\"packetType\">The runtime type of the packet candidate.</param>");
        builder.Append(indent);
        builder.AppendLine("/// <returns>The resolved packet identity.</returns>");
        builder.Append(indent);
        builder.AppendLine("/// <exception cref=\"global::System.ArgumentNullException\">Thrown if <paramref name=\"packetType\"/> is <see langword=\"null\"/>.</exception>");
        builder.Append(indent);
        builder.AppendLine("/// <exception cref=\"global::PacketWire.PacketTypeNotRegisteredException\">Thrown if the packet type is not registered in this protocol.</exception>");
        builder.Append(indent);

        builder.AppendLine(
            "public static global::PacketWire.PacketIdentity GetIdentity(global::System.Type packetType)");

        builder.Append(indent);
        builder.AppendLine("{");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "global::System.ArgumentNullException.ThrowIfNull(packetType);");

        builder.AppendLine();

        builder.Append(bodyIndent);

        builder.AppendLine(
            "if (!TryGetIdentity(packetType, out global::PacketWire.PacketIdentity identity))");

        builder.Append(bodyIndent);
        builder.AppendLine("{");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "    throw new global::PacketWire.PacketTypeNotRegisteredException(packetType);");

        builder.Append(bodyIndent);
        builder.AppendLine("}");

        builder.AppendLine();

        builder.Append(bodyIndent);

        builder.AppendLine(
            "return identity;");

        builder.Append(indent);
        builder.AppendLine("}");

        builder.AppendLine();

        builder.Append(indent);
        builder.AppendLine("/// <summary>");
        builder.Append(indent);
        builder.AppendLine("/// Attempts to resolve the wire identity for the packet type <typeparamref name=\"TPacket\"/>.");
        builder.Append(indent);
        builder.AppendLine("/// </summary>");
        builder.Append(indent);
        builder.AppendLine("/// <typeparam name=\"TPacket\">The packet candidate type.</typeparam>");
        builder.Append(indent);
        builder.AppendLine("/// <param name=\"identity\">When this method returns, contains the resolved packet identity if registered; otherwise, default.</param>");
        builder.Append(indent);
        builder.AppendLine("/// <returns><see langword=\"true\"/> if the packet type is registered in this protocol; otherwise, <see langword=\"false\"/>.</returns>");
        builder.Append(indent);

        builder.AppendLine(
            "public static bool TryGetIdentity<TPacket>(");

        builder.Append(indent);

        builder.AppendLine(
            "    out global::PacketWire.PacketIdentity identity)");

        builder.Append(indent);
        builder.AppendLine("{");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "return TryGetIdentity(typeof(TPacket), out identity);");

        builder.Append(indent);
        builder.AppendLine("}");

        builder.AppendLine();

        builder.Append(indent);
        builder.AppendLine("/// <summary>");
        builder.Append(indent);
        builder.AppendLine("/// Resolves the wire identity for the packet type <typeparamref name=\"TPacket\"/>, throwing if not registered.");
        builder.Append(indent);
        builder.AppendLine("/// </summary>");
        builder.Append(indent);
        builder.AppendLine("/// <typeparam name=\"TPacket\">The packet candidate type.</typeparam>");
        builder.Append(indent);
        builder.AppendLine("/// <returns>The resolved packet identity.</returns>");
        builder.Append(indent);
        builder.AppendLine("/// <exception cref=\"global::PacketWire.PacketTypeNotRegisteredException\">Thrown if the packet type is not registered in this protocol.</exception>");
        builder.Append(indent);

        builder.AppendLine(
            "public static global::PacketWire.PacketIdentity GetIdentity<TPacket>()");

        builder.Append(indent);
        builder.AppendLine("{");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "return GetIdentity(typeof(TPacket));");

        builder.Append(indent);
        builder.AppendLine("}");
    }

    /// <summary>
    /// Emits the public <c>Serialize</c> facade method that frames and encodes a packet into a byte array.
    /// </summary>
    /// <param name="builder">The <see cref="StringBuilder"/> accumulating generated source code.</param>
    /// <param name="indent">The indentation string for member declarations.</param>
    /// <param name="bodyIndent">The indentation string for method bodies.</param>
    /// <param name="registryClassName">The name of the protocol's internal registry class.</param>
    private static void EmitSerialize(
        StringBuilder builder,
        string indent,
        string bodyIndent,
        string registryClassName)
    {
        builder.Append(indent);
        builder.AppendLine("/// <summary>");
        builder.Append(indent);
        builder.AppendLine("/// Serializes a registered packet object into a complete, framed binary payload.");
        builder.Append(indent);
        builder.AppendLine("/// </summary>");
        builder.Append(indent);
        builder.AppendLine("/// <param name=\"packet\">The packet instance to serialize.</param>");
        builder.Append(indent);
        builder.AppendLine("/// <returns>A newly allocated byte array containing the framed and encoded packet.</returns>");
        builder.Append(indent);
        builder.AppendLine("/// <exception cref=\"global::System.ArgumentNullException\">Thrown if <paramref name=\"packet\"/> is <see langword=\"null\"/>.</exception>");
        builder.Append(indent);
        builder.AppendLine("/// <exception cref=\"global::PacketWire.PacketTypeNotRegisteredException\">Thrown if the packet type is not registered in this protocol.</exception>");
        builder.Append(indent);
        builder.AppendLine("/// <exception cref=\"global::PacketWire.PacketBufferException\">Thrown if the required buffer size exceeds maximum managed buffer capacity.</exception>");
        builder.Append(indent);
        builder.AppendLine("/// <exception cref=\"global::System.InvalidOperationException\">Thrown if internal codec serialization invariants are violated.</exception>");
        builder.Append(indent);

        builder.AppendLine(
            "public static byte[] Serialize(object packet)");

        builder.Append(indent);
        builder.AppendLine("{");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "global::System.ArgumentNullException.ThrowIfNull(packet);");

        builder.AppendLine();

        builder.Append(bodyIndent);
        builder.Append("if (!global::PacketWire.Generated.");
        builder.Append(registryClassName);

        builder.AppendLine(
            ".TryGetEncodedLength(");

        builder.Append(bodyIndent);
        builder.AppendLine("        packet,");

        builder.Append(bodyIndent);
        builder.AppendLine("        __packetWireDefinition,");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "        out global::PacketWire.PacketIdentity identity,");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "        out int payloadLength))");

        builder.Append(bodyIndent);
        builder.AppendLine("{");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "    throw new global::PacketWire.PacketTypeNotRegisteredException(packet.GetType());");

        builder.Append(bodyIndent);
        builder.AppendLine("}");

        builder.AppendLine();

        builder.Append(bodyIndent);

        builder.AppendLine(
            "ulong frameLength = global::PacketWire.PacketFrameCodec.CalculateFrameLength(");

        builder.Append(bodyIndent);
        builder.AppendLine("    payloadLength,");

        builder.Append(bodyIndent);
        builder.AppendLine("    __packetWireDefinition);");

        builder.AppendLine();

        builder.Append(bodyIndent);

        builder.AppendLine(
            "if (frameLength > int.MaxValue)");

        builder.Append(bodyIndent);
        builder.AppendLine("{");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "    throw new global::PacketWire.PacketBufferException(");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "        $\"The encoded packet requires {frameLength} bytes, which exceeds the maximum managed buffer size supported by PacketWire.\");");

        builder.Append(bodyIndent);
        builder.AppendLine("}");

        builder.AppendLine();

        builder.Append(bodyIndent);

        builder.AppendLine(
            "byte[] frame = new byte[(int)frameLength];");

        builder.AppendLine();

        builder.Append(bodyIndent);

        builder.AppendLine(
            "global::PacketWire.PacketWriter payloadWriter = new(");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "    new global::System.Span<byte>(");

        builder.Append(bodyIndent);
        builder.AppendLine("        frame,");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "        __packetWireDefinition.HeaderLength,");

        builder.Append(bodyIndent);
        builder.AppendLine("        payloadLength),");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "    __packetWireDefinition.ByteOrder);");

        builder.AppendLine();

        builder.Append(bodyIndent);
        builder.Append("if (!global::PacketWire.Generated.");
        builder.Append(registryClassName);

        builder.AppendLine(
            ".TryWrite(");

        builder.Append(bodyIndent);
        builder.AppendLine("        ref payloadWriter,");

        builder.Append(bodyIndent);
        builder.AppendLine("        packet,");

        builder.Append(bodyIndent);
        builder.AppendLine("        __packetWireDefinition,");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "        out global::PacketWire.PacketIdentity writtenIdentity))");

        builder.Append(bodyIndent);
        builder.AppendLine("{");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "    throw new global::System.InvalidOperationException(");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "        \"Packet registry dispatch failed after the packet type had already been resolved.\");");

        builder.Append(bodyIndent);
        builder.AppendLine("}");

        builder.AppendLine();

        builder.Append(bodyIndent);

        builder.AppendLine(
            "if (writtenIdentity != identity)");

        builder.Append(bodyIndent);
        builder.AppendLine("{");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "    throw new global::System.InvalidOperationException(");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "        \"Packet registry returned inconsistent identities between length calculation and serialization.\");");

        builder.Append(bodyIndent);
        builder.AppendLine("}");

        builder.AppendLine();

        builder.Append(bodyIndent);

        builder.AppendLine(
            "if (payloadWriter.WrittenCount != payloadLength)");

        builder.Append(bodyIndent);
        builder.AppendLine("{");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "    throw new global::System.InvalidOperationException(");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "        $\"Generated codec reported payload length {payloadLength} but wrote {payloadWriter.WrittenCount} bytes.\");");

        builder.Append(bodyIndent);
        builder.AppendLine("}");

        builder.AppendLine();

        builder.Append(bodyIndent);

        builder.AppendLine(
            "_ = global::PacketWire.PacketFrameCodec.WriteHeader(");

        builder.Append(bodyIndent);
        builder.AppendLine("    frame,");

        builder.Append(bodyIndent);
        builder.AppendLine("    frameLength,");

        builder.Append(bodyIndent);
        builder.AppendLine("    identity.Category,");

        builder.Append(bodyIndent);
        builder.AppendLine("    identity.Id,");

        builder.Append(bodyIndent);
        builder.AppendLine("    __packetWireDefinition);");

        builder.AppendLine();

        builder.Append(bodyIndent);
        builder.AppendLine("return frame;");

        builder.Append(indent);
        builder.AppendLine("}");
    }

    /// <summary>
    /// Emits the public <c>Deserialize</c> facade methods for decoding binary frames into untyped or typed packet objects.
    /// </summary>
    /// <param name="builder">The <see cref="StringBuilder"/> accumulating generated source code.</param>
    /// <param name="indent">The indentation string for member declarations.</param>
    /// <param name="bodyIndent">The indentation string for method bodies.</param>
    /// <param name="registryClassName">The name of the protocol's internal registry class.</param>
    private static void EmitDeserialize(
        StringBuilder builder,
        string indent,
        string bodyIndent,
        string registryClassName)
    {
        builder.Append(indent);
        builder.AppendLine("/// <summary>");
        builder.Append(indent);
        builder.AppendLine("/// Deserializes a framed binary payload into its registered packet object instance.");
        builder.Append(indent);
        builder.AppendLine("/// </summary>");
        builder.Append(indent);
        builder.AppendLine("/// <param name=\"packetBytes\">The read-only span of bytes containing the complete framed packet.</param>");
        builder.Append(indent);
        builder.AppendLine("/// <returns>The deserialized packet object.</returns>");
        builder.Append(indent);
        builder.AppendLine("/// <exception cref=\"global::PacketWire.PacketProtectionRequiredException\">Thrown if the frame indicates that payload protection is required.</exception>");
        builder.Append(indent);
        builder.AppendLine("/// <exception cref=\"global::PacketWire.PacketBufferException\">Thrown if the buffer is malformed or framing invariants are violated.</exception>");
        builder.Append(indent);
        builder.AppendLine("/// <exception cref=\"global::PacketWire.PacketIdentityNotRegisteredException\">Thrown if the frame's packet identity is not registered in this protocol.</exception>");
        builder.Append(indent);

        builder.AppendLine(
            "public static object Deserialize(global::System.ReadOnlySpan<byte> packetBytes)");

        builder.Append(indent);
        builder.AppendLine("{");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "global::PacketWire.PacketFrameView frame =");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "    global::PacketWire.PacketFrameCodec.ReadFrame(");

        builder.Append(bodyIndent);
        builder.AppendLine("        packetBytes,");

        builder.Append(bodyIndent);
        builder.AppendLine("        __packetWireDefinition);");

        builder.AppendLine();

        builder.Append(bodyIndent);

        builder.AppendLine(
            "if ((frame.Header.Flags & global::PacketWire.PacketFrameOptions.Protected) != 0)");

        builder.Append(bodyIndent);
        builder.AppendLine("{");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "    throw new global::PacketWire.PacketProtectionRequiredException(frame.Header.Flags);");

        builder.Append(bodyIndent);
        builder.AppendLine("}");

        builder.AppendLine();

        builder.Append(bodyIndent);

        builder.AppendLine(
            "if (frame.Header.Flags != global::PacketWire.PacketFrameOptions.None)");

        builder.Append(bodyIndent);
        builder.AppendLine("{");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "    throw new global::PacketWire.PacketBufferException(");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "        $\"Packet frame options '{frame.Header.Flags}' cannot be processed by the plain Deserialize overload.\");");

        builder.Append(bodyIndent);
        builder.AppendLine("}");

        builder.AppendLine();

        builder.Append(bodyIndent);

        builder.AppendLine(
            "global::PacketWire.PacketIdentity identity = new(");

        builder.Append(bodyIndent);
        builder.AppendLine("    frame.Header.PacketCategory,");

        builder.Append(bodyIndent);
        builder.AppendLine("    frame.Header.PacketId);");

        builder.AppendLine();

        builder.Append(bodyIndent);

        builder.AppendLine(
            "global::PacketWire.PacketReader reader = new(");

        builder.Append(bodyIndent);
        builder.AppendLine("    frame.Payload,");

        builder.Append(bodyIndent);
        builder.AppendLine("    __packetWireDefinition.ByteOrder);");

        builder.AppendLine();

        builder.Append(bodyIndent);
        builder.Append("if (!global::PacketWire.Generated.");
        builder.Append(registryClassName);

        builder.AppendLine(
            ".TryRead(");

        builder.Append(bodyIndent);
        builder.AppendLine("        identity,");

        builder.Append(bodyIndent);
        builder.AppendLine("        ref reader,");

        builder.Append(bodyIndent);
        builder.AppendLine("        __packetWireDefinition,");

        builder.Append(bodyIndent);
        builder.AppendLine("        out object? packet) ||");

        builder.Append(bodyIndent);
        builder.AppendLine("    packet is null)");

        builder.Append(bodyIndent);
        builder.AppendLine("{");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "    throw new global::PacketWire.PacketIdentityNotRegisteredException(identity);");

        builder.Append(bodyIndent);
        builder.AppendLine("}");

        builder.AppendLine();

        builder.Append(bodyIndent);

        builder.AppendLine(
            "if (reader.Remaining != 0)");

        builder.Append(bodyIndent);
        builder.AppendLine("{");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "    throw new global::PacketWire.PacketBufferException(");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "        $\"Generated codec did not consume the complete payload. Remaining: {reader.Remaining} bytes.\");");

        builder.Append(bodyIndent);
        builder.AppendLine("}");

        builder.AppendLine();

        builder.Append(bodyIndent);
        builder.AppendLine("return packet;");

        builder.Append(indent);
        builder.AppendLine("}");
        builder.AppendLine();

        builder.Append(indent);
        builder.AppendLine("/// <summary>");
        builder.Append(indent);
        builder.AppendLine("/// Deserializes a framed binary payload into a strongly typed packet instance of <typeparamref name=\"TPacket\"/>.");
        builder.Append(indent);
        builder.AppendLine("/// </summary>");
        builder.Append(indent);
        builder.AppendLine("/// <typeparam name=\"TPacket\">The expected packet type.</typeparam>");
        builder.Append(indent);
        builder.AppendLine("/// <param name=\"packetBytes\">The read-only span of bytes containing the complete framed packet.</param>");
        builder.Append(indent);
        builder.AppendLine("/// <returns>The deserialized packet instance of type <typeparamref name=\"TPacket\"/>.</returns>");
        builder.Append(indent);
        builder.AppendLine("/// <exception cref=\"global::PacketWire.PacketProtectionRequiredException\">Thrown if the frame indicates that payload protection is required.</exception>");
        builder.Append(indent);
        builder.AppendLine("/// <exception cref=\"global::PacketWire.PacketBufferException\">Thrown if the buffer is malformed or framing invariants are violated.</exception>");
        builder.Append(indent);
        builder.AppendLine("/// <exception cref=\"global::PacketWire.PacketIdentityNotRegisteredException\">Thrown if the frame's packet identity is not registered in this protocol.</exception>");
        builder.Append(indent);
        builder.AppendLine("/// <exception cref=\"global::PacketWire.PacketTypeMismatchException\">Thrown if the decoded packet's runtime type does not match <typeparamref name=\"TPacket\"/>.</exception>");
        builder.Append(indent);

        builder.AppendLine(
            "public static TPacket Deserialize<TPacket>(global::System.ReadOnlySpan<byte> packetBytes)");

        builder.Append(indent);
        builder.AppendLine("{");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "object packet = Deserialize(packetBytes);");

        builder.AppendLine();

        builder.Append(bodyIndent);

        builder.AppendLine(
            "if (packet is TPacket typedPacket)");

        builder.Append(bodyIndent);
        builder.AppendLine("{");

        builder.Append(bodyIndent);
        builder.AppendLine("    return typedPacket;");

        builder.Append(bodyIndent);
        builder.AppendLine("}");

        builder.AppendLine();

        builder.Append(bodyIndent);

        builder.AppendLine(
            "throw new global::PacketWire.PacketTypeMismatchException(");

        builder.Append(bodyIndent);
        builder.AppendLine("    typeof(TPacket),");

        builder.Append(bodyIndent);
        builder.AppendLine("    packet.GetType());");

        builder.Append(indent);
        builder.AppendLine("}");
    }

    /// <summary>
    /// Locates the <c>PacketProtocolAttribute</c> application on the protocol type symbol if present.
    /// </summary>
    /// <param name="protocolType">The named type symbol of the protocol class.</param>
    /// <returns>The matching <see cref="AttributeData"/>, or <see langword="null"/> if not found.</returns>
    private static AttributeData? FindProtocolAttribute(
        INamedTypeSymbol protocolType)
    {
        return protocolType
            .GetAttributes()
            .FirstOrDefault(
                static attribute =>
                    attribute.AttributeClass
                        ?.ToDisplayString()
                    == GeneratorMetadataNames.PacketProtocolAttribute);
    }

    /// <summary>
    /// Reads and validates the constructor arguments from a <c>PacketProtocolAttribute</c> application into a configuration record.
    /// </summary>
    /// <param name="attribute">The attribute data instance.</param>
    /// <param name="configuration">When this method returns, contains the parsed configuration if valid; otherwise, default.</param>
    /// <returns><see langword="true"/> if the configuration was successfully read; otherwise, <see langword="false"/>.</returns>
    private static bool TryReadProtocolConfiguration(
        AttributeData attribute,
        out ProtocolConfiguration configuration)
    {
        configuration = default;

        if (attribute.ConstructorArguments.Length < 4 ||
            attribute.ConstructorArguments[0].Value is not int packetIdSize ||
            attribute.ConstructorArguments[1].Value is not int packetLengthSize ||
            attribute.ConstructorArguments[2].Value is not int collectionCountSize ||
            attribute.ConstructorArguments[3].Value is not int byteOrder)
        {
            return false;
        }

        int packetCategorySize = 1;

        if (attribute.ConstructorArguments.Length >= 5)
        {
            if (attribute.ConstructorArguments[4].Value
                is not int configuredCategorySize)
            {
                return false;
            }

            packetCategorySize =
                configuredCategorySize;
        }

        configuration =
            new ProtocolConfiguration(
                packetIdSize,
                packetLengthSize,
                collectionCountSize,
                byteOrder,
                packetCategorySize);

        return true;
    }

    /// <summary>
    /// Translates an integer byte size value into its corresponding <c>PacketIntegerSize</c> C# enum expression.
    /// </summary>
    /// <param name="value">The byte width (1, 2, 4, or 8).</param>
    /// <returns>The fully qualified enum member expression string.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the integer size is unsupported.</exception>
    private static string GetIntegerSizeExpression(
        int value)
    {
        return value switch
        {
            1 => "global::PacketWire.PacketIntegerSize.OneByte",
            2 => "global::PacketWire.PacketIntegerSize.TwoBytes",
            4 => "global::PacketWire.PacketIntegerSize.FourBytes",
            8 => "global::PacketWire.PacketIntegerSize.EightBytes",
            _ => throw new InvalidOperationException(
                $"Unsupported packet integer size '{value}'.")
        };
    }

    /// <summary>
    /// Translates an endianness integer value into its corresponding <c>PacketByteOrder</c> C# enum expression.
    /// </summary>
    /// <param name="value">The endianness integer value (1 for LittleEndian, 2 for BigEndian).</param>
    /// <returns>The fully qualified enum member expression string.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the byte order is unsupported.</exception>
    private static string GetByteOrderExpression(
        int value)
    {
        return value switch
        {
            1 => "global::PacketWire.PacketByteOrder.LittleEndian",
            2 => "global::PacketWire.PacketByteOrder.BigEndian",
            _ => throw new InvalidOperationException(
                $"Unsupported packet byte order '{value}'.")
        };
    }

    /// <summary>
    /// Appends the C# accessibility keyword corresponding to the declared accessibility of the protocol class.
    /// </summary>
    /// <param name="builder">The <see cref="StringBuilder"/> accumulating generated source code.</param>
    /// <param name="accessibility">The declared accessibility of the protocol class symbol.</param>
    /// <exception cref="InvalidOperationException">Thrown if accessibility is not public or internal.</exception>
    private static void AppendAccessibility(
        StringBuilder builder,
        Accessibility accessibility)
    {
        switch (accessibility)
        {
            case Accessibility.Public:
                builder.Append("public ");
                break;

            case Accessibility.Internal:
                builder.Append("internal ");
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported top-level protocol accessibility '{accessibility}'.");
        }
    }

    /// <summary>
    /// Computes the Roslyn source hint name for the generated facade companion file.
    /// </summary>
    /// <param name="protocolType">The named type symbol of the protocol class.</param>
    /// <returns>The string hint name.</returns>
    private static string GetHintName(
        INamedTypeSymbol protocolType)
    {
        string identity =
            GetSymbolKey(
                protocolType);

        string sanitized =
            SanitizeIdentifier(
                protocolType.Name);

        ulong hash =
            ComputeStableHash(identity);

        return
            "PacketWire.ProtocolFacade." +
            sanitized +
            "." +
            hash.ToString(
                "X16",
                CultureInfo.InvariantCulture) +
            ".g.cs";
    }

    /// <summary>
    /// Retrieves a unique identity key for a named type symbol using fully qualified display format.
    /// </summary>
    /// <param name="symbol">The named type symbol.</param>
    /// <returns>A string key uniquely identifying the type symbol.</returns>
    private static string GetSymbolKey(
        INamedTypeSymbol symbol)
    {
        return symbol.ToDisplayString(
            SymbolDisplayFormat.FullyQualifiedFormat);
    }

    /// <summary>
    /// Locates the primary source code location for diagnostic reporting on a symbol.
    /// </summary>
    /// <param name="symbol">The code analysis symbol.</param>
    /// <returns>The source location if available; otherwise, <see cref="Location.None"/>.</returns>
    private static Location GetBestLocation(
        ISymbol symbol)
    {
        return symbol.Locations.FirstOrDefault(
                   static location => location.IsInSource)
            ?? symbol.Locations.FirstOrDefault()
            ?? Location.None;
    }

    /// <summary>
    /// Replaces non-alphanumeric characters in an identifier string with underscores to produce a valid C# identifier.
    /// </summary>
    /// <param name="value">The raw identifier candidate string.</param>
    /// <returns>A sanitized C# identifier string.</returns>
    private static string SanitizeIdentifier(
        string value)
    {
        StringBuilder builder =
            new(value.Length);

        foreach (char character in value)
        {
            builder.Append(
                char.IsLetterOrDigit(character) ||
                character == '_'
                    ? character
                    : '_');
        }

        return builder.ToString();
    }

    /// <summary>
    /// Computes a 64-bit Fowler-Noll-Vo (FNV-1a) hash for the given string to ensure deterministic unique naming.
    /// </summary>
    /// <param name="value">The string to hash.</param>
    /// <returns>The 64-bit unsigned hash value.</returns>
    private static ulong ComputeStableHash(
        string value)
    {
        const ulong offsetBasis =
            14695981039346656037UL;

        const ulong prime =
            1099511628211UL;

        ulong hash =
            offsetBasis;

        foreach (char character in value)
        {
            hash ^= character;
            hash *= prime;
        }

        return hash;
    }

    /// <summary>
    /// Encapsulates the binary framing configuration parsed from a <c>PacketProtocolAttribute</c>.
    /// </summary>
    private readonly struct ProtocolConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ProtocolConfiguration"/> struct.
        /// </summary>
        /// <param name="packetIdSize">The packet identifier byte width.</param>
        /// <param name="packetLengthSize">The packet length byte width.</param>
        /// <param name="collectionCountSize">The collection count prefix byte width.</param>
        /// <param name="byteOrder">The protocol byte order (endianness).</param>
        /// <param name="packetCategorySize">The packet category byte width.</param>
        internal ProtocolConfiguration(
            int packetIdSize,
            int packetLengthSize,
            int collectionCountSize,
            int byteOrder,
            int packetCategorySize)
        {
            PacketIdSize = packetIdSize;
            PacketLengthSize = packetLengthSize;
            CollectionCountSize = collectionCountSize;
            ByteOrder = byteOrder;
            PacketCategorySize = packetCategorySize;
        }

        /// <summary>
        /// Gets the configured packet identifier size in bytes.
        /// </summary>
        internal int PacketIdSize { get; }

        /// <summary>
        /// Gets the configured packet length field size in bytes.
        /// </summary>
        internal int PacketLengthSize { get; }

        /// <summary>
        /// Gets the configured collection count prefix size in bytes.
        /// </summary>
        internal int CollectionCountSize { get; }

        /// <summary>
        /// Gets the configured byte order (1 for LittleEndian, 2 for BigEndian).
        /// </summary>
        internal int ByteOrder { get; }

        /// <summary>
        /// Gets the configured packet category field size in bytes.
        /// </summary>
        internal int PacketCategorySize { get; }
    }
}

