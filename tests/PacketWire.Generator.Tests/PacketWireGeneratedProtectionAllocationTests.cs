using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace PacketWire.Generator.Tests;

/// <summary>
/// Verifies memory allocation behavior and buffer pooling (<see cref="System.Buffers.ArrayPool{T}"/>) in generated protected serialization and deserialization routines.
/// </summary>
public sealed class PacketWireGeneratedProtectionAllocationTests
{
    /// <summary>
    /// Verifies that emitted protected facades rent and return buffers to <see cref="System.Buffers.ArrayPool{T}.Shared"/> across serialize and deserialize paths.
    /// </summary>
    [Fact]
    public void ProtectedFacadeUsesPooledTemporaryPlaintextBuffers()
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

        GeneratedSourceResult facade =
            Assert.Single(
                result.GeneratedSources.Where(
                    static generated =>
                        generated.HintName.StartsWith(
                            "PacketWire.ProtocolFacade.",
                            StringComparison.Ordinal)));

        string text =
            facade.SourceText.ToString();

        Assert.Equal(
            2,
            CountOccurrences(
                text,
                "global::System.Buffers.ArrayPool<byte>.Shared.Rent("));

        Assert.Equal(
            2,
            CountOccurrences(
                text,
                "global::System.Buffers.ArrayPool<byte>.Shared.Return(plaintextBuffer);"));

        Assert.Equal(
            2,
            CountOccurrences(
                text,
                "global::System.Security.Cryptography.CryptographicOperations.ZeroMemory(plaintextPayload);"));

        Assert.DoesNotContain(
            "byte[] plaintextPayload = new byte[payloadLength];",
            text,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "byte[] plaintextPayload = new byte[maximumPlaintextLength];",
            text,
            StringComparison.Ordinal);

        Assert.Contains(
            "byte[] frame = new byte[(int)frameLength];",
            text,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that zero-length payloads execute cleanly without allocating unnecessary pooled buffers or throwing on rental.
    /// </summary>
    [Fact]
    public void ProtectedFacadeSupportsZeroLengthPayloadWithPooledBuffer()
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
            public sealed class EmptyPacket
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
                public static class PooledZeroPayloadHarness
                {
                    public static bool Validate()
                    {
                        byte[] key =
                        [
                            0x00,
                            0x01,
                            0x02,
                            0x03,
                            0x04,
                            0x05,
                            0x06,
                            0x07,
                            0x08,
                            0x09,
                            0x0A,
                            0x0B,
                            0x0C,
                            0x0D,
                            0x0E,
                            0x0F
                        ];

                        using global::PacketWire.Security.AesGcmPayloadProtector protector =
                            new(
                                key);

                        byte[] frame =
                            global::ApplicationProtocol.Serialize(
                                new global::EmptyPacket(),
                                protector);

                        if (frame.Length != 34)
                        {
                            return false;
                        }

                        if (frame[2] !=
                            (byte)global::PacketWire.PacketFrameOptions.Protected)
                        {
                            return false;
                        }

                        global::EmptyPacket restored =
                            global::ApplicationProtocol.Deserialize<
                                global::EmptyPacket>(
                                frame,
                                protector);

                        return
                            restored is not null;
                    }
                }
            }
            """;

        Assert.True(
            GeneratorTestHost.InvokeHarness<bool>(
                result,
                harnessSource,
                "PacketWire.Generator.Tests.Dynamic.PooledZeroPayloadHarness",
                "Validate"));
    }

    /// <summary>
    /// Counts occurrences of a target substring within text.
    /// </summary>
    /// <param name="text">The source text.</param>
    /// <param name="value">The substring to search for.</param>
    /// <returns>The number of occurrences found.</returns>
    private static int CountOccurrences(
        string text,
        string value)
    {
        int count = 0;
        int searchIndex = 0;

        while (true)
        {
            int index =
                text.IndexOf(
                    value,
                    searchIndex,
                    StringComparison.Ordinal);

            if (index < 0)
            {
                return count;
            }

            count++;

            searchIndex =
                index +
                value.Length;
        }
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