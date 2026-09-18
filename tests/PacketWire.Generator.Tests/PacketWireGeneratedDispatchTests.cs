using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace PacketWire.Generator.Tests;

/// <summary>
/// Verifies dynamic dispatch, registry lookup, and type-to-identity mappings generated for protocol implementations.
/// </summary>
public sealed class PacketWireGeneratedDispatchTests
{
    /// <summary>
    /// Expected raw payload bytes for login packet serialization tests.
    /// </summary>
    private static readonly byte[] ExpectedLoginPayload =
    [
        0x78,
        0x56,
        0x34,
        0x12,

        0x41,
        0x00,
        0x00,
        0x00
    ];

    /// <summary>
    /// Verifies that generated registry methods serialize and deserialize registered packets without using reflection.
    /// </summary>
    [Fact]
    public void RegistryDispatchesEncodeAndDecodeWithoutReflectionSerialization()
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
                [PacketField(0)]
                public int UserId { get; init; }

                [PacketField(1)]
                [FixedString(4)]
                public string Name { get; init; } = string.Empty;
            }

            [Packet(typeof(TestProtocol), 1, 2)]
            public sealed class SecurityRequest
            {
                [PacketField(0)]
                public ushort Code { get; init; }
            }
            """;

        GeneratorTestResult result =
            GeneratorTestHost.RunWithOutput(source);

        AssertSuccessfulCompilation(result);

        string registryClassName =
            GetSingleRegistryClassName(
                result);

        string harnessSource = $$"""
            namespace PacketWire.Generator.Tests.Dynamic
            {
                public static class DispatchHarness
                {
                    private static global::PacketWire.PacketProtocolDefinition CreateDefinition()
                    {
                        return new global::PacketWire.PacketProtocolDefinition(
                            global::PacketWire.PacketIntegerSize.TwoBytes,
                            global::PacketWire.PacketIntegerSize.TwoBytes,
                            global::PacketWire.PacketIntegerSize.TwoBytes,
                            global::PacketWire.PacketByteOrder.LittleEndian);
                    }

                    public static byte[] SerializeLogin()
                    {
                        object packet =
                            new global::LoginRequest
                            {
                                UserId = 0x12345678,
                                Name = "A"
                            };

                        global::PacketWire.PacketProtocolDefinition definition =
                            CreateDefinition();

                        bool lengthFound =
                            global::PacketWire.Generated.{{registryClassName}}.TryGetEncodedLength(
                                packet,
                                definition,
                                out global::PacketWire.PacketIdentity lengthIdentity,
                                out int encodedLength);

                        if (!lengthFound)
                        {
                            throw new global::System.InvalidOperationException(
                                "Packet length dispatch failed.");
                        }

                        if (lengthIdentity != new global::PacketWire.PacketIdentity(1, 1))
                        {
                            throw new global::System.InvalidOperationException(
                                "Unexpected packet identity.");
                        }

                        if (encodedLength != 8)
                        {
                            throw new global::System.InvalidOperationException(
                                $"Unexpected encoded length: {encodedLength}.");
                        }

                        byte[] payload =
                            new byte[encodedLength];

                        global::PacketWire.PacketWriter writer =
                            new global::PacketWire.PacketWriter(
                                payload,
                                definition.ByteOrder);

                        bool writeFound =
                            global::PacketWire.Generated.{{registryClassName}}.TryWrite(
                                ref writer,
                                packet,
                                definition,
                                out global::PacketWire.PacketIdentity writeIdentity);

                        if (!writeFound)
                        {
                            throw new global::System.InvalidOperationException(
                                "Packet write dispatch failed.");
                        }

                        if (writeIdentity != lengthIdentity)
                        {
                            throw new global::System.InvalidOperationException(
                                "Length and write identities differ.");
                        }

                        if (writer.WrittenCount != encodedLength)
                        {
                            throw new global::System.InvalidOperationException(
                                "Encoded length does not match written length.");
                        }

                        return payload;
                    }

                    public static bool RoundTripLogin()
                    {
                        byte[] payload =
                            SerializeLogin();

                        global::PacketWire.PacketProtocolDefinition definition =
                            CreateDefinition();

                        global::PacketWire.PacketReader reader =
                            new global::PacketWire.PacketReader(
                                payload,
                                definition.ByteOrder);

                        bool readFound =
                            global::PacketWire.Generated.{{registryClassName}}.TryRead(
                                new global::PacketWire.PacketIdentity(1, 1),
                                ref reader,
                                definition,
                                out object? packet);

                        if (!readFound ||
                            packet is not global::LoginRequest login)
                        {
                            return false;
                        }

                        return
                            reader.Remaining == 0 &&
                            login.UserId == 0x12345678 &&
                            login.Name == "A";
                    }
                }
            }
            """;

        byte[] payload =
            GeneratorTestHost.InvokeHarness<byte[]>(
                result,
                harnessSource,
                "PacketWire.Generator.Tests.Dynamic.DispatchHarness",
                "SerializeLogin");

        Assert.Equal(
            ExpectedLoginPayload,
            payload);

        Assert.True(
            GeneratorTestHost.InvokeHarness<bool>(
                result,
                harnessSource,
                "PacketWire.Generator.Tests.Dynamic.DispatchHarness",
                "RoundTripLogin"));
    }

    /// <summary>
    /// Verifies that packets sharing the same numeric ID in different categories dispatch to distinct codecs.
    /// </summary>
    [Fact]
    public void SameIdInDifferentCategoriesDispatchesToDifferentCodecs()
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
            public sealed class AuthenticationPacket
            {
                [PacketField(0)]
                public int Value { get; init; }
            }

            [Packet(typeof(TestProtocol), 1, 2)]
            public sealed class SecurityPacket
            {
                [PacketField(0)]
                public ushort Value { get; init; }
            }
            """;

        GeneratorTestResult result =
            GeneratorTestHost.RunWithOutput(source);

        AssertSuccessfulCompilation(result);

        string registryClassName =
            GetSingleRegistryClassName(
                result);

        string harnessSource = $$"""
            namespace PacketWire.Generator.Tests.Dynamic
            {
                public static class CategoryDispatchHarness
                {
                    public static bool Validate()
                    {
                        global::PacketWire.PacketProtocolDefinition definition =
                            new global::PacketWire.PacketProtocolDefinition(
                                global::PacketWire.PacketIntegerSize.TwoBytes,
                                global::PacketWire.PacketIntegerSize.TwoBytes,
                                global::PacketWire.PacketIntegerSize.TwoBytes,
                                global::PacketWire.PacketByteOrder.LittleEndian);

                        object authentication =
                            new global::AuthenticationPacket
                            {
                                Value = 123
                            };

                        object security =
                            new global::SecurityPacket
                            {
                                Value = 456
                            };

                        if (!global::PacketWire.Generated.{{registryClassName}}.TryGetEncodedLength(
                                authentication,
                                definition,
                                out global::PacketWire.PacketIdentity authenticationIdentity,
                                out int authenticationLength))
                        {
                            return false;
                        }

                        if (!global::PacketWire.Generated.{{registryClassName}}.TryGetEncodedLength(
                                security,
                                definition,
                                out global::PacketWire.PacketIdentity securityIdentity,
                                out int securityLength))
                        {
                            return false;
                        }

                        return
                            authenticationIdentity == new global::PacketWire.PacketIdentity(1, 1) &&
                            securityIdentity == new global::PacketWire.PacketIdentity(2, 1) &&
                            authenticationLength == 4 &&
                            securityLength == 2;
                    }
                }
            }
            """;

        Assert.True(
            GeneratorTestHost.InvokeHarness<bool>(
                result,
                harnessSource,
                "PacketWire.Generator.Tests.Dynamic.CategoryDispatchHarness",
                "Validate"));
    }

    /// <summary>
    /// Verifies that attempting to write an unregistered packet type fails gracefully without writing data or producing an identity.
    /// </summary>
    [Fact]
    public void UnknownPacketTypeDoesNotWriteOrProduceIdentity()
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
                [PacketField(0)]
                public int Value { get; init; }
            }
            """;

        GeneratorTestResult result =
            GeneratorTestHost.RunWithOutput(source);

        AssertSuccessfulCompilation(result);

        string registryClassName =
            GetSingleRegistryClassName(
                result);

        string harnessSource = $$"""
            namespace PacketWire.Generator.Tests.Dynamic
            {
                public static class UnknownWriteHarness
                {
                    public static bool Validate()
                    {
                        global::PacketWire.PacketProtocolDefinition definition =
                            new global::PacketWire.PacketProtocolDefinition(
                                global::PacketWire.PacketIntegerSize.TwoBytes,
                                global::PacketWire.PacketIntegerSize.TwoBytes,
                                global::PacketWire.PacketIntegerSize.TwoBytes,
                                global::PacketWire.PacketByteOrder.LittleEndian);

                        object unknown =
                            "not-a-packet";

                        bool lengthFound =
                            global::PacketWire.Generated.{{registryClassName}}.TryGetEncodedLength(
                                unknown,
                                definition,
                                out global::PacketWire.PacketIdentity lengthIdentity,
                                out int encodedLength);

                        byte[] buffer =
                            new byte[16];

                        global::PacketWire.PacketWriter writer =
                            new global::PacketWire.PacketWriter(
                                buffer,
                                definition.ByteOrder);

                        bool writeFound =
                            global::PacketWire.Generated.{{registryClassName}}.TryWrite(
                                ref writer,
                                unknown,
                                definition,
                                out global::PacketWire.PacketIdentity writeIdentity);

                        return
                            !lengthFound &&
                            !writeFound &&
                            lengthIdentity == default &&
                            writeIdentity == default &&
                            encodedLength == 0 &&
                            writer.WrittenCount == 0;
                    }
                }
            }
            """;

        Assert.True(
            GeneratorTestHost.InvokeHarness<bool>(
                result,
                harnessSource,
                "PacketWire.Generator.Tests.Dynamic.UnknownWriteHarness",
                "Validate"));
    }

    /// <summary>
    /// Verifies that attempting to read an unrecognized identity does not consume payload bytes.
    /// </summary>
    [Fact]
    public void UnknownIdentityDoesNotConsumePayload()
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
                [PacketField(0)]
                public int Value { get; init; }
            }
            """;

        GeneratorTestResult result =
            GeneratorTestHost.RunWithOutput(source);

        AssertSuccessfulCompilation(result);

        string registryClassName =
            GetSingleRegistryClassName(
                result);

        string harnessSource = $$"""
            namespace PacketWire.Generator.Tests.Dynamic
            {
                public static class UnknownReadHarness
                {
                    public static bool Validate()
                    {
                        byte[] payload =
                        [
                            0x01,
                            0x02,
                            0x03,
                            0x04
                        ];

                        global::PacketWire.PacketProtocolDefinition definition =
                            new global::PacketWire.PacketProtocolDefinition(
                                global::PacketWire.PacketIntegerSize.TwoBytes,
                                global::PacketWire.PacketIntegerSize.TwoBytes,
                                global::PacketWire.PacketIntegerSize.TwoBytes,
                                global::PacketWire.PacketByteOrder.LittleEndian);

                        global::PacketWire.PacketReader reader =
                            new global::PacketWire.PacketReader(
                                payload,
                                definition.ByteOrder);

                        bool found =
                            global::PacketWire.Generated.{{registryClassName}}.TryRead(
                                new global::PacketWire.PacketIdentity(99, 99),
                                ref reader,
                                definition,
                                out object? packet);

                        return
                            !found &&
                            packet is null &&
                            reader.ConsumedCount == 0 &&
                            reader.Remaining == payload.Length;
                    }
                }
            }
            """;

        Assert.True(
            GeneratorTestHost.InvokeHarness<bool>(
                result,
                harnessSource,
                "PacketWire.Generator.Tests.Dynamic.UnknownReadHarness",
                "Validate"));
    }

    /// <summary>
    /// Extracts the internal generated registry class name from the generator output.
    /// </summary>
    /// <param name="result">The generator test result.</param>
    /// <returns>The generated registry class name.</returns>
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

