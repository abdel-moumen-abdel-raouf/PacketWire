using System.Buffers.Binary;
using System.Text;

namespace PacketWire;

/// <summary>
/// A high-performance, stack-only ref struct for sequentially encoding binary primitives, fixed strings, and raw byte spans into a destination buffer.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="PacketWriter"/> operates directly on a mutable <see cref="Span{T}"/> and tracks written byte offsets.
/// Multi-byte numeric writes are encoded according to the configured <see cref="PacketByteOrder"/> (little-endian or big-endian).
/// </para>
/// <para>
/// Fixed strings are encoded as UTF-8, cannot contain embedded null characters, and have their full wire width zero-padded.
/// Buffer capacity overflows fail closed with a <see cref="PacketBufferException"/>.
/// </para>
/// </remarks>
public ref struct PacketWriter
{
    /// <summary>
    /// UTF-8 encoding configured to throw on invalid characters and omit the byte order mark (BOM).
    /// </summary>
    private static readonly UTF8Encoding Utf8Encoding =
        new(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true);

    /// <summary>
    /// The destination byte buffer receiving serialized data.
    /// </summary>
    private readonly Span<byte> buffer;

    /// <summary>
    /// The byte order used for multi-byte numeric serialization.
    /// </summary>
    private readonly PacketByteOrder byteOrder;

    /// <summary>
    /// The current byte offset position within the destination buffer.
    /// </summary>
    private int position;

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketWriter"/> struct over the specified buffer with the given byte order.
    /// </summary>
    /// <param name="buffer">The destination buffer to write into.</param>
    /// <param name="byteOrder">The byte order for multi-byte values (<see cref="PacketByteOrder.LittleEndian"/> or <see cref="PacketByteOrder.BigEndian"/>).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="byteOrder"/> is not a valid endianness.</exception>
    public PacketWriter(
        Span<byte> buffer,
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
    /// Gets the total capacity of the destination buffer in bytes.
    /// </summary>
    /// <value>The total byte count of the buffer.</value>
    public readonly int Capacity => buffer.Length;

    /// <summary>
    /// Gets the number of bytes written so far to the buffer.
    /// </summary>
    /// <value>The zero-based count of bytes written.</value>
    public readonly int WrittenCount => position;

    /// <summary>
    /// Gets the remaining writable space in the destination buffer in bytes.
    /// </summary>
    /// <value>The number of bytes remaining before the buffer is full.</value>
    public readonly int Remaining => buffer.Length - position;

    /// <summary>
    /// Gets the byte order used for encoding multi-byte numeric primitives.
    /// </summary>
    /// <value>The active <see cref="PacketByteOrder"/>.</value>
    public readonly PacketByteOrder ByteOrder => byteOrder;

    /// <summary>
    /// Gets a read-only view of the slice of the buffer that has already been written.
    /// </summary>
    /// <value>A <see cref="ReadOnlySpan{T}"/> containing all bytes written up to the current position.</value>
    public readonly ReadOnlySpan<byte> WrittenSpan =>
        buffer[..position];

    /// <summary>
    /// Writes an 8-bit unsigned byte to the buffer and advances the position by 1 byte.
    /// </summary>
    /// <param name="value">The byte value to write.</param>
    /// <exception cref="PacketBufferException">Thrown when fewer than 1 byte of writable space remains.</exception>
    public void WriteByte(byte value)
    {
        EnsureAvailable(sizeof(byte));

        buffer[position] = value;
        position += sizeof(byte);
    }

    /// <summary>
    /// Writes an 8-bit signed byte to the buffer and advances the position by 1 byte.
    /// </summary>
    /// <param name="value">The signed byte value to write.</param>
    /// <exception cref="PacketBufferException">Thrown when fewer than 1 byte of writable space remains.</exception>
    public void WriteSByte(sbyte value)
    {
        WriteByte(unchecked((byte)value));
    }

    /// <summary>
    /// Writes a 1-byte boolean value (<c>0x01</c> for <see langword="true"/>, <c>0x00</c> for <see langword="false"/>) to the buffer and advances by 1 byte.
    /// </summary>
    /// <param name="value">The boolean value to encode.</param>
    /// <exception cref="PacketBufferException">Thrown when fewer than 1 byte of writable space remains.</exception>
    public void WriteBoolean(bool value)
    {
        WriteByte(value ? (byte)1 : (byte)0);
    }

    /// <summary>
    /// Writes a 16-bit signed integer using the configured byte order and advances the position by 2 bytes.
    /// </summary>
    /// <param name="value">The 16-bit signed integer value to encode.</param>
    /// <exception cref="PacketBufferException">Thrown when fewer than 2 bytes of writable space remain.</exception>
    public void WriteInt16(short value)
    {
        Span<byte> destination = GetDestination(sizeof(short));

        if (byteOrder == PacketByteOrder.LittleEndian)
        {
            BinaryPrimitives.WriteInt16LittleEndian(destination, value);
        }
        else
        {
            BinaryPrimitives.WriteInt16BigEndian(destination, value);
        }

        position += sizeof(short);
    }

    /// <summary>
    /// Writes a 16-bit unsigned integer using the configured byte order and advances the position by 2 bytes.
    /// </summary>
    /// <param name="value">The 16-bit unsigned integer value to encode.</param>
    /// <exception cref="PacketBufferException">Thrown when fewer than 2 bytes of writable space remain.</exception>
    public void WriteUInt16(ushort value)
    {
        Span<byte> destination = GetDestination(sizeof(ushort));

        if (byteOrder == PacketByteOrder.LittleEndian)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(destination, value);
        }
        else
        {
            BinaryPrimitives.WriteUInt16BigEndian(destination, value);
        }

        position += sizeof(ushort);
    }

    /// <summary>
    /// Writes a 32-bit signed integer using the configured byte order and advances the position by 4 bytes.
    /// </summary>
    /// <param name="value">The 32-bit signed integer value to encode.</param>
    /// <exception cref="PacketBufferException">Thrown when fewer than 4 bytes of writable space remain.</exception>
    public void WriteInt32(int value)
    {
        Span<byte> destination = GetDestination(sizeof(int));

        if (byteOrder == PacketByteOrder.LittleEndian)
        {
            BinaryPrimitives.WriteInt32LittleEndian(destination, value);
        }
        else
        {
            BinaryPrimitives.WriteInt32BigEndian(destination, value);
        }

        position += sizeof(int);
    }

    /// <summary>
    /// Writes a 32-bit unsigned integer using the configured byte order and advances the position by 4 bytes.
    /// </summary>
    /// <param name="value">The 32-bit unsigned integer value to encode.</param>
    /// <exception cref="PacketBufferException">Thrown when fewer than 4 bytes of writable space remain.</exception>
    public void WriteUInt32(uint value)
    {
        Span<byte> destination = GetDestination(sizeof(uint));

        if (byteOrder == PacketByteOrder.LittleEndian)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(destination, value);
        }
        else
        {
            BinaryPrimitives.WriteUInt32BigEndian(destination, value);
        }

        position += sizeof(uint);
    }

    /// <summary>
    /// Writes a 64-bit signed integer using the configured byte order and advances the position by 8 bytes.
    /// </summary>
    /// <param name="value">The 64-bit signed integer value to encode.</param>
    /// <exception cref="PacketBufferException">Thrown when fewer than 8 bytes of writable space remain.</exception>
    public void WriteInt64(long value)
    {
        Span<byte> destination = GetDestination(sizeof(long));

        if (byteOrder == PacketByteOrder.LittleEndian)
        {
            BinaryPrimitives.WriteInt64LittleEndian(destination, value);
        }
        else
        {
            BinaryPrimitives.WriteInt64BigEndian(destination, value);
        }

        position += sizeof(long);
    }

    /// <summary>
    /// Writes a 64-bit unsigned integer using the configured byte order and advances the position by 8 bytes.
    /// </summary>
    /// <param name="value">The 64-bit unsigned integer value to encode.</param>
    /// <exception cref="PacketBufferException">Thrown when fewer than 8 bytes of writable space remain.</exception>
    public void WriteUInt64(ulong value)
    {
        Span<byte> destination = GetDestination(sizeof(ulong));

        if (byteOrder == PacketByteOrder.LittleEndian)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(destination, value);
        }
        else
        {
            BinaryPrimitives.WriteUInt64BigEndian(destination, value);
        }

        position += sizeof(ulong);
    }

    /// <summary>
    /// Writes a 32-bit IEEE 754 single-precision floating point number and advances the position by 4 bytes.
    /// </summary>
    /// <param name="value">The float value to encode.</param>
    /// <exception cref="PacketBufferException">Thrown when fewer than 4 bytes of writable space remain.</exception>
    public void WriteSingle(float value)
    {
        WriteInt32(BitConverter.SingleToInt32Bits(value));
    }

    /// <summary>
    /// Writes a 64-bit IEEE 754 double-precision floating point number and advances the position by 8 bytes.
    /// </summary>
    /// <param name="value">The double value to encode.</param>
    /// <exception cref="PacketBufferException">Thrown when fewer than 8 bytes of writable space remain.</exception>
    public void WriteDouble(double value)
    {
        WriteInt64(BitConverter.DoubleToInt64Bits(value));
    }

    /// <summary>
    /// Writes a contiguous sequence of raw bytes into the destination buffer and advances the position by its length.
    /// </summary>
    /// <param name="value">The span of bytes to copy into the buffer.</param>
    /// <exception cref="PacketBufferException">Thrown when fewer than the required bytes of writable space remain.</exception>
    public void WriteBytes(ReadOnlySpan<byte> value)
    {
        Span<byte> destination = GetDestination(value.Length);

        value.CopyTo(destination);
        position += value.Length;
    }

    /// <summary>
    /// Encodes a string as fixed-width UTF-8 bytes with zero padding and advances the position by <paramref name="byteLength"/>.
    /// </summary>
    /// <param name="value">The string value to encode. Must not be null or contain embedded null characters (<c>'\0'</c>).</param>
    /// <param name="byteLength">The exact byte width of the fixed string field on the wire.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="byteLength"/> is negative or zero.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="value"/> contains embedded null characters (<c>'\0'</c>),
    /// or when the UTF-8 encoded string exceeds <paramref name="byteLength"/> bytes.
    /// </exception>
    /// <exception cref="PacketBufferException">Thrown when fewer than <paramref name="byteLength"/> bytes of writable space remain.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the UTF-8 encoder produces an unexpected byte count.</exception>
    public void WriteFixedString(
        string value,
        int byteLength)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(byteLength);

        if (value.AsSpan().Contains('\0'))
        {
            throw new ArgumentException(
                "Fixed strings cannot contain the null character.",
                nameof(value));
        }

        int encodedLength = Utf8Encoding.GetByteCount(value);

        if (encodedLength > byteLength)
        {
            throw new ArgumentException(
                $"The UTF-8 encoded value requires {encodedLength} bytes, " +
                $"but the fixed field allows only {byteLength} bytes.",
                nameof(value));
        }

        Span<byte> destination = GetDestination(byteLength);

        destination.Clear();

        if (encodedLength > 0)
        {
            int bytesWritten = Utf8Encoding.GetBytes(
                value.AsSpan(),
                destination);

            if (bytesWritten != encodedLength)
            {
                throw new InvalidOperationException(
                    "The UTF-8 encoder produced an unexpected byte count.");
            }
        }

        position += byteLength;
    }

    /// <summary>
    /// Obtains a mutable destination span of the specified length at the current position without advancing.
    /// </summary>
    /// <param name="length">The required number of bytes.</param>
    /// <returns>A mutable span of the requested length.</returns>
    /// <exception cref="PacketBufferException">Thrown when fewer than <paramref name="length"/> bytes of writable space remain.</exception>
    private readonly Span<byte> GetDestination(int length)
    {
        EnsureAvailable(length);

        return buffer.Slice(position, length);
    }

    /// <summary>
    /// Ensures that at least <paramref name="requiredLength"/> bytes of writable space remain in the destination buffer.
    /// </summary>
    /// <param name="requiredLength">The required byte count.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="requiredLength"/> is negative.</exception>
    /// <exception cref="PacketBufferException">Thrown when <paramref name="requiredLength"/> exceeds <see cref="Remaining"/>.</exception>
    private readonly void EnsureAvailable(int requiredLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(requiredLength);

        if (requiredLength > Remaining)
        {
            throw new PacketBufferException(
                $"The packet buffer does not have enough writable space. " +
                $"Required: {requiredLength} bytes. Remaining: {Remaining} bytes.");
        }
    }
}
