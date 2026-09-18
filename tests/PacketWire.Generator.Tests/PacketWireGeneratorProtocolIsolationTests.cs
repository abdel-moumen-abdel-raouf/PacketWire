using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace PacketWire.Generator.Tests;

/// <summary>
/// Contains generator tests verifying protocol isolation across multiple protocols declaring identical
/// simple type names in distinct namespaces, ensuring independent facades, registries, and framing.
/// </summary>
public sealed class PacketWireGeneratorProtocolIsolationTests
{
    /// <summary>
    /// Protocol source declaring identical type names across two distinct namespaces and protocol configurations.
    /// </summary>
    private const string ProtocolSource = """
        using PacketWire;

        namespace Alpha.Protocols
        {
            [PacketProtocol(
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketByteOrder.LittleEndian,
                PacketIntegerSize.OneByte)]
            public sealed partial class ApplicationProtocol
            {
            }

            [Packet(typeof(ApplicationProtocol), 0x1234, 3)]
            public sealed class MessagePacket
            {
                [PacketField(0)]
                public int Value { get; init; }
            }
        }

        namespace Beta.Protocols
        {
            [PacketProtocol(
                PacketIntegerSize.FourBytes,
                PacketIntegerSize.FourBytes,
                PacketIntegerSize.OneByte,
                PacketByteOrder.BigEndian,
                PacketIntegerSize.TwoBytes)]
            public sealed partial class ApplicationProtocol
            {
            }

            [Packet(typeof(ApplicationProtocol), 0x1234, 3)]
            public sealed class MessagePacket
            {
                [PacketField(0)]
                public int Value { get; init; }
            }
        }
        """;

