using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace PacketWire.Generator.Tests;

/// <summary>
/// Contains generator tests verifying incremental Roslyn driver execution, proper invalidation of stale
/// outputs upon identity changes, clean removal of deleted packet sources, and caching stability.
/// </summary>
public sealed class PacketWireGeneratorIncrementalTests
{
    /// <summary>
    /// Verifies that running an incremental generator driver with updated packet identity updates
    /// the generated outputs without leaving stale identity mappings.
    /// </summary>
    [Fact]
    public void SameDriverUpdatesChangedPacketIdentityWithoutStaleOutput()
    {
        const string initialSource = """
            using PacketWire;

            [PacketProtocol(
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketByteOrder.LittleEndian)]
            public sealed partial class ApplicationProtocol
            {
            }

            [Packet(typeof(ApplicationProtocol), 1, 1)]
            public sealed class MessagePacket
            {
                [PacketField(0)]
                public int Value { get; init; }
            }
            """;

        const string updatedSource = """
            using PacketWire;

            [PacketProtocol(
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketByteOrder.LittleEndian)]
            public sealed partial class ApplicationProtocol
            {
            }

            [Packet(typeof(ApplicationProtocol), 2, 1)]
            public sealed class MessagePacket
            {
                [PacketField(0)]
                public int Value { get; init; }
            }
            """;

        GeneratorIncrementalTestResult result =
            GeneratorTestHost.RunIncrementally(
                initialSource,
                updatedSource);

        AssertSuccessfulCompilation(
            result.Initial);

        AssertSuccessfulCompilation(
            result.Updated);

        Assert.Equal(
            3,
            result.Initial.GeneratedSources.Length);

        Assert.Equal(
            3,
            result.Updated.GeneratedSources.Length);

        AssertUniqueHintNames(
            result.Initial);

        AssertUniqueHintNames(
            result.Updated);

        Assert.Equal(
            GetSortedHintNames(
                result.Initial),
            GetSortedHintNames(
                result.Updated));

        const string harnessSource = """
            namespace PacketWire.Generator.Tests.Dynamic
            {
                public static class IdentityUpdateHarness
                {
                    public static bool IsInitialIdentity()
                    {
                        global::PacketWire.PacketIdentity identity =
                            global::ApplicationProtocol.GetIdentity<
                                global::MessagePacket>();

                        return
                            identity.Category == 1 &&
                            identity.Id == 1;
                    }

                    public static bool IsUpdatedIdentity()
                    {
                        global::PacketWire.PacketIdentity identity =
                            global::ApplicationProtocol.GetIdentity<
                                global::MessagePacket>();

                        return
                            identity.Category == 1 &&
                            identity.Id == 2;
                    }
                }
            }
            """;

        Assert.True(
            GeneratorTestHost.InvokeHarness<bool>(
                result.Initial,
                harnessSource,
                "PacketWire.Generator.Tests.Dynamic.IdentityUpdateHarness",
                "IsInitialIdentity"));

        Assert.True(
            GeneratorTestHost.InvokeHarness<bool>(
                result.Updated,
                harnessSource,
                "PacketWire.Generator.Tests.Dynamic.IdentityUpdateHarness",
                "IsUpdatedIdentity"));

        string initialRegistry =
            GetSingleRegistryText(
                result.Initial);

        string updatedRegistry =
            GetSingleRegistryText(
                result.Updated);

        Assert.NotEqual(
            initialRegistry,
            updatedRegistry);
    }

