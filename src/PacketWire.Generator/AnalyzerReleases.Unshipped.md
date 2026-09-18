; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
PWG001 | PacketWire.Generator | Error | Invalid packet protocol reference
PWG002 | PacketWire.Generator | Error | Duplicate packet ID
PWG003 | PacketWire.Generator | Error | Packet ID exceeds protocol capacity
PWG004 | PacketWire.Generator | Error | Duplicate packet field order
PWG005 | PacketWire.Generator | Error | Unclassified public instance property
PWG006 | PacketWire.Generator | Error | Conflicting packet property metadata
PWG007 | PacketWire.Generator | Error | Invalid packet field member
PWG008 | PacketWire.Generator | Error | Invalid packet field order
PWG009 | PacketWire.Generator | Error | Unsupported packet field type
PWG010 | PacketWire.Generator | Error | Missing fixed string metadata
PWG011 | PacketWire.Generator | Error | Invalid fixed string metadata
PWG012 | PacketWire.Generator | Error | Nullable field missing optional metadata
PWG013 | PacketWire.Generator | Error | Optional metadata on non-nullable field
PWG014 | PacketWire.Generator | Error | Nested type missing packet contract metadata
PWG015 | PacketWire.Generator | Error | Maximum count applied to non-collection
PWG016 | PacketWire.Generator | Error | Invalid maximum collection count
PWG017 | PacketWire.Generator | Error | Nullable collection element unsupported
PWG018 | PacketWire.Generator | Error | Packet category exceeds protocol capacity
PWG019 | PacketWire.Generator | Error | Wire contract cannot be constructed
PWG020 | PacketWire.Generator | Error | Wire contract inheritance unsupported
PWG021 | PacketWire.Generator | Error | Packet protocol must be partial
PWG022 | PacketWire.Generator | Error | Unsupported packet protocol declaration

