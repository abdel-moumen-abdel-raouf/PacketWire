# PacketWire

**High-performance, source-generated binary packet serialization, framing, and payload protection for .NET.**

PacketWire provides compile-time binary protocol codecs with zero reflection overhead, deterministic span-based wire framing, configurable integer widths, and authenticated payload protection using AES-GCM (AEAD).

```text
       +-------------------------------------------------------------+
       |                         PacketWire                          |
       |                                                             |
       |     DTO  ------------------------->  Complete Frame Bytes   |
       |             [Compile-Time Codec]                            |
       |                                                             |
       |     Complete Frame Bytes  -------->  DTO                    |
       |             [Fail-Closed Parser]                            |
       +-------------------------------------------------------------+
```

---

## Overview

PacketWire is designed for systems that communicate over binary wire protocols, such as game servers, financial messaging engines, IoT gateways, telemetry collectors, and high-frequency microservices.

Rather than relying on runtime reflection, boxing, or runtime code generation (Reflection.Emit), PacketWire utilizes a **Roslyn incremental source generator** (`PacketWire.Generator`) to synthesize specialized serialization routines, deserialization parsers, registry tables, and strongly-typed dispatch facades directly into your compilation unit.

### Strict Scope & Non-Goals

PacketWire maintains a strict, unambiguous separation of concerns:

- **PacketWire IS**: A binary serialization, deserialization, framing validation, and payload protection library.
- **PacketWire IS NOT**: A networking transport framework.

PacketWire contains **no sockets, TCP/UDP listeners, clients, connections, streams, channels, background threads, or I/O abstractions**. Networking transport belongs entirely in the consumer's application code or transport pipeline. PacketWire's sole operational contract is converting between high-level DTOs and discrete, framed byte sequences.

---

## Key Capabilities

- **Compile-Time Codecs**: Zero reflection, zero expression trees, and no runtime code emission on the serialization hot path.
- **Span-Based Execution**: Memory-efficient reading and writing built directly on `Span<T>` and `ReadOnlySpan<T>`.
- **Configurable Wire Framing**: Fully customizable field widths for packet lengths, category identifiers, and packet IDs (1, 2, 4, or 8 bytes) with little-endian or big-endian encoding.
- **Multi-Protocol & Domain Partitioning**: Multiple protocols can coexist in the same compilation unit with isolated packet namespaces and duplicate category/ID combinations.
- **Fail-Closed Validation**: Truncated payloads, buffer overflows, unexpected trailing bytes, invalid flags, out-of-range counts, and malformed paddings are immediately rejected.
- **Authenticated Payload Protection**: Native AES-GCM AEAD payload encryption with the frame header authenticated as Associated Data (AAD), binding wire metadata directly to ciphertext.
- **Memory Denial-of-Service Defense**: Enforce collection upper bounds (`[MaxCount]`) prior to allocating arrays or lists during deserialization.
- **Modular Packaging**: Decoupled abstractions, runtime engine, analyzer-only source generator, and security implementations.

---

## Package Architecture & Topology

PacketWire is organized into five modular NuGet packages:

```text
                      +-----------------------------+
                      |   PacketWire.Abstractions   |
                      +-----------------------------+
                                     ^
                                     |
                +--------------------+--------------------+
                |                                         |
+-------------------------------+         +------------------------------------+
|      PacketWire.Runtime       |         |  PacketWire.Security.Abstractions  |
+-------------------------------+         +------------------------------------+
                ^                                         ^
                |                                         |
+-------------------------------+         +------------------------------------+
|     PacketWire.Generator      |         |     PacketWire.Security.AesGcm     |
|   (Analyzer / Source-Only)    |         +------------------------------------+
+-------------------------------+
```

| Package | Role | Purpose |
|---|---|---|
| `PacketWire.Abstractions` | Compile-Time & Runtime | Protocol and packet contract attributes (`[PacketProtocol]`, `[Packet]`, `[PacketField]`, `[FixedString]`, `[Optional]`, `[MaxCount]`). |
| `PacketWire.Runtime` | Runtime Library | High-performance span-based binary readers/writers, frame encoders/decoders, collection codecs, presence markers, and runtime exceptions. |
| `PacketWire.Generator` | Roslyn Analyzer | Incremental source generator that synthesizes specialized codecs, registry dispatchers, and public protocol facades at compile time. |
| `PacketWire.Security.Abstractions` | Abstraction | `IPayloadProtector` interface and security exception models (`PayloadProtectionException`). |
| `PacketWire.Security.AesGcm` | Security Provider | Authenticated encryption and decryption (AEAD) provider using `System.Security.Cryptography.AesGcm`. |

---

## Installation & Package References

To use PacketWire in your application, add references to `PacketWire.Runtime` and `PacketWire.Generator`. `PacketWire.Generator` is an analyzer-only package and must be marked with `PrivateAssets="all"` so it does not flow transitively to downstream dependencies.