    /// <summary>
    /// Verifies that removing a packet declaration removes its corresponding generated codec
    /// and registry mappings upon driver re-execution.
    /// </summary>
    [Fact]
    public void SameDriverRemovesStalePacketArtifactsWhenPacketIsDeleted()
    {
        const string initialSource = """
            using PacketWire;

            [PacketProtocol(
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketByteOrder.LittleEndian)]
            public sealed partial class ApplicationProtocol
            {
            }

            [Packet(typeof(ApplicationProtocol), 1, 1)]
            public sealed class FirstPacket
            {
                [PacketField(0)]
                public int Value { get; init; }
            }

            [Packet(typeof(ApplicationProtocol), 2, 1)]
            public sealed class SecondPacket
            {
                [PacketField(0)]
                public int Value { get; init; }
            }
            """;

        const string updatedSource = """
            using PacketWire;

            [PacketProtocol(
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketByteOrder.LittleEndian)]
            public sealed partial class ApplicationProtocol
            {
            }

            [Packet(typeof(ApplicationProtocol), 2, 1)]
            public sealed class SecondPacket
            {
                [PacketField(0)]
                public int Value { get; init; }
            }
            """;

        GeneratorIncrementalTestResult result =
            GeneratorTestHost.RunIncrementally(
                initialSource,
                updatedSource);

        AssertSuccessfulCompilation(
            result.Initial);

        AssertSuccessfulCompilation(
            result.Updated);

        Assert.Equal(
            4,
            result.Initial.GeneratedSources.Length);

        Assert.Equal(
            3,
            result.Updated.GeneratedSources.Length);

        AssertUniqueHintNames(
            result.Initial);

        AssertUniqueHintNames(
            result.Updated);

        string updatedGeneratedText =
            string.Join(
                Environment.NewLine,
                result.Updated.GeneratedSources.Select(
                    static generated =>
                        generated.SourceText.ToString()));

        Assert.DoesNotContain(
            "global::FirstPacket",
            updatedGeneratedText,
            StringComparison.Ordinal);

        Assert.Contains(
            "global::SecondPacket",
            updatedGeneratedText,
            StringComparison.Ordinal);

        const string harnessSource = """
            namespace PacketWire.Generator.Tests.Dynamic
            {
                public static class PacketRemovalHarness
                {
                    public static bool Validate()
                    {
                        global::PacketWire.PacketIdentity identity =
                            global::ApplicationProtocol.GetIdentity<
                                global::SecondPacket>();

                        if (identity.Category != 1 ||
                            identity.Id != 2)
                        {
                            return false;
                        }

                        byte[] frame =
                            global::ApplicationProtocol.Serialize(
                                new global::SecondPacket
                                {
                                    Value = 0x11223344
                                });

                        global::SecondPacket restored =
                            global::ApplicationProtocol.Deserialize<
                                global::SecondPacket>(
                                frame);

                        return
                            restored.Value ==
                            0x11223344;
                    }
                }
            }
            """;

        Assert.True(
            GeneratorTestHost.InvokeHarness<bool>(
                result.Updated,
                harnessSource,
                "PacketWire.Generator.Tests.Dynamic.PacketRemovalHarness",
                "Validate"));
    }

    /// <summary>
    /// Verifies that changes to source files unrelated to PacketWire contracts do not alter
    /// the emitted generator output across incremental iterations.
    /// </summary>
    [Fact]
    public void SameDriverIgnoresUnrelatedSourceChangesForGeneratedOutput()
    {
        const string initialSource = """
            using PacketWire;

            namespace Sample
            {
                [PacketProtocol(
                    PacketIntegerSize.TwoBytes,
                    PacketIntegerSize.TwoBytes,
                    PacketIntegerSize.TwoBytes,
                    PacketByteOrder.LittleEndian)]
                public sealed partial class ApplicationProtocol
                {
                }

                [Packet(typeof(ApplicationProtocol), 1, 1)]
                public sealed class MessagePacket
                {
                    [PacketField(0)]
                    public int Value { get; init; }
                }
            }
            """;

        const string updatedSource = """
            using PacketWire;

            namespace Sample
            {
                public sealed class UnrelatedRuntimeType
                {
                    public string Description { get; init; } =
                        string.Empty;
                }

                [PacketProtocol(
                    PacketIntegerSize.TwoBytes,
                    PacketIntegerSize.TwoBytes,
                    PacketIntegerSize.TwoBytes,
                    PacketByteOrder.LittleEndian)]
                public sealed partial class ApplicationProtocol
                {
                }

                [Packet(typeof(ApplicationProtocol), 1, 1)]
                public sealed class MessagePacket
                {
                    [PacketField(0)]
                    public int Value { get; init; }
                }
            }
            """;

        GeneratorIncrementalTestResult result =
            GeneratorTestHost.RunIncrementally(
                initialSource,
                updatedSource);

        AssertSuccessfulCompilation(
            result.Initial);

        AssertSuccessfulCompilation(
            result.Updated);

        AssertUniqueHintNames(
            result.Initial);

        AssertUniqueHintNames(
            result.Updated);

        string[] initialSnapshot =
            CreateCanonicalSnapshot(
                result.Initial);

        string[] updatedSnapshot =
            CreateCanonicalSnapshot(
                result.Updated);

        Assert.Equal(
            initialSnapshot,
            updatedSnapshot);
    }

