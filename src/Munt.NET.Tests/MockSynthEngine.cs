using System.Collections.Generic;
using Munt.NET;

namespace Munt.NET.Tests;

/// <summary>
/// Test double for ISynthEngine. Records every method call in order.
/// </summary>
public sealed class MockSynthEngine : ISynthEngine
{
    public record Event
    {
        public record Msg(uint Value) : Event;
        public record Sysex(byte[] Data) : Event;
        public record Render(int FrameCount) : Event;
        public record ResetCall : Event;
        public record DisposeCall : Event;
    }

    public List<Event> Events { get; } = new();
    public short FillValue { get; set; } = 0;

    public void PlayMsg(uint msg) => Events.Add(new Event.Msg(msg));

    public void PlaySysex(byte[] data)
    {
        var copy = new byte[data.Length];
        data.CopyTo(copy, 0);
        Events.Add(new Event.Sysex(copy));
    }

    public void Render(short[] buffer, uint frameCount)
    {
        int samples = (int)frameCount * 2;
        for (int i = 0; i < samples && i < buffer.Length; i++)
            buffer[i] = FillValue;
        Events.Add(new Event.Render((int)frameCount));
    }

    public void Reset() => Events.Add(new Event.ResetCall());

    public void Dispose() => Events.Add(new Event.DisposeCall());
}
