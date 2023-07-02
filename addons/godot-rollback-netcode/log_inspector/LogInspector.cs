
using System;
using Godot;
using Dictionary = Godot.Collections.Dictionary;
using Array = Godot.Collections.Array;

[Tool]
public class LogInspector : Control
{
	 
	public const var LogData = GD.Load("res://addons/godot-rollback-netcode/log_inspector/LogData.gd");
	public const var ReplayServer = GD.Load("res://addons/godot-rollback-netcode/log_inspector/ReplayServer.gd");
	
	public onready var file_dialog = GetNode("FileDialog");
	public onready var progress_dialog = GetNode("ProgressDialog");
	public onready var data_description_label = GetNode("MarginContainer/VBoxContainer/LoadToolbar/DataDescriptionLabel");
	public onready var data_description_label_default_text = data_description_label.text;
	public onready var mode_button = GetNode("MarginContainer/VBoxContainer/LoadToolbar/ModeButton");
	public onready var state_input_viewer = GetNode("MarginContainer/VBoxContainer/StateInputViewer");
	public onready var frame_viewer = GetNode("MarginContainer/VBoxContainer/FrameViewer");
	public onready var replay_server = GetNode("ReplayServer");
	public onready var replay_server_status_label = GetNode("MarginContainer/VBoxContainer/ReplayToolbar/ServerContainer/HBoxContainer/ReplayStatusLabel");
	public onready var start_server_button = GetNode("MarginContainer/VBoxContainer/ReplayToolbar/ServerContainer/HBoxContainer/StartServerButton");
	public onready var stop_server_button = GetNode("MarginContainer/VBoxContainer/ReplayToolbar/ServerContainer/HBoxContainer/StopServerButton");
	public onready var disconnect_button = GetNode("MarginContainer/VBoxContainer/ReplayToolbar/ServerContainer/HBoxContainer/DisconnectButton");
	public onready var launch_game_button = GetNode("MarginContainer/VBoxContainer/ReplayToolbar/ClientContainer/HBoxContainer/LaunchGameButton");
	public onready var show_peer_field = GetNode("MarginContainer/VBoxContainer/ReplayToolbar/ClientContainer/HBoxContainer/ShowPeerField");
	
	enum DataMode {
		STATE_INPUT,
		FRAME,
	}
	
	public const string LOADING_LABEL := "Loading %s..."
	
	public LogData log_data = new LogData()
	
	public Array _files_to_load  = new Array(){};
	
	public void _Ready()
	{  
		state_input_viewer.SetLogData(log_data);
		frame_viewer.SetLogData(log_data);
		
		log_data.Connect("load_error", this, "_on_log_data_load_error");
		log_data.Connect("load_progress", this, "_on_log_data_load_progress");
		log_data.Connect("load_finished", this, "_on_log_data_load_finished");
		log_data.Connect("data_updated", this, "refresh_from_log_data");
		
		state_input_viewer.SetReplayServer(replay_server);
		frame_viewer.SetReplayServer(replay_server);
		
		file_dialog.current_dir = OS.GetUserDataDir() + "/detailed_logs/";
		
		// Show && make full screen if the scene is being run on its own.
		if(GetParent() == GetTree().root)
		{
			visible = true;
			anchor_right = 1;
			anchor_bottom = 1;
			margin_right = 0;
			margin_bottom = 0;
			StartLogInspector();
	
		}
	}
	
	public void _OnLogInspectorAboutToShow()
	{  
		StartLogInspector();
	
	}
	
	public void StartLogInspector()
	{  
		UpdateReplayServerStatus();
		replay_server.StartListening();
	
	}
	
	public void SetEditorInterface(__TYPE editor_interface)
	{  
		replay_server.SetEditorInterface(editor_interface);
	
	}
	
	public void _OnClearButtonPressed()
	{  
		if(log_data.IsLoading())
		{
			return;
		
		}
		log_data.Clear();
		data_description_label.text = data_description_label_default_text;
		state_input_viewer.Clear();
		frame_viewer.Clear();
	
	}
	
	public void _OnAddLogButtonPressed()
	{  
		file_dialog.current_file = "";
		file_dialog.current_path = "";
		file_dialog.ShowModal();
		file_dialog.Invalidate();
	
	}
	
	public void _OnFileDialogFilesSelected(PoolStringArray paths)
	{  
		if(paths.Size() > 0)
		{
			bool already_loading = (_files_to_load.Size() > 0) || log_data.IsLoading();
			foreach(var path in paths)
			{
				_files_to_load.Append(path);
			}
			if(!already_loading)
			{
				var first_file = _files_to_load.PopFront();
				progress_dialog.SetLabel(LOADING_LABEL % first_file.GetFile());
				progress_dialog.PopupCentered();
				log_data.LoadLogFile(first_file);
	
			}
		}
	}
	