### Standard Consumer Configuration

```xml
<ItemGroup>
  <!-- Runtime binary framing, codecs, and reader/writer primitives -->
  <PackageReference Include="PacketWire.Runtime" Version="0.1.0" />

  <!-- Incremental source generator (analyzer-only, compile-time) -->
  <PackageReference
    Include="PacketWire.Generator"
    Version="0.1.0"
    PrivateAssets="all" />
</ItemGroup>
```

### With AES-GCM Payload Protection

```xml
<ItemGroup>
  <!-- AES-GCM payload encryption provider -->
  <PackageReference Include="PacketWire.Security.AesGcm" Version="0.1.0" />
</ItemGroup>
```

> [!NOTE]
> PacketWire is currently at version `0.1.0`. `PacketWire.Runtime` brings `PacketWire.Abstractions` and `PacketWire.Security.Abstractions` transitively.

---

## Quick Start

### 1. Declare a Protocol Facade (Example 1)

Declare a partial class decorated with `[PacketProtocol]`. This class serves as the API surface on which PacketWire emits static serialization and deserialization methods.

```csharp
using PacketWire;

[PacketProtocol(
    PacketIntegerSize.TwoBytes,      // Packet length width: 2 bytes (max 65,535 B)
    PacketIntegerSize.TwoBytes,      // Packet ID width: 2 bytes (max 65,535 IDs)
    PacketIntegerSize.TwoBytes,      // Collection count width: 2 bytes
    PacketByteOrder.LittleEndian)]   // Endianness for all multi-byte wire integers
public sealed partial class GameProtocol
{
}
```

### 2. Declare a Packet Contract (Example 2)

Mark your DTO with `[Packet]` and order its properties using `[PacketField]`:

```csharp
using PacketWire;

[Packet(typeof(GameProtocol), id: 100)]
public sealed class PingPacket
{
    [PacketField(0)]
    public int SequenceNumber { get; init; }

    [PacketField(1)]
    public long ClientTimestampUtc { get; init; }
}
```

### 3. Serialize and Deserialize (Examples 3, 4, & 18)

Use the generated static facade methods directly on `GameProtocol`:

```csharp
using System;

// Example 3: Plain Serialization
PingPacket outgoing = new()
{
    SequenceNumber = 42,
    ClientTimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
};

byte[] frameBytes = GameProtocol.Serialize(outgoing);

// Example 4: Strongly-Typed Deserialization
PingPacket incoming = GameProtocol.Deserialize<PingPacket>(frameBytes);

Console.WriteLine($"Restored Ping #{incoming.SequenceNumber}");

// Example 18: Complete Plain Round Trip Verification
byte[] roundTripBytes = GameProtocol.Serialize(incoming);
PingPacket verified = GameProtocol.Deserialize<PingPacket>(roundTripBytes);
```

---

## Wire Frame Format

PacketWire frames are structured to enable zero-allocation, single-pass decoding. The clear protocol header precedes the payload and contains all framing metadata required to route and process the packet:

```text
+---------------------+-------------------+------------------------+-------------------+----------------------------+
| PacketLength        | Flags             | PacketCategory         | PacketId          | Payload                    |
| (1, 2, 4, or 8 B)   | (1 Byte)          | (1, 2, 4, or 8 B)      | (1, 2, 4, or 8 B) | (Length - HeaderLength B)  |
+---------------------+-------------------+------------------------+-------------------+----------------------------+
```

### Header Fields

1. **`PacketLength`**: Total frame length in bytes (including header and payload), encoded according to the protocol's configured `PacketLengthSize` and `PacketByteOrder`.
2. **`Flags`**: 1-byte bitfield (`PacketFrameOptions`). Bit `0x01` indicates `PacketFrameOptions.Protected` (payload is encrypted with an `IPayloadProtector`). Bits not recognized by the runtime trigger an immediate `PacketBufferException`.
3. **`PacketCategory`**: Numeric category partition identifier, encoded according to `PacketCategorySize` (defaults to `1` byte). Allows partitioning packet namespaces across subsystems.
4. **`PacketId`**: Numeric packet identifier within the category, encoded according to `PacketIdSize`.
5. **`Payload`**: The contiguous bytes containing serialized fields (or AEAD ciphertext overhead in protected frames).

### Fail-Closed Validation Rules

The frame parser (`PacketFrameCodec.ReadFrame`) enforces strict validation:
- **Buffer Length**: Source buffer must contain at least `HeaderLength` bytes.
- **Length Consistency**: Source span length must **exactly match** `PacketLength`. Any truncated buffers or surplus trailing bytes trigger `PacketBufferException`.
- **Field Bounds**: `PacketCategory` and `PacketId` must not exceed the capacity of their configured integer sizes.

---

## Protocol Definition & Endianness

Protocols define the wire format rules for all associated packets:

