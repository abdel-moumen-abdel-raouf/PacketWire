namespace PacketWire;

/// <summary>
/// Encodes, decodes, and calculates size boundaries for variable-width unsigned integers in PacketWire protocol frames.
/// </summary>
/// <remarks>
/// Handles 1-byte, 2-byte, 4-byte, and 8-byte unsigned integer conversions according to the protocol's configured
/// <see cref="PacketIntegerSize"/> and endianness.
/// </remarks>
public static class PacketIntegerCodec
{
    /// <summary>
    /// Returns the exact byte count occupied on the wire by the specified <see cref="PacketIntegerSize"/>.
    /// </summary>
    /// <param name="size">The packet integer size specification.</param>
    /// <returns>The number of bytes (<c>1</c>, <c>2</c>, <c>4</c>, or <c>8</c>).</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/> is not a supported integer size.</exception>
    public static int GetByteCount(PacketIntegerSize size)
    {
        return size switch
        {
            PacketIntegerSize.OneByte => 1,
            PacketIntegerSize.TwoBytes => 2,
            PacketIntegerSize.FourBytes => 4,
            PacketIntegerSize.EightBytes => 8,
            _ => throw new ArgumentOutOfRangeException(
                nameof(size),
                size,
                "The packet integer size must be a defined, non-zero value.")
        };
    }

    /// <summary>
    /// Returns the maximum numeric value that can be represented in the specified <see cref="PacketIntegerSize"/>.
    /// </summary>
    /// <param name="size">The packet integer size specification.</param>
    /// <returns>The maximum unsigned 64-bit value that fits in the specified byte width.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/> is not a supported integer size.</exception>
    public static ulong GetMaximumValue(PacketIntegerSize size)
    {
        return size switch
        {
            PacketIntegerSize.OneByte => byte.MaxValue,
            PacketIntegerSize.TwoBytes => ushort.MaxValue,
            PacketIntegerSize.FourBytes => uint.MaxValue,
            PacketIntegerSize.EightBytes => ulong.MaxValue,
            _ => throw new ArgumentOutOfRangeException(
                nameof(size),
                size,
                "The packet integer size must be a defined, non-zero value.")
        };
    }

    /// <summary>
    /// Writes an unsigned 64-bit integer into the writer according to the target <see cref="PacketIntegerSize"/>.
    /// </summary>
    /// <param name="writer">The packet writer destination.</param>
    /// <param name="value">The unsigned numeric value to encode.</param>
    /// <param name="size">The integer byte width to write.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="value"/> exceeds the maximum capacity of <paramref name="size"/>,
    /// or when <paramref name="size"/> is not a supported integer size.
    /// </exception>
    public static void Write(
        ref PacketWriter writer,
        ulong value,
        PacketIntegerSize size)
    {
        ulong maximumValue = GetMaximumValue(size);

        if (value > maximumValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                $"The value does not fit in a {GetByteCount(size)}-byte unsigned packet integer.");
        }

        switch (size)
        {
            case PacketIntegerSize.OneByte:
                writer.WriteByte((byte)value);
                break;

            case PacketIntegerSize.TwoBytes:
                writer.WriteUInt16((ushort)value);
                break;

            case PacketIntegerSize.FourBytes:
                writer.WriteUInt32((uint)value);
                break;

            case PacketIntegerSize.EightBytes:
                writer.WriteUInt64(value);
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(size),
                    size,
                    "The packet integer size must be a defined, non-zero value.");
        }
    }

    /// <summary>
    /// Reads an unsigned integer from the reader according to the specified <see cref="PacketIntegerSize"/>.
    /// </summary>
    /// <param name="reader">The packet reader source.</param>
    /// <param name="size">The integer byte width to read.</param>
    /// <returns>The decoded value as an unsigned 64-bit integer.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/> is not a supported integer size.</exception>
    public static ulong Read(
        ref PacketReader reader,
        PacketIntegerSize size)
    {
        return size switch
        {
            PacketIntegerSize.OneByte => reader.ReadByte(),
            PacketIntegerSize.TwoBytes => reader.ReadUInt16(),
            PacketIntegerSize.FourBytes => reader.ReadUInt32(),
            PacketIntegerSize.EightBytes => reader.ReadUInt64(),
            _ => throw new ArgumentOutOfRangeException(
                nameof(size),
                size,
                "The packet integer size must be a defined, non-zero value.")
        };
    }
}
