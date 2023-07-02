
using System;
using Godot;
using Dictionary = Godot.Collections.Dictionary;
using Array = Godot.Collections.Array;

[Tool]
public class FrameViewer : Control
{
	 
	public const var Logger = GD.Load("res://addons/godot-rollback-netcode/Logger.gd");
	public const var ReplayServer = GD.Load("res://addons/godot-rollback-netcode/log_inspector/ReplayServer.gd");
	public const var LogData = GD.Load("res://addons/godot-rollback-netcode/log_inspector/LogData.gd");
	
	public onready var time_field = GetNode("VBoxContainer/HBoxContainer/Time");
	public onready var seek_on_replay_peer_field = GetNode("VBoxContainer/HBoxContainer/SeekOnReplayPeerField");
	public onready var auto_replay_to_current_field = GetNode("VBoxContainer/HBoxContainer/ReplayContainer/HBoxContainer/AutoReplayToCurrentField");
	public onready var replay_to_current_button = GetNode("VBoxContainer/HBoxContainer/ReplayContainer/HBoxContainer/ReplayToCurrentButton");
	public onready var data_graph = GetNode("VBoxContainer/VSplitContainer/DataGraph");
	public onready var data_grid = GetNode("VBoxContainer/VSplitContainer/DataGrid");
	public onready var settings_dialog = GetNode("SettingsDialog");
	
	public LogData log_data
	public ReplayServer replay_server
	public int replay_peer_id
	public int replay_frame = -1;
	public int replay_last_interpolation_frame_time = 0;
	
	public Dictionary current_frames  = new Dictionary(){};
	
	public void SetLogData(LogData _log_data)
	{  
		log_data = _log_data;
		data_graph.SetLogData(log_data);
		data_grid.SetLogData(log_data);
		settings_dialog.SetupSettingsDialog(log_data, data_graph, data_grid);
	
	}
	
	public void RefreshFromLogData()
	{  
		if(log_data.IsLoading())
		{
			return;
		
		}
		time_field.max_value = log_data.end_time - log_data.start_time;
		
		data_graph.RefreshFromLogData();
		data_grid.RefreshFromLogData();
		settings_dialog.RefreshFromLogData();
		
		replay_frame = -1;
		_OnTimeValueChanged(time_field.value);
	
	}
	
	public void SetReplayServer(ReplayServer _replay_server)
	{  
		if(replay_server != null)
		{
			replay_server.Disconnect("game_disconnected", this, "_on_replay_server_game_disconnected");
		
		}
		replay_server = _replay_server;
		
		if(replay_server)
		{
			replay_server.Connect("game_disconnected", this, "_on_replay_server_game_disconnected");
	
		}
	}
	
	public void _OnReplayServerGameDisconnected()
	{  
		replay_frame = -1;
	
	}
	
	public void SetReplayPeerId(int _replay_peer_id)
	{  
		replay_peer_id = _replay_peer_id;
	
	}
	
	public void RefreshReplay()
	{  
		replay_frame = -1;
		if(auto_replay_to_current_field.pressed)
		{
			ReplayToCurrentFrame();
	
		}
	}
	
	public void Clear()
	{  
		current_frames.Clear();
		RefreshFromLogData();
	
	}
	
	public void _OnTimeValueChanged(float value)
	{  
		if(log_data.IsLoading())
		{
			return;
		
		}
		var time  = (int)(value)
		
		// Update our tracking of the current frame.
		foreach(var peer_id in log_data.peer_ids)
		{
			LogData frame.FrameData = log_data.GetFrameByTime(peer_id, log_data.start_time + time);
			if(frame)
			{
				current_frames[peer_id] = frame.frame;
			}
			else
			{
				current_frames[peer_id] = 0;
		
			}
		}
		data_graph.cursor_time = time;
		data_grid.cursor_time = time;
		
		if(auto_replay_to_current_field.pressed)
		{
			ReplayToCurrentFrame();
	
		}
	}
	
	public void _OnPreviousFrameButtonPressed()
	{  
		JumpToPreviousFrame();
	
	}
	
	public void JumpToPreviousFrame()
	{  
		if(log_data.IsLoading())
		{
			return;
		
		}
		int frame_time  = 0;
		
		if(seek_on_replay_peer_field.pressed)
		{
			frame_time = _GetPreviousFrameTimeForPeer(replay_peer_id);
		}
		else
		{
			foreach(var peer_id in current_frames)
			{
				frame_time = (int)(Mathf.Max(frame_time, _GetPreviousFrameTimeForPeer(peer_id)))
		
			}
		}
		if(frame_time > log_data.start_time)
		{
			time_field.value = frame_time - log_data.start_time;
		}
		else
		{
			time_field.value = 0;
	
		}
	}
	
	public int _GetPreviousFrameTimeForPeer(int peer_id)
	{  
		var frame_id = current_frames[peer_id];
		if(frame_id > 0)
		{
			frame_id -= 1;
		}
		LogData frame.FrameData = log_data.GetFrame(peer_id, frame_id);
		return frame.start_time;
	
	}
	
	public void _OnNextFrameButtonPressed()
	{  
		JumpToNextFrame();
	
	}
	
