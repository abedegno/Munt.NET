using Munt.NET;
using Xunit;

namespace Munt.NET.Tests;

public class XmiSequencerTests
{
    [Fact]
    public void ReadVarLen_SingleByte_ReturnsValue()
    {
        var data = new byte[] { 0x40 };
        int pos = 0;
        int result = XmiSequencer.ReadVarLen(data, ref pos);
        Assert.Equal(64, result);
        Assert.Equal(1, pos);
    }

    [Fact]
    public void ReadVarLen_TwoBytes_ReturnsValue()
    {
        var data = new byte[] { 0x81, 0x00 };
        int pos = 0;
        int result = XmiSequencer.ReadVarLen(data, ref pos);
        Assert.Equal(128, result);
        Assert.Equal(2, pos);
    }

    [Fact]
    public void ReadVarLen_ThreeBytes_ReturnsValue()
    {
        var data = new byte[] { 0x81, 0x80, 0x00 };
        int pos = 0;
        int result = XmiSequencer.ReadVarLen(data, ref pos);
        Assert.Equal(16384, result);
        Assert.Equal(3, pos);
    }

    [Fact]
    public void ReadBE32_ReturnsCorrectValue()
    {
        var data = new byte[] { 0x00, 0x00, 0x01, 0x00 };
        int result = XmiSequencer.ReadBE32(data, 0);
        Assert.Equal(256, result);
    }

    [Fact]
    public void ReadBE32_LargeValue()
    {
        var data = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        int result = XmiSequencer.ReadBE32(data, 0);
        Assert.Equal(0x01020304, result);
    }

    [Theory]
    [InlineData(0x80, 2)]
    [InlineData(0x90, 2)]
    [InlineData(0xA0, 2)]
    [InlineData(0xB0, 2)]
    [InlineData(0xC0, 1)]
    [InlineData(0xD0, 1)]
    [InlineData(0xE0, 2)]
    [InlineData(0xF0, 0)]
    public void MidiMsgDataLength_ReturnsCorrectLength(byte status, int expected)
    {
        int result = XmiSequencer.MidiMsgDataLength(status);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void MidiMsgDataLength_IgnoresChannel()
    {
        Assert.Equal(2, XmiSequencer.MidiMsgDataLength(0x95));
        Assert.Equal(1, XmiSequencer.MidiMsgDataLength(0xCF));
    }

    [Fact]
    public void FindEvntChunk_ValidXmi_ReturnsOffset()
    {
        var data = BuildMinimalXmi(new byte[] { 0xFF, 0x2F, 0x00 });
        int offset = XmiSequencer.FindEvntChunk(data);
        Assert.True(offset >= 0, "EVNT chunk should be found");
        string id = System.Text.Encoding.ASCII.GetString(data, offset, 4);
        Assert.Equal("EVNT", id);
    }

    [Fact]
    public void FindEvntChunk_NoEvntChunk_ReturnsNegative()
    {
        var data = new byte[]
        {
            0x46, 0x4F, 0x52, 0x4D,
            0x00, 0x00, 0x00, 0x04,
            0x58, 0x44, 0x49, 0x52,
        };
        int offset = XmiSequencer.FindEvntChunk(data);
        Assert.Equal(-1, offset);
    }

    [Fact]
    public void ParseXmi_NoteOnOff_CreatesImplicitNoteOff()
    {
        byte[] evntPayload = new byte[]
        {
            0x90, 60, 100,
            0x78,
        };
        var data = BuildMinimalXmi(evntPayload);
        var events = XmiSequencer.ParseXmi(data);

        Assert.Equal(2, events.Count);
        Assert.Equal(0u, events[0].Tick);
        Assert.Equal(0x90, events[0].Data[0]);
        Assert.Equal(60, events[0].Data[1]);
        Assert.Equal(100, events[0].Data[2]);
        Assert.Equal(120u, events[1].Tick);
        Assert.Equal(0x80, events[1].Data[0]);
        Assert.Equal(60, events[1].Data[1]);
    }

    [Fact]
    public void ParseXmi_DeltaTime_AccumulatesCorrectly()
    {
        byte[] evntPayload = new byte[]
        {
            0x90, 60, 100,
            0x01,
            30, 30,
            0x90, 64, 100,
            0x01,
        };
        var data = BuildMinimalXmi(evntPayload);
        var events = XmiSequencer.ParseXmi(data);

        Assert.Equal(4, events.Count);
        Assert.Equal(0u, events[0].Tick);
        Assert.Equal(1u, events[1].Tick);
        Assert.Equal(60u, events[2].Tick);
        Assert.Equal(61u, events[3].Tick);
    }

    [Fact]
    public void ParseXmi_SysEx_ParsedCorrectly()
    {
        byte[] evntPayload = new byte[]
        {
            0xF0, 0x05,
            0x41, 0x10, 0x16, 0x12, 0xF7,
        };
        var data = BuildMinimalXmi(evntPayload);
        var events = XmiSequencer.ParseXmi(data);

        Assert.Single(events);
        Assert.Equal(0xF0, events[0].Data[0]);
        Assert.Equal(6, events[0].Data.Length);
    }

    [Fact]
    public void ParseXmi_ProgramChange_SingleDataByte()
    {
        byte[] evntPayload = new byte[]
        {
            0xC0, 0x05,
        };
        var data = BuildMinimalXmi(evntPayload);
        var events = XmiSequencer.ParseXmi(data);

        Assert.Single(events);
        Assert.Equal(2, events[0].Data.Length);
        Assert.Equal(0xC0, events[0].Data[0]);
        Assert.Equal(5, events[0].Data[1]);
    }

    [Fact]
    public void ParseXmi_NoEvntChunk_Throws()
    {
        var data = new byte[]
        {
            0x46, 0x4F, 0x52, 0x4D,
            0x00, 0x00, 0x00, 0x04,
            0x58, 0x44, 0x49, 0x52,
        };
        Assert.Throws<InvalidOperationException>(() => XmiSequencer.ParseXmi(data));
    }

    static byte[] BuildMinimalXmi(byte[] evntPayload)
    {
        int evntSize = evntPayload.Length;
        int formSize = 4 + 8 + evntSize;

        var ms = new System.IO.MemoryStream();
        var bw = new System.IO.BinaryWriter(ms);

        bw.Write(System.Text.Encoding.ASCII.GetBytes("FORM"));
        bw.Write(ToBE32(formSize));
        bw.Write(System.Text.Encoding.ASCII.GetBytes("XMID"));
        bw.Write(System.Text.Encoding.ASCII.GetBytes("EVNT"));
        bw.Write(ToBE32(evntSize));
        bw.Write(evntPayload);

        return ms.ToArray();
    }

    static byte[] ToBE32(int value)
    {
        return new byte[]
        {
            (byte)((value >> 24) & 0xFF),
            (byte)((value >> 16) & 0xFF),
            (byte)((value >> 8) & 0xFF),
            (byte)(value & 0xFF),
        };
    }
}
