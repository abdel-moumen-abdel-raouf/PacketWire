namespace PacketWire;

/// <summary>
/// Encodes and decodes the 1-byte wire presence marker used for optional properties in PacketWire packets and contracts.
/// </summary>
/// <remarks>
/// Optional fields write <c>0x01</c> if a value is present, or <c>0x00</c> if null/absent.
/// On read, any byte value other than <c>0x00</c> or <c>0x01</c> fails closed with a <see cref="PacketBufferException"/>.
/// </remarks>
public static class PacketPresenceCodec
{
    /// <summary>
    /// Writes an optional-presence marker byte (<c>0x01</c> for present, <c>0x00</c> for absent) to the writer.
    /// </summary>
    /// <param name="writer">The packet writer destination.</param>
    /// <param name="isPresent"><see langword="true"/> if the optional value is present; otherwise, <see langword="false"/>.</param>
    public static void Write(
        ref PacketWriter writer,
        bool isPresent)
    {
        writer.WriteByte(
            isPresent
                ? (byte)1
                : (byte)0);
    }

    /// <summary>
    /// Reads and validates an optional-presence marker byte from the reader.
    /// </summary>
    /// <param name="reader">The packet reader source.</param>
    /// <returns><see langword="true"/> if the field is marked present (<c>0x01</c>); <see langword="false"/> if marked absent (<c>0x00</c>).</returns>
    /// <exception cref="PacketBufferException">Thrown when the byte value on the wire is neither <c>0</c> nor <c>1</c>.</exception>
    public static bool Read(
        ref PacketReader reader)
    {
        byte value = reader.ReadByte();

        return value switch
        {
            0 => false,
            1 => true,
            _ => throw new PacketBufferException(
                $"Invalid optional-presence wire value '{value}'. " +
                "Only 0 and 1 are valid.")
        };
    }
}
