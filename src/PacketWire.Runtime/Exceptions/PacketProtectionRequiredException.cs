namespace PacketWire;

/// <summary>
/// Exception thrown when attempting to deserialize a protected (encrypted/authenticated) packet frame using a plain deserialization overload.
/// </summary>
/// <remarks>
/// When a frame arrives with the <see cref="PacketFrameOptions.Protected"/> flag set in its header, callers must supply an
/// <see cref="Security.IPayloadProtector"/> to decrypt and authenticate the payload. Attempting to process such a frame through plain
/// deserialization fails closed by throwing this exception.
/// </remarks>
public sealed class PacketProtectionRequiredException : Exception
{
    /// <summary>
    /// The default error message used when no custom message is supplied.
    /// </summary>
    private const string DefaultMessage =
        "The packet payload is protected and requires a payload protector.";

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketProtectionRequiredException"/> class with a default error message.
    /// </summary>
    public PacketProtectionRequiredException()
        : base(DefaultMessage)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketProtectionRequiredException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public PacketProtectionRequiredException(
        string? message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketProtectionRequiredException"/> class with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or <see langword="null"/>.</param>
    public PacketProtectionRequiredException(
        string? message,
        Exception? innerException)
        : base(
            message,
            innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketProtectionRequiredException"/> class with the received frame options.
    /// </summary>
    /// <param name="options">The frame options flags from the wire header that required payload protection.</param>
    public PacketProtectionRequiredException(
        PacketFrameOptions options)
        : base(
            $"The packet frame options '{options}' require a payload protector.")
    {
        Options = options;
    }

    /// <summary>
    /// Gets the packet frame options flags that required protection.
    /// </summary>
    /// <value>A <see cref="PacketFrameOptions"/> value indicating the header flags encountered.</value>
    public PacketFrameOptions Options { get; }
}