using System;
using System.Collections.Generic;
using System.IO;

namespace Munt.NET;

public sealed class XmiPlayer : IDisposable
{
    private readonly ISynthEngine _synth;
    private readonly int _sampleRate;
    private readonly double _ticksPerSample;

    private List<XmiSequencer.MidiEvent>? _events;
    private int _nextEventIdx;
    private double _currentTick;
    private double _endTick;
    private bool _disposed;

    public bool Loop { get; set; }

    public bool IsPlaying => _events != null && _events.Count > 0;

    public XmiPlayer(ISynthEngine synth, int sampleRate = 44100)
    {
        _synth = synth ?? throw new ArgumentNullException(nameof(synth));
        _sampleRate = sampleRate;
        _ticksPerSample = 120.0 / sampleRate;
    }

    public void Load(string xmiFilePath) => Load(File.ReadAllBytes(xmiFilePath));

    public void Load(byte[] xmiData)
    {
        CheckDisposed();
        _events = XmiSequencer.ParseXmi(xmiData);
        _nextEventIdx = 0;
        _currentTick = 0;
        _endTick = 0;
        if (_events.Count > 0)
        {
            uint maxTick = 0;
            foreach (var e in _events)
                if (e.Tick > maxTick) maxTick = e.Tick;
            // Ensure at least one sample of progression per loop so loop mode can't spin.
            _endTick = Math.Max(maxTick, _ticksPerSample);
        }
    }

    public void Render(short[] buffer, int frameCount)
    {
        CheckDisposed();

        if (_events == null || _events.Count == 0)
        {
            Array.Clear(buffer, 0, Math.Min(buffer.Length, frameCount * 2));
            return;
        }

        int samplesRemaining = frameCount;
        int outputOffset = 0;

        while (samplesRemaining > 0)
        {
            if (_nextEventIdx >= _events.Count)
            {
                // All events fired — render tail until end tick.
                double ticksRemaining = _endTick - _currentTick;
                if (ticksRemaining > 0)
                {
                    int samplesUntilEnd = (int)Math.Ceiling(ticksRemaining / _ticksPerSample);
                    int tailChunk = Math.Min(samplesRemaining, samplesUntilEnd);
                    if (tailChunk > 0)
                    {
                        RenderChunk(buffer, outputOffset, tailChunk);
                        _currentTick += tailChunk * _ticksPerSample;
                        outputOffset += tailChunk * 2;
                        samplesRemaining -= tailChunk;
                    }
                    continue;
                }

                if (Loop)
                {
                    _nextEventIdx = 0;
                    _currentTick = 0;
                    _synth.Reset();
                    continue;
                }
                else
                {
                    RenderChunk(buffer, outputOffset, samplesRemaining);
                    return;
                }
            }

            var evt = _events[_nextEventIdx];
            double ticksUntilEvent = evt.Tick - _currentTick;

            if (ticksUntilEvent <= 0)
            {
                FireEvent(evt);
                _nextEventIdx++;
                continue;
            }

            int samplesUntilEvent = (int)Math.Ceiling(ticksUntilEvent / _ticksPerSample);
            int chunk = Math.Min(samplesRemaining, samplesUntilEvent);

            if (chunk > 0)
            {
                RenderChunk(buffer, outputOffset, chunk);
                _currentTick += chunk * _ticksPerSample;
                outputOffset += chunk * 2;
                samplesRemaining -= chunk;
            }
        }
    }

    public void Render(short[] buffer, uint frameCount) => Render(buffer, (int)frameCount);

    public void Stop()
    {
        CheckDisposed();
        _events = null;
        _nextEventIdx = 0;
        _currentTick = 0;
        _synth.Reset();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }

    private void CheckDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(XmiPlayer));
    }

    private void FireEvent(XmiSequencer.MidiEvent evt)
    {
        if (evt.Data[0] == 0xF0)
        {
            _synth.PlaySysex(evt.Data);
        }
        else
        {
            uint msg = evt.Data[0];
            if (evt.Data.Length > 1) msg |= (uint)(evt.Data[1] << 8);
            if (evt.Data.Length > 2) msg |= (uint)(evt.Data[2] << 16);
            _synth.PlayMsg(msg);
        }
    }

    private short[]? _scratch;
    private void RenderChunk(short[] dest, int sampleOffset, int frameCount)
    {
        int neededSamples = frameCount * 2;
        if (_scratch == null || _scratch.Length < neededSamples)
            _scratch = new short[neededSamples];

        _synth.Render(_scratch, (uint)frameCount);

        Array.Copy(_scratch, 0, dest, sampleOffset, neededSamples);
    }
}
