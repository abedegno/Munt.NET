# Munt.NET

[![NuGet](https://img.shields.io/nuget/v/Munt.NET.svg)](https://www.nuget.org/packages/Munt.NET)
[![CI](https://github.com/abedegno/Munt.NET/actions/workflows/ci.yml/badge.svg)](https://github.com/abedegno/Munt.NET/actions/workflows/ci.yml)

A .NET wrapper for the [Munt](https://github.com/munt/munt) mt32emu library, providing Roland CM-32L/MT-32 emulation.

The NuGet package includes pre-built native mt32emu libraries for Windows (x64), Linux (x64), and macOS (ARM64) — no need to build mt32emu yourself.

## Installation

```bash
dotnet add package Munt.NET
```

## Usage

### Render MIDI messages to PCM

```csharp
using Munt.NET;

using var synth = new Mt32EmuSynth();
synth.LoadRoms("CM32L_CONTROL.ROM", "CM32L_PCM.ROM");
synth.SetSampleRate(44100);
synth.Open();

// Send MIDI messages (packed 32-bit: status | data1<<8 | data2<<16)
synth.PlayMsg(0x6440C0); // Program Change: channel 0, program 0x40
synth.PlayMsg(0x647F90); // Note On: channel 0, note 0x40, velocity 0x7F

// Render to 16-bit stereo PCM
short[] buffer = new short[4096];
synth.Render(buffer, 2048); // 2048 stereo frames
```

### Render an XMI file to PCM

```csharp
using Munt.NET;

using var synth = new Mt32EmuSynth();
synth.LoadRoms("CM32L_CONTROL.ROM", "CM32L_PCM.ROM");
synth.SetSampleRate(44100);
synth.Open();

short[] pcm = XmiSequencer.RenderXmiToPcm(synth, "music.xmi", sampleRate: 44100);
// pcm contains interleaved 16-bit stereo samples
```

## ROM Files

CM-32L or MT-32 ROM files are required but cannot be distributed with this package.
You must source them separately. Both standard names and MAME-style names are supported:

- `CM32L_CONTROL.ROM` / `cm32l_ctrl_1_02.rom` / `cm32l_ctrl_1_00.rom`
- `CM32L_PCM.ROM` / `cm32l_pcm.rom`

## Platform Support

| Platform | Runtime ID | Included in NuGet |
|----------|-----------|-------------------|
| Windows x64 | win-x64 | Yes |
| Linux x64 | linux-x64 | Yes |
| macOS ARM64 | osx-arm64 | Yes |

## Building from Source

Only needed if you're contributing or need a platform not listed above.

```bash
# Clone
git clone https://github.com/abedegno/Munt.NET.git
cd Munt.NET

# Build the mt32emu native library (requires cmake)
git clone --depth 1 https://github.com/munt/munt.git munt-src
cd munt-src/mt32emu
cmake -B build -DCMAKE_BUILD_TYPE=Release -Dlibmt32emu_SHARED=ON
cmake --build build --config Release

# Copy native lib to runtimes (macOS example)
mkdir -p ../../src/Munt.NET/runtimes/osx-arm64/native
cp build/libmt32emu.dylib ../../src/Munt.NET/runtimes/osx-arm64/native/
cd ../..

# On macOS you can also install via Homebrew
# brew install mt32emu
# cp /opt/homebrew/lib/libmt32emu.dylib src/Munt.NET/runtimes/osx-arm64/native/

# Build and test
dotnet build Munt.NET.sln
dotnet test Munt.NET.sln
```

## License

This wrapper is licensed under the [MIT License](LICENSE).

The [mt32emu](https://github.com/munt/munt) native library is licensed under
[LGPL-2.1-or-later](https://www.gnu.org/licenses/old-licenses/lgpl-2.1.html)
and is distributed as a shared library in compliance with the LGPL.

## Credits

- [Munt](https://github.com/munt/munt) — the mt32emu MT-32/CM-32L emulation engine
