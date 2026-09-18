using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace PacketWire.Generator.Tests;

/// <summary>
/// Verifies that emitted protocol facade classes serialize, deserialize, expose definitions, resolve identities, and validate protocol modifiers.
/// </summary>
public sealed class PacketWireGeneratedProtocolFacadeTests
{
    /// <summary>
    /// Expected serialized frame bytes for ping packet tests.
    /// </summary>
    private static readonly byte[] ExpectedPacket =
    [
        0x0E,
        0x00,

        0x00,

        0x03,

        0x34,
        0x12,

        0x44,
        0x33,
        0x22,
        0x11,

        0x41,
        0x00,
        0x00,
        0x00
    ];

    /// <summary>
    /// Verifies full end-to-end serialization and typed/untyped deserialization via public facade methods.
    /// </summary>
    [Fact]
    public void PublicFacadeSerializesCompletePacketAndDeserializesDto()
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
                [PacketField(0)]
                public int UserId { get; init; }

                [PacketField(1)]
                [FixedString(4)]
                public string Name { get; init; } = string.Empty;
            }
            """;

        GeneratorTestResult result =
            GeneratorTestHost.RunWithOutput(source);

        AssertSuccessfulCompilation(result);

        const string harnessSource = """
            namespace PacketWire.Generator.Tests.Dynamic
            {
                public static class FacadeHarness
                {
                    public static byte[] Serialize()
                    {
                        return global::ApplicationProtocol.Serialize(
                            new global::LoginRequest
                            {
                                UserId = 0x11223344,
                                Name = "A"
                            });
                    }

                    public static bool DeserializeUntyped()
                    {
                        byte[] packet =
                            Serialize();

                        object value =
                            global::ApplicationProtocol.Deserialize(
                                packet);

                        return
                            value is global::LoginRequest login &&
                            login.UserId == 0x11223344 &&
                            login.Name == "A";
                    }

                    public static bool DeserializeTyped()
                    {
                        byte[] packet =
                            Serialize();

                        global::LoginRequest login =
                            global::ApplicationProtocol.Deserialize<global::LoginRequest>(
                                packet);

                        return
                            login.UserId == 0x11223344 &&
                            login.Name == "A";
                    }

                    public static bool ValidateDefinition()
                    {
                        global::PacketWire.PacketProtocolDefinition definition =
                            global::ApplicationProtocol.Definition;

                        return
                            definition.HeaderLength == 6 &&
                            definition.PacketLengthSize ==
                                global::PacketWire.PacketIntegerSize.TwoBytes &&
                            definition.PacketCategorySize ==
                                global::PacketWire.PacketIntegerSize.OneByte &&
                            definition.PacketIdSize ==
                                global::PacketWire.PacketIntegerSize.TwoBytes &&
                            definition.CollectionCountSize ==
                                global::PacketWire.PacketIntegerSize.TwoBytes &&
                            definition.ByteOrder ==
                                global::PacketWire.PacketByteOrder.LittleEndian;
                    }

                    public static bool ValidateIdentity()
                    {
                        global::PacketWire.PacketIdentity identity =
                            global::ApplicationProtocol.GetIdentity(
                                typeof(global::LoginRequest));

                        return
                            identity.Category == 3 &&
                            identity.Id == 0x1234;
                    }
                }
            }
            """;

        byte[] packet =
            GeneratorTestHost.InvokeHarness<byte[]>(
                result,
                harnessSource,
                "PacketWire.Generator.Tests.Dynamic.FacadeHarness",
                "Serialize");

        Assert.Equal(
            ExpectedPacket,
            packet);

        Assert.True(
            GeneratorTestHost.InvokeHarness<bool>(
                result,
                harnessSource,
                "PacketWire.Generator.Tests.Dynamic.FacadeHarness",
                "DeserializeUntyped"));

        Assert.True(
            GeneratorTestHost.InvokeHarness<bool>(
                result,
                harnessSource,
                "PacketWire.Generator.Tests.Dynamic.FacadeHarness",
                "DeserializeTyped"));

        Assert.True(
            GeneratorTestHost.InvokeHarness<bool>(
                result,
                harnessSource,
                "PacketWire.Generator.Tests.Dynamic.FacadeHarness",
                "ValidateDefinition"));

        Assert.True(
            GeneratorTestHost.InvokeHarness<bool>(
                result,
                harnessSource,
                "PacketWire.Generator.Tests.Dynamic.FacadeHarness",
                "ValidateIdentity"));
    }

    /// <summary>
    /// Verifies that serializing an unregistered type throws <see cref="PacketWire.PacketTypeNotRegisteredException"/>.
    /// </summary>
    [Fact]
    public void SerializeUnknownTypeThrowsPacketTypeNotRegisteredException()
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

            [Packet(typeof(ApplicationProtocol), 1)]
            public sealed class KnownPacket
            {
            }
            """;

        GeneratorTestResult result =
            GeneratorTestHost.RunWithOutput(source);

        AssertSuccessfulCompilation(result);

        const string harnessSource = """
            namespace PacketWire.Generator.Tests.Dynamic
            {
                public static class UnknownTypeHarness
                {
                    public static bool Validate()
                    {
                        try
                        {
                            _ =
                                global::ApplicationProtocol.Serialize(
                                    "not-a-packet");

                            return false;
                        }
                        catch (global::PacketWire.PacketTypeNotRegisteredException)
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
                "PacketWire.Generator.Tests.Dynamic.UnknownTypeHarness",
                "Validate"));
    }

    /// <summary>
    /// Verifies that deserializing an unknown identity throws <see cref="PacketWire.PacketIdentityNotRegisteredException"/>.
    /// </summary>
    [Fact]
    public void DeserializeUnknownIdentityThrowsPacketIdentityNotRegisteredException()
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

        AssertSuccessfulCompilation(result);

        const string harnessSource = """
            namespace PacketWire.Generator.Tests.Dynamic
            {
                public static class UnknownIdentityHarness
                {
                    public static bool Validate()
                    {
                        byte[] packet =
                        [
                            0x06,
                            0x00,

                            0x00,

                            0x09,

                            0x09,
                            0x00
                        ];

                        try
                        {
                            _ =
                                global::ApplicationProtocol.Deserialize(
                                    packet);

                            return false;
                        }
                        catch (global::PacketWire.PacketIdentityNotRegisteredException exception)
                        {
                            return
                                exception.Identity.Category == 9 &&
                                exception.Identity.Id == 9;
                        }
                    }
                }
            }
            """;

        Assert.True(
            GeneratorTestHost.InvokeHarness<bool>(
                result,
                harnessSource,
                "PacketWire.Generator.Tests.Dynamic.UnknownIdentityHarness",
                "Validate"));
    }

    /// <summary>
    /// Verifies that generic deserialization expecting type A throws <see cref="PacketWire.PacketTypeMismatchException"/> when receiving type B.
    /// </summary>
    [Fact]
    public void TypedDeserializeRejectsDifferentPacketType()
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

            [Packet(typeof(ApplicationProtocol), 1)]
            public sealed class FirstPacket
            {
            }

            [Packet(typeof(ApplicationProtocol), 2)]
            public sealed class SecondPacket
            {
            }
            """;

        GeneratorTestResult result =
            GeneratorTestHost.RunWithOutput(source);

        AssertSuccessfulCompilation(result);

        const string harnessSource = """
            namespace PacketWire.Generator.Tests.Dynamic
            {
                public static class TypeMismatchHarness
                {
                    public static bool Validate()
                    {
                        byte[] packet =
                            global::ApplicationProtocol.Serialize(
                                new global::FirstPacket());

                        try
                        {
                            _ =
                                global::ApplicationProtocol.Deserialize<global::SecondPacket>(
                                    packet);

                            return false;
                        }
                        catch (global::PacketWire.PacketTypeMismatchException exception)
                        {
                            return
                                exception.ExpectedType == typeof(global::SecondPacket) &&
                                exception.ActualType == typeof(global::FirstPacket);
                        }
                    }
                }
            }
            """;

        Assert.True(
            GeneratorTestHost.InvokeHarness<bool>(
                result,
                harnessSource,
                "PacketWire.Generator.Tests.Dynamic.TypeMismatchHarness",
                "Validate"));
    }

    /// <summary>
    /// Verifies that declaring a non-partial protocol class produces diagnostic PWG021.
    /// </summary>
    [Fact]
    public void NonPartialProtocolProducesPWG021()
    {
        const string source = """
            using PacketWire;

            [PacketProtocol(
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketIntegerSize.TwoBytes,
                PacketByteOrder.LittleEndian)]
            public sealed class ApplicationProtocol
            {
            }

            [Packet(typeof(ApplicationProtocol), 1)]
            public sealed class TestPacket
            {
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            GeneratorTestHost.Run(source);

        Assert.Contains(
            diagnostics,
            static diagnostic =>
                diagnostic.Id == "PWG021");
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