    /// <summary>
    /// Verifies that identical simple type names defined across separate namespaces generate unique,
    /// disambiguated protocol artifacts, registries, codecs, and facades.
    /// </summary>
    [Fact]
    public void SameSimpleNamesAcrossNamespacesGenerateUniqueProtocolArtifacts()
    {
        GeneratorTestResult result =
            GeneratorTestHost.RunWithOutput(
                ProtocolSource);

        AssertSuccessfulCompilation(
            result);

        Assert.Equal(
            6,
            result.GeneratedSources.Length);

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

        GeneratedSourceResult[] registries =
            result.GeneratedSources
                .Where(
                    static generated =>
                        generated.HintName.StartsWith(
                            "PacketWire.Registry.",
                            StringComparison.Ordinal))
                .ToArray();

        GeneratedSourceResult[] facades =
            result.GeneratedSources
                .Where(
                    static generated =>
                        generated.HintName.StartsWith(
                            "PacketWire.ProtocolFacade.",
                            StringComparison.Ordinal))
                .ToArray();

        GeneratedSourceResult[] codecs =
            result.GeneratedSources
                .Where(
                    static generated =>
                        !generated.HintName.StartsWith(
                            "PacketWire.Registry.",
                            StringComparison.Ordinal) &&
                        !generated.HintName.StartsWith(
                            "PacketWire.ProtocolFacade.",
                            StringComparison.Ordinal))
                .ToArray();

        Assert.Equal(
            2,
            registries.Length);

        Assert.Equal(
            2,
            facades.Length);

        Assert.Equal(
            2,
            codecs.Length);

        Assert.Equal(
            2,
            codecs.Count(
                static codec =>
                    codec.HintName.StartsWith(
                        "PacketWire.MessagePacket.",
                        StringComparison.Ordinal)));

        string combinedRegistryText =
            string.Join(
                Environment.NewLine,
                registries.Select(
                    static generated =>
                        generated.SourceText.ToString()));

        Assert.Contains(
            "typeof(global::Alpha.Protocols.MessagePacket)",
            combinedRegistryText,
            StringComparison.Ordinal);

        Assert.Contains(
            "typeof(global::Beta.Protocols.MessagePacket)",
            combinedRegistryText,
            StringComparison.Ordinal);

        string combinedFacadeText =
            string.Join(
                Environment.NewLine,
                facades.Select(
                    static generated =>
                        generated.SourceText.ToString()));

        Assert.Contains(
            "namespace Alpha.Protocols",
            combinedFacadeText,
            StringComparison.Ordinal);

        Assert.Contains(
            "namespace Beta.Protocols",
            combinedFacadeText,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that runtime execution of isolated protocols maintains strict separation of framing configurations,
    /// byte orders, and packet type registrations without cross-contamination.
    /// </summary>
    [Fact]
    public void ProtocolFacadesRemainIsolatedAcrossNamespacesAndDefinitions()
    {
        GeneratorTestResult result =
            GeneratorTestHost.RunWithOutput(
                ProtocolSource);

        AssertSuccessfulCompilation(
            result);

        const string harnessSource = """
            namespace PacketWire.Generator.Tests.Dynamic
            {
                public static class ProtocolIsolationHarness
                {
                    public static bool Validate()
                    {
                        global::PacketWire.PacketIdentity alphaIdentity =
                            global::Alpha.Protocols.ApplicationProtocol.GetIdentity<
                                global::Alpha.Protocols.MessagePacket>();

                        global::PacketWire.PacketIdentity betaIdentity =
                            global::Beta.Protocols.ApplicationProtocol.GetIdentity<
                                global::Beta.Protocols.MessagePacket>();

                        if (alphaIdentity.Category != 3 ||
                            alphaIdentity.Id != 0x1234 ||
                            betaIdentity.Category != 3 ||
                            betaIdentity.Id != 0x1234)
                        {
                            return false;
                        }

                        //
                        // The same wire identity exists in both protocols,
                        // but type registration remains protocol-local.
                        //
                        if (global::Alpha.Protocols.ApplicationProtocol.TryGetIdentity<
                                global::Beta.Protocols.MessagePacket>(
                                out global::PacketWire.PacketIdentity alphaCrossIdentity))
                        {
                            return false;
                        }

                        if (alphaCrossIdentity != default)
                        {
                            return false;
                        }

                        if (global::Beta.Protocols.ApplicationProtocol.TryGetIdentity<
                                global::Alpha.Protocols.MessagePacket>(
                                out global::PacketWire.PacketIdentity betaCrossIdentity))
                        {
                            return false;
                        }

                        if (betaCrossIdentity != default)
                        {
                            return false;
                        }

                        global::PacketWire.PacketProtocolDefinition alphaDefinition =
                            global::Alpha.Protocols.ApplicationProtocol.Definition;

                        global::PacketWire.PacketProtocolDefinition betaDefinition =
                            global::Beta.Protocols.ApplicationProtocol.Definition;

                        if (alphaDefinition.HeaderLength != 6 ||
                            alphaDefinition.ByteOrder !=
                                global::PacketWire.PacketByteOrder.LittleEndian ||
                            alphaDefinition.PacketLengthSize !=
                                global::PacketWire.PacketIntegerSize.TwoBytes ||
                            alphaDefinition.PacketCategorySize !=
                                global::PacketWire.PacketIntegerSize.OneByte ||
                            alphaDefinition.PacketIdSize !=
                                global::PacketWire.PacketIntegerSize.TwoBytes)
                        {
                            return false;
                        }

                        if (betaDefinition.HeaderLength != 11 ||
                            betaDefinition.ByteOrder !=
                                global::PacketWire.PacketByteOrder.BigEndian ||
                            betaDefinition.PacketLengthSize !=
                                global::PacketWire.PacketIntegerSize.FourBytes ||
                            betaDefinition.PacketCategorySize !=
                                global::PacketWire.PacketIntegerSize.TwoBytes ||
                            betaDefinition.PacketIdSize !=
                                global::PacketWire.PacketIntegerSize.FourBytes)
                        {
                            return false;
                        }

                        byte[] alphaFrame =
                            global::Alpha.Protocols.ApplicationProtocol.Serialize(
                                new global::Alpha.Protocols.MessagePacket
                                {
                                    Value = 0x11223344
                                });

                        byte[] betaFrame =
                            global::Beta.Protocols.ApplicationProtocol.Serialize(
                                new global::Beta.Protocols.MessagePacket
                                {
                                    Value = 0x11223344
                                });

                        if (!ValidateAlphaBytes(
                                alphaFrame))
                        {
                            return false;
                        }

                        if (!ValidateBetaBytes(
                                betaFrame))
                        {
                            return false;
                        }

                        object alphaUntyped =
                            global::Alpha.Protocols.ApplicationProtocol.Deserialize(
                                alphaFrame);

                        object betaUntyped =
                            global::Beta.Protocols.ApplicationProtocol.Deserialize(
                                betaFrame);

                        if (alphaUntyped is not global::Alpha.Protocols.MessagePacket alphaObject ||
                            betaUntyped is not global::Beta.Protocols.MessagePacket betaObject)
                        {
                            return false;
                        }

                        if (alphaObject.Value != 0x11223344 ||
                            betaObject.Value != 0x11223344)
                        {
                            return false;
                        }

                        global::Alpha.Protocols.MessagePacket alphaTyped =
                            global::Alpha.Protocols.ApplicationProtocol.Deserialize<
                                global::Alpha.Protocols.MessagePacket>(
                                alphaFrame);

                        global::Beta.Protocols.MessagePacket betaTyped =
                            global::Beta.Protocols.ApplicationProtocol.Deserialize<
                                global::Beta.Protocols.MessagePacket>(
                                betaFrame);

                        return
                            alphaTyped.Value == 0x11223344 &&
                            betaTyped.Value == 0x11223344;
                    }

                    private static bool ValidateAlphaBytes(
                        byte[] packet)
                    {
                        if (packet.Length != 10)
                        {
                            return false;
                        }

                        return
                            packet[0] == 0x0A &&
                            packet[1] == 0x00 &&

                            packet[2] == 0x00 &&

                            packet[3] == 0x03 &&

                            packet[4] == 0x34 &&
                            packet[5] == 0x12 &&

                            packet[6] == 0x44 &&
                            packet[7] == 0x33 &&
                            packet[8] == 0x22 &&
                            packet[9] == 0x11;
                    }

                    private static bool ValidateBetaBytes(
                        byte[] packet)
                    {
                        if (packet.Length != 15)
                        {
                            return false;
                        }

                        return
                            packet[0] == 0x00 &&
                            packet[1] == 0x00 &&
                            packet[2] == 0x00 &&
                            packet[3] == 0x0F &&

                            packet[4] == 0x00 &&

                            packet[5] == 0x00 &&
                            packet[6] == 0x03 &&

                            packet[7] == 0x00 &&
                            packet[8] == 0x00 &&
                            packet[9] == 0x12 &&
                            packet[10] == 0x34 &&

                            packet[11] == 0x11 &&
                            packet[12] == 0x22 &&
                            packet[13] == 0x33 &&
                            packet[14] == 0x44;
                    }
                }
            }
            """;

        Assert.True(
            GeneratorTestHost.InvokeHarness<bool>(
                result,
                harnessSource,
                "PacketWire.Generator.Tests.Dynamic.ProtocolIsolationHarness",
                "Validate"));
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