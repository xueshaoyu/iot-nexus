using IoTPlatform.Protocols.Drivers;

namespace IoTPlatform.Tests;

public class BitFieldTests
{
    [Theory]
    [InlineData("4x10", false, 0, 0, "4x10")]
    [InlineData("4x10.3", true, 3, 1, "4x10")]
    [InlineData("4x10:3", true, 3, 1, "4x10")]
    [InlineData("4x10.0:4", true, 0, 4, "4x10")]
    [InlineData("4x10.0-3", true, 0, 4, "4x10")]
    [InlineData("4x10.5-5", true, 5, 1, "4x10")]
    [InlineData("0x8.15", true, 15, 1, "0x8")]
    [InlineData("4x10.8-3", false, 0, 0, "4x10.8-3")]
    [InlineData("4x10.60:8", false, 0, 0, "4x10.60:8")]
    [InlineData("4x10.3:0", false, 0, 0, "4x10.3:0")]
    [InlineData("4x10.3-4:1", false, 0, 0, "4x10.3-4:1")]
    public void Parse_AddressSuffix(String address, Boolean ok, Int32 start, Int32 width, String register)
    {
        var parsed = BitField.TryParse(address, out var field);

        Assert.Equal(ok, parsed);
        Assert.Equal(register, BitField.RegisterAddress(address));
        if (!ok) return;
        Assert.Equal(start, field.Start);
        Assert.Equal(width, field.Width);
    }

    [Fact]
    public void Extract_SingleBit_AndRange()
    {
        Assert.Equal(1L, BitField.Extract((UInt16)0b1010, new BitField(1, 1)));
        Assert.Equal(0L, BitField.Extract((UInt16)0b1010, new BitField(0, 1)));
        Assert.Equal(0xFL, BitField.Extract((UInt16)0x00F0, new BitField(4, 4)));
        Assert.Equal(0xFL, BitField.Extract((Int16)0x00F0, new BitField(4, 4)));
        Assert.Equal(1L, BitField.Extract((Int16)(-32768), new BitField(15, 1)));
    }

    [Fact]
    public void Insert_KeepsNeighborBits()
    {
        var register = BitField.Insert(0x00F0, new BitField(0, 1), 1);
        Assert.Equal(0x00F1UL, register);

        register = BitField.Insert(0x00FF, new BitField(4, 4), 0);
        Assert.Equal(0x000FUL, register);

        register = BitField.Insert(0x0005, new BitField(1, 1), 1);
        Assert.Equal(0x0007UL, register);

        Assert.Equal((UInt16)0x0007, BitField.Box((UInt16)0x0005, register));
        Assert.Equal(new Byte[] { 0x00, 0x03 }, BitField.ToDriverBytes((UInt16)1, 3));
    }

    [Fact]
    public void FieldValue_AcceptsBoolAndRounds()
    {
        Assert.True(BitField.TryFieldValue(true, out var on));
        Assert.Equal(1UL, on);
        Assert.True(BitField.TryFieldValue(2.4, out var rounded));
        Assert.Equal(2UL, rounded);
        Assert.False(BitField.TryFieldValue("3", out _));
    }
}
