namespace PacketWire;

/// <summary>
/// Encodes, decodes, and validates collection element counts for sequence properties in PacketWire packets and contracts.
/// </summary>
/// <remarks>
/// Collection counts are prefixed on the wire using the protocol's configured <see cref="PacketProtocolDefinition.CollectionCountSize"/>.
/// During deserialization, the count is strictly validated against <see cref="int.MaxValue"/> and any configured maximum count
/// before element reading or collection allocation occurs, mitigating denial-of-service memory exhaustion vectors.
/// </remarks>
public static class PacketCollectionCodec
{
    /// <summary>
    /// Calculates the wire byte length required for the collection count header and validates that the count fits protocol limits.
    /// </summary>
    /// <param name="count">The number of items in the collection. Must be non-negative.</param>
    /// <param name="definition">The protocol definition defining the collection count integer size.</param>
    /// <param name="maximumCount">Optional maximum permissible item count enforced on the field.</param>
    /// <returns>The number of bytes occupied by the collection count header.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="definition"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="count"/> is negative, exceeds <paramref name="maximumCount"/>,
    /// or exceeds the protocol's maximum collection capacity.
    /// </exception>
    public static int GetEncodedCountLength(
        int count,
        PacketProtocolDefinition definition,
        int? maximumCount = null)
    {
        ArgumentNullException.ThrowIfNull(definition);

        ValidateCountForWrite(
            count,
            definition,
            maximumCount);

        return PacketIntegerCodec.GetByteCount(
            definition.CollectionCountSize);
    }

    /// <summary>
    /// Writes the collection item count header into the packet writer.
    /// </summary>
    /// <param name="writer">The packet writer destination.</param>
    /// <param name="count">The collection element count to encode.</param>
    /// <param name="definition">The protocol definition defining the collection count integer size.</param>
    /// <param name="maximumCount">Optional maximum permissible item count enforced on the field.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="definition"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="count"/> is negative, exceeds <paramref name="maximumCount"/>, or exceeds protocol limits.</exception>
    public static void WriteCount(
        ref PacketWriter writer,
        int count,
        PacketProtocolDefinition definition,
        int? maximumCount = null)
    {
        _ = GetEncodedCountLength(
            count,
            definition,
            maximumCount);

        PacketIntegerCodec.Write(
            ref writer,
            (ulong)count,
            definition.CollectionCountSize);
    }

    /// <summary>
    /// Reads and validates a collection item count header from the packet reader.
    /// </summary>
    /// <param name="reader">The packet reader source.</param>
    /// <param name="definition">The protocol definition defining the collection count integer size.</param>
    /// <param name="maximumCount">Optional maximum permissible item count declared via <see cref="MaxCountAttribute"/>.</param>
    /// <returns>The validated collection item count as an integer.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="definition"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maximumCount"/> is negative.</exception>
    /// <exception cref="PacketBufferException">
    /// Thrown when the wire count exceeds <see cref="int.MaxValue"/>, or exceeds the contract's <paramref name="maximumCount"/>.
    /// </exception>
    public static int ReadCount(
        ref PacketReader reader,
        PacketProtocolDefinition definition,
        int? maximumCount = null)
    {
        ArgumentNullException.ThrowIfNull(definition);

        ValidateMaximumCount(
            maximumCount);

        ulong wireCount =
            PacketIntegerCodec.Read(
                ref reader,
                definition.CollectionCountSize);

        if (wireCount > int.MaxValue)
        {
            throw new PacketBufferException(
                $"The collection count '{wireCount}' exceeds the maximum " +
                $"collection size supported by the .NET collection model: {int.MaxValue}.");
        }

        if (maximumCount.HasValue &&
            wireCount > (ulong)maximumCount.Value)
        {
            throw new PacketBufferException(
                $"The received collection count is {wireCount}, " +
                $"but the contract allows a maximum of {maximumCount.Value}.");
        }

        return (int)wireCount;
    }

    /// <summary>
    /// Validates collection count boundaries prior to serialization.
    /// </summary>
    /// <param name="count">The item count to validate.</param>
    /// <param name="definition">The protocol definition providing collection count capacity.</param>
    /// <param name="maximumCount">Optional configured maximum item count.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when count is negative, exceeds <paramref name="maximumCount"/>, or exceeds protocol capacity.</exception>
    private static void ValidateCountForWrite(
        int count,
        PacketProtocolDefinition definition,
        int? maximumCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(
            count);

        ValidateMaximumCount(
            maximumCount);

        if (maximumCount.HasValue &&
            count > maximumCount.Value)
        {
            throw new ArgumentOutOfRangeException(
                nameof(count),
                count,
                $"The collection contains {count} items, " +
                $"but the configured maximum is {maximumCount.Value}.");
        }

        ulong wireCount =
            (ulong)count;

        if (wireCount > definition.MaximumCollectionCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(count),
                count,
                $"The collection count does not fit in the configured " +
                $"{PacketIntegerCodec.GetByteCount(definition.CollectionCountSize)}-byte " +
                $"collection-count field. Maximum: {definition.MaximumCollectionCount}.");
        }
    }

    /// <summary>
    /// Validates that the configured maximum count parameter is non-negative.
    /// </summary>
    /// <param name="maximumCount">The maximum count to validate.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maximumCount"/> is negative.</exception>
    private static void ValidateMaximumCount(
        int? maximumCount)
    {
        if (maximumCount.HasValue &&
            maximumCount.Value < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumCount),
                maximumCount.Value,
                "The maximum collection count cannot be negative.");
        }
    }
}