	public void JumpToNextFrame()
	{  
		if(log_data.IsLoading())
		{
			return;
		
		}
		var frame_time  = log_data.end_time;
		
		if(seek_on_replay_peer_field.pressed)
		{
			frame_time = _GetNextFrameTimeForPeer(replay_peer_id);
		}
		else
		{
			foreach(var peer_id in current_frames)
			{
				var peer_frame_time = _GetNextFrameTimeForPeer(peer_id);
				if(peer_frame_time != 0)
				{
					frame_time = (int)(Mathf.Min(frame_time, _GetNextFrameTimeForPeer(peer_id)))
		
				}
			}
		}
		if(frame_time > log_data.start_time)
		{
			time_field.value = frame_time - log_data.start_time;
		}
		else
		{
			time_field.value = 0;
	
		}
	}
	
	public int _GetNextFrameTimeForPeer(int peer_id)
	{  
		var frame_id = current_frames[peer_id];
		if(frame_id < log_data.GetFrameCount(peer_id) - 1)
		{
			frame_id += 1;
			LogData frame.FrameData = log_data.GetFrame(peer_id, frame_id);
			return frame.start_time;
		}
		return 0;
	
	}
	
	public void ReplayToCurrentFrame()
	{  
		if(!replay_server && !replay_server.IsConnectedToGame())
		{
			return;
		}
		if(log_data.IsLoading())
		{
			return;
		}
		if(log_data.peer_ids.Size() == 0)
		{
			return;
		}
		if(!current_frames.Has(replay_peer_id))
		{
			return;
		
		}
		int current_frame_id = current_frames[replay_peer_id];
		
		// If replay_frame is ahead of current frame, we have to replay from the beginning.
		if(replay_frame > current_frame_id)
		{
			replay_frame = -1;
		
		// Reset replay.
		}
		if(replay_frame == -1)
		{
			replay_last_interpolation_frame_time = 0;
			replay_server.SendMatchInfo(log_data, replay_peer_id);
		
		}
		replay_frame += 1;
		foreach(var frame_id in GD.Range(replay_frame, log_data.frames[replay_peer_id].Size()))
		{
			if(frame_id > current_frame_id)
			{
				break;
			}
			LogData frame_data.FrameData = log_data.GetFrame(replay_peer_id, frame_id);
			_SendReplayFrameData(frame_data);
		
		}
		replay_frame = current_frame_id;
	
	}
	
	public void _SendReplayFrameData(LogData frame_data.FrameData)
	{  
		int frame_type = frame_data.data["frame_type"];
		
		Dictionary msg  = new Dictionary(){
			type = "execute_frame",
			frame_type = frame_type,
			rollback_ticks = frame_data.data.Get("rollback_ticks", 0),
		};
		
		Dictionary input_frames_received  = new Dictionary(){};
		
		if(frame_type == Logger.FrameType.TICK)
		{
			var tick = (int)(frame_data.data["tick"])
			if(tick > 0)
			{
				// Get input for local peer.
				input_frames_received[replay_peer_id] = new Dictionary(){
					tick: log_data.input[tick].GetInputForPeer(replay_peer_id, replay_peer_id),
				};
			}
			replay_last_interpolation_frame_time = frame_data.data["end_time"];
		}
		else if(frame_type == Logger.FrameType.INTERPOLATION_FRAME)
		{
			var start_time = frame_data.data["start_time"];
			if(replay_last_interpolation_frame_time > 0)
			{
				msg["delta"] = (start_time - replay_last_interpolation_frame_time) / 1000.0;
			}
			else
			{
				// If we can't know the actual delta, let's use a small value that's
				// bigger than zero, arbitrarily 1.0/120.0
				msg["delta"] = 0.00833333;
			}
			replay_last_interpolation_frame_time = start_time;
		
		// Get input received from each of the peers.
		}
		foreach(var peer_id in log_data.peer_ids)
		{
			Array ticks = frame_data.data.Get("remote_ticks_received_from_%s" % peer_id, new Array(){});
			if(ticks.Size() > 0)
			{
				Dictionary peer_input_ticks  = new Dictionary(){};
				foreach(var tick in ticks)
				{
					tick = (int)(tick)
					peer_input_ticks[tick] = log_data.input[tick].GetInputForPeer(peer_id, replay_peer_id);
				}
				input_frames_received[peer_id] = peer_input_ticks;
			}
		}
		msg["input_frames_received"] = input_frames_received;
		
		replay_server.SendMessage(msg);
	
	}
	
	public void _UnhandledKeyInput(InputEventKey event)
	{  
		if(event.pressed)
		{
			if(event.scancode == KEY_PAGEUP)
			{
				JumpToNextFrame();
			}
			else if(event.scancode == KEY_PAGEDOWN)
			{
				JumpToPreviousFrame();
			}
			else if(event.scancode == KEY_UP)
			{
				time_field.value += 1;
			}
			else if(event.scancode == KEY_DOWN)
			{
				time_field.value -= 1;
	
			}
		}
	}
	
	public void _OnStartButtonPressed()
	{  
		time_field.value = 0;
	
	}
	
	public void _OnEndButtonPressed()
	{  
		time_field.value = time_field.max_value;
	
	}
	
	public void _OnDataGraphCursorTimeChanged(__TYPE cursor_time)
	{  
		time_field.value = cursor_time;
	
	}
	
	public void _OnSettingsButtonPressed()
	{  
		settings_dialog.PopupCentered();
	
	}
	
	public void _OnReplayToCurrentButtonPressed()
	{  
		ReplayToCurrentFrame();
	
	}
	
	public void _OnAutoReplayToCurrentFieldToggled(bool button_pressed)
	{  
		replay_to_current_button.disabled = button_pressed;
		if(button_pressed)
		{
			ReplayToCurrentFrame();
	
	
		}
	}
	
	
	
}