```csharp
public PacketProtocolAttribute(
    PacketIntegerSize packetIdSize,
    PacketIntegerSize packetLengthSize,
    PacketIntegerSize collectionCountSize,
    PacketByteOrder byteOrder,
    PacketIntegerSize packetCategorySize = PacketIntegerSize.OneByte)
```

### Little-Endian Protocol (Example 9)

```csharp
[PacketProtocol(
    PacketIntegerSize.TwoBytes,
    PacketIntegerSize.TwoBytes,
    PacketIntegerSize.TwoBytes,
    PacketByteOrder.LittleEndian)]
public sealed partial class LittleEndianProtocol
{
}
```

### Big-Endian Protocol (Example 10)

```csharp
[PacketProtocol(
    PacketIntegerSize.FourBytes,
    PacketIntegerSize.FourBytes,
    PacketIntegerSize.FourBytes,
    PacketByteOrder.BigEndian,
    packetCategorySize: PacketIntegerSize.TwoBytes)]
public sealed partial class NetworkStandardProtocol
{
}
```

---

## Packet Identities & Domain Partitioning

Packet identity in PacketWire is the composite key `(Category, Id)`.

### Category & ID Identity Behavior (Example 7)

```csharp
// Category: 1 (e.g. Chat Subsystem), Packet ID: 101
[Packet(typeof(GameProtocol), id: 101, category: 1)]
public sealed class ChatMessagePacket
{
    [PacketField(0)]
    [FixedString(64)]
    public string Sender { get; init; } = string.Empty;

    [PacketField(1)]
    [FixedString(256)]
    public string Content { get; init; } = string.Empty;
}
```

### Packet Identity Lookups (Example 6)

PacketWire provides static lookup methods on generated protocol facades:

```csharp
// Generic identity lookup
PacketIdentity identityA = GameProtocol.GetIdentity<ChatMessagePacket>();
Console.WriteLine($"Category: {identityA.Category}, ID: {identityA.Id}");

// Non-throwing identity lookup
if (GameProtocol.TryGetIdentity<ChatMessagePacket>(out PacketIdentity identityB))
{
    // Found identity
}

// Reflection Type lookup (useful for dynamic routing)
PacketIdentity identityC = GameProtocol.GetIdentity(typeof(ChatMessagePacket));
```

### Multiple Protocols with Identical Category and ID (Example 8)

Because packet registries are strictly scoped to their owning protocol definition, different protocols can legally reuse the same numeric `Category` and `Id` without conflict:

```csharp
[PacketProtocol(
    PacketIntegerSize.TwoBytes,
    PacketIntegerSize.TwoBytes,
    PacketIntegerSize.TwoBytes,
    PacketByteOrder.LittleEndian)]
public sealed partial class ProtocolA { }

[PacketProtocol(
    PacketIntegerSize.FourBytes,
    PacketIntegerSize.FourBytes,
    PacketIntegerSize.FourBytes,
    PacketByteOrder.BigEndian)]
public sealed partial class ProtocolB { }

// ProtocolA: Category 1, ID 10
[Packet(typeof(ProtocolA), id: 10, category: 1)]
public sealed class AlphaTelemetryPacket
{
    [PacketField(0)]
    public int Value { get; init; }
}

// ProtocolB: Category 1, ID 10 (Different protocol, completely isolated)
[Packet(typeof(ProtocolB), id: 10, category: 1)]
public sealed class BetaTelemetryPacket
{
    [PacketField(0)]
    public int Value { get; init; }
}
```

---

## Supported Field Shapes & Contracts

All serialized fields must be ordered using `[PacketField(order)]` in strictly ascending order (`0, 1, 2, ...`). Any unmapped public property causes compilation error `PWG005` unless marked with `[PacketIgnore]`.

### Supported Types

- **Numeric Primitives**: `bool`, `byte`, `sbyte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `float`, `double`.
- **Enums**: Any enum backed by a supported integral primitive type.
- **Fixed Strings**: `string` decorated with `[FixedString(byteLength)]`.
- **Optional Fields**: Nullable types (`T?`) decorated with `[Optional]`.
- **Collections**: `T[]` and `List<T>` with optional `[MaxCount(limit)]`.
- **Nested Contracts**: Types decorated with `[PacketContract]`.

### Fixed-Width UTF-8 Strings (Example 11)

Fixed strings allocate a constant number of bytes on the wire. Strings shorter than the allocated width are zero-padded. On read, the codec verifies that all padding bytes are strictly `0x00`. Embedded null bytes (`'\0'`) are rejected.

```csharp
[Packet(typeof(GameProtocol), id: 201)]
public sealed class UserProfilePacket
{
    [PacketField(0)]
    public int UserId { get; init; }

