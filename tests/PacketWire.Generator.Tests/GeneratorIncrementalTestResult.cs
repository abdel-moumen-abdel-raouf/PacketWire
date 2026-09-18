namespace PacketWire.Generator.Tests;

/// <summary>
/// Holds the results of initial and updated generator driver runs during incremental generator testing.
/// </summary>
internal sealed class GeneratorIncrementalTestResult
{
    /// <summary>
    /// Initializes a new instance of <see cref="GeneratorIncrementalTestResult"/>.
    /// </summary>
    /// <param name="initial">The result of the initial generator execution.</param>
    /// <param name="updated">The result of the subsequent incremental generator execution.</param>
    internal GeneratorIncrementalTestResult(
        GeneratorTestResult initial,
        GeneratorTestResult updated)
    {
        ArgumentNullException.ThrowIfNull(
            initial);

        ArgumentNullException.ThrowIfNull(
            updated);

        Initial = initial;
        Updated = updated;
    }

    /// <summary>
    /// Gets the result of the initial compilation and generator run.
    /// </summary>
    internal GeneratorTestResult Initial { get; }

    /// <summary>
    /// Gets the result of the updated compilation and generator run.
    /// </summary>
    internal GeneratorTestResult Updated { get; }
}