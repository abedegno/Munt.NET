using System;

namespace Munt.NET;

/// <summary>
/// Abstraction over a MIDI synthesizer engine for real-time playback.
/// Implementations: Mt32EmuEngine (CM-32L/MT-32), MeltySynthEngine (SoundFont),
/// AdlMidiEngine (OPL/AdLib).
/// </summary>
public interface ISynthEngine : IDisposable
{
    /// <summary>
    /// Send a short MIDI message. Packed format:
    /// status | (data1 &lt;&lt; 8) | (data2 &lt;&lt; 16).
    /// </summary>
    void PlayMsg(uint msg);

    /// <summary>
    /// Send a SysEx message (F0..F7 inclusive).
    /// </summary>
    void PlaySysex(byte[] data);

    /// <summary>
    /// Render stereo 16-bit PCM into the buffer.
    /// buffer is interleaved L,R,L,R,...; length must be at least frameCount * 2.
    /// </summary>
    void Render(short[] buffer, uint frameCount);

    /// <summary>
    /// Reset all MIDI state — all notes off, all controllers reset.
    /// Called on loop restart.
    /// </summary>
    void Reset();
}
