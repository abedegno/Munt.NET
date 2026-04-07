using System;
using System.Collections.Generic;
using System.IO;

namespace Mt32Emu.NET;

public static class XmiSequencer
{
    private struct MidiEvent : IComparable<MidiEvent>
    {
        public uint Tick;
        public byte[] Data; // short msg: 1-3 bytes; sysex: full F0..F7

        public int CompareTo(MidiEvent other) => Tick.CompareTo(other.Tick);
    }

    /// <summary>
    /// Parse an XMI file and render it to interleaved 16-bit stereo PCM via the given synth.
    /// </summary>
    public static short[] RenderXmiToPcm(Mt32EmuSynth synth, string xmiFilePath, int sampleRate = 44100)
    {
        var events = ParseXmi(File.ReadAllBytes(xmiFilePath));
        return RenderEvents(synth, events, sampleRate);
    }

    static List<MidiEvent> ParseXmi(byte[] data)
    {
        // Find the EVNT chunk inside FORM:XMID
        int evntOffset = FindEvntChunk(data);
        if (evntOffset < 0)
            throw new InvalidOperationException("EVNT chunk not found in XMI file");

        int evntSize = ReadBE32(data, evntOffset + 4);
        int pos = evntOffset + 8;
        int end = pos + evntSize;

        var events = new List<MidiEvent>();
        uint tick = 0;

        while (pos < end)
        {
            // Read delta time: in XMI, delta is a sequence of bytes < 128 (delay intervals).
            // Any byte >= 128 is a status byte, not part of the delta.
            while (pos < end && data[pos] < 0x80)
            {
                tick += data[pos];
                pos++;
            }

            if (pos >= end) break;

            byte status = data[pos];

            if (status == 0xFF) // Meta event
            {
                pos++; // status
                if (pos >= end) break;
                byte metaType = data[pos++];
                int metaLen = ReadVarLen(data, ref pos);
                pos += metaLen; // skip meta event data
                continue;
            }

            if (status == 0xF0) // SysEx
            {
                int sysexStart = pos;
                pos++; // skip F0
                int sysexLen = ReadVarLen(data, ref pos);
                var sysexData = new byte[1 + sysexLen]; // F0 + payload (includes F7)
                sysexData[0] = 0xF0;
                Array.Copy(data, pos, sysexData, 1, sysexLen);
                pos += sysexLen;

                events.Add(new MidiEvent { Tick = tick, Data = sysexData });
                continue;
            }

            if (status < 0x80) continue; // shouldn't happen, skip

            // Channel messages
            pos++; // consume status byte
            int msgLen = MidiMsgDataLength(status);

            byte d1 = (msgLen >= 1 && pos < end) ? data[pos++] : (byte)0;
            byte d2 = (msgLen >= 2 && pos < end) ? data[pos++] : (byte)0;

            events.Add(new MidiEvent
            {
                Tick = tick,
                Data = msgLen == 2 ? new[] { status, d1, d2 } : new[] { status, d1 }
            });

            // XMI Note On: read duration and schedule Note Off
            if ((status & 0xF0) == 0x90 && d2 > 0)
            {
                int duration = ReadVarLen(data, ref pos);
                byte noteOffStatus = (byte)(0x80 | (status & 0x0F));
                events.Add(new MidiEvent
                {
                    Tick = tick + (uint)duration,
                    Data = new byte[] { noteOffStatus, d1, 0x00 }
                });
            }
        }

        events.Sort();
        return events;
    }

    static short[] RenderEvents(Mt32EmuSynth synth, List<MidiEvent> events, int sampleRate)
    {
        double samplesPerTick = sampleRate / 120.0; // XMI fixed at 120 ticks/sec
        var output = new List<short[]>();
        int totalSamples = 0;
        uint currentTick = 0;
        short[] renderBuf = new short[4096]; // stereo frames -> 2048 frames * 2 channels

        for (int i = 0; i < events.Count; i++)
        {
            var evt = events[i];

            // Render audio for the gap between current position and this event
            if (evt.Tick > currentTick)
            {
                uint deltaTicks = evt.Tick - currentTick;
                int samplesToRender = (int)(deltaTicks * samplesPerTick) * 2; // *2 for stereo
                int framesNeeded = samplesToRender / 2;

                while (framesNeeded > 0)
                {
                    int chunkFrames = Math.Min(framesNeeded, renderBuf.Length / 2);
                    synth.Render(renderBuf, (uint)chunkFrames);
                    int chunkSamples = chunkFrames * 2;

                    var chunk = new short[chunkSamples];
                    Array.Copy(renderBuf, chunk, chunkSamples);
                    output.Add(chunk);
                    totalSamples += chunkSamples;
                    framesNeeded -= chunkFrames;
                }

                currentTick = evt.Tick;
            }

            // Send the MIDI event
            if (evt.Data[0] == 0xF0)
            {
                synth.PlaySysex(evt.Data);
            }
            else
            {
                uint msg = evt.Data[0];
                if (evt.Data.Length > 1) msg |= (uint)(evt.Data[1] << 8);
                if (evt.Data.Length > 2) msg |= (uint)(evt.Data[2] << 16);
                synth.PlayMsg(msg);
            }
        }

        // Render tail until synth goes silent (max 5 seconds)
        int maxTailFrames = sampleRate * 5;
        int tailFrames = 0;
        while (synth.IsActive && tailFrames < maxTailFrames)
        {
            int chunkFrames = Math.Min(2048, maxTailFrames - tailFrames);
            var buf = new short[chunkFrames * 2];
            synth.Render(buf, (uint)chunkFrames);
            output.Add(buf);
            totalSamples += buf.Length;
            tailFrames += chunkFrames;
        }

        // Flatten to single array
        var result = new short[totalSamples];
        int offset = 0;
        foreach (var chunk in output)
        {
            Array.Copy(chunk, 0, result, offset, chunk.Length);
            offset += chunk.Length;
        }

        return result;
    }

    static int FindEvntChunk(byte[] data)
    {
        // Walk IFF chunks looking for "EVNT"
        int pos = 0;
        while (pos + 8 <= data.Length)
        {
            string id = System.Text.Encoding.ASCII.GetString(data, pos, 4);
            if (id == "EVNT")
                return pos;

            if (id == "FORM")
            {
                // FORM chunks: skip the 4-byte type ID, then recurse into contents
                pos += 12; // skip "FORM" + size + type
                continue;
            }

            if (id == "CAT ")
            {
                pos += 12;
                continue;
            }

            // Regular chunk: skip past it
            int size = ReadBE32(data, pos + 4);
            pos += 8 + size;
            if (size % 2 != 0) pos++; // IFF padding
        }
        return -1;
    }

    static int ReadBE32(byte[] data, int offset)
    {
        return (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
    }

    static int ReadVarLen(byte[] data, ref int pos)
    {
        int value = 0;
        while (pos < data.Length)
        {
            byte b = data[pos++];
            value = (value << 7) | (b & 0x7F);
            if ((b & 0x80) == 0) break;
        }
        return value;
    }

    static int MidiMsgDataLength(byte status)
    {
        switch (status & 0xF0)
        {
            case 0x80: return 2; // Note Off
            case 0x90: return 2; // Note On
            case 0xA0: return 2; // Poly Aftertouch
            case 0xB0: return 2; // Control Change
            case 0xC0: return 1; // Program Change
            case 0xD0: return 1; // Channel Aftertouch
            case 0xE0: return 2; // Pitch Bend
            default: return 0;
        }
    }
}
