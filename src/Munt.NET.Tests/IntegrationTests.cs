using System;
using System.IO;
using System.Linq;
using Munt.NET;
using Xunit;

namespace Munt.NET.Tests;

[Trait("Category", "Integration")]
public class IntegrationTests
{
    private static string? GetRomPath()
    {
        return Environment.GetEnvironmentVariable("MUNT_ROM_PATH");
    }

    private static (string control, string pcm)? FindRoms(string romDir)
    {
        var namePairs = new[]
        {
            ("CM32L_CONTROL.ROM", "CM32L_PCM.ROM"),
            ("cm32l_ctrl_1_02.rom", "cm32l_pcm.rom"),
            ("cm32l_ctrl_1_00.rom", "cm32l_pcm.rom"),
        };

        foreach (var (ctrlName, pcmName) in namePairs)
        {
            var control = Path.Combine(romDir, ctrlName);
            var pcm = Path.Combine(romDir, pcmName);
            if (File.Exists(control) && File.Exists(pcm))
                return (control, pcm);
        }
        return null;
    }

    [Fact]
    public void RenderXmiToPcm_WithRoms_ProducesAudio()
    {
        var romPath = GetRomPath();
        if (string.IsNullOrEmpty(romPath))
            return;

        var roms = FindRoms(romPath);
        if (roms == null)
            return;

        var xmiPath = Environment.GetEnvironmentVariable("MUNT_TEST_XMI");
        if (string.IsNullOrEmpty(xmiPath) || !File.Exists(xmiPath))
            return;

        using var synth = new Mt32EmuSynth();
        synth.LoadRoms(roms.Value.control, roms.Value.pcm);
        synth.SetSampleRate(44100);
        synth.Open();

        short[] pcm = XmiSequencer.RenderXmiToPcm(synth, xmiPath, 44100);

        Assert.True(pcm.Length > 0, "PCM output should not be empty");
        Assert.True(pcm.Any(s => s != 0), "PCM output should contain non-zero samples");
    }
}
