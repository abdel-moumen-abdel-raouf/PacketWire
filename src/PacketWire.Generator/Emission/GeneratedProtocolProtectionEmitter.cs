using System.Text;

namespace PacketWire.Generator;

/// <summary>
/// Emits protected serialization and deserialization facade methods for protocols requiring or supporting cryptographic payload protectors.
/// </summary>
internal static partial class GeneratedProtocolFacadeEmitter
{
    /// <summary>
    /// Emits the protected <c>Serialize</c> facade method that encrypts or authenticates packet payloads using an <c>IPayloadProtector</c>.
    /// </summary>
    /// <param name="builder">The <see cref="StringBuilder"/> accumulating generated source code.</param>
    /// <param name="indent">The indentation string for member declarations.</param>
    /// <param name="bodyIndent">The indentation string for method bodies.</param>
    /// <param name="registryClassName">The name of the protocol's internal registry class.</param>
    private static void EmitProtectedSerialize(
        StringBuilder builder,
        string indent,
        string bodyIndent,
        string registryClassName)
    {
        builder.Append(indent);
        builder.AppendLine("/// <summary>");
        builder.Append(indent);
        builder.AppendLine("/// Serializes and protects a registered packet object using the specified payload protector.");
        builder.Append(indent);
        builder.AppendLine("/// </summary>");
        builder.Append(indent);
        builder.AppendLine("/// <param name=\"packet\">The packet instance to serialize.</param>");
        builder.Append(indent);
        builder.AppendLine("/// <param name=\"protector\">The payload protector used to protect the serialized payload.</param>");
        builder.Append(indent);
        builder.AppendLine("/// <returns>A newly allocated byte array containing the framed and protected packet.</returns>");
        builder.Append(indent);
        builder.AppendLine("/// <exception cref=\"global::System.ArgumentNullException\">Thrown if <paramref name=\"packet\"/> or <paramref name=\"protector\"/> is <see langword=\"null\"/>.</exception>");
        builder.Append(indent);
        builder.AppendLine("/// <exception cref=\"global::PacketWire.PacketTypeNotRegisteredException\">Thrown if the packet type is not registered in this protocol.</exception>");
        builder.Append(indent);
        builder.AppendLine("/// <exception cref=\"global::System.InvalidOperationException\">Thrown if the protector returns an invalid protected payload length.</exception>");
        builder.Append(indent);
        builder.AppendLine("/// <exception cref=\"global::PacketWire.Security.PayloadProtectionException\">Thrown if payload protection fails.</exception>");
        builder.Append(indent);

        builder.AppendLine(
            "public static byte[] Serialize(");

        builder.Append(indent);
        builder.AppendLine("    object packet,");

        builder.Append(indent);

        builder.AppendLine(
            "    global::PacketWire.Security.IPayloadProtector protector)");

        builder.Append(indent);
        builder.AppendLine("{");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "global::System.ArgumentNullException.ThrowIfNull(packet);");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "global::System.ArgumentNullException.ThrowIfNull(protector);");

        builder.AppendLine();

        builder.Append(bodyIndent);

        builder.Append(
            "if (!global::PacketWire.Generated.");

        builder.Append(registryClassName);

        builder.AppendLine(
            ".TryGetEncodedLength(");

        builder.Append(bodyIndent);
        builder.AppendLine("        packet,");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "        __packetWireDefinition,");

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
            "int protectedPayloadLength =");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "    protector.GetProtectedPayloadLength(payloadLength);");

        builder.AppendLine();

        builder.Append(bodyIndent);

        builder.AppendLine(
            "if (protectedPayloadLength < 0)");

        builder.Append(bodyIndent);
        builder.AppendLine("{");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "    throw new global::System.InvalidOperationException(");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "        $\"The payload protector returned an invalid protected payload length: {protectedPayloadLength}.\");");

        builder.Append(bodyIndent);
        builder.AppendLine("}");

        builder.AppendLine();

        builder.Append(bodyIndent);

        builder.AppendLine(
            "ulong frameLength = global::PacketWire.PacketFrameCodec.CalculateFrameLength(");

        builder.Append(bodyIndent);
        builder.AppendLine("    protectedPayloadLength,");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "    __packetWireDefinition);");

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
            "        $\"The protected packet requires {frameLength} bytes, which exceeds the maximum managed buffer size supported by PacketWire.\");");

        builder.Append(bodyIndent);
        builder.AppendLine("}");

        builder.AppendLine();

        builder.Append(bodyIndent);

        builder.AppendLine(
            "byte[] plaintextBuffer = global::System.Buffers.ArrayPool<byte>.Shared.Rent(");

        builder.Append(bodyIndent);
        builder.AppendLine(
            "    global::System.Math.Max(payloadLength, 1));");

        builder.AppendLine();

        builder.Append(bodyIndent);
        builder.AppendLine(
            "global::System.Span<byte> plaintextPayload = new(");

        builder.Append(bodyIndent);
        builder.AppendLine(
            "    plaintextBuffer,");

        builder.Append(bodyIndent);
        builder.AppendLine(
            "    0,");

        builder.Append(bodyIndent);
        builder.AppendLine(
            "    payloadLength);");

        builder.AppendLine();

        builder.Append(bodyIndent);
        builder.AppendLine("try");

        builder.Append(bodyIndent);
        builder.AppendLine("{");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "    global::PacketWire.PacketWriter payloadWriter = new(");

        builder.Append(bodyIndent);
        builder.AppendLine("        plaintextPayload,");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "        __packetWireDefinition.ByteOrder);");

        builder.AppendLine();

        builder.Append(bodyIndent);

        builder.Append(
            "    if (!global::PacketWire.Generated.");

        builder.Append(registryClassName);

        builder.AppendLine(
            ".TryWrite(");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "            ref payloadWriter,");

        builder.Append(bodyIndent);
        builder.AppendLine("            packet,");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "            __packetWireDefinition,");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "            out global::PacketWire.PacketIdentity writtenIdentity))");

        builder.Append(bodyIndent);
        builder.AppendLine("    {");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "        throw new global::System.InvalidOperationException(");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "            \"Packet registry dispatch failed after the packet type had already been resolved.\");");

        builder.Append(bodyIndent);
        builder.AppendLine("    }");

        builder.AppendLine();

        builder.Append(bodyIndent);

        builder.AppendLine(
            "    if (writtenIdentity != identity)");

        builder.Append(bodyIndent);
        builder.AppendLine("    {");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "        throw new global::System.InvalidOperationException(");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "            \"Packet registry returned inconsistent identities between length calculation and serialization.\");");

        builder.Append(bodyIndent);
        builder.AppendLine("    }");

        builder.AppendLine();

        builder.Append(bodyIndent);

        builder.AppendLine(
            "    if (payloadWriter.WrittenCount != payloadLength)");

        builder.Append(bodyIndent);
        builder.AppendLine("    {");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "        throw new global::System.InvalidOperationException(");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "            $\"Generated codec reported payload length {payloadLength} but wrote {payloadWriter.WrittenCount} bytes.\");");

        builder.Append(bodyIndent);
        builder.AppendLine("    }");

        builder.AppendLine();

        builder.Append(bodyIndent);

        builder.AppendLine(
            "    byte[] frame = new byte[(int)frameLength];");

        builder.AppendLine();

        builder.Append(bodyIndent);

        builder.AppendLine(
            "    _ = global::PacketWire.PacketFrameCodec.WriteHeader(");

        builder.Append(bodyIndent);
        builder.AppendLine("        frame,");

        builder.Append(bodyIndent);
        builder.AppendLine("        frameLength,");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "        global::PacketWire.PacketFrameOptions.Protected,");

        builder.Append(bodyIndent);
        builder.AppendLine("        identity.Category,");

        builder.Append(bodyIndent);
        builder.AppendLine("        identity.Id,");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "        __packetWireDefinition);");

        builder.AppendLine();

        builder.Append(bodyIndent);

        builder.AppendLine(
            "    global::System.ReadOnlySpan<byte> associatedData =");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "        new global::System.ReadOnlySpan<byte>(");

        builder.Append(bodyIndent);
        builder.AppendLine("            frame,");

        builder.Append(bodyIndent);
        builder.AppendLine("            0,");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "            __packetWireDefinition.HeaderLength);");

        builder.AppendLine();

        builder.Append(bodyIndent);

        builder.AppendLine(
            "    global::System.Span<byte> protectedDestination =");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "        new global::System.Span<byte>(");

        builder.Append(bodyIndent);
        builder.AppendLine("            frame,");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "            __packetWireDefinition.HeaderLength,");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "            protectedPayloadLength);");

        builder.AppendLine();

        builder.Append(bodyIndent);

        builder.AppendLine(
            "    int protectedBytesWritten = protector.Protect(");

        builder.Append(bodyIndent);
        builder.AppendLine("        plaintextPayload,");

        builder.Append(bodyIndent);
        builder.AppendLine("        associatedData,");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "        protectedDestination);");

        builder.AppendLine();

        builder.Append(bodyIndent);

        builder.AppendLine(
            "    if (protectedBytesWritten != protectedPayloadLength)");

        builder.Append(bodyIndent);
        builder.AppendLine("    {");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "        throw new global::System.InvalidOperationException(");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "            $\"The payload protector declared {protectedPayloadLength} protected bytes but wrote {protectedBytesWritten} bytes.\");");

        builder.Append(bodyIndent);
        builder.AppendLine("    }");

        builder.AppendLine();

        builder.Append(bodyIndent);
        builder.AppendLine("    return frame;");

        builder.Append(bodyIndent);
        builder.AppendLine("}");

        builder.Append(bodyIndent);
        builder.AppendLine("finally");

        builder.Append(bodyIndent);
        builder.AppendLine("{");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "    global::System.Security.Cryptography.CryptographicOperations.ZeroMemory(plaintextPayload);");

        builder.AppendLine();

        builder.Append(bodyIndent);
        builder.AppendLine(
            "    global::System.Buffers.ArrayPool<byte>.Shared.Return(plaintextBuffer);");

        builder.Append(bodyIndent);
        builder.AppendLine("}");

        builder.Append(indent);
        builder.AppendLine("}");
    }

    /// <summary>
    /// Emits the protected <c>Deserialize</c> facade methods that unprotect and deserialize packet frames using an <c>IPayloadProtector</c>.
    /// </summary>
    /// <param name="builder">The <see cref="StringBuilder"/> accumulating generated source code.</param>
    /// <param name="indent">The indentation string for member declarations.</param>
    /// <param name="bodyIndent">The indentation string for method bodies.</param>
    /// <param name="registryClassName">The name of the protocol's internal registry class.</param>
    private static void EmitProtectedDeserialize(
        StringBuilder builder,
        string indent,
        string bodyIndent,
        string registryClassName)
    {
        builder.Append(indent);
        builder.AppendLine("/// <summary>");
        builder.Append(indent);
        builder.AppendLine("/// Deserializes and unprotects a framed binary payload using the specified payload protector.");
        builder.Append(indent);
        builder.AppendLine("/// </summary>");
        builder.Append(indent);
        builder.AppendLine("/// <param name=\"packetBytes\">The read-only span of bytes containing the complete framed and protected packet.</param>");
        builder.Append(indent);
        builder.AppendLine("/// <param name=\"protector\">The payload protector used to unprotect the payload.</param>");
        builder.Append(indent);
        builder.AppendLine("/// <returns>The deserialized packet object.</returns>");
        builder.Append(indent);
        builder.AppendLine("/// <exception cref=\"global::System.ArgumentNullException\">Thrown if <paramref name=\"protector\"/> is <see langword=\"null\"/>.</exception>");
        builder.Append(indent);
        builder.AppendLine("/// <exception cref=\"global::PacketWire.PacketBufferException\">Thrown if the buffer is malformed or framing invariants are violated.</exception>");
        builder.Append(indent);
        builder.AppendLine("/// <exception cref=\"global::PacketWire.PacketIdentityNotRegisteredException\">Thrown if the frame's packet identity is not registered in this protocol.</exception>");
        builder.Append(indent);
        builder.AppendLine("/// <exception cref=\"global::PacketWire.Security.PayloadProtectionException\">Thrown if payload unprotection fails.</exception>");
        builder.Append(indent);

        builder.AppendLine(
            "public static object Deserialize(");

        builder.Append(indent);

        builder.AppendLine(
            "    global::System.ReadOnlySpan<byte> packetBytes,");

        builder.Append(indent);

        builder.AppendLine(
            "    global::PacketWire.Security.IPayloadProtector protector)");

        builder.Append(indent);
        builder.AppendLine("{");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "global::System.ArgumentNullException.ThrowIfNull(protector);");

        builder.AppendLine();

        builder.Append(bodyIndent);

        builder.AppendLine(
            "global::PacketWire.PacketFrameView frame =");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "    global::PacketWire.PacketFrameCodec.ReadFrame(");

        builder.Append(bodyIndent);
        builder.AppendLine("        packetBytes,");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "        __packetWireDefinition);");

        builder.AppendLine();

        builder.Append(bodyIndent);

        builder.AppendLine(
            "if (frame.Header.Flags == global::PacketWire.PacketFrameOptions.None)");

        builder.Append(bodyIndent);
        builder.AppendLine("{");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "    return Deserialize(packetBytes);");

        builder.Append(bodyIndent);
        builder.AppendLine("}");

        builder.AppendLine();

        builder.Append(bodyIndent);

        builder.AppendLine(
            "if (frame.Header.Flags != global::PacketWire.PacketFrameOptions.Protected)");

        builder.Append(bodyIndent);
        builder.AppendLine("{");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "    throw new global::PacketWire.PacketBufferException(");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "        $\"Packet frame options '{frame.Header.Flags}' are not supported by the protected Deserialize overload.\");");

        builder.Append(bodyIndent);
        builder.AppendLine("}");

        builder.AppendLine();

        builder.Append(bodyIndent);

        builder.AppendLine(
            "int maximumPlaintextLength =");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "    protector.GetMaximumPlaintextLength(frame.PayloadLength);");

        builder.AppendLine();

        builder.Append(bodyIndent);

        builder.AppendLine(
            "if (maximumPlaintextLength < 0)");

        builder.Append(bodyIndent);
        builder.AppendLine("{");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "    throw new global::System.InvalidOperationException(");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "        $\"The payload protector returned an invalid maximum plaintext length: {maximumPlaintextLength}.\");");

        builder.Append(bodyIndent);
        builder.AppendLine("}");

        builder.AppendLine();

        builder.Append(bodyIndent);

        builder.AppendLine(
            "byte[] plaintextBuffer = global::System.Buffers.ArrayPool<byte>.Shared.Rent(");

        builder.Append(bodyIndent);
        builder.AppendLine(
            "    global::System.Math.Max(maximumPlaintextLength, 1));");

        builder.AppendLine();

        builder.Append(bodyIndent);
        builder.AppendLine(
            "global::System.Span<byte> plaintextPayload = new(");

        builder.Append(bodyIndent);
        builder.AppendLine(
            "    plaintextBuffer,");

        builder.Append(bodyIndent);
        builder.AppendLine(
            "    0,");

        builder.Append(bodyIndent);
        builder.AppendLine(
            "    maximumPlaintextLength);");

        builder.AppendLine();

        builder.Append(bodyIndent);
        builder.AppendLine("try");

        builder.Append(bodyIndent);
        builder.AppendLine("{");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "    int plaintextLength = protector.Unprotect(");

        builder.Append(bodyIndent);
        builder.AppendLine("        frame.Payload,");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "        packetBytes.Slice(");

        builder.Append(bodyIndent);
        builder.AppendLine("            0,");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "            __packetWireDefinition.HeaderLength),");

        builder.Append(bodyIndent);
        builder.AppendLine("        plaintextPayload);");

        builder.AppendLine();

        builder.Append(bodyIndent);

        builder.AppendLine(
            "    if ((uint)plaintextLength > (uint)plaintextPayload.Length)");

        builder.Append(bodyIndent);
        builder.AppendLine("    {");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "        throw new global::System.InvalidOperationException(");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "            $\"The payload protector reported an invalid plaintext length: {plaintextLength}.\");");

        builder.Append(bodyIndent);
        builder.AppendLine("    }");

        builder.AppendLine();

        builder.Append(bodyIndent);

        builder.AppendLine(
            "    global::PacketWire.PacketIdentity identity = new(");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "        frame.Header.PacketCategory,");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "        frame.Header.PacketId);");

        builder.AppendLine();

        builder.Append(bodyIndent);

        builder.AppendLine(
            "    global::PacketWire.PacketReader reader = new(");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "        new global::System.ReadOnlySpan<byte>(");

        builder.Append(bodyIndent);
        builder.AppendLine("            plaintextBuffer,");

        builder.Append(bodyIndent);
        builder.AppendLine("            0,");

        builder.Append(bodyIndent);
        builder.AppendLine("            plaintextLength),");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "        __packetWireDefinition.ByteOrder);");

        builder.AppendLine();

        builder.Append(bodyIndent);

        builder.Append(
            "    if (!global::PacketWire.Generated.");

        builder.Append(registryClassName);

        builder.AppendLine(
            ".TryRead(");

        builder.Append(bodyIndent);
        builder.AppendLine("            identity,");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "            ref reader,");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "            __packetWireDefinition,");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "            out object? packet) ||");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "        packet is null)");

        builder.Append(bodyIndent);
        builder.AppendLine("    {");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "        throw new global::PacketWire.PacketIdentityNotRegisteredException(identity);");

        builder.Append(bodyIndent);
        builder.AppendLine("    }");

        builder.AppendLine();

        builder.Append(bodyIndent);

        builder.AppendLine(
            "    if (reader.Remaining != 0)");

        builder.Append(bodyIndent);
        builder.AppendLine("    {");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "        throw new global::PacketWire.PacketBufferException(");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "            $\"Generated codec did not consume the complete unprotected payload. Remaining: {reader.Remaining} bytes.\");");

        builder.Append(bodyIndent);
        builder.AppendLine("    }");

        builder.AppendLine();

        builder.Append(bodyIndent);
        builder.AppendLine("    return packet;");

        builder.Append(bodyIndent);
        builder.AppendLine("}");

        builder.Append(bodyIndent);
        builder.AppendLine("finally");

        builder.Append(bodyIndent);
        builder.AppendLine("{");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "    global::System.Security.Cryptography.CryptographicOperations.ZeroMemory(plaintextPayload);");

        builder.AppendLine();

        builder.Append(bodyIndent);
        builder.AppendLine(
            "    global::System.Buffers.ArrayPool<byte>.Shared.Return(plaintextBuffer);");

        builder.Append(bodyIndent);
        builder.AppendLine("}");

        builder.Append(indent);
        builder.AppendLine("}");

        builder.AppendLine();

        builder.Append(indent);
        builder.AppendLine("/// <summary>");
        builder.Append(indent);
        builder.AppendLine("/// Deserializes and unprotects a framed binary payload using the specified payload protector into a strongly typed packet instance.");
        builder.Append(indent);
        builder.AppendLine("/// </summary>");
        builder.Append(indent);
        builder.AppendLine("/// <typeparam name=\"TPacket\">The expected packet type.</typeparam>");
        builder.Append(indent);
        builder.AppendLine("/// <param name=\"packetBytes\">The read-only span of bytes containing the complete framed and protected packet.</param>");
        builder.Append(indent);
        builder.AppendLine("/// <param name=\"protector\">The payload protector used to unprotect the payload.</param>");
        builder.Append(indent);
        builder.AppendLine("/// <returns>The deserialized packet instance of type <typeparamref name=\"TPacket\"/>.</returns>");
        builder.Append(indent);
        builder.AppendLine("/// <exception cref=\"global::System.ArgumentNullException\">Thrown if <paramref name=\"protector\"/> is <see langword=\"null\"/>.</exception>");
        builder.Append(indent);
        builder.AppendLine("/// <exception cref=\"global::PacketWire.PacketBufferException\">Thrown if the buffer is malformed or framing invariants are violated.</exception>");
        builder.Append(indent);
        builder.AppendLine("/// <exception cref=\"global::PacketWire.PacketIdentityNotRegisteredException\">Thrown if the frame's packet identity is not registered in this protocol.</exception>");
        builder.Append(indent);
        builder.AppendLine("/// <exception cref=\"global::PacketWire.PacketTypeMismatchException\">Thrown if the decoded packet's runtime type does not match <typeparamref name=\"TPacket\"/>.</exception>");
        builder.Append(indent);
        builder.AppendLine("/// <exception cref=\"global::PacketWire.Security.PayloadProtectionException\">Thrown if payload unprotection fails.</exception>");
        builder.Append(indent);

        builder.AppendLine(
            "public static TPacket Deserialize<TPacket>(");

        builder.Append(indent);

        builder.AppendLine(
            "    global::System.ReadOnlySpan<byte> packetBytes,");

        builder.Append(indent);

        builder.AppendLine(
            "    global::PacketWire.Security.IPayloadProtector protector)");

        builder.Append(indent);
        builder.AppendLine("{");

        builder.Append(bodyIndent);

        builder.AppendLine(
            "object packet = Deserialize(");

        builder.Append(bodyIndent);
        builder.AppendLine("    packetBytes,");

        builder.Append(bodyIndent);
        builder.AppendLine("    protector);");

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
}