    [PacketField(1)]
    [FixedString(16)] // Exactly 16 UTF-8 bytes on the wire
    public string Username { get; init; } = string.Empty;
}
```

### Optional Fields (Example 12)

Optional fields write a 1-byte presence flag on the wire (`0x01` for present, `0x00` for null/absent). Any other presence byte fails closed with `PacketBufferException`.

```csharp
[Packet(typeof(GameProtocol), id: 202)]
public sealed class OptionalDetailsPacket
{
    [PacketField(0)]
    public int Id { get; init; }

    [PacketField(1)]
    [Optional]
    public int? HighScore { get; init; }

    [PacketField(2)]
    [Optional]
    [FixedString(32)]
    public string? CustomTitle { get; init; }
}
```

### Arrays, Lists, and MaxCount Limits (Examples 13, 14, & 15)

Collections are length-prefixed using the protocol's configured `CollectionCountSize`. To prevent memory exhaustion attacks from malicious packets declaring massive collection counts, decorate collections with `[MaxCount]`:

```csharp
[Packet(typeof(GameProtocol), id: 203)]
public sealed class InventoryPacket
{
    // Example 13 & 15: Array with MaxCount limit
    [PacketField(0)]
    [MaxCount(64)]
    public int[] ItemIds { get; init; } = [];

    // Example 14: List<T>
    [PacketField(1)]
    public List<int> EquipmentSlots { get; init; } = [];
}
```

### Nested Packet Contracts (Example 16)

Reusable sub-objects are marked with `[PacketContract]` and serialized inline without independent headers:

```csharp
[PacketContract]
public sealed class Vector3Contract
{
    [PacketField(0)]
    public float X { get; init; }

    [PacketField(1)]
    public float Y { get; init; }

    [PacketField(2)]
    public float Z { get; init; }
}

[Packet(typeof(GameProtocol), id: 204)]
public sealed class PlayerPositionPacket
{
    [PacketField(0)]
    public int PlayerId { get; init; }

    [PacketField(1)]
    public Vector3Contract Position { get; init; } = new();
}
```

### Enum Fields (Example 17)

Enums are encoded using their underlying primitive type, respecting the protocol's configured endianness:

```csharp
public enum PlayerState : byte
{
    Disconnected = 0,
    Lobby = 1,
    InGame = 2
}

[Packet(typeof(GameProtocol), id: 205)]
public sealed class PlayerStatusPacket
{
    [PacketField(0)]
    public PlayerState State { get; init; }
}
```

---

## Generated Protocol Facade Surface

For every `[PacketProtocol]` class, the source generator emits exactly **11 public members**:

```csharp
public sealed partial class GameProtocol
{
    // Protocol metadata definition
    public static PacketProtocolDefinition Definition { get; }

    // Type identity lookups
    public static bool TryGetIdentity(Type packetType, out PacketIdentity identity);
    public static PacketIdentity GetIdentity(Type packetType);
    public static bool TryGetIdentity<TPacket>(out PacketIdentity identity);
    public static PacketIdentity GetIdentity<TPacket>();

    // Plain serialization and deserialization
    public static byte[] Serialize(object packet);
    public static object Deserialize(ReadOnlySpan<byte> packetBytes);
    public static TPacket Deserialize<TPacket>(ReadOnlySpan<byte> packetBytes);

    // Protected serialization and deserialization
    public static byte[] Serialize(object packet, IPayloadProtector protector);
    public static object Deserialize(ReadOnlySpan<byte> packetBytes, IPayloadProtector protector);
    public static TPacket Deserialize<TPacket>(ReadOnlySpan<byte> packetBytes, IPayloadProtector protector);
}
```

### Untyped Dispatch (Example 5)

Untyped deserialization reads the header, identifies the registered packet type, and returns an `object` without any manual dispatch switch statements:

```csharp
object incomingPacket = GameProtocol.Deserialize(frameBytes);

switch (incomingPacket)
{
    case PingPacket ping:
        Console.WriteLine($"Received Ping: {ping.SequenceNumber}");
        break;
    case ChatMessagePacket chat:
        Console.WriteLine($"Received Chat: {chat.Sender}: {chat.Content}");
        break;
    default:
        Console.WriteLine($"Unknown packet type: {incomingPacket.GetType().Name}");
        break;
}
```

---

## Low-Level Framing Inspection (Example 19)

You can inspect the wire frame header and raw payload slice without instantiating packet DTOs using `PacketFrameCodec.ReadFrame`:

```csharp
using PacketWire;

// Parse the header and obtain a zero-copy view of the frame
PacketFrameView frameView = PacketFrameCodec.ReadFrame(frameBytes, GameProtocol.Definition);

PacketFrameHeader header = frameView.Header;
Console.WriteLine($"Total Frame Length: {header.PacketLength} bytes");
Console.WriteLine($"Flags:              {header.Flags}");
Console.WriteLine($"Category:           {header.PacketCategory}");
Console.WriteLine($"Packet ID:          {header.PacketId}");
Console.WriteLine($"Payload Length:     {frameView.PayloadLength} bytes");

