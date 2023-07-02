
using System;
using Godot;
using Dictionary = Godot.Collections.Dictionary;
using Array = Godot.Collections.Array;


public class PerfTimer : Reference
{
	 
	public Dictionary start_timings  = new Dictionary(){};
	public Dictionary timings  = new Dictionary(){};
	
	public void Clear()
	{  
		start_timings.Clear();
		timings.Clear();
	
	}
	
	public void Start(String name)
	{  
		start_timings[name] = OS.GetTicksUsec();
	
	}
	
	public void Stop(String name)
	{  
		System.Diagnostics.Debug.Assert(start_timings.Has(name), "stop() without Start() for %s" % name);
		timings[name] = OS.GetTicksUsec() - start_timings[name];
		start_timings.Erase(name);
	
	}
	
	public int GetTotal()
	{  
		int total  = 0;
		foreach(var key in timings)
		{
			total += timings[key];
		}
		return total;
	
	}
	
	public void PrintTimings()
	{  
		System.Diagnostics.Debug.Assert(start_timings.Size() == 0, "there are unstopped timers: %s" % GD.Str(start_timings.Keys()));
		var total  = GetTotal();
		foreach(var key in timings)
		{
			Print ("%s: %s ms" % [key, (float)(timings[key]) / 1000.0]);
		}
		GD.Print(" * total: %s" % ((float)(total) / 1000.0));
	
	
	}
	
	
	
}