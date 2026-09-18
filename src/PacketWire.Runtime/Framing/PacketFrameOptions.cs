namespace PacketWire;

/// <summary>
/// Bitwise flags representing header options and security controls for PacketWire packet frames.
/// </summary>
/// <remarks>
/// Frame options are encoded as a 1-byte field immediately following the packet length field in the wire frame header.
/// Any unassigned or unsupported flag bits encountered on the wire cause the codec to fail closed with a <see cref="PacketBufferException"/>.
/// </remarks>
[System.Flags]
public enum PacketFrameOptions : byte
{
    /// <summary>
    /// No frame options are set. Indicates a standard plaintext (unprotected) packet frame.
    /// </summary>
    None = 0,

    /// <summary>
    /// Indicates that the packet payload is cryptographically protected (encrypted and authenticated) using an <see cref="Security.IPayloadProtector"/>.
    /// </summary>
    Protected = 1 << 0
}