// Direct read-only span over the payload bytes
ReadOnlySpan<byte> rawPayload = frameView.Payload;
```

---

## AES-GCM Payload Protection

PacketWire includes native authenticated encryption via `PacketWire.Security.AesGcm`.

### Protected Wire Payload Layout

When protected serialization is used, the wire payload replaces the raw field bytes with an AEAD envelope adding exactly **28 bytes of cryptographic overhead**:

```text
+-----------------------+----------------------------------+-----------------------+
| Nonce (IV)            | Ciphertext                       | Authentication Tag    |
| (12 Bytes / 96 Bits)  | (Equal to Plaintext Length)      | (16 Bytes / 128 Bits) |
+-----------------------+----------------------------------+-----------------------+
```

### Header Authentication via Associated Data (AAD)

The protocol frame header (`PacketLength | Flags | PacketCategory | PacketId`) remains unencrypted so intermediate network routers and dispatchers can inspect packet identity without holding encryption keys.

However, the complete header is supplied as **Authenticated Associated Data (AAD)** to the AES-GCM cipher during both encryption and decryption. This cryptographically binds packet identity to the ciphertext:
- **Tamper Rejection**: Any modification to the clear header (such as altering the Packet ID, Category, or Length) invalidates the AEAD authentication tag, failing closed immediately.
- **Splicing Defense**: Transposing a valid ciphertext onto a different packet frame header is detected and rejected.

### Protected Serialization & Deserialization (Examples 20 & 21)

```csharp
using System.Security.Cryptography;
using PacketWire.Security;

// Demonstration key (32 bytes for AES-256; 16 and 24 bytes are also supported)
byte[] secretKey = new byte[32];
RandomNumberGenerator.Fill(secretKey);

using AesGcmPayloadProtector protector = new(secretKey);

// Example 20: Protected Serialization
PingPacket packet = new() { SequenceNumber = 12345 };
byte[] protectedFrame = GameProtocol.Serialize(packet, protector);

// Example 21: Protected Deserialization
PingPacket decrypted = GameProtocol.Deserialize<PingPacket>(protectedFrame, protector);
```

> [!CAUTION]
> Hardcoding cryptographic keys in source code is insecure. Always obtain key material from a secure key management system (e.g. Azure Key Vault, AWS KMS, or local hardware security modules).

### Wrong-Key & Tampering Rejection (Examples 22 & 23)

When authentication fails, destination buffers are immediately wiped with `Span<byte>.Clear()` to prevent leaking partial plaintext, and a `PayloadProtectionException` is thrown:

```csharp
// Example 22: Wrong-Key Rejection
byte[] wrongKey = new byte[32];
RandomNumberGenerator.Fill(wrongKey);
using AesGcmPayloadProtector wrongProtector = new(wrongKey);

try
{
    GameProtocol.Deserialize<PingPacket>(protectedFrame, wrongProtector);
}
catch (PayloadProtectionException)
{
    Console.WriteLine("Decryption rejected: invalid cryptographic key.");
}

// Example 23: Header Tampering Detection
byte[] tamperedFrame = (byte[])protectedFrame.Clone();
tamperedFrame[3] ^= 0x01; // Alter Category byte in the clear header

try
{
    GameProtocol.Deserialize<PingPacket>(tamperedFrame, protector);
}
catch (PayloadProtectionException)
{
    Console.WriteLine("Header tampering detected: frame rejected via AAD validation.");
}
```

---

## Custom Payload Protectors (Example 29)

You can provide custom payload protection strategies (e.g. ChaCha20-Poly1305, custom hardware crypto, or test passthrough wrappers) by implementing `IPayloadProtector`:

```csharp
using System;
using PacketWire.Security;

public sealed class XorMaskPayloadProtector : IPayloadProtector
{
    private readonly byte mask;

    public XorMaskPayloadProtector(byte mask) => this.mask = mask;

    public int GetProtectedPayloadLength(int plaintextLength) => plaintextLength;

    public int GetMaximumPlaintextLength(int protectedPayloadLength) => protectedPayloadLength;

    public int Protect(
        ReadOnlySpan<byte> plaintext,
        ReadOnlySpan<byte> associatedData,
        Span<byte> destination)
    {
        for (int i = 0; i < plaintext.Length; i++)
        {
            destination[i] = (byte)(plaintext[i] ^ mask);
        }
        return plaintext.Length;
    }

