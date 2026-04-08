using Munt.NET;
using Xunit;

namespace Munt.NET.Tests;

public class Mt32EmuSynthTests
{
    [Fact]
    public void Dispose_CalledTwice_NoError()
    {
        var synth = new Mt32EmuSynth();
        synth.Dispose();
        synth.Dispose();
    }

    [Fact]
    public void PlayMsg_AfterDispose_ThrowsObjectDisposed()
    {
        var synth = new Mt32EmuSynth();
        synth.Dispose();
        Assert.Throws<ObjectDisposedException>(() => synth.PlayMsg(0));
    }

    [Fact]
    public void PlaySysex_AfterDispose_ThrowsObjectDisposed()
    {
        var synth = new Mt32EmuSynth();
        synth.Dispose();
        Assert.Throws<ObjectDisposedException>(() => synth.PlaySysex(new byte[] { 0xF0, 0xF7 }));
    }

    [Fact]
    public void Render_AfterDispose_ThrowsObjectDisposed()
    {
        var synth = new Mt32EmuSynth();
        synth.Dispose();
        Assert.Throws<ObjectDisposedException>(() => synth.Render(new short[1024], 512));
    }

    [Fact]
    public void SetSampleRate_AfterDispose_ThrowsObjectDisposed()
    {
        var synth = new Mt32EmuSynth();
        synth.Dispose();
        Assert.Throws<ObjectDisposedException>(() => synth.SetSampleRate(44100));
    }

    [Fact]
    public void Open_WithoutRoms_ThrowsInvalidOperation()
    {
        using var synth = new Mt32EmuSynth();
        Assert.Throws<InvalidOperationException>(() => synth.Open());
    }

    [Fact]
    public void LoadRoms_InvalidPath_ThrowsInvalidOperation()
    {
        using var synth = new Mt32EmuSynth();
        Assert.Throws<InvalidOperationException>(() =>
            synth.LoadRoms("/nonexistent/control.rom", "/nonexistent/pcm.rom"));
    }
}
