
using System;
using Godot;
using Dictionary = Godot.Collections.Dictionary;
using Array = Godot.Collections.Array;


public class SyncDebugger : Node
{
	 
	public const var DebugOverlay = GD.Load("res://addons/godot-rollback-netcode/debugger/DebugOverlay.tscn");
	public const var DebugStateComparer = GD.Load("res://addons/godot-rollback-netcode/DebugStateComparer.gd");
	
	public const string JSON_INDENT = "    ";
	
	private __TYPE _canvas_layer;
	private __TYPE _debug_overlay;
	public bool _debug_pressed = false;
	
	public bool print_previous_state  = false;
	
	public void _Ready()
	{  
		SyncManager.Connect("rollback_flagged", this, "_on_SyncManager_rollback_flagged");
		SyncManager.Connect("prediction_missed", this, "_on_SyncManager_prediction_missed");
		SyncManager.Connect("skip_ticks_flagged", this, "_on_SyncManager_skip_ticks_flagged");
		SyncManager.Connect("remote_state_mismatch", this, "_on_SyncManager_remote_state_mismatch");
		SyncManager.Connect("peer_pinged_back", this, "_on_SyncManager_peer_pinged_back");
		SyncManager.Connect("state_loaded", this, "_on_SyncManager_state_loaded");
		SyncManager.Connect("tick_finished", this, "_on_SyncManager_tick_finished");
	
	}
	
	public void CreateDebugOverlay(__TYPE overlay_instance = null)
	{  
		if(_debug_overlay != null)
		{
			_debug_overlay.QueueFree();
			_canvas_layer.RemoveChild(_debug_overlay);
		
		}
		if(overlay_instance == null)
		{
			overlay_instance = DebugOverlay.Instance();
		}
		if(_canvas_layer == null)
		{
			_canvas_layer = new CanvasLayer()
			AddChild(_canvas_layer);
		
		}
		_debug_overlay = overlay_instance;
		_canvas_layer.AddChild(_debug_overlay);
	
	}
	
	public void ShowDebugOverlay(bool _visible = true)
	{  
		if(_visible && !_debug_overlay)
		{
			CreateDebugOverlay();
		}
		if(_debug_overlay)
		{
			_debug_overlay.visible = _visible;
	
		}
	}
	
	public void HideDebugOverlay()
	{  
		if(_debug_overlay)
		{
			ShowDebugOverlay(false);
	
		}
	}
	
	public bool IsDebugOverlayShown()
	{  
		if(_debug_overlay)
		{
			return _debug_overlay.visible;
		}
		return false;
	
	}
	
	public void _OnSyncManagerSkipTicksFlagged(int count)
	{  
		Print ("-----");
		Print ("Skipping %s local Tick(s) to adjust for peer advantage" % count);
	
	}
	
	public void _OnSyncManagerPredictionMissed(int tick, int peer_id, Dictionary local_input, Dictionary remote_input)
	{  
		Print ("-----");
		Print ("Prediction missed on tick %s for peer %s" % [tick, peer_id]);
		Print ("Received input: %s" % SyncManager.hash_serializer.Serialize(remote_input));
		Print ("Predicted input: %s" % SyncManager.hash_serializer.Serialize(local_input));
		
		if(_debug_overlay)
		{
			_debug_overlay.AddMessage(peer_id, "%Rollback s %s ticks" % [tick, SyncManager.rollback_ticks]);
	
		}
	}
	
	public void _OnSyncManagerRollbackFlagged(int tick)
	{  
		Print ("-----");
		Print ("Rolling back to tick %s (rollback %s Tick(s))" % [tick, SyncManager.rollback_ticks]);
	
	}
	
	public void _OnSyncManagerRemoteStateMismatch(int tick, int peer_id, int local_hash, int remote_hash)
	{  
		Print ("-----");
		Print ("On tick %s, remote void State (%s) from %s doesn't match local State (%s)" % [tick, remote_hash, peer_id, local_hash]);
		
		if(_debug_overlay)
		{
			_debug_overlay.AddMessage(peer_id, "%State s mismatch" % tick);
	
		}
	}
	
	public void _OnSyncManagerPeerPingedBack(SyncManager peer.Peer)
	{  
		Print ("-----");
		Print ("Peer %RTT s %s ms | local lag %s | remote lag %s | advantage %s" % [peer.peer_id, peer.rtt, peer.local_lag, peer.remote_lag, peer.calculated_advantage]);
		if(_debug_overlay)
		{
			_debug_overlay.UpdatePeer(peer);
	
		}
	}
	
	public void _OnSyncManagerStateLoaded(int rollback_ticks)
	{  
	
	}
	
	public void _OnSyncManagerTickFinished(bool is_rollback)
	{  
	
	}
	
	public void _UnhandledInput(InputEvent event)
	{  
		var action_pressed = event.IsActionPressed("sync_debug");
		if(action_pressed)
		{
			if(!_debug_pressed)
			{
				_debug_pressed = true;
				ShowDebugOverlay(!is_debug_overlay_shown());
			}
		}
		else
		{
			_debug_pressed = false;
	
	
		}
	}
	
	
	
}