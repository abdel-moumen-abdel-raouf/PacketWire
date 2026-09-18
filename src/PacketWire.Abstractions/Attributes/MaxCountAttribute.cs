namespace PacketWire;

/// <summary>
/// Specifies the maximum permissible number of elements for a collection property in a PacketWire packet or contract.
/// </summary>
/// <remarks>
/// During deserialization, the wire count header is validated against <see cref="MaxCount"/> before memory is allocated
/// or elements are parsed. If the received collection count exceeds this limit, deserialization fails closed, protecting
/// consumers from untrusted input attempting excessive memory allocations.
/// </remarks>
[AttributeUsage(
    AttributeTargets.Property,
    AllowMultiple = false,
    Inherited = true)]
public sealed class MaxCountAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MaxCountAttribute"/> class with the specified maximum count.
    /// </summary>
    /// <param name="maxCount">The maximum permissible element count. Must be zero or greater.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxCount"/> is negative.</exception>
    public MaxCountAttribute(int maxCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxCount);

        MaxCount = maxCount;
    }

    /// <summary>
    /// Gets the maximum permissible number of elements for the target collection property.
    /// </summary>
    /// <value>A non-negative integer representing the maximum allowed element count.</value>
    public int MaxCount { get; }
}
