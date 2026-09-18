using PacketWire;

namespace PacketWire.Runtime.Tests;

/// <summary>
/// Verifies round-trip serialization and deserialization of all supported primitive data types across different byte orderings.
/// </summary>
public sealed class PacketPrimitiveRoundTripTests
{
    /// <summary>
    /// Verifies that all supported primitive types round-trip accurately in little-endian byte order.
    /// </summary>
    [Fact]
    public void PrimitiveValuesRoundTripInLittleEndianMode()
    {
        byte[] buffer = new byte[64];

        PacketWriter writer = new(
            buffer,
            PacketByteOrder.LittleEndian);

        writer.WriteByte(0xFE);
        writer.WriteSByte(-12);
        writer.WriteInt16(-12345);
        writer.WriteUInt16(54321);
        writer.WriteInt32(-123456789);
        writer.WriteUInt32(3_000_000_000);
        writer.WriteInt64(-1_234_567_890_123_456_789);
        writer.WriteUInt64(12_345_678_901_234_567_890);
        writer.WriteSingle(123.5F);
        writer.WriteDouble(-9876.125);
        writer.WriteBoolean(true);

        PacketReader reader = new(
            writer.WrittenSpan,
            PacketByteOrder.LittleEndian);

        Assert.Equal((byte)0xFE, reader.ReadByte());
        Assert.Equal((sbyte)-12, reader.ReadSByte());
        Assert.Equal((short)-12345, reader.ReadInt16());
        Assert.Equal((ushort)54321, reader.ReadUInt16());
        Assert.Equal(-123456789, reader.ReadInt32());
        Assert.Equal(3_000_000_000U, reader.ReadUInt32());
        Assert.Equal(
            -1_234_567_890_123_456_789L,
            reader.ReadInt64());

        Assert.Equal(
            12_345_678_901_234_567_890UL,
            reader.ReadUInt64());

        Assert.Equal(123.5F, reader.ReadSingle());
        Assert.Equal(-9876.125, reader.ReadDouble());
        Assert.True(reader.ReadBoolean());

        Assert.Equal(0, reader.Remaining);
    }

    /// <summary>
    /// Verifies that primitive types round-trip accurately in big-endian byte order.
    /// </summary>
    [Fact]
    public void PrimitiveValuesRoundTripInBigEndianMode()
    {
        byte[] buffer = new byte[64];

        PacketWriter writer = new(
            buffer,
            PacketByteOrder.BigEndian);

        writer.WriteInt16(-12345);
        writer.WriteUInt16(54321);
        writer.WriteInt32(-123456789);
        writer.WriteUInt32(3_000_000_000);
        writer.WriteInt64(-1_234_567_890_123_456_789);
        writer.WriteUInt64(12_345_678_901_234_567_890);
        writer.WriteSingle(123.5F);
        writer.WriteDouble(-9876.125);

        PacketReader reader = new(
            writer.WrittenSpan,
            PacketByteOrder.BigEndian);

        Assert.Equal((short)-12345, reader.ReadInt16());
        Assert.Equal((ushort)54321, reader.ReadUInt16());
        Assert.Equal(-123456789, reader.ReadInt32());
        Assert.Equal(3_000_000_000U, reader.ReadUInt32());

        Assert.Equal(
            -1_234_567_890_123_456_789L,
            reader.ReadInt64());

        Assert.Equal(
            12_345_678_901_234_567_890UL,
            reader.ReadUInt64());

        Assert.Equal(123.5F, reader.ReadSingle());
        Assert.Equal(-9876.125, reader.ReadDouble());

        Assert.Equal(0, reader.Remaining);
    }
}
