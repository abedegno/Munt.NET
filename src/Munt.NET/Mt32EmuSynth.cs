using System;

namespace Munt.NET;

public sealed class Mt32EmuSynth : IDisposable
{
    private IntPtr _context;
    private bool _opened;
    private bool _disposed;

    public Mt32EmuSynth()
    {
        _context = Mt32EmuImports.mt32emu_create_context(IntPtr.Zero, IntPtr.Zero);
        if (_context == IntPtr.Zero)
            throw new InvalidOperationException("Failed to create mt32emu context");
    }

    public void LoadRoms(string controlRomPath, string pcmRomPath)
    {
        CheckDisposed();
        var rc1 = Mt32EmuImports.mt32emu_add_rom_file(_context, controlRomPath);
        if (rc1 < 0)
            throw new InvalidOperationException($"Failed to load control ROM '{controlRomPath}': {rc1}");

        var rc2 = Mt32EmuImports.mt32emu_add_rom_file(_context, pcmRomPath);
        if (rc2 < 0)
            throw new InvalidOperationException($"Failed to load PCM ROM '{pcmRomPath}': {rc2}");
    }

    public void SetSampleRate(double sampleRate)
    {
        CheckDisposed();
        Mt32EmuImports.mt32emu_set_stereo_output_samplerate(_context, sampleRate);
    }

    public void Open()
    {
        CheckDisposed();
        var rc = Mt32EmuImports.mt32emu_open_synth(_context);
        if (rc != Mt32EmuReturnCode.OK)
            throw new InvalidOperationException($"Failed to open synth: {rc}");
        _opened = true;
    }

    public void PlayMsg(uint msg)
    {
        CheckDisposed();
        Mt32EmuImports.mt32emu_play_msg(_context, msg);
    }

    public unsafe void PlaySysex(byte[] data)
    {
        CheckDisposed();
        fixed (byte* ptr = data)
        {
            Mt32EmuImports.mt32emu_play_sysex(_context, ptr, (uint)data.Length);
        }
    }

    public unsafe void Render(short[] buffer, uint frameCount)
    {
        CheckDisposed();
        fixed (short* ptr = buffer)
        {
            Mt32EmuImports.mt32emu_render_bit16s(_context, ptr, frameCount);
        }
    }

    public bool IsActive
    {
        get
        {
            CheckDisposed();
            return Mt32EmuImports.mt32emu_is_active(_context) != 0;
        }
    }

    private void CheckDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(Mt32EmuSynth));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_opened)
            Mt32EmuImports.mt32emu_close_synth(_context);
        if (_context != IntPtr.Zero)
            Mt32EmuImports.mt32emu_free_context(_context);
        _context = IntPtr.Zero;
    }
}
