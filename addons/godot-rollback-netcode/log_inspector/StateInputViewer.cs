
using System;
using Godot;
using Dictionary = Godot.Collections.Dictionary;
using Array = Godot.Collections.Array;

[Tool]
public class StateInputViewer : VBoxContainer
{
	 
	public const var LogData = GD.Load("res://addons/godot-rollback-netcode/log_inspector/LogData.gd");
	public const var ReplayServer = GD.Load("res://addons/godot-rollback-netcode/log_inspector/ReplayServer.gd");
	public const var DebugStateComparer = GD.Load("res://addons/godot-rollback-netcode/DebugStateComparer.gd");
	
	public const string JSON_INDENT = "    ";
	
	public onready var tick_number_field = GetNode("HBoxContainer/TickNumber");
	public onready var input_data_tree = GetNode("GridContainer/InputPanel/InputDataTree");
	public onready var input_mismatches_data_tree = GetNode("GridContainer/InputMismatchesPanel/InputMismatchesDataTree");
	public onready var state_data_tree = GetNode("GridContainer/StatePanel/StateDataTree");
	public onready var state_mismatches_data_tree = GetNode("GridContainer/StateMismatchesPanel/StateMismatchesDataTree");
	
	public LogData log_data
	public ReplayServer replay_server
	public int replay_peer_id
	
	public void _Ready()
	{  
		foreach(var tree in [input_mismatches_data_tree, state_mismatches_data_tree])
		{
			tree.SetColumnTitle(1, "Local");
			tree.SetColumnTitle(2, "Remote");
			tree.SetColumnTitlesVisible(true);
	
		}
	}
	
	public void SetLogData(LogData _log_data)
	{  
		log_data = _log_data;
	
	}
	
	public void SetReplayServer(ReplayServer _replay_server)
	{  
		replay_server = _replay_server;
	
	}
	
	public void SetReplayPeerId(int _replay_peer_id)
	{  
		replay_peer_id = _replay_peer_id;
	
	}
	
	public void RefreshFromLogData()
	{  
		tick_number_field.max_value = log_data.max_tick;
		_OnTickNumberValueChanged(tick_number_field.value);
	
	}
	
	public void RefreshReplay()
	{  
		if(log_data.IsLoading())
		{
			return;
		
		}
		if(replay_server && replay_server.IsConnectedToGame())
		{
			int tick = (int)(tick_number_field.value)
			LogData state_frame.StateData = log_data.state.Get(tick, null);
			if(state_frame)
			{
				Dictionary state_data
				if(state_frame.mismatches.Has(replay_peer_id))
				{
					state_data = state_frame.mismatches[replay_peer_id];
				}
				else
				{
					state_data = state_frame.state;
				
				}
				replay_server.SendMessage(new Dictionary(){
					type = "load_state",
					state = state_data,
				});
	
			}
		}
	}
	
	public void Clear()
	{  
		tick_number_field.max_value = 0;
		tick_number_field.value = 0;
		_ClearTrees();
	
	}
	
	public void _ClearTrees()
	{  
		input_data_tree.Clear();
		input_mismatches_data_tree.Clear();
		state_data_tree.Clear();
		state_mismatches_data_tree.Clear();
	
	}
	
	public void _OnTickNumberValueChanged(float value)
	{  
		if(log_data.IsLoading())
		{
			return;
		
		}
		int tick = (int)(value)
		
		LogData input_frame.InputData = log_data.input.Get(tick, null);
		LogData state_frame.StateData = log_data.state.Get(tick, null);
		
		_ClearTrees();
		
		if(input_frame)
		{
			_CreateTreeItemsFromDictionary(input_data_tree, input_data_tree.CreateItem(), input_frame.input);
			_CreateTreeFromMismatches(input_mismatches_data_tree, input_frame.input, input_frame.mismatches);
		
		}
		if(state_frame)
		{
			_CreateTreeItemsFromDictionary(state_data_tree, state_data_tree.CreateItem(), state_frame.state);
			_CreateTreeFromMismatches(state_mismatches_data_tree, state_frame.state, state_frame.mismatches);
		
		}
		RefreshReplay();
	
	}
	
	public Dictionary _ConvertArrayToDictionary(Array a)
	{  
		Dictionary d  = new Dictionary(){};
		foreach(var i in GD.Range(a.Size()))
		{
			d[i] = a[i];
		}
		return d;
	
	}
	
	public void _CreateTreeItemsFromDictionary(Tree tree, TreeItem parent_item, Dictionary data, int data_column = 1)
	{  
		foreach(var key in data)
		{
			var value = data[key];
			
			var item = tree.CreateItem(parent_item);
			item.SetText(0, GD.Str(key));
			
			if(value is Dictionary)	
			{
				_CreateTreeItemsFromDictionary(tree, item, value);
			}
			else if(value is Array)
			{
				_CreateTreeItemsFromDictionary(tree, item, _ConvertArrayToDictionary(value));
			}
			else
			{
				item.SetText(data_column, GD.Str(value));
			
			}
			if(key is String && key.BeginsWith("/root/SyncManager/"))
			{
				item.collapsed = true;
	
			}
		}
	}
	
