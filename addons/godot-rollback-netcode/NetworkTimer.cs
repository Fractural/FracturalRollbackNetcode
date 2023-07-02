
using System;
using Godot;
using Dictionary = Godot.Collections.Dictionary;
using Array = Godot.Collections.Array;


public class NetworkTimer : Node
{
	 
	//class_name NetworkTimer
	
	[Export]  public bool autostart  = false;
	[Export]  public bool one_shot  = false;
	[Export]  public int wait_ticks  = 0;
	[Export]  public bool hash_state  = true;
	
	public int ticks_left  = 0;
	
	public bool _running  = false;
	
	[Signal] delegate void Timeout ();
	
	public void _Ready()
	{  
		AddToGroup("network_sync");
		SyncManager.Connect("sync_stopped", this, "_on_SyncManager_sync_stopped");
		if(autostart)
		{
			Start();
	
		}
	}
	
	public bool IsStopped()
	{  
		return !_running;
	
	}
	
	public void Start(int ticks = -1)
	{  
		if(ticks > 0)
		{
			wait_ticks = ticks;
		}
		ticks_left = wait_ticks;
		_running = true;
	
	}
	
	public void Stop()
	{  
		_running = false;
		ticks_left = 0;
	
	}
	
	public void _OnSyncManagerSyncStopped()
	{  
		Stop();
	
	}
	
	public void _NetworkProcess(Dictionary _input)
	{  
		if(!_running)
		{
			return;
		}
		if(ticks_left <= 0)
		{
			_running = false;
			return;
		
		}
		ticks_left -= 1;
		
		if(ticks_left == 0)
		{
			if(!one_shot)
			{
				ticks_left = wait_ticks;
			}
			EmitSignal("timeout");
	
		}
	}
	
	public Dictionary _SaveState()
	{  
		if(hash_state)
		{
			return new Dictionary(){
				running = _running,
				wait_ticks = wait_ticks,
				ticks_left = ticks_left,
			};
		}
		else
		{
			return new Dictionary(){
				_running = _running,
				_wait_ticks = wait_ticks,
				_ticks_left = ticks_left,
			};
	
		}
	}
	
	public void _LoadState(Dictionary state)
	{  
		if(hash_state)
		{
			_running = state["running"];
			wait_ticks = state["wait_ticks"];
			ticks_left = state["ticks_left"];
		}
		else
		{
			_running = state["_running"];
			wait_ticks = state["_wait_ticks"];
			ticks_left = state["_ticks_left"];
	
	
		}
	}
	
	
	
}