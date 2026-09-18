namespace PacketWire;

/// <summary>
/// Exception thrown when attempting to serialize or look up identity for a packet type that has not been registered in the target protocol.
/// </summary>
/// <remarks>
/// Every packet type must be attributed with <see cref="PacketAttribute"/> referencing the target protocol definition.
/// If an unregistered type is passed to serialization or identity lookup methods, this exception is thrown.
/// </remarks>
public sealed class PacketTypeNotRegisteredException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PacketTypeNotRegisteredException"/> class with the unregistered type.
    /// </summary>
    /// <param name="packetType">The runtime type that failed registration lookup.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="packetType"/> is <see langword="null"/>.</exception>
    public PacketTypeNotRegisteredException(Type packetType)
        : base(
            $"Packet type '{packetType?.FullName}' is not registered in the selected protocol.")
    {
        ArgumentNullException.ThrowIfNull(packetType);

        PacketType = packetType;
    }

    /// <summary>
    /// Gets the unregistered packet type that caused the exception.
    /// </summary>
    /// <value>The <see cref="Type"/> that is not registered in the protocol.</value>
    public Type PacketType { get; }
}
