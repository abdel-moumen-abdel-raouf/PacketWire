namespace PacketWire.Generator;

/// <summary>
/// Categorizes the structural data shape of a packet field for codec generation.
/// </summary>
internal enum PacketFieldShape
{
    /// <summary>
    /// Primitive scalar value (e.g. integer, boolean, floating point, raw byte span).
    /// </summary>
    Scalar = 0,

    /// <summary>
    /// Value type backed by an underlying enumeration primitive.
    /// </summary>
    EnumerationValue = 1,

    /// <summary>
    /// Fixed-length UTF-8 encoded string with zero padding.
    /// </summary>
    FixedText = 2,

    /// <summary>
    /// Homogeneous collection or list of elements prefixed with a count header.
    /// </summary>
    Sequence = 3,

    /// <summary>
    /// Complex nested type attributed with <c>PacketContractAttribute</c>.
    /// </summary>
    NestedContract = 4
}
