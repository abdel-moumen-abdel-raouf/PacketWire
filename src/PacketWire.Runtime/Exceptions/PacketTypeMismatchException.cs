namespace PacketWire;

/// <summary>
/// Exception thrown when a generic typed deserialization method decodes a packet whose concrete runtime type does not match the requested type.
/// </summary>
/// <remarks>
/// When calling generic deserialization methods (e.g. <c>Deserialize&lt;TPacket&gt;</c>), the wire frame's registered packet identity
/// is resolved. If the resulting packet instance is not assignable to <c>TPacket</c>, this exception is thrown fail-closed.
/// </remarks>
public sealed class PacketTypeMismatchException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PacketTypeMismatchException"/> class with the expected and actual packet types.
    /// </summary>
    /// <param name="expectedType">The type requested by the caller.</param>
    /// <param name="actualType">The concrete runtime type of the deserialized packet object.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="expectedType"/> or <paramref name="actualType"/> is <see langword="null"/>.</exception>
    public PacketTypeMismatchException(
        Type expectedType,
        Type actualType)
        : base(
            $"The decoded packet type '{actualType?.FullName}' does not match the requested type '{expectedType?.FullName}'.")
    {
        ArgumentNullException.ThrowIfNull(expectedType);
        ArgumentNullException.ThrowIfNull(actualType);

        ExpectedType = expectedType;
        ActualType = actualType;
    }

    /// <summary>
    /// Gets the expected packet type requested by the caller.
    /// </summary>
    /// <value>The <see cref="Type"/> requested during deserialization.</value>
    public Type ExpectedType { get; }

    /// <summary>
    /// Gets the actual runtime packet type decoded from the wire frame.
    /// </summary>
    /// <value>The concrete <see cref="Type"/> produced by the codec registry.</value>
    public Type ActualType { get; }
}
