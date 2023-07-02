
using System;
using Godot;
using Dictionary = Godot.Collections.Dictionary;
using Array = Godot.Collections.Array;

[Tool]
public class FrameViewerSettingsDialog : WindowDialog
{
	 
	public const var LogData = GD.Load("res://addons/godot-rollback-netcode/log_inspector/LogData.gd");
	public const var DataGraph = GD.Load("res://addons/godot-rollback-netcode/log_inspector/FrameDataGraph.gd");
	public const var DataGrid = GD.Load("res://addons/godot-rollback-netcode/log_inspector/FrameDataGrid.gd");
	public const var TimeOffsetSetting = GD.Load("res://addons/godot-rollback-netcode/log_inspector/FrameViewerTimeOffsetSetting.tscn");
	
	public onready var show_network_arrows_field  = GetNode("MarginContainer/GridContainer/ShowNetworkArrows");
	public onready var network_arrows_peer1_field  = GetNode("MarginContainer/GridContainer/NetworkArrowsPeer1");
	public onready var network_arrows_peer2_field  = GetNode("MarginContainer/GridContainer/NetworkArrowsPeer2");
	public onready var show_rollback_ticks_field = GetNode("MarginContainer/GridContainer/ShowRollbackTicks");
	public onready var max_rollback_ticks_field = GetNode("MarginContainer/GridContainer/MaxRollbackTicks");
	public onready var time_offset_container = GetNode("MarginContainer/GridContainer/TimeOffsetContainer");
	
	public LogData log_data
	public DataGraph data_graph
	public DataGrid data_grid
	
	public void SetupSettingsDialog(LogData _log_data, DataGraph _data_graph, __TYPE _data_grid)
	{  
		log_data = _log_data;
		data_graph = _data_graph;
		data_grid = _data_grid;
		RefreshFromLogData();
	
	}
	
	public void RefreshFromLogData()
	{  
		_RebuildPeerOptions(network_arrows_peer1_field);
		_RebuildPeerOptions(network_arrows_peer2_field);
		_RebuildPeerTimeOffsetFields();
		
		show_network_arrows_field.pressed = data_graph.canvas.show_network_arrows;
		var network_arrow_peers = data_graph.canvas.network_arrow_peers.Duplicate();
		network_arrow_peers.Sort();
		if(network_arrow_peers.Size() > 0)
		{
			network_arrows_peer1_field.Select(network_arrows_peer1_field.GetItemIndex(network_arrow_peers[0]));
		}
		if(network_arrow_peers.Size() > 1)
		{
			network_arrows_peer2_field.Select(network_arrows_peer2_field.GetItemIndex(network_arrow_peers[1]));
		
		}
		show_rollback_ticks_field.pressed = data_graph.canvas.show_rollback_ticks;
		max_rollback_ticks_field.text = GD.Str(data_graph.canvas.max_rollback_ticks);
	
	}
	
	public void _RebuildPeerOptions(OptionButton option_button)
	{  
		var value = option_button.GetSelectedId();
		option_button.Clear();
		foreach(var peer_id in log_data.peer_ids)
		{
			option_button.AddItem("Peer %s" % peer_id, peer_id);
		}
		if(option_button.GetSelectedId() != value)
		{
			option_button.Select(option_button.GetItemIndex(value));
	
		}
	}
	
	public void _RebuildPeerTimeOffsetFields()
	{  
		// Remove all the old Fields (disconnect signals).
		foreach(var child in time_offset_container.GetChildren())
		{
			child.Disconnect("time_offset_changed", this, "_on_peer_time_offset_changed");
			time_offset_container.RemoveChild(child);
			child.QueueFree();
		
		// Re-create new fields && connect the signals.
		}
		foreach(var peer_id in log_data.peer_ids)
		{
			var child = TimeOffsetSetting.Instance();
			child.name = GD.Str(peer_id);
			time_offset_container.AddChild(child);
			child.SetupTimeOffsetSetting("Peer %s" % peer_id, log_data.peer_time_offsets[peer_id]);
			child.Connect("time_offset_changed", this, "_on_peer_time_offset_changed", new Array(){peer_id});
	
		}
	}
	
	public void _OnPeerTimeOffsetChanged(__TYPE value, __TYPE peer_id)
	{  
		log_data.SetPeerTimeOffset(peer_id, value);
	
	}
	
	public void UpdateNetworkArrows()
	{  
		if(show_network_arrows_field.pressed)
		{
			if(network_arrows_peer1_field.GetSelectedId() != network_arrows_peer2_field.GetSelectedId())
			{
				data_graph.canvas.show_network_arrows = true;
				data_graph.canvas.network_arrow_peers = new Array(){
					network_arrows_peer1_field.GetSelectedId(),
					network_arrows_peer2_field.GetSelectedId(),
				};
				data_graph.canvas.Update();
			}
		}
		else
		{
			data_graph.canvas.show_network_arrows = false;
			data_graph.canvas.Update();
	
		}
	}
	
	public void _OnShowNetworkArrowsToggled(bool button_pressed)
	{  
		UpdateNetworkArrows();
	
	}
	
	public void _OnNetworkArrowsPeer1ItemSelected(int index)
	{  
		UpdateNetworkArrows();
	
	}
	
	public void _OnNetworkArrowsPeer2ItemSelected(int index)
	{  
		UpdateNetworkArrows();
	
	}
	
	public void _OnShowRollbackTicksPressed()
	{  
		data_graph.canvas.show_rollback_ticks = show_rollback_ticks_field.pressed;
		data_graph.canvas.Update();
	
	}
	
	public void _OnMaxRollbackTicksTextChanged(String new_text)
	{  
		var value = max_rollback_ticks_field.text;
		if(value.IsValidInteger())
		{
			var value_int = value.ToInt();
			if(value_int > 0)
			{
				data_graph.canvas.max_rollback_ticks = value_int;
				data_graph.canvas.Update();
	
	
			}
		}
	}
	
	
	
}