using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace PacketWire.Generator;

/// <summary>
/// Emits internal registry dispatchers that map packet types to identities and route serialization/deserialization calls for a protocol.
/// </summary>
internal static class GeneratedRegistryEmitter
{
    /// <summary>
    /// Groups validated packet candidates by their associated protocol and emits a generated registry source file for each protocol group.
    /// </summary>
    /// <param name="context">The Roslyn source production context for adding generated sources.</param>
    /// <param name="packets">The immutable array of packet candidates discovered during compilation.</param>
    /// <param name="generatedWireTypeKeys">The set of successfully generated wire type symbol keys.</param>
    internal static void EmitRegistries(
        SourceProductionContext context,
        ImmutableArray<PacketCandidate> packets,
        HashSet<string> generatedWireTypeKeys)
    {
        IEnumerable<IGrouping<string, PacketCandidate>> protocolGroups =
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
            IGrouping<string, PacketCandidate> protocolGroup
            in protocolGroups)
        {
            PacketCandidate[] registryEntries =
                protocolGroup
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
                    .ThenBy(
                        static packet =>
                            GetSymbolKey(
                                packet.PacketType),
                        StringComparer.Ordinal)
                    .ToArray();

            if (registryEntries.Length == 0)
            {
                continue;
            }

            INamedTypeSymbol protocolType =
                registryEntries[0].ProtocolType;

            context.AddSource(
                GetHintName(protocolType),
                Emit(
                    protocolType,
                    registryEntries));
        }
    }

    /// <summary>
    /// Computes the Roslyn source hint name for the generated registry file associated with the specified protocol type.
    /// </summary>
    /// <param name="protocolType">The named type symbol representing the protocol marker.</param>
    /// <returns>A string hint name suitable for source output.</returns>
    internal static string GetHintName(
        INamedTypeSymbol protocolType)
    {
        string identity =
            GetSymbolKey(
                protocolType);

        string sanitized =
            SanitizeIdentifier(
                protocolType.Name);

        ulong hash =
            ComputeStableHash(
                identity);

        return
            "PacketWire.Registry." +
            sanitized +
            "." +
            hash.ToString(
                "X16",
                CultureInfo.InvariantCulture) +
            ".g.cs";
    }

    /// <summary>
    /// Computes the deterministic C# identifier for the internal static registry class generated for the specified protocol.
    /// </summary>
    /// <param name="protocolType">The named type symbol representing the protocol marker.</param>
    /// <returns>A string identifier for the generated registry class.</returns>
    internal static string GetRegistryClassName(
        INamedTypeSymbol protocolType)
    {
        string identity =
            GetSymbolKey(
                protocolType);

        string sanitized =
            SanitizeIdentifier(
                protocolType.Name);

        ulong hash =
            ComputeStableHash(
                identity);

        return
            "__PacketWireRegistry_" +
            sanitized +
            "_" +
            hash.ToString(
                "X16",
                CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Emits the complete C# source text of the generated registry class for the given protocol and its registered packet candidates.
    /// </summary>
    /// <param name="protocolType">The named type symbol representing the protocol marker.</param>
    /// <param name="entries">The array of validated packet candidates registered to this protocol.</param>
    /// <returns>A <see cref="SourceText"/> containing the generated registry source code.</returns>
    private static SourceText Emit(
        INamedTypeSymbol protocolType,
        PacketCandidate[] entries)
    {
        StringBuilder builder =
            new();

        string registryClassName =
            GetRegistryClassName(
                protocolType);

        builder.AppendLine("// <auto-generated/>");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();
        builder.AppendLine("namespace PacketWire.Generated");
        builder.AppendLine("{");

        builder.Append(
            "    internal static class ");

        builder.AppendLine(
            registryClassName);

        builder.AppendLine("    {");

        builder.Append(
            "        internal static int PacketCount => ");

        builder.Append(
            entries.Length.ToString(
                CultureInfo.InvariantCulture));

        builder.AppendLine(";");
        builder.AppendLine();

        EmitTypeToIdentity(
            builder,
            entries);

        builder.AppendLine();

        EmitIdentityToType(
            builder,
            entries);

        builder.AppendLine();

        EmitGetEncodedLength(
            builder,
            entries);

        builder.AppendLine();

        EmitWrite(
            builder,
            entries);

        builder.AppendLine();

        EmitRead(
            builder,
            entries);

        builder.AppendLine("    }");
        builder.AppendLine("}");

        return SourceText.From(
            builder.ToString(),
            Encoding.UTF8);
    }

    /// <summary>
    /// Emits the <c>TryGetIdentity</c> method mapping a <see cref="Type"/> to its registered <c>PacketIdentity</c>.
    /// </summary>
    /// <param name="builder">The <see cref="StringBuilder"/> accumulating generated source code.</param>
    /// <param name="entries">The array of registered packet candidates.</param>
    private static void EmitTypeToIdentity(
        StringBuilder builder,
        PacketCandidate[] entries)
    {
        builder.AppendLine(
            "        internal static bool TryGetIdentity(");

        builder.AppendLine(
            "            global::System.Type packetType,");

        builder.AppendLine(
            "            out global::PacketWire.PacketIdentity identity)");

        builder.AppendLine("        {");

        builder.AppendLine(
            "            global::System.ArgumentNullException.ThrowIfNull(packetType);");

        foreach (PacketCandidate entry in entries)
        {
            string packetTypeName =
                GetTypeName(
                    entry.PacketType);

            builder.AppendLine();

            builder.Append(
                "            if (packetType == typeof(");

            builder.Append(
                packetTypeName);

            builder.AppendLine("))");
            builder.AppendLine("            {");

            builder.Append(
                "                identity = ");

            AppendIdentityConstruction(
                builder,
                entry);

            builder.AppendLine(";");

            builder.AppendLine(
                "                return true;");

            builder.AppendLine("            }");
        }

        builder.AppendLine();

        builder.AppendLine(
            "            identity = default;");

        builder.AppendLine(
            "            return false;");

        builder.AppendLine("        }");
    }

    /// <summary>
    /// Emits the <c>TryGetPacketType</c> method resolving a runtime <c>PacketIdentity</c> to its registered <see cref="Type"/>.
    /// </summary>
    /// <param name="builder">The <see cref="StringBuilder"/> accumulating generated source code.</param>
    /// <param name="entries">The array of registered packet candidates.</param>
    private static void EmitIdentityToType(
        StringBuilder builder,
        PacketCandidate[] entries)
    {
        builder.AppendLine(
            "        internal static bool TryGetPacketType(");

        builder.AppendLine(
            "            global::PacketWire.PacketIdentity identity,");

        builder.AppendLine(
            "            out global::System.Type? packetType)");

        builder.AppendLine("        {");

        foreach (PacketCandidate entry in entries)
        {
            string packetTypeName =
                GetTypeName(
                    entry.PacketType);

            builder.AppendLine();

            EmitIdentityCondition(
                builder,
                entry,
                "            ");

            builder.AppendLine("            {");

            builder.Append(
                "                packetType = typeof(");

            builder.Append(
                packetTypeName);

            builder.AppendLine(");");

            builder.AppendLine(
                "                return true;");

            builder.AppendLine("            }");
        }

        builder.AppendLine();

        builder.AppendLine(
            "            packetType = null;");

        builder.AppendLine(
            "            return false;");

        builder.AppendLine("        }");
    }

    /// <summary>
    /// Emits the <c>TryGetEncodedLength</c> method calculating the serialized byte length of a packet object and resolving its identity.
    /// </summary>
    /// <param name="builder">The <see cref="StringBuilder"/> accumulating generated source code.</param>
    /// <param name="entries">The array of registered packet candidates.</param>
    private static void EmitGetEncodedLength(
        StringBuilder builder,
        PacketCandidate[] entries)
    {
        builder.AppendLine(
            "        internal static bool TryGetEncodedLength(");

        builder.AppendLine(
            "            object packet,");

        builder.AppendLine(
            "            global::PacketWire.PacketProtocolDefinition definition,");

        builder.AppendLine(
            "            out global::PacketWire.PacketIdentity identity,");

        builder.AppendLine(
            "            out int encodedLength)");

        builder.AppendLine("        {");

        builder.AppendLine(
            "            global::System.ArgumentNullException.ThrowIfNull(packet);");

        builder.AppendLine(
            "            global::System.ArgumentNullException.ThrowIfNull(definition);");

        for (
            int index = 0;
            index < entries.Length;
            index++)
        {
            PacketCandidate entry =
                entries[index];

            string packetTypeName =
                GetTypeName(
                    entry.PacketType);

            string codecClassName =
                GeneratedCodecEmitter.GetCodecClassName(
                    entry.PacketType);

            string localName =
                "__packet" +
                index.ToString(
                    CultureInfo.InvariantCulture);

            builder.AppendLine();

            builder.Append(
                "            if (packet is ");

            builder.Append(
                packetTypeName);

            builder.Append(' ');
            builder.Append(localName);
            builder.AppendLine(")");

            builder.AppendLine("            {");

            builder.Append(
                "                identity = ");

            AppendIdentityConstruction(
                builder,
                entry);

            builder.AppendLine(";");

            builder.Append(
                "                encodedLength = ");

            builder.Append(codecClassName);

            builder.Append(
                ".GetEncodedLength(");

            builder.Append(localName);

            builder.AppendLine(
                ", definition);");

            builder.AppendLine(
                "                return true;");

            builder.AppendLine("            }");
        }

        builder.AppendLine();

        builder.AppendLine(
            "            identity = default;");

        builder.AppendLine(
            "            encodedLength = 0;");

        builder.AppendLine(
            "            return false;");

        builder.AppendLine("        }");
    }

    /// <summary>
    /// Emits the <c>TryWrite</c> method serializing a packet object to a <c>PacketWriter</c> using its generated codec.
    /// </summary>
    /// <param name="builder">The <see cref="StringBuilder"/> accumulating generated source code.</param>
    /// <param name="entries">The array of registered packet candidates.</param>
    private static void EmitWrite(
        StringBuilder builder,
        PacketCandidate[] entries)
    {
        builder.AppendLine(
            "        internal static bool TryWrite(");

        builder.AppendLine(
            "            ref global::PacketWire.PacketWriter writer,");

        builder.AppendLine(
            "            object packet,");

        builder.AppendLine(
            "            global::PacketWire.PacketProtocolDefinition definition,");

        builder.AppendLine(
            "            out global::PacketWire.PacketIdentity identity)");

        builder.AppendLine("        {");

        builder.AppendLine(
            "            global::System.ArgumentNullException.ThrowIfNull(packet);");

        builder.AppendLine(
            "            global::System.ArgumentNullException.ThrowIfNull(definition);");

        for (
            int index = 0;
            index < entries.Length;
            index++)
        {
            PacketCandidate entry =
                entries[index];

            string packetTypeName =
                GetTypeName(
                    entry.PacketType);

            string codecClassName =
                GeneratedCodecEmitter.GetCodecClassName(
                    entry.PacketType);

            string localName =
                "__packet" +
                index.ToString(
                    CultureInfo.InvariantCulture);

            builder.AppendLine();

            builder.Append(
                "            if (packet is ");

            builder.Append(
                packetTypeName);

            builder.Append(' ');
            builder.Append(localName);
            builder.AppendLine(")");

            builder.AppendLine("            {");

            builder.Append(
                "                identity = ");

            AppendIdentityConstruction(
                builder,
                entry);

            builder.AppendLine(";");

            builder.Append(
                "                ");

            builder.Append(codecClassName);

            builder.Append(
                ".Write(ref writer, ");

            builder.Append(localName);

            builder.AppendLine(
                ", definition);");

            builder.AppendLine(
                "                return true;");

            builder.AppendLine("            }");
        }

        builder.AppendLine();

        builder.AppendLine(
            "            identity = default;");

        builder.AppendLine(
            "            return false;");

        builder.AppendLine("        }");
    }

    /// <summary>
    /// Emits the <c>TryRead</c> method deserializing a packet object from a <c>PacketReader</c> matching the given identity.
    /// </summary>
    /// <param name="builder">The <see cref="StringBuilder"/> accumulating generated source code.</param>
    /// <param name="entries">The array of registered packet candidates.</param>
    private static void EmitRead(
        StringBuilder builder,
        PacketCandidate[] entries)
    {
        builder.AppendLine(
            "        internal static bool TryRead(");

        builder.AppendLine(
            "            global::PacketWire.PacketIdentity identity,");

        builder.AppendLine(
            "            ref global::PacketWire.PacketReader reader,");

        builder.AppendLine(
            "            global::PacketWire.PacketProtocolDefinition definition,");

        builder.AppendLine(
            "            out object? packet)");

        builder.AppendLine("        {");

        builder.AppendLine(
            "            global::System.ArgumentNullException.ThrowIfNull(definition);");

        foreach (PacketCandidate entry in entries)
        {
            string codecClassName =
                GeneratedCodecEmitter.GetCodecClassName(
                    entry.PacketType);

            builder.AppendLine();

            EmitIdentityCondition(
                builder,
                entry,
                "            ");

            builder.AppendLine("            {");

            builder.Append(
                "                packet = ");

            builder.Append(codecClassName);

            builder.AppendLine(
                ".Read(ref reader, definition);");

            builder.AppendLine(
                "                return true;");

            builder.AppendLine("            }");
        }

        builder.AppendLine();

        builder.AppendLine(
            "            packet = null;");

        builder.AppendLine(
            "            return false;");

        builder.AppendLine("        }");
    }

    /// <summary>
    /// Emits an <c>if</c> condition matching a <c>PacketIdentity</c>'s category and ID against a candidate.
    /// </summary>
    /// <param name="builder">The <see cref="StringBuilder"/> accumulating generated source code.</param>
    /// <param name="entry">The packet candidate containing expected category and ID.</param>
    /// <param name="indent">The indentation string for formatting emitted code.</param>
    private static void EmitIdentityCondition(
        StringBuilder builder,
        PacketCandidate entry,
        string indent)
    {
        builder.Append(indent);

        builder.Append(
            "if (identity.Category == ");

        AppendUnsignedLongLiteral(
            builder,
            entry.PacketCategory);

        builder.Append(
            " && identity.Id == ");

        AppendUnsignedLongLiteral(
            builder,
            entry.PacketId);

        builder.AppendLine(")");
    }

    /// <summary>
    /// Emits the expression that constructs a <c>PacketIdentity</c> with unsigned long literals.
    /// </summary>
    /// <param name="builder">The <see cref="StringBuilder"/> accumulating generated source code.</param>
    /// <param name="entry">The packet candidate containing category and ID.</param>
    private static void AppendIdentityConstruction(
        StringBuilder builder,
        PacketCandidate entry)
    {
        builder.Append(
            "new global::PacketWire.PacketIdentity(");

        AppendUnsignedLongLiteral(
            builder,
            entry.PacketCategory);

        builder.Append(", ");

        AppendUnsignedLongLiteral(
            builder,
            entry.PacketId);

        builder.Append(')');
    }

    /// <summary>
    /// Emits a 64-bit unsigned integer literal formatted with an invariant culture and the 'UL' suffix.
    /// </summary>
    /// <param name="builder">The <see cref="StringBuilder"/> accumulating generated source code.</param>
    /// <param name="value">The 64-bit unsigned integer value.</param>
    private static void AppendUnsignedLongLiteral(
        StringBuilder builder,
        ulong value)
    {
        builder.Append(
            value.ToString(
                CultureInfo.InvariantCulture));

        builder.Append("UL");
    }

    /// <summary>
    /// Formats a named type symbol into a fully qualified C# type reference string.
    /// </summary>
    /// <param name="type">The named type symbol to format.</param>
    /// <returns>The fully qualified type name string.</returns>
    private static string GetTypeName(
        INamedTypeSymbol type)
    {
        return type.ToDisplayString(
            SymbolDisplayFormat.FullyQualifiedFormat);
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
}