    /// <summary>
    /// Retrieves sorted generator hint names from the specified result.
    /// </summary>
    /// <param name="result">The generator test result to inspect.</param>
    /// <returns>An array of sorted hint name strings.</returns>
    private static string[] GetSortedHintNames(
        GeneratorTestResult result)
    {
        return result.GeneratedSources
            .Select(
                static generated =>
                    generated.HintName)
            .OrderBy(
                static hintName =>
                    hintName,
                StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Creates a canonically sorted snapshot of generated source texts and hint names.
    /// </summary>
    /// <param name="result">The generator test result to snapshot.</param>
    /// <returns>An array of snapshot strings ordered by hint name.</returns>
    private static string[] CreateCanonicalSnapshot(
        GeneratorTestResult result)
    {
        return result.GeneratedSources
            .OrderBy(
                static generated =>
                    generated.HintName,
                StringComparer.Ordinal)
            .Select(
                static generated =>
                    generated.HintName +
                    "\n<<<PACKETWIRE-SOURCE>>>\n" +
                    generated.SourceText.ToString())
            .ToArray();
    }

    /// <summary>
    /// Retrieves the source text of the single emitted registry class.
    /// </summary>
    /// <param name="result">The generator test result to inspect.</param>
    /// <returns>The source text of the registry.</returns>
    private static string GetSingleRegistryText(
        GeneratorTestResult result)
    {
        GeneratedSourceResult registry =
            Assert.Single(
                result.GeneratedSources.Where(
                    static generated =>
                        generated.HintName.StartsWith(
                            "PacketWire.Registry.",
                            StringComparison.Ordinal)));

        return
            registry.SourceText.ToString();
    }

    /// <summary>
    /// Asserts that all generated hint names are distinct.
    /// </summary>
    /// <param name="result">The generator test result to inspect.</param>
    private static void AssertUniqueHintNames(
        GeneratorTestResult result)
    {
        string[] hintNames =
            result.GeneratedSources
                .Select(
                    static generated =>
                        generated.HintName)
                .ToArray();

        Assert.Equal(
            hintNames.Length,
            hintNames
                .Distinct(
                    StringComparer.Ordinal)
                .Count());
    }

    /// <summary>
    /// Asserts that code generation and compilation succeeded with no diagnostic errors.
    /// </summary>
    /// <param name="result">The generator test result to inspect.</param>
    private static void AssertSuccessfulCompilation(
        GeneratorTestResult result)
    {
        Assert.DoesNotContain(
            result.GeneratorDiagnostics,
            static diagnostic =>
                diagnostic.Severity
                == DiagnosticSeverity.Error);

        ImmutableArray<Diagnostic> errors =
            result.OutputCompilation
                .GetDiagnostics()
                .Where(
                    static diagnostic =>
                        diagnostic.Severity
                        == DiagnosticSeverity.Error)
                .ToImmutableArray();

        Assert.True(
            errors.IsEmpty,
            string.Join(
                Environment.NewLine,
                errors.Select(
                    static diagnostic =>
                        diagnostic.ToString())));
    }
}