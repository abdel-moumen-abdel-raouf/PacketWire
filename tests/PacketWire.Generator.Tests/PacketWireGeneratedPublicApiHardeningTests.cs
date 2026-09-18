using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace PacketWire.Generator.Tests;

/// <summary>
/// Contains generator integration tests verifying public API hardening, internal encapsulation,
/// generic identity queries, and absence of transport-layer coupling in generated facades.
/// </summary>
public sealed class PacketWireGeneratedPublicApiHardeningTests
{
    /// <summary>
    /// Verifies that generic identity queries work for registered packet types and fail closed
    /// for unregistered types.
    /// </summary>
    [Fact]
    public void GenericIdentityApiWorksAndUnknownTypesRemainFailClosed()
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

            [Packet(typeof(ApplicationProtocol), 0x1234, 3)]
            public sealed class LoginRequest
            {
            }
            """;

        GeneratorTestResult result =
            GeneratorTestHost.RunWithOutput(
                source);

        AssertSuccessfulCompilation(
            result);

        const string harnessSource = """
            namespace PacketWire.Generator.Tests.Dynamic
            {
                public static class PublicIdentityHarness
                {
                    public static bool Validate()
                    {
                        global::PacketWire.PacketIdentity direct =
                            global::ApplicationProtocol.GetIdentity<global::LoginRequest>();

                        if (direct.Category != 3 ||
                            direct.Id != 0x1234)
                        {
                            return false;
                        }

                        if (!global::ApplicationProtocol.TryGetIdentity<global::LoginRequest>(
                                out global::PacketWire.PacketIdentity discovered))
                        {
                            return false;
                        }

                        if (discovered != direct)
                        {
                            return false;
                        }

                        if (global::ApplicationProtocol.TryGetIdentity<string>(
                                out global::PacketWire.PacketIdentity unknownIdentity))
                        {
                            return false;
                        }

                        if (unknownIdentity != default)
                        {
                            return false;
                        }

                        try
                        {
                            _ =
                                global::ApplicationProtocol.GetIdentity<string>();

                            return false;
                        }
                        catch (global::PacketWire.PacketTypeNotRegisteredException exception)
                        {
                            return
                                exception.PacketType ==
                                typeof(string);
                        }
                    }
                }
            }
            """;

        Assert.True(
            GeneratorTestHost.InvokeHarness<bool>(
                result,
                harnessSource,
                "PacketWire.Generator.Tests.Dynamic.PublicIdentityHarness",
                "Validate"));
    }

    /// <summary>
    /// Verifies that generated registry and codec classes remain internal, and that the public
    /// protocol facade exposes only pure serialization APIs without socket or transport abstractions.
    /// </summary>
    [Fact]
    public void GeneratedImplementationStaysInternalAndFacadeContainsNoTransportApi()
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

            [Packet(typeof(ApplicationProtocol), 1, 1)]
            public sealed class TestPacket
            {
                [PacketField(0)]
                public int Value { get; init; }
            }
            """;

        GeneratorTestResult result =
            GeneratorTestHost.RunWithOutput(
                source);

        AssertSuccessfulCompilation(
            result);

        GeneratedSourceResult registrySource =
            Assert.Single(
                result.GeneratedSources.Where(
                    static generated =>
                        generated.HintName.StartsWith(
                            "PacketWire.Registry.",
                            StringComparison.Ordinal)));

        GeneratedSourceResult facadeSource =
            Assert.Single(
                result.GeneratedSources.Where(
                    static generated =>
                        generated.HintName.StartsWith(
                            "PacketWire.ProtocolFacade.",
                            StringComparison.Ordinal)));

        GeneratedSourceResult codecSource =
            Assert.Single(
                result.GeneratedSources.Where(
                    static generated =>
                        !generated.HintName.StartsWith(
                            "PacketWire.Registry.",
                            StringComparison.Ordinal) &&
                        !generated.HintName.StartsWith(
                            "PacketWire.ProtocolFacade.",
                            StringComparison.Ordinal)));

        string registryText =
            registrySource.SourceText.ToString();

        string codecText =
            codecSource.SourceText.ToString();

        string facadeText =
            facadeSource.SourceText.ToString();

        Assert.Contains(
            "internal static class __PacketWireRegistry_",
            registryText,
            StringComparison.Ordinal);

        Assert.Contains(
            "internal static class __PacketWireCodec_",
            codecText,
            StringComparison.Ordinal);

        string[] forbiddenTransportTerms =
        [
            "SendAsync",
            "ReceiveAsync",
            "ConnectAsync",
            "ListenAsync",
            "TcpClient",
            "TcpListener",
            "Socket",
            "NetworkStream",
            "UdpClient"
        ];

        foreach (string forbiddenTerm in forbiddenTransportTerms)
        {
            Assert.DoesNotContain(
                forbiddenTerm,
                facadeText,
                StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains(
            "public static byte[] Serialize(object packet)",
            facadeText,
            StringComparison.Ordinal);

        Assert.Contains(
            "public static object Deserialize(global::System.ReadOnlySpan<byte> packetBytes)",
            facadeText,
            StringComparison.Ordinal);

        Assert.Contains(
            "public static bool TryGetIdentity<TPacket>(",
            facadeText,
            StringComparison.Ordinal);

        Assert.Contains(
            "public static global::PacketWire.PacketIdentity GetIdentity<TPacket>()",
            facadeText,
            StringComparison.Ordinal);
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