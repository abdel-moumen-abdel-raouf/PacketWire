using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace PacketWire.Generator.Tests;

/// <summary>
/// Verifies protocol framing flag validation, specifically ensuring plain deserialization rejects protected frames.
/// </summary>
public sealed class PacketWireGeneratedProtocolFlagTests
{
    /// <summary>
    /// Verifies that attempting to deserialize a frame with <see cref="PacketWire.PacketFrameOptions.Protected"/> flag set using a plain deserialize method throws <see cref="PacketWire.PacketProtectionRequiredException"/>.
    /// </summary>
    [Fact]
    public void PlainDeserializeRejectsProtectedFrame()
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
            public sealed class KnownPacket
            {
            }
            """;

        GeneratorTestResult result =
            GeneratorTestHost.RunWithOutput(source);

        AssertSuccessfulCompilation(
            result);

        const string harnessSource = """
            namespace PacketWire.Generator.Tests.Dynamic
            {
                public static class ProtectedFlagHarness
                {
                    public static bool Validate()
                    {
                        global::PacketWire.PacketProtocolDefinition definition =
                            global::ApplicationProtocol.Definition;

                        byte[] frame =
                            new byte[definition.HeaderLength];

                        _ =
                            global::PacketWire.PacketFrameCodec.WriteFrame(
                                frame,
                                global::PacketWire.PacketFrameOptions.Protected,
                                1,
                                1,
                                global::System.ReadOnlySpan<byte>.Empty,
                                definition);

                        try
                        {
                            _ =
                                global::ApplicationProtocol.Deserialize(
                                    frame);

                            return false;
                        }
                        catch (global::PacketWire.PacketProtectionRequiredException)
                        {
                            return true;
                        }
                    }
                }
            }
            """;

        Assert.True(
            GeneratorTestHost.InvokeHarness<bool>(
                result,
                harnessSource,
                "PacketWire.Generator.Tests.Dynamic.ProtectedFlagHarness",
                "Validate"));
    }

    /// <summary>
    /// Asserts that generator diagnostics and output compilation contain no compilation errors.
    /// </summary>
    /// <param name="result">The generator test result.</param>
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