	public void RefreshFromLogData()
	{  
		if(log_data.IsLoading())
		{
			return;
		
		}
		data_description_label.text = "%s Logs (peer ids: %s) && %s ticks" % [log_data.peer_ids.Size(), log_data.peer_ids, log_data.max_tick]
		if(log_data.mismatches.Size() > 0)
		{
			data_description_label.text += " with %s mismatches" % log_data.mismatches.Size();
		
		}
		show_peer_field.Clear();
		foreach(var peer_id in log_data.peer_ids)
		{
			show_peer_field.AddItem("Peer %s" % peer_id, peer_id);
		
		}
		RefreshReplay();
		state_input_viewer.RefreshFromLogData();
		frame_viewer.RefreshFromLogData();
	
	}
	
	public void _OnLogDataLoadError(__TYPE msg)
	{  
		progress_dialog.Hide();
		_files_to_load.Clear();
		OS.Alert(msg);
	
	}
	
	public void _OnLogDataLoadProgress(__TYPE current, __TYPE total)
	{  
		progress_dialog.UpdateProgress(current, total);
	
	}
	
	public void _OnLogDataLoadFinished()
	{  
		if(_files_to_load.Size() > 0)
		{
			var next_file = _files_to_load.PopFront();
			progress_dialog.SetLabel(LOADING_LABEL % next_file.GetFile());
			log_data.LoadLogFile(next_file);
		}
		else
		{
			progress_dialog.Hide();
	
		}
	}
	
	public void _OnModeButtonItemSelected(int index)
	{  
		state_input_viewer.visible = false;
		frame_viewer.visible = false;
		
		if(index == DataMode.STATE_INPUT)
		{
			state_input_viewer.visible = true;
		}
		else if(index == DataMode.FRAME)
		{
			frame_viewer.visible = true;
		
		}
		RefreshReplay();
	
	}
	
	public void _OnStartServerButtonPressed()
	{  
		replay_server.StartListening();
	
	}
	
	public void _OnStopServerButtonPressed()
	{  
		if(replay_server.IsConnectedToGame())
		{
			replay_server.DisconnectFromGame(false);
		}
		else
		{
			replay_server.StopListening();
	
		}
	}
	
	public void UpdateReplayServerStatus()
	{  
		switch( replay_server.GetStatus())
		{
			case ReplayServer.Status.NONE:
				replay_server_status_label.text = "Disabled.";
				start_server_button.disabled = false;
				stop_server_button.disabled = true;
				disconnect_button.disabled = true;
				launch_game_button.disabled = true;
				break;
			case ReplayServer.Status.LISTENING:
				replay_server_status_label.text = "Listening for connections...";
				start_server_button.disabled = true;
				stop_server_button.disabled = false;
				disconnect_button.disabled = true;
				launch_game_button.disabled = false;
				break;
			case ReplayServer.Status.CONNECTED:
				replay_server_status_label.text = "Connected to game.";
				start_server_button.disabled = true;
				stop_server_button.disabled = false;
				disconnect_button.disabled = false;
				launch_game_button.disabled = true;
	
				break;
		}
	}
	
	public void RefreshReplay()
	{  
		var replay_peer_id = show_peer_field.GetSelectedId();
		
		if(replay_server)
		{
			replay_server.SendMatchInfo(log_data, replay_peer_id);
		
		}
		state_input_viewer.SetReplayPeerId(replay_peer_id);
		frame_viewer.SetReplayPeerId(replay_peer_id);
		
		var mode = mode_button.selected;
		if(mode == DataMode.STATE_INPUT)
		{
			state_input_viewer.RefreshReplay();
		}
		else if(mode == DataMode.FRAME)
		{
			frame_viewer.RefreshReplay();
	
		}
	}
	
	public void _OnReplayServerStartedListening()
	{  
		UpdateReplayServerStatus();
	
	}
	
	public void _OnReplayServerStoppedListening()
	{  
		UpdateReplayServerStatus();
	
	}
	
	public void _OnReplayServerGameConnected()
	{  
		UpdateReplayServerStatus();
		RefreshReplay();
	
	}
	
	public void _OnReplayServerGameDisconnected()
	{  
		UpdateReplayServerStatus();
	
	}
	
	public void _OnLaunchGameButtonPressed()
	{  
		replay_server.LaunchGame();
	
	}
	
	public void _OnDisconnectButtonPressed()
	{  
		replay_server.DisconnectFromGame();
	
	}
	
	public void _OnShowPeerFieldItemSelected(int index)
	{  
		RefreshReplay();
	
	
	}
	
	
	
}