namespace PacketWire.Security;

/// <summary>
/// Exception thrown when cryptographic payload protection or unprotection operations fail.
/// </summary>
/// <remarks>
/// This exception indicates failures in cryptographic operations, including authentication tag mismatch,
/// corrupted ciphertext, tampered associated data (header), invalid key lengths, or insufficient buffer lengths.
/// </remarks>
public sealed class PayloadProtectionException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PayloadProtectionException"/> class.
    /// </summary>
    public PayloadProtectionException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PayloadProtectionException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public PayloadProtectionException(
        string? message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PayloadProtectionException"/> class with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or <see langword="null"/>.</param>
    public PayloadProtectionException(
        string? message,
        Exception? innerException)
        : base(
            message,
            innerException)
    {
    }
}