    public int Unprotect(
        ReadOnlySpan<byte> protectedPayload,
        ReadOnlySpan<byte> associatedData,
        Span<byte> destination)
    {
        for (int i = 0; i < protectedPayload.Length; i++)
        {
            destination[i] = (byte)(protectedPayload[i] ^ mask);
        }
        return protectedPayload.Length;
    }
}
```

---

## Error Handling & Exception Catalog (Examples 24 – 28)

PacketWire follows a strictly fail-closed error handling philosophy.

| Exception | Namespace | Typical Trigger |
|---|---|---|
| `PacketProtectionRequiredException` | `PacketWire` | A frame carrying `PacketFrameOptions.Protected` was passed to a plain `Deserialize` overload lacking an `IPayloadProtector`. |
| `PacketIdentityNotRegisteredException` | `PacketWire` | Deserializing a frame whose `(Category, Id)` does not exist in the protocol registry. |
| `PacketTypeNotRegisteredException` | `PacketWire` | Calling `Serialize` with a DTO type not registered under the protocol. |
| `PacketTypeMismatchException` | `PacketWire` | Calling generic `Deserialize<TPacket>` on a frame belonging to a different registered packet type. |
| `PacketBufferException` | `PacketWire` | Frame buffer is truncated, has unexpected trailing bytes, or contains corrupted presence markers. |
| `PayloadProtectionException` | `PacketWire.Security` | AEAD authentication failed (tampered ciphertext, altered header AAD, or wrong key). |

### Exception Code Examples

```csharp
// Example 24: PacketProtectionRequiredException
try
{
    // Calling plain deserialize on an encrypted frame
    GameProtocol.Deserialize<PingPacket>(protectedFrame);
}
catch (PacketProtectionRequiredException ex)
{
    Console.WriteLine($"Frame requires protection: {ex.Message}");
}

// Example 25: PacketIdentityNotRegisteredException
try
{
    byte[] invalidHeaderFrame = new byte[GameProtocol.Definition.HeaderLength];
    PacketFrameCodec.WriteHeader(
        invalidHeaderFrame,
        (ulong)invalidHeaderFrame.Length,
        PacketFrameOptions.None,
        packetCategory: 99,
        packetId: 9999,
        GameProtocol.Definition);

    GameProtocol.Deserialize(invalidHeaderFrame);
}
catch (PacketIdentityNotRegisteredException ex)
{
    Console.WriteLine($"Unrecognized identity: Category={ex.Identity.Category}, Id={ex.Identity.Id}");
}

// Example 26: PacketTypeNotRegisteredException
try
{
    GameProtocol.Serialize("NotAPacketString");
}
catch (PacketTypeNotRegisteredException ex)
{
    Console.WriteLine($"Unregistered type: {ex.PacketType.FullName}");
}

// Example 27: PacketTypeMismatchException
try
{
    // Frame contains PingPacket, but generic call expects UserProfilePacket
    GameProtocol.Deserialize<UserProfilePacket>(frameBytes);
}
catch (PacketTypeMismatchException ex)
{
    Console.WriteLine($"Type mismatch: Expected={ex.ExpectedType.Name}, Actual={ex.ActualType.Name}");
}

// Example 28: PacketBufferException
try
{
    // Truncated buffer (less than declared frame length)
    GameProtocol.Deserialize<PingPacket>(frameBytes.AsSpan(0, 4));
}
catch (PacketBufferException ex)
{
    Console.WriteLine($"Buffer error: {ex.Message}");
}
```

---

## Integration with External Transports (Example 30)

To maintain architectural purity, PacketWire does not manage TCP sockets, UDP channels, or network streams. Here is the canonical pattern for integrating PacketWire with an external transport:

```csharp
using System;
using System.IO;
using PacketWire;

public static class TransportPipeline
{
    // Sender Side: DTO -> PacketWire.Serialize -> byte[] -> Network Transport
    public static void SendPacket<TPacket>(Stream transportStream, TPacket packet)
        where TPacket : class
    {
        byte[] frame = GameProtocol.Serialize(packet);

        // Transport writes the framed byte packet
        transportStream.Write(frame);
    }