	public void _CreateTreeFromMismatches(Tree tree, Dictionary data, Dictionary mismatches)
	{  
		if(mismatches.Size() == 0)
		{
			return;
		
		}
		var root = tree.CreateItem();
		foreach(var peer_id in mismatches)
		{
			var peer_data = mismatches[peer_id];
			
			var peer_item = tree.CreateItem(root);
			peer_item.SetText(0, "Peer %s" % peer_id);
			
			var comparer = new DebugStateComparer()
			comparer.FindMismatches(data, peer_data);
			
			foreach(var mismatch in comparer.mismatches)
			{
				var mismatch_item = tree.CreateItem(peer_item);
				mismatch_item.SetExpandRight(0, true);
				mismatch_item.SetExpandRight(1, true);
				
				switch( mismatch.type)
				{
					case DebugStateComparer.MismatchType.MISSING:
						mismatch_item.SetText(0, "[MISSING] %s" % mismatch.path);
						
						if(mismatch.local_state is Dictionary)
						{
							_CreateTreeItemsFromDictionary(tree, mismatch_item, mismatch.local_state);
						}
						else if(mismatch.local_state is Array)
						{
							_CreateTreeItemsFromDictionary(tree, mismatch_item, _ConvertArrayToDictionary(mismatch.local_state));
						}
						else
						{
							var child = tree.CreateItem(mismatch_item);
							child.SetText(1, JSON.Print(mismatch.local_state, JSON_INDENT));
					
						}
						break;
					case DebugStateComparer.MismatchType.EXTRA:
						mismatch_item.SetText(0, "[EXTRA] %s" % mismatch.path);
						
						if(mismatch.remote_state is Dictionary)
						{
							_CreateTreeItemsFromDictionary(tree, mismatch_item, mismatch.remote_state, 2);
						}
						else if(mismatch.remote_state is Array)
						{
							_CreateTreeItemsFromDictionary(tree, mismatch_item, _ConvertArrayToDictionary(mismatch.remote_state), 2);
						}
						else
						{
							var child = tree.CreateItem(mismatch_item);
							child.SetText(2, JSON.Print(mismatch.remote_state, JSON_INDENT));
					
						}
						break;
					case DebugStateComparer.MismatchType.REORDER:
						mismatch_item.SetText(0, "[REORDER] %s" % mismatch.path);
						
						foreach(var i in GD.Range(Mathf.Max(mismatch.local_state.Size(), mismatch.remote_state.Size())))
						{
							var order_item = tree.CreateItem(mismatch_item);
							if(i < mismatch.local_state.Size())
							{
								order_item.SetText(1, mismatch.local_state[i]);
							}
							if(i < mismatch.remote_state.Size())
							{
								order_item.SetText(2, mismatch.remote_state[i]);
					
							}
						}
						break;
					case DebugStateComparer.MismatchType.DIFFERENCE:
						mismatch_item.SetText(0, "[DIFF] %s" % mismatch.path);
						
						var child = tree.CreateItem(mismatch_item);
						child.SetText(1, JSON.Print(mismatch.local_state, JSON_INDENT));
						child.SetText(2, JSON.Print(mismatch.remote_state, JSON_INDENT));
	
						break;
				}
			}
		}
	}
	
	public void _OnPreviousMismatchButtonPressed()
	{  
		if(log_data.IsLoading())
		{
			return;
		
		}
		var current_tick  = (int)(tick_number_field.value)
		int previous_mismatch  = -1;
		foreach(var mismatch_tick in log_data.mismatches)
		{
			if(mismatch_tick < current_tick)
			{
				previous_mismatch = mismatch_tick;
			}
			else
			{
				break;
			}
		}
		if(previous_mismatch != -1)
		{
			tick_number_field.value = previous_mismatch;
	
		}
	}
	
	public void _OnNextMismatchButtonPressed()
	{  
		if(log_data.IsLoading())
		{
			return;
		
		}
		var current_tick  = (int)(tick_number_field.value)
		int next_mismatch  = -1;
		foreach(var mismatch_tick in log_data.mismatches)
		{
			if(mismatch_tick > current_tick)
			{
				next_mismatch = mismatch_tick;
				break;
			}
		}
		if(next_mismatch != -1)
		{
			tick_number_field.value = next_mismatch;
	
		}
	}
	
	public void _OnStartButtonPressed()
	{  
		tick_number_field.value = 0;
	
	}
	
	public void _OnEndButtonPressed()
	{  
		tick_number_field.value = tick_number_field.max_value;
	
	
	}
	
	
	
}