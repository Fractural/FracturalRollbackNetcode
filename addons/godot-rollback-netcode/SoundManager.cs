
using System;
using Godot;
using Dictionary = Godot.Collections.Dictionary;
using Array = Godot.Collections.Array;


public class SoundManager : Node
{
	 
	public const string DEFAULT_SOUND_BUS_SETTING := "network/rollback/sound_manager/default_sound_bus"
	
	public string default_bus  = "Master";
	public Dictionary ticks  = new Dictionary(){};
	
	public __TYPE SyncManager;
	
	public void _Ready()
	{  
		if(ProjectSettings.HasSetting(DEFAULT_SOUND_BUS_SETTING))
		{
			default_bus = ProjectSettings.GetSetting(DEFAULT_SOUND_BUS_SETTING);
	
		}
	}
	
	public void SetupSoundManager(__TYPE _sync_manager)
	{  
		SyncManager = _sync_manager;
		SyncManager.Connect("tick_retired", this, "_on_SyncManager_tick_retired");
		SyncManager.Connect("sync_stopped", this, "_on_SyncManager_sync_stopped");
	
	}
	
	public void PlaySound(String identifier, AudioStream sound, Dictionary info = new Dictionary(){})
	{  
		if(SyncManager.IsRespawning())
		{
			return;
		
		}
		if(ticks.Has(SyncManager.current_tick))
		{
			if(ticks[SyncManager.current_tick].Has(identifier))
			{
				return;
			}
		}
		else
		{
			ticks[SyncManager.current_tick] = new Dictionary(){};
		}
		ticks[SyncManager.current_tick][identifier] = true;
		
		var node;
		if(info.Has("position"))
		{
			node = new AudioStreamPlayer2D()
		}
		else
		{
			node = new AudioStreamPlayer()
		
		}
		node.stream = sound;
		node.volume_db = info.Get("volume_db", 0.0);
		node.pitch_scale = info.Get("pitch_scale", 1.0);
		node.bus = info.Get("bus", default_bus);
		
		AddChild(node);
		if(info.Has("position"))
		{
			node.global_position = info["position"];
		
		}
		node.Play();
		
		node.Connect("finished", this, "_on_audio_finished", new Array(){node});
	
	}
	
	public void _OnAudioFinished(Node node)
	{  
		RemoveChild(node);
		node.QueueFree();
	
	}
	
	public void _OnSyncManagerTickRetired(__TYPE tick)
	{  
		ticks.Erase(tick);
	
	}
	
	public void _OnSyncManagerSyncStopped()
	{  
		ticks.Clear();
	
	
	}
	
	
	
}