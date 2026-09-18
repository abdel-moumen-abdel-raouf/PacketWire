using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace PacketWire.Generator.Tests;

/// <summary>
/// Contains generator tests verifying emission hardening, deduplicated shared nested contract codecs,
/// canonical registry ordering by category and packet ID, and incremental diagnostic recovery.
/// </summary>
public sealed class PacketWireGeneratorEmissionHardeningTests
{
    /// <summary>
    /// Verifies that multiple packets referencing the same shared nested contract emit only a single codec
    /// and generate unique source hint names.
    /// </summary>
    [Fact]
    public void SharedNestedContractEmitsSingleCodecAndUniqueHintNames()
    {
        const string source = """
            using PacketWire;

            [PacketProtocol(
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketByteOrder.LittleEndian)]
            public sealed partial class ApplicationProtocol
            {
            }

            [PacketContract]
            public sealed class SharedValue
            {
                [PacketField(0)]
                public int Code { get; init; }
            }

            [Packet(typeof(ApplicationProtocol), 1, 1)]
            public sealed class FirstPacket
            {
                [PacketField(0)]
                public SharedValue First { get; init; } = new();

                [PacketField(1)]
                public SharedValue Second { get; init; } = new();
            }

            [Packet(typeof(ApplicationProtocol), 2, 1)]
            public sealed class SecondPacket
            {
                [PacketField(0)]
                public SharedValue Value { get; init; } = new();
            }
            """;

        GeneratorTestResult result =
            GeneratorTestHost.RunWithOutput(
                source);

        AssertSuccessfulCompilation(
            result);

        Assert.Equal(
            5,
            result.GeneratedSources.Length);

        AssertUniqueHintNames(
            result);

        GeneratedSourceResult sharedCodec =
            Assert.Single(
                result.GeneratedSources.Where(
                    static generated =>
                        generated.HintName.StartsWith(
                            "PacketWire.SharedValue.",
                            StringComparison.Ordinal)));

        string codecText =
            sharedCodec.SourceText.ToString();

        Assert.Contains(
            "global::SharedValue value",
            codecText,
            StringComparison.Ordinal);

        Assert.Single(
            result.GeneratedSources.Where(
                static generated =>
                    generated.HintName.StartsWith(
                        "PacketWire.Registry.",
                        StringComparison.Ordinal)));

        Assert.Single(
            result.GeneratedSources.Where(
                static generated =>
                    generated.HintName.StartsWith(
                        "PacketWire.ProtocolFacade.",
                        StringComparison.Ordinal)));
    }

    /// <summary>
    /// Verifies that packet entries within the generated registry are canonically sorted primarily
    /// by category and secondarily by packet identifier.
    /// </summary>
    [Fact]
    public void RegistryEmissionOrderIsCanonicalByCategoryThenPacketId()
    {
        const string source = """
            using PacketWire;

            [PacketProtocol(
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketByteOrder.LittleEndian)]
            public sealed partial class ApplicationProtocol
            {
            }

            [Packet(typeof(ApplicationProtocol), 5, 2)]
            public sealed class PacketC
            {
            }

            [Packet(typeof(ApplicationProtocol), 9, 1)]
            public sealed class PacketA
            {
            }

            [Packet(typeof(ApplicationProtocol), 1, 1)]
            public sealed class PacketD
            {
            }

            [Packet(typeof(ApplicationProtocol), 5, 1)]
            public sealed class PacketB
            {
            }
            """;

        GeneratorTestResult result =
            GeneratorTestHost.RunWithOutput(
                source);

        AssertSuccessfulCompilation(
            result);

        GeneratedSourceResult registry =
            Assert.Single(
                result.GeneratedSources.Where(
                    static generated =>
                        generated.HintName.StartsWith(
                            "PacketWire.Registry.",
                            StringComparison.Ordinal)));

        string registryText =
            registry.SourceText.ToString();

        int packetDIndex =
            registryText.IndexOf(
                "typeof(global::PacketD)",
                StringComparison.Ordinal);

        int packetBIndex =
            registryText.IndexOf(
                "typeof(global::PacketB)",
                StringComparison.Ordinal);

        int packetAIndex =
            registryText.IndexOf(
                "typeof(global::PacketA)",
                StringComparison.Ordinal);

        int packetCIndex =
            registryText.IndexOf(
                "typeof(global::PacketC)",
                StringComparison.Ordinal);

        Assert.True(
            packetDIndex >= 0);

        Assert.True(
            packetBIndex >= 0);

        Assert.True(
            packetAIndex >= 0);

        Assert.True(
            packetCIndex >= 0);

        Assert.True(
            packetDIndex <
            packetBIndex);

        Assert.True(
            packetBIndex <
            packetAIndex);

        Assert.True(
            packetAIndex <
            packetCIndex);
    }

    /// <summary>
    /// Verifies that fixing duplicate packet identity declarations clears PWG002 diagnostics on subsequent incremental runs.
    /// </summary>
    [Fact]
    public void SameDriverClearsDuplicateIdentityDiagnosticsAfterIdentityIsFixed()
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
            }

            [Packet(typeof(ApplicationProtocol), 1, 1)]
            public sealed class SecondPacket
            {
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

            [Packet(typeof(ApplicationProtocol), 1, 1)]
            public sealed class FirstPacket
            {
            }

            [Packet(typeof(ApplicationProtocol), 2, 1)]
            public sealed class SecondPacket
            {
            }
            """;

        GeneratorIncrementalTestResult result =
            GeneratorTestHost.RunIncrementally(
                initialSource,
                updatedSource);

        Diagnostic[] initialDuplicateDiagnostics =
            result.Initial.GeneratorDiagnostics
                .Where(
                    static diagnostic =>
                        diagnostic.Id ==
                        "PWG002")
                .ToArray();

        Assert.Equal(
            2,
            initialDuplicateDiagnostics.Length);

        Assert.DoesNotContain(
            result.Initial.GeneratorDiagnostics,
            static diagnostic =>
                diagnostic.Severity ==
                    DiagnosticSeverity.Error &&
                diagnostic.Id !=
                    "PWG002");

        Assert.DoesNotContain(
            result.Updated.GeneratorDiagnostics,
            static diagnostic =>
                diagnostic.Id ==
                "PWG002");

        AssertSuccessfulCompilation(
            result.Updated);

        AssertUniqueHintNames(
            result.Updated);

        const string harnessSource = """
            namespace PacketWire.Generator.Tests.Dynamic
            {
                public static class DiagnosticStateHarness
                {
                    public static bool Validate()
                    {
                        global::PacketWire.PacketIdentity first =
                            global::ApplicationProtocol.GetIdentity<
                                global::FirstPacket>();

                        global::PacketWire.PacketIdentity second =
                            global::ApplicationProtocol.GetIdentity<
                                global::SecondPacket>();

                        return
                            first.Category == 1 &&
                            first.Id == 1 &&
                            second.Category == 1 &&
                            second.Id == 2;
                    }
                }
            }
            """;

        Assert.True(
            GeneratorTestHost.InvokeHarness<bool>(
                result.Updated,
                harnessSource,
                "PacketWire.Generator.Tests.Dynamic.DiagnosticStateHarness",
                "Validate"));
    }

    /// <summary>
    /// Asserts that all generated hint names are distinct across the generated sources.
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