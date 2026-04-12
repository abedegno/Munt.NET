using System;
using System.IO;
using System.Linq;
using Munt.NET;
using Xunit;

namespace Munt.NET.Tests;

public class XmiPlayerTests
{
    [Fact]
    public void Constructor_Uninitialised_NotPlaying()
    {
        using var synth = new MockSynthEngine();
        using var player = new XmiPlayer(synth, sampleRate: 44100);
        Assert.False(player.IsPlaying);
    }

    [Fact]
    public void Load_ValidXmi_StartsPlaying()
    {
        using var synth = new MockSynthEngine();
        using var player = new XmiPlayer(synth, 44100);

        byte[] xmi = BuildMinimalXmi(new byte[] { 0xC0, 5 });
        player.Load(xmi);

        Assert.True(player.IsPlaying);
    }

    [Fact]
    public void Render_FiresEventAtCorrectTime()
    {
        using var synth = new MockSynthEngine();
        using var player = new XmiPlayer(synth, 44100);

        byte[] xmi = BuildMinimalXmi(new byte[]
        {
            0xC0, 5,
            120,
            0x90, 60, 100,
            0x01,
        });
        player.Load(xmi);

        var buf = new short[22050 * 2];
        player.Render(buf, 22050);

        var msgs = synth.Events.OfType<MockSynthEngine.Event.Msg>().ToList();
        Assert.Contains(msgs, m => (m.Value & 0xFF) == 0xC0);
        Assert.DoesNotContain(msgs, m => (m.Value & 0xFF) == 0x90);
    }

    [Fact]
    public void Render_FiresAllEventsAfterFullDuration()
    {
        using var synth = new MockSynthEngine();
        using var player = new XmiPlayer(synth, 44100);

        byte[] xmi = BuildMinimalXmi(new byte[]
        {
            0xC0, 5,
            120,
            0x90, 60, 100,
            0x01,
        });
        player.Load(xmi);

        var buf = new short[44100 * 2 * 2];
        player.Render(buf, 44100 * 2);

        var msgs = synth.Events.OfType<MockSynthEngine.Event.Msg>().ToList();
        Assert.Contains(msgs, m => (m.Value & 0xFF) == 0xC0);
        Assert.Contains(msgs, m => (m.Value & 0xFF) == 0x90);
    }

    [Fact]
    public void Loop_True_RestartsEventsAfterEnd()
    {
        using var synth = new MockSynthEngine();
        using var player = new XmiPlayer(synth, 44100);

        byte[] xmi = BuildMinimalXmi(new byte[] { 0xC0, 5 });
        player.Load(xmi);
        player.Loop = true;

        var buf = new short[44100 * 2 * 2];
        player.Render(buf, 44100 * 2);

        var programChanges = synth.Events.OfType<MockSynthEngine.Event.Msg>()
            .Count(m => (m.Value & 0xFF) == 0xC0);
        Assert.True(programChanges >= 2, $"Expected at least 2 loops, got {programChanges}");
    }

    [Fact]
    public void Loop_False_StopsAfterEnd()
    {
        using var synth = new MockSynthEngine();
        using var player = new XmiPlayer(synth, 44100);

        byte[] xmi = BuildMinimalXmi(new byte[] { 0xC0, 5 });
        player.Load(xmi);
        player.Loop = false;

        var buf = new short[44100 * 2 * 2];
        player.Render(buf, 44100 * 2);

        var programChanges = synth.Events.OfType<MockSynthEngine.Event.Msg>()
            .Count(m => (m.Value & 0xFF) == 0xC0);
        Assert.Equal(1, programChanges);
    }

    [Fact]
    public void Render_FillsBufferCompletely()
    {
        using var synth = new MockSynthEngine();
        synth.FillValue = 0;
        using var player = new XmiPlayer(synth, 44100);

        byte[] xmi = BuildMinimalXmi(new byte[] { 0xC0, 5 });
        player.Load(xmi);

        var buf = new short[1024];
        player.Render(buf, 512);

        int totalRendered = synth.Events.OfType<MockSynthEngine.Event.Render>()
            .Sum(r => r.FrameCount);
        Assert.Equal(512, totalRendered);
    }

    [Fact]
    public void Stop_ResetsSynth()
    {
        using var synth = new MockSynthEngine();
        using var player = new XmiPlayer(synth, 44100);

        byte[] xmi = BuildMinimalXmi(new byte[] { 0xC0, 5 });
        player.Load(xmi);
        player.Stop();

        Assert.False(player.IsPlaying);
        Assert.Contains(synth.Events, e => e is MockSynthEngine.Event.ResetCall);
    }

    [Fact]
    public void Sysex_ForwardedToSynth()
    {
        using var synth = new MockSynthEngine();
        using var player = new XmiPlayer(synth, 44100);

        byte[] xmi = BuildMinimalXmi(new byte[]
        {
            0xF0, 0x05,
            0x41, 0x10, 0x16, 0x12, 0xF7,
        });
        player.Load(xmi);

        var buf = new short[1024];
        player.Render(buf, 512);

        var sysexEvents = synth.Events.OfType<MockSynthEngine.Event.Sysex>().ToList();
        Assert.Single(sysexEvents);
        Assert.Equal(0xF0, sysexEvents[0].Data[0]);
        Assert.Equal(6, sysexEvents[0].Data.Length);
    }

    private static byte[] BuildMinimalXmi(byte[] evntPayload)
    {
        int evntSize = evntPayload.Length;
        int formSize = 4 + 8 + evntSize;

        var ms = new MemoryStream();
        var bw = new BinaryWriter(ms);
        bw.Write(System.Text.Encoding.ASCII.GetBytes("FORM"));
        bw.Write(ToBE32(formSize));
        bw.Write(System.Text.Encoding.ASCII.GetBytes("XMID"));
        bw.Write(System.Text.Encoding.ASCII.GetBytes("EVNT"));
        bw.Write(ToBE32(evntSize));
        bw.Write(evntPayload);
        return ms.ToArray();
    }

    private static byte[] ToBE32(int value) => new byte[]
    {
        (byte)((value >> 24) & 0xFF),
        (byte)((value >> 16) & 0xFF),
        (byte)((value >> 8) & 0xFF),
        (byte)(value & 0xFF),
    };
}
