
using System;
using Godot;
using GDC = Godot.Collections;
using Fractural.Utils;

namespace Fractural.RollbackNetcode
{
    public class PerfTimer : Reference
    {
        public GDC.Dictionary start_timings = new GDC.Dictionary() { };
        public GDC.Dictionary timings = new GDC.Dictionary() { };

        public void Clear()
        {
            start_timings.Clear();
            timings.Clear();
        }

        public void Start(string name)
        {
            start_timings[name] = (uint)Time.GetTicksUsec();
        }

        public void Stop(string name)
        {
            System.Diagnostics.Debug.Assert(start_timings.Contains(name), $"stop() without Start() for {name}");
            timings[name] = (uint)Time.GetTicksUsec() - start_timings.Get<uint>(name);
            start_timings.Remove(name);
        }

        public uint GetTotal()
        {
            uint total = 0;
            foreach (string key in timings.Keys)
                total += timings.Get<uint>(key);
            return total;
        }

        public void PrintTimings()
        {
            System.Diagnostics.Debug.Assert(start_timings.Count == 0, $"there are unstopped timers: {string.Join(", ", start_timings.Keys)}");
            var total = GetTotal();
            foreach (var key in timings.Keys)
                GD.Print($"{key}: {(float)(timings[key]) / 1000.0} ms");
            GD.Print($" * total: {(float)(total) / 1000.0}");
        }
    }
}