    // Receiver Side: Network Transport -> Complete Frame Bytes -> PacketWire.Deserialize -> DTO
    public static object ReceivePacket(ReadOnlySpan<byte> completeFrameBytes)
    {
        // Fail-closed validation and DTO restoration
        return GameProtocol.Deserialize(completeFrameBytes);
    }
}
```

---

## Source Generator Diagnostics Reference

PacketWire validates your protocol contracts at compile time, providing descriptive Roslyn diagnostics in the `PWG001` through `PWG022` range:

| Diagnostic ID | Severity | Title | Typical Cause | Remediation |
|---|---|---|---|---|
| `PWG001` | Error | Invalid packet protocol | Packet references a type not marked with `[PacketProtocol]`. | Add `[PacketProtocol(...)]` to the target protocol class. |
| `PWG002` | Error | Duplicate packet identity | Multiple packets in the same protocol share the same Category and ID. | Assign a unique Category or ID to each packet contract. |
| `PWG003` | Error | Packet ID exceeds protocol capacity | Declared packet ID exceeds the maximum value supported by `PacketIdSize`. | Increase `PacketIdSize` or choose an ID within range (e.g. max 255 for `OneByte`). |
| `PWG004` | Error | Duplicate packet field order | Multiple properties in the same contract declare the same `[PacketField(n)]` order index. | Assign unique ascending order indices to each field. |
| `PWG005` | Error | Unclassified packet property | Public instance property lacks both `[PacketField]` and `[PacketIgnore]`. | Mark property with `[PacketField(n)]` for serialization or `[PacketIgnore]` to skip. |
| `PWG006` | Error | Conflicting packet property metadata | Property combines `[PacketIgnore]` with `[PacketField]`, `[FixedString]`, etc. | Remove conflicting field attributes from ignored properties. |
| `PWG007` | Error | Invalid packet field member | Property decorated as a packet field is static, indexer, or lacks valid accessors. | Ensure property is a public instance property with getter and setter / init. |
| `PWG008` | Error | Invalid packet field order | Property specifies a negative order index in `[PacketField(-1)]`. | Use zero or positive integers for field order indices. |
| `PWG009` | Error | Unsupported packet field type | Property uses a CLR type unsupported by PacketWire codecs. | Use supported primitives, enums, `[FixedString]`, collections, or `[PacketContract]`. |
| `PWG010` | Error | Fixed string metadata required | String property lacks `[FixedString(byteLength)]`. | Add `[FixedString(n)]` specifying the exact UTF-8 byte width. |
| `PWG011` | Error | Invalid fixed string metadata | `[FixedString]` applied to non-string type or byte length <= 0. | Apply only to strings and specify a positive byte length (> 0). |
| `PWG012` | Error | Optional metadata required | Nullable property (`T?`) lacks `[Optional]`. | Add `[Optional]` to the nullable property. |
| `PWG013` | Error | Optional field must be nullable | `[Optional]` applied to a non-nullable property. | Make the property nullable (`T?`) or remove `[Optional]`. |
| `PWG014` | Error | Nested packet contract required | Complex property type is not marked with `[PacketContract]`. | Decorate the nested class or struct with `[PacketContract]`. |
| `PWG015` | Error | Maximum count requires a collection | `[MaxCount]` applied to a scalar or non-collection property. | Apply `[MaxCount]` only to array or list properties. |
| `PWG016` | Error | Invalid maximum collection count | `[MaxCount]` specifies a negative count (< 0). | Specify a non-negative maximum count limit. |
| `PWG017` | Error | Nullable collection element is unsupported | Collection property uses a nullable element type (`T?[]`). | Use non-nullable collection elements (elements do not carry per-item presence markers). |
| `PWG018` | Error | Packet category exceeds protocol capacity | Declared category exceeds the maximum value supported by `PacketCategorySize`. | Increase `PacketCategorySize` or assign a category within range. |
| `PWG019` | Error | Wire contract cannot be constructed | Contract is abstract or lacks an accessible parameterless constructor. | Provide a public or internal parameterless constructor. |
| `PWG020` | Error | Wire contract inheritance is unsupported | Wire contract derives from a custom base class. | Remove base class inheritance; contracts must derive directly from `object` or `ValueType`. |
| `PWG021` | Error | Packet protocol must be partial | Class marked with `[PacketProtocol]` lacks the `partial` modifier. | Add the `partial` keyword to the protocol class declaration. |
| `PWG022` | Error | Unsupported packet protocol declaration | Protocol class is nested, generic, or not a class. | Declare the protocol as a top-level, non-generic class. |

---

## Performance & Allocation Characteristics

PacketWire is engineered for high throughput and predictable memory behavior:
- **Direct Span Encoding**: Reading and writing integers uses direct bit-shifting or `BinaryPrimitives` without intermediary stream objects.
- **Pre-Calculated Buffers**: Serialization pre-measures the exact required payload length, allocating exactly one byte array for the complete frame.
- **Zero Reflection on Hot Path**: The source generator generates direct field assignment code, eliminating runtime reflection and boxing.

### Local Baseline Measurements

The repository includes a dedicated benchmark harness (`perf/PacketWire.Performance`). Measurements on a representative workstation (.NET 10.0 x64):

| Operation | Typical Throughput | Allocations |
|---|---|---|
| **Plain Serialization** | ~14–18M ops/sec | 1 frame `byte[]` |
| **Plain Deserialization** | ~18–25M ops/sec | 1 DTO instance |
| **AES-GCM Serialization** | ~1.5–2.5M ops/sec | 1 frame `byte[]` |
| **AES-GCM Deserialization** | ~1.8–2.8M ops/sec | 1 DTO instance |

> [!NOTE]
> These measurements represent local hardware baselines under controlled benchmark loops and are **not API SLAs**. Actual throughput and latency vary based on payload size, processor architecture, runtime tiering, and OS cryptographic acceleration.

---

## Repository Structure

```text
PacketWire/
├── src/
│   ├── PacketWire.Abstractions/           # Attributes and protocol configuration models
│   ├── PacketWire.Runtime/                # Span readers/writers, framing, and collection codecs
│   ├── PacketWire.Generator/              # Roslyn incremental source generator (analyzers)
│   ├── PacketWire.Security.Abstractions/  # IPayloadProtector interface and security exceptions
│   └── PacketWire.Security.AesGcm/        # AES-GCM AEAD payload protector
├── tests/
│   ├── PacketWire.Abstractions.Tests/     # Abstraction unit tests (32 tests)
│   ├── PacketWire.Runtime.Tests/          # Framing and reader/writer unit tests (101 tests)
│   ├── PacketWire.Generator.Tests/        # Roslyn generator test host and assertions (85 tests)
│   └── PacketWire.Security.AesGcm.Tests/  # AES-GCM crypto and tamper tests (7 tests)
├── integration/
│   ├── PacketWire.Consumer/               # Consumer library exercising generated facades
│   └── PacketWire.Consumer.Tests/         # End-to-end consumer integration tests (13 tests)
├── perf/
│   └── PacketWire.Performance/            # Micro-benchmarks and allocation profiling
├── Directory.Build.props                  # Central build properties, authors, and package metadata
├── LICENSE                                # Apache License 2.0
├── README.md                              # This technical manual
└── PacketWire.slnx                        # Solution file
```

---

## Quality Gates & Verification

PacketWire maintains strict quality gates across all builds:

- **100% Test Passing Rate**: 238 tests passing across 5 test suites in both Debug and Release configurations.
- **Zero Compiler Warnings**: Built with `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`.
- **Complete XML Documentation**: All 105 authored C# source files across the repository have 100% XML documentation coverage (`0` undocumented declarations).
- **No Suppression**: Zero `CS1591` suppressions, zero `<NoWarn>` documentation disablements, and zero `#pragma warning disable` comments.

