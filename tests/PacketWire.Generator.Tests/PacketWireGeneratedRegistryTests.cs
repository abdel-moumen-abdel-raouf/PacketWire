using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace PacketWire.Generator.Tests;

/// <summary>
/// Contains generator integration tests verifying bidirectional type-to-identity and identity-to-type
/// mappings generated in protocol registries.
/// </summary>
public sealed class PacketWireGeneratedRegistryTests
{
    /// <summary>
    /// Verifies that the generated registry accurately maps packet types to composite identities
    /// and vice versa across multiple registered packet models.
    /// </summary>
    [Fact]
    public void RegistryMapsPacketTypesAndCompositeIdentitiesBothWays()
    {
        const string source = """
            using PacketWire;

            [PacketProtocol(
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketByteOrder.LittleEndian)]
            public sealed partial class TestProtocol
            {
            }

            [Packet(typeof(TestProtocol), 1, 1)]
            public sealed class LoginRequest
            {
            }

            [Packet(typeof(TestProtocol), 2, 1)]
            public sealed class LoginResponse
            {
            }

            [Packet(typeof(TestProtocol), 1, 2)]
            public sealed class SecurityRequest
            {
            }
            """;

        GeneratorTestResult result =
            GeneratorTestHost.RunWithOutput(source);

        AssertSuccessfulCompilation(result);

        string registryClassName =
            GetSingleRegistryClassName(result);

        string harnessSource = $$"""
            namespace PacketWire.Generator.Tests.Dynamic
            {
                public static class RegistryHarness
                {
                    public static bool Validate()
                    {
                        if (global::PacketWire.Generated.{{registryClassName}}.PacketCount != 3)
                        {
                            return false;
                        }

                        if (!global::PacketWire.Generated.{{registryClassName}}.TryGetIdentity(
                                typeof(global::LoginRequest),
                                out global::PacketWire.PacketIdentity loginRequestIdentity))
                        {
                            return false;
                        }

                        if (loginRequestIdentity != new global::PacketWire.PacketIdentity(1, 1))
                        {
                            return false;
                        }

                        if (!global::PacketWire.Generated.{{registryClassName}}.TryGetIdentity(
                                typeof(global::LoginResponse),
                                out global::PacketWire.PacketIdentity loginResponseIdentity))
                        {
                            return false;
                        }

                        if (loginResponseIdentity != new global::PacketWire.PacketIdentity(1, 2))
                        {
                            return false;
                        }

                        if (!global::PacketWire.Generated.{{registryClassName}}.TryGetIdentity(
                                typeof(global::SecurityRequest),
                                out global::PacketWire.PacketIdentity securityIdentity))
                        {
                            return false;
                        }

                        if (securityIdentity != new global::PacketWire.PacketIdentity(2, 1))
                        {
                            return false;
                        }

                        if (!global::PacketWire.Generated.{{registryClassName}}.TryGetPacketType(
                                new global::PacketWire.PacketIdentity(1, 1),
                                out global::System.Type? loginRequestType))
                        {
                            return false;
                        }

                        if (loginRequestType != typeof(global::LoginRequest))
                        {
                            return false;
                        }

                        if (!global::PacketWire.Generated.{{registryClassName}}.TryGetPacketType(
                                new global::PacketWire.PacketIdentity(2, 1),
                                out global::System.Type? securityType))
                        {
                            return false;
                        }

                        return securityType == typeof(global::SecurityRequest);
                    }
                }
            }
            """;

        bool valid =
            GeneratorTestHost.InvokeHarness<bool>(
                result,
                harnessSource,
                "PacketWire.Generator.Tests.Dynamic.RegistryHarness",
                "Validate");

        Assert.True(valid);
    }

    /// <summary>
    /// Verifies that packets declared without an explicit category default to category zero in the registry.
    /// </summary>
    [Fact]
    public void RegistryUsesDefaultCategoryZero()
    {
        const string source = """
            using PacketWire;

            [PacketProtocol(
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketByteOrder.LittleEndian)]
            public sealed partial class TestProtocol
            {
            }

            [Packet(typeof(TestProtocol), 25)]
            public sealed class DefaultPacket
            {
            }
            """;

        GeneratorTestResult result =
            GeneratorTestHost.RunWithOutput(source);

        AssertSuccessfulCompilation(result);

        string registryClassName =
            GetSingleRegistryClassName(result);

        string harnessSource = $$"""
            namespace PacketWire.Generator.Tests.Dynamic
            {
                public static class DefaultCategoryHarness
                {
                    public static bool Validate()
                    {
                        if (!global::PacketWire.Generated.{{registryClassName}}.TryGetIdentity(
                                typeof(global::DefaultPacket),
                                out global::PacketWire.PacketIdentity identity))
                        {
                            return false;
                        }

                        return
                            identity.Category == 0 &&
                            identity.Id == 25;
                    }
                }
            }
            """;

        Assert.True(
            GeneratorTestHost.InvokeHarness<bool>(
                result,
                harnessSource,
                "PacketWire.Generator.Tests.Dynamic.DefaultCategoryHarness",
                "Validate"));
    }

