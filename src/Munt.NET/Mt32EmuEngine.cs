using System;
using System.IO;

namespace Munt.NET;

/// <summary>
/// ISynthEngine backed by mt32emu (Munt). Authentic CM-32L / MT-32 emulation.
/// </summary>
public sealed class Mt32EmuEngine : ISynthEngine
{
    private readonly Mt32EmuSynth _synth;

    public Mt32EmuEngine(string romDirectory, int sampleRate = 44100)
    {
        var roms = FindRoms(romDirectory);
        if (roms == null)
            throw new InvalidOperationException(
                $"CM-32L/MT-32 ROM files not found in '{romDirectory}'. " +
                "Need control+pcm ROM pair.");

        _synth = new Mt32EmuSynth();
        try
        {
            _synth.LoadRoms(roms.Value.control, roms.Value.pcm);
            _synth.SetSampleRate(sampleRate);
            _synth.Open();
        }
        catch
        {
            _synth.Dispose();
            throw;
        }
    }

    public void PlayMsg(uint msg) => _synth.PlayMsg(msg);

    public void PlaySysex(byte[] data) => _synth.PlaySysex(data);

    public void Render(short[] buffer, uint frameCount)
        => _synth.Render(buffer, frameCount);

    public void Reset()
    {
        // All-notes-off on all 16 channels (CC 123 = 0x7B)
        for (byte ch = 0; ch < 16; ch++)
        {
            uint msg = (uint)(0xB0 | ch) | (0x7Bu << 8);
            _synth.PlayMsg(msg);
        }
    }

    public void Dispose() => _synth.Dispose();

    private static (string control, string pcm)? FindRoms(string dir)
    {
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            return null;

        var namePairs = new[]
        {
            ("CM32L_CONTROL.ROM", "CM32L_PCM.ROM"),
            ("cm32l_ctrl_1_02.rom", "cm32l_pcm.rom"),
            ("cm32l_ctrl_1_00.rom", "cm32l_pcm.rom"),
            ("MT32_CONTROL.ROM", "MT32_PCM.ROM"),
            ("mt32_ctrl_1_07.rom", "mt32_pcm.rom"),
            ("mt32_ctrl_1_06.rom", "mt32_pcm.rom"),
            ("mt32_ctrl_1_05.rom", "mt32_pcm.rom"),
            ("mt32_ctrl_1_04.rom", "mt32_pcm.rom"),
        };

        foreach (var (ctrl, pcm) in namePairs)
        {
            var ctrlPath = Path.Combine(dir, ctrl);
            var pcmPath = Path.Combine(dir, pcm);
            if (File.Exists(ctrlPath) && File.Exists(pcmPath))
                return (ctrlPath, pcmPath);
        }

        return null;
    }
}