---

## Security Considerations

1. **Authenticated Framing Binding**: Always use AES-GCM payload protection when communicating over untrusted networks. Tampering with unencrypted routing headers is caught by AES-GCM Associated Data (AAD) validation.
2. **Buffer Erasure on Failure**: `AesGcmPayloadProtector` immediately zeroes decrypted memory buffers upon cryptographic authentication failure, preventing partial plaintext leakage.
3. **Key Management**: Keys must be 16 bytes (AES-128), 24 bytes (AES-192), or 32 bytes (AES-256). Rotate keys regularly according to NIST SP 800-38D guidelines.
4. **Collection Limits**: Always specify `[MaxCount(limit)]` on untrusted collections to safeguard against memory exhaustion attacks.

---

## Troubleshooting & FAQ

### Frequently Asked Questions

**Q: Can I use TCP or WebSockets directly with PacketWire?**  
A: Yes, by passing PacketWire's serialized `byte[]` to your socket or stream, and passing received frames to `Deserialize`. PacketWire handles serialization and framing; you handle the transport.

**Q: Why do my strings require `[FixedString]`?**  
A: Fixed-width strings ensure deterministic frame layouts and eliminate length-prefix ambiguities on binary wire protocols.

**Q: Why does my protocol class produce an error saying it cannot be found?**  
A: Ensure your protocol class is declared with the `partial` modifier (`public sealed partial class MyProtocol`) and is a top-level non-generic class.

**Q: How do I handle optional value types in strict build environments?**  
A: When compiling with `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, nullable value types (e.g. `int?`) may report compiler warning `CS8629`. You can suppress `CS8629` in your consumer project or use optional reference types (e.g. `[Optional] [FixedString(32)] string?`).

---

## Contributing

Contributions to PacketWire are welcome! To get started:

1. Clone the repository: `git clone https://github.com/abdel-moumen-abdel-raouf/PacketWire.git`
2. Build the solution: `dotnet build PacketWire.slnx -c Release`
3. Run all tests: `dotnet test PacketWire.slnx -c Release`
4. Ensure all changes include corresponding unit tests and full XML documentation comments.

---

## Author & Project Origin

PacketWire was conceived and system-designed by **Abdel Moumen Abdel Raouf** (عبد المؤمن عبد الرؤوف), also known as **Ali Al-Masry** (علي المصري), a .NET Software Engineer.

- **Original concept and system design**: Abdel Moumen Abdel Raouf
- **GitHub**: [https://github.com/abdel-moumen-abdel-raouf](https://github.com/abdel-moumen-abdel-raouf)

### Previous Work

- **ES6 Audit Core**: [https://github.com/abdel-moumen-abdel-raouf/es6-audit-core](https://github.com/abdel-moumen-abdel-raouf/es6-audit-core)

---

## License

PacketWire is licensed under the **Apache License, Version 2.0**. See the [LICENSE](LICENSE) file for details.
