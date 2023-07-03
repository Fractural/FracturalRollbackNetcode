
using System;
using Godot;
using GDC = Godot.Collections;


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
        System.Diagnostics.Debug.Assert(start_timings.Contains(name), "stop() without Start() for %s" % name);
        timings[name] = (uint)Time.GetTicksUsec() - start_timings[name];
        start_timings.Erase(name);

    }

    public int GetTotal()
    {
        int total = 0;
        foreach (var key in timings)
        {
            total += timings[key];
        }
        return total;

    }

    public void PrintTimings()
    {
        System.Diagnostics.Debug.Assert(start_timings.Size() == 0, "there are unstopped timers: %s" % GD.Str(start_timings.Keys()));
        var total = GetTotal();
        foreach (var key in timings)
        {
            Print("%s: %s ms" % [key, (float)(timings[key]) / 1000.0]);
        }
        GD.Print(" * total: %s" % ((float)(total) / 1000.0));


    }



}