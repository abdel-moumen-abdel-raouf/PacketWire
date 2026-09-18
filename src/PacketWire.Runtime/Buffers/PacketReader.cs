using System.Buffers.Binary;
using System.Text;

namespace PacketWire;

/// <summary>
/// A high-performance, stack-only ref struct for sequentially reading binary primitives, fixed strings, and byte spans from a buffer.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="PacketReader"/> operates over a <see cref="ReadOnlySpan{T}"/> and tracks the current read position.
/// All multi-byte numeric reads honor the configured <see cref="PacketByteOrder"/> (little-endian or big-endian).
/// </para>
/// <para>
/// Attempts to read beyond available bytes fail closed with a <see cref="PacketBufferException"/>.
/// Fixed strings are verified to ensure valid UTF-8 encoding and that all bytes following the first null byte are zero-filled.
/// Booleans are validated to be strictly <c>0</c> or <c>1</c>.
/// </para>
/// </remarks>
public ref struct PacketReader
{
    /// <summary>
    /// UTF-8 encoding configured to throw on invalid byte sequences and omit the byte order mark (BOM).
    /// </summary>
    private static readonly UTF8Encoding Utf8Encoding =
        new(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true);

    /// <summary>
    /// The underlying read-only byte buffer being traversed.
    /// </summary>
    private readonly ReadOnlySpan<byte> buffer;

    /// <summary>
    /// The byte order used for multi-byte numeric deserialization.
    /// </summary>
    private readonly PacketByteOrder byteOrder;

    /// <summary>
    /// The current byte offset position in the buffer.
    /// </summary>
    private int position;

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketReader"/> struct over the specified buffer with the given byte order.
    /// </summary>
    /// <param name="buffer">The read-only span of bytes to read from.</param>
    /// <param name="byteOrder">The byte order for multi-byte values (<see cref="PacketByteOrder.LittleEndian"/> or <see cref="PacketByteOrder.BigEndian"/>).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="byteOrder"/> is not a valid endianness.</exception>
    public PacketReader(
        ReadOnlySpan<byte> buffer,
        PacketByteOrder byteOrder)
    {
        if (byteOrder is not PacketByteOrder.LittleEndian and
            not PacketByteOrder.BigEndian)
        {
            throw new ArgumentOutOfRangeException(nameof(byteOrder));
        }

        this.buffer = buffer;
        this.byteOrder = byteOrder;
        position = 0;
    }

    /// <summary>
    /// Gets the total length of the underlying buffer in bytes.
    /// </summary>
    /// <value>The total byte count of the buffer.</value>
    public readonly int Length => buffer.Length;

    /// <summary>
    /// Gets the number of bytes consumed so far from the buffer.
    /// </summary>
    /// <value>The zero-based position representing bytes read.</value>
    public readonly int ConsumedCount => position;

    /// <summary>
    /// Gets the number of bytes remaining to be read in the buffer.
    /// </summary>
    /// <value>The remaining unconsumed byte count.</value>
    public readonly int Remaining => buffer.Length - position;

    /// <summary>
    /// Gets the byte order used for decoding multi-byte numeric primitives.
    /// </summary>
    /// <value>The active <see cref="PacketByteOrder"/>.</value>
    public readonly PacketByteOrder ByteOrder => byteOrder;

    /// <summary>
    /// Gets a slice of the buffer representing all remaining unconsumed bytes.
    /// </summary>
    /// <value>A <see cref="ReadOnlySpan{T}"/> from the current position to the end of the buffer.</value>
    public readonly ReadOnlySpan<byte> RemainingSpan =>
        buffer[position..];

    /// <summary>
    /// Reads an 8-bit unsigned byte from the buffer and advances the position by 1 byte.
    /// </summary>
    /// <returns>The byte read.</returns>
    /// <exception cref="PacketBufferException">Thrown when fewer than 1 byte remains in the buffer.</exception>
    public byte ReadByte()
    {
        ReadOnlySpan<byte> source = GetSource(sizeof(byte));

        byte value = source[0];
        position += sizeof(byte);

        return value;
    }

    /// <summary>
    /// Reads an 8-bit signed byte from the buffer and advances the position by 1 byte.
    /// </summary>
    /// <returns>The signed byte read.</returns>
    /// <exception cref="PacketBufferException">Thrown when fewer than 1 byte remains in the buffer.</exception>
    public sbyte ReadSByte()
    {
        return unchecked((sbyte)ReadByte());
    }

    /// <summary>
    /// Reads a 1-byte boolean value from the buffer and advances the position by 1 byte.
    /// </summary>
    /// <returns><see langword="false"/> if the wire byte is <c>0</c>; <see langword="true"/> if <c>1</c>.</returns>
    /// <exception cref="PacketBufferException">Thrown when fewer than 1 byte remains, or when the byte is neither <c>0</c> nor <c>1</c>.</exception>
    public bool ReadBoolean()
    {
        byte value = ReadByte();

        return value switch
        {
            0 => false,
            1 => true,
            _ => throw new PacketBufferException(
                $"Invalid Boolean wire value '{value}'. " +
                "Only 0 and 1 are valid.")
        };
    }

    /// <summary>
    /// Reads a 16-bit signed integer using the configured byte order and advances the position by 2 bytes.
    /// </summary>
    /// <returns>The 16-bit signed integer read.</returns>
    /// <exception cref="PacketBufferException">Thrown when fewer than 2 bytes remain in the buffer.</exception>
    public short ReadInt16()
    {
        ReadOnlySpan<byte> source = GetSource(sizeof(short));

        short value =
            byteOrder == PacketByteOrder.LittleEndian
                ? BinaryPrimitives.ReadInt16LittleEndian(source)
                : BinaryPrimitives.ReadInt16BigEndian(source);

        position += sizeof(short);

        return value;
    }

    /// <summary>
    /// Reads a 16-bit unsigned integer using the configured byte order and advances the position by 2 bytes.
    /// </summary>
    /// <returns>The 16-bit unsigned integer read.</returns>
    /// <exception cref="PacketBufferException">Thrown when fewer than 2 bytes remain in the buffer.</exception>
    public ushort ReadUInt16()
    {
        ReadOnlySpan<byte> source = GetSource(sizeof(ushort));

        ushort value =
            byteOrder == PacketByteOrder.LittleEndian
                ? BinaryPrimitives.ReadUInt16LittleEndian(source)
                : BinaryPrimitives.ReadUInt16BigEndian(source);

        position += sizeof(ushort);

        return value;
    }

    /// <summary>
    /// Reads a 32-bit signed integer using the configured byte order and advances the position by 4 bytes.
    /// </summary>
    /// <returns>The 32-bit signed integer read.</returns>
    /// <exception cref="PacketBufferException">Thrown when fewer than 4 bytes remain in the buffer.</exception>
    public int ReadInt32()
    {
        ReadOnlySpan<byte> source = GetSource(sizeof(int));

        int value =
            byteOrder == PacketByteOrder.LittleEndian
                ? BinaryPrimitives.ReadInt32LittleEndian(source)
                : BinaryPrimitives.ReadInt32BigEndian(source);

        position += sizeof(int);

        return value;
    }

    /// <summary>
    /// Reads a 32-bit unsigned integer using the configured byte order and advances the position by 4 bytes.
    /// </summary>
    /// <returns>The 32-bit unsigned integer read.</returns>
    /// <exception cref="PacketBufferException">Thrown when fewer than 4 bytes remain in the buffer.</exception>
    public uint ReadUInt32()
    {
        ReadOnlySpan<byte> source = GetSource(sizeof(uint));

        uint value =
            byteOrder == PacketByteOrder.LittleEndian
                ? BinaryPrimitives.ReadUInt32LittleEndian(source)
                : BinaryPrimitives.ReadUInt32BigEndian(source);

        position += sizeof(uint);

        return value;
    }

    /// <summary>
    /// Reads a 64-bit signed integer using the configured byte order and advances the position by 8 bytes.
    /// </summary>
    /// <returns>The 64-bit signed integer read.</returns>
    /// <exception cref="PacketBufferException">Thrown when fewer than 8 bytes remain in the buffer.</exception>
    public long ReadInt64()
    {
        ReadOnlySpan<byte> source = GetSource(sizeof(long));

        long value =
            byteOrder == PacketByteOrder.LittleEndian
                ? BinaryPrimitives.ReadInt64LittleEndian(source)
                : BinaryPrimitives.ReadInt64BigEndian(source);

        position += sizeof(long);

        return value;
    }

    /// <summary>
    /// Reads a 64-bit unsigned integer using the configured byte order and advances the position by 8 bytes.
    /// </summary>
    /// <returns>The 64-bit unsigned integer read.</returns>
    /// <exception cref="PacketBufferException">Thrown when fewer than 8 bytes remain in the buffer.</exception>
    public ulong ReadUInt64()
    {
        ReadOnlySpan<byte> source = GetSource(sizeof(ulong));

        ulong value =
            byteOrder == PacketByteOrder.LittleEndian
                ? BinaryPrimitives.ReadUInt64LittleEndian(source)
                : BinaryPrimitives.ReadUInt64BigEndian(source);

        position += sizeof(ulong);

        return value;
    }

    /// <summary>
    /// Reads a 32-bit IEEE 754 single-precision floating point number and advances the position by 4 bytes.
    /// </summary>
    /// <returns>The single-precision float read.</returns>
    /// <exception cref="PacketBufferException">Thrown when fewer than 4 bytes remain in the buffer.</exception>
    public float ReadSingle()
    {
        return BitConverter.Int32BitsToSingle(ReadInt32());
    }

    /// <summary>
    /// Reads a 64-bit IEEE 754 double-precision floating point number and advances the position by 8 bytes.
    /// </summary>
    /// <returns>The double-precision float read.</returns>
    /// <exception cref="PacketBufferException">Thrown when fewer than 8 bytes remain in the buffer.</exception>
    public double ReadDouble()
    {
        return BitConverter.Int64BitsToDouble(ReadInt64());
    }

    /// <summary>
    /// Reads a contiguous sequence of raw bytes from the buffer and advances the position by <paramref name="length"/>.
    /// </summary>
    /// <param name="length">The number of bytes to slice.</param>
    /// <returns>A <see cref="ReadOnlySpan{T}"/> pointing to the requested bytes.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="length"/> is negative.</exception>
    /// <exception cref="PacketBufferException">Thrown when fewer than <paramref name="length"/> bytes remain in the buffer.</exception>
    public ReadOnlySpan<byte> ReadBytes(int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        ReadOnlySpan<byte> source = GetSource(length);
        position += length;

        return source;
    }

    /// <summary>
    /// Reads a fixed-width UTF-8 encoded string of the specified byte length from the buffer and advances the position by <paramref name="byteLength"/>.
    /// </summary>
    /// <param name="byteLength">The exact byte width of the fixed string field on the wire.</param>
    /// <returns>The decoded string, excluding any trailing zero padding.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="byteLength"/> is negative or zero.</exception>
    /// <exception cref="PacketBufferException">
    /// Thrown when fewer than <paramref name="byteLength"/> bytes remain, when non-zero bytes are found after padding begins,
    /// or when the string bytes contain invalid UTF-8 sequences.
    /// </exception>
    public string ReadFixedString(int byteLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(byteLength);

        ReadOnlySpan<byte> source = GetSource(byteLength);

        int nullIndex = source.IndexOf((byte)0);
        ReadOnlySpan<byte> textBytes;

        if (nullIndex < 0)
        {
            textBytes = source;
        }
        else
        {
            textBytes = source[..nullIndex];

            for (int index = nullIndex; index < source.Length; index++)
            {
                if (source[index] != 0)
                {
                    throw new PacketBufferException(
                        "A fixed UTF-8 string contains non-zero data " +
                        "after the beginning of its zero padding.");
                }
            }
        }

        string value;

        try
        {
            value = Utf8Encoding.GetString(textBytes);
        }
        catch (DecoderFallbackException exception)
        {
            throw new PacketBufferException(
                $"The fixed string contains invalid UTF-8 data: {exception.Message}");
        }

        position += byteLength;

        return value;
    }

    /// <summary>
    /// Obtains a slice of the buffer of the specified length starting at the current position without advancing.
    /// </summary>
    /// <param name="length">The required length in bytes.</param>
    /// <returns>A span of the requested length.</returns>
    /// <exception cref="PacketBufferException">Thrown when fewer than <paramref name="length"/> bytes remain.</exception>
    private readonly ReadOnlySpan<byte> GetSource(int length)
    {
        EnsureAvailable(length);

        return buffer.Slice(position, length);
    }

    /// <summary>
    /// Ensures that at least <paramref name="requiredLength"/> bytes remain to be read in the buffer.
    /// </summary>
    /// <param name="requiredLength">The required number of readable bytes.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="requiredLength"/> is negative.</exception>
    /// <exception cref="PacketBufferException">Thrown when <paramref name="requiredLength"/> exceeds <see cref="Remaining"/>.</exception>
    private readonly void EnsureAvailable(int requiredLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(requiredLength);

        if (requiredLength > Remaining)
        {
            throw new PacketBufferException(
                $"The packet buffer does not contain enough readable data. " +
                $"Required: {requiredLength} bytes. Remaining: {Remaining} bytes.");
        }
    }
}