    /// <summary>
    /// Verifies that the generated registry returns false and default values when querying unregistered types or identities.
    /// </summary>
    [Fact]
    public void RegistryReturnsFalseForUnknownTypeAndIdentity()
    {
        const string source = """
            using PacketWire;

            [PacketProtocol(
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketByteOrder.LittleEndian)]
            public sealed partial class TestProtocol
            {
            }

            [Packet(typeof(TestProtocol), 1, 1)]
            public sealed class KnownPacket
            {
            }
            """;

        GeneratorTestResult result =
            GeneratorTestHost.RunWithOutput(source);

        AssertSuccessfulCompilation(result);

        string registryClassName =
            GetSingleRegistryClassName(result);

        string harnessSource = $$"""
            namespace PacketWire.Generator.Tests.Dynamic
            {
                public static class UnknownRegistryHarness
                {
                    public static bool Validate()
                    {
                        bool typeFound =
                            global::PacketWire.Generated.{{registryClassName}}.TryGetIdentity(
                                typeof(string),
                                out global::PacketWire.PacketIdentity identity);

                        if (typeFound ||
                            identity != default)
                        {
                            return false;
                        }

                        bool identityFound =
                            global::PacketWire.Generated.{{registryClassName}}.TryGetPacketType(
                                new global::PacketWire.PacketIdentity(99, 99),
                                out global::System.Type? packetType);

                        return
                            !identityFound &&
                            packetType is null;
                    }
                }
            }
            """;

        Assert.True(
            GeneratorTestHost.InvokeHarness<bool>(
                result,
                harnessSource,
                "PacketWire.Generator.Tests.Dynamic.UnknownRegistryHarness",
                "Validate"));
    }

    /// <summary>
    /// Verifies that distinct protocols in the same compilation generate independent registries.
    /// </summary>
    [Fact]
    public void DifferentProtocolsGenerateIndependentRegistries()
    {
        const string source = """
            using PacketWire;

            [PacketProtocol(
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketByteOrder.LittleEndian)]
            public sealed partial class FirstProtocol
            {
            }

            [PacketProtocol(
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketByteOrder.LittleEndian)]
            public sealed partial class SecondProtocol
            {
            }

            [Packet(typeof(FirstProtocol), 1, 1)]
            public sealed class FirstPacket
            {
            }

            [Packet(typeof(SecondProtocol), 1, 1)]
            public sealed class SecondPacket
            {
            }
            """;

        GeneratorTestResult result =
            GeneratorTestHost.RunWithOutput(source);

        AssertSuccessfulCompilation(result);

        GeneratedSourceResult[] registrySources =
            result.GeneratedSources
                .Where(
                    static generatedSource =>
                        generatedSource.HintName.StartsWith(
                            "PacketWire.Registry.",
                            StringComparison.Ordinal))
                .ToArray();

        Assert.Equal(
            2,
            registrySources.Length);
    }

    /// <summary>
    /// Extracts the generated internal registry class name from the generator test output.
    /// </summary>
    /// <param name="result">The generator test result containing emitted sources.</param>
    /// <returns>The class name of the generated registry.</returns>
    private static string GetSingleRegistryClassName(
        GeneratorTestResult result)
    {
        GeneratedSourceResult registrySource =
            Assert.Single(
                result.GeneratedSources.Where(
                    static generatedSource =>
                        generatedSource.HintName.StartsWith(
                            "PacketWire.Registry.",
                            StringComparison.Ordinal)));

        string source =
            registrySource.SourceText.ToString();

        const string marker =
            "internal static class ";

        int markerIndex =
            source.IndexOf(
                marker,
                StringComparison.Ordinal);

        Assert.True(
            markerIndex >= 0);

        int nameStart =
            markerIndex +
            marker.Length;

        int nameEnd =
            source.IndexOf(
                '\n',
                nameStart);

        Assert.True(
            nameEnd > nameStart);

        return source[
                nameStart..
                nameEnd]
            .Trim();
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

