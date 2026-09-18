namespace PacketWire;

/// <summary>
/// Specifies the fixed UTF-8 byte length for a string property within a PacketWire packet or contract.
/// </summary>
/// <remarks>
/// Fixed strings are encoded as UTF-8 bytes up to the specified byte length. If the encoded text requires
/// fewer bytes than <see cref="ByteLength"/>, the remaining buffer space is filled with zero bytes.
/// On deserialization, the codec verifies that all bytes following the first zero byte are also zero;
/// non-zero data encountered within the padding region causes deserialization to fail. Fixed string values
/// must not contain embedded null characters (<c>'\0'</c>).
/// </remarks>
[AttributeUsage(
    AttributeTargets.Property,
    AllowMultiple = false,
    Inherited = true)]
public sealed class FixedStringAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FixedStringAttribute"/> class with the specified byte length.
    /// </summary>
    /// <param name="byteLength">The exact number of bytes allocated on the wire for the string field. Must be greater than zero.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="byteLength"/> is less than or equal to zero.</exception>
    public FixedStringAttribute(int byteLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(byteLength);

        ByteLength = byteLength;
    }

    /// <summary>
    /// Gets the exact number of bytes allocated for the UTF-8 encoded string on the wire.
    /// </summary>
    /// <value>A positive integer representing the fixed byte width on the wire.</value>
    public int ByteLength { get; }
}
