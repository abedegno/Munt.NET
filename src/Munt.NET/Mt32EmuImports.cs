using System;
using System.Runtime.InteropServices;

namespace Munt.NET;

internal static class Mt32EmuImports
{
    const string LibraryName = "libmt32emu";

    [DllImport(LibraryName)]
    public static extern IntPtr mt32emu_create_context(IntPtr reportHandler, IntPtr instanceData);

    [DllImport(LibraryName)]
    public static extern void mt32emu_free_context(IntPtr context);

    [DllImport(LibraryName)]
    public static extern Mt32EmuReturnCode mt32emu_add_rom_file(IntPtr context, string filename);

    [DllImport(LibraryName)]
    public static extern void mt32emu_set_stereo_output_samplerate(IntPtr context, double samplerate);

    [DllImport(LibraryName)]
    public static extern Mt32EmuReturnCode mt32emu_open_synth(IntPtr context);

    [DllImport(LibraryName)]
    public static extern void mt32emu_close_synth(IntPtr context);

    [DllImport(LibraryName)]
    public static extern Mt32EmuReturnCode mt32emu_play_msg(IntPtr context, uint msg);

    [DllImport(LibraryName)]
    public static extern unsafe Mt32EmuReturnCode mt32emu_play_sysex(IntPtr context, byte* sysex, uint len);

    [DllImport(LibraryName)]
    public static extern unsafe void mt32emu_render_bit16s(IntPtr context, short* stream, uint len);

    [DllImport(LibraryName)]
    public static extern int mt32emu_is_active(IntPtr context);
}
