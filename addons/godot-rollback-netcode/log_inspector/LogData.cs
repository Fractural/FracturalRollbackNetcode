
using System;
using Godot;
using Dictionary = Godot.Collections.Dictionary;
using Array = Godot.Collections.Array;

[Tool]
public class LogData : Reference
{
	 
	public const var Logger = GD.Load("res://addons/godot-rollback-netcode/Logger.gd");
	
	public class StateData:
		int tick
		Dictionary state
		int state_hash
		Dictionary mismatches  = new Dictionary(){};
		
		public void _Init(int _tick, Dictionary _state)
		{	  
			tick = _tick;
			state = _state;
			state_hash = _state["$"];
		
		}
	
		public bool CompareState(int peer_id, Dictionary peer_state)
		{	  
			if(state_hash == peer_state["$"])
			{
				return true;
			
			}
			mismatches[peer_id] = peer_state;
			return false;
	
		}
	
	public class InputData:
		int tick
		Dictionary input
		int input_hash
		Dictionary mismatches  = new Dictionary(){};
		
		public void _Init(int _tick, Dictionary _input)
		{	  
			tick = _tick;
			input = SortDictionary(_input);
			input_hash = input.Hash();
		
		}
	
		public Dictionary SortDictionary(Dictionary d)
		{	  
			var keys = d.Keys();
			keys.Sort();
			
			Dictionary ret  = new Dictionary(){};
			foreach(var key in keys)
			{
				var val = d[key];
				if(val is Dictionary)
				{
					val = SortDictionary(val);
				}
				ret[key] = val;
			
			}
			return ret;
		
		}
	
		public bool CompareInput(int peer_id, Dictionary peer_input)
		{	  
			var sorted_peer_input = SortDictionary(peer_input);
			if(sorted_peer_input.Hash() == input_hash)
			{
				return true;
			
			}
			mismatches[peer_id] = sorted_peer_input;
			return false;
		
		}
	
		public Dictionary GetInputForPeer(int peer_id, int according_to_peer_id = -1)
		{	  
			if(according_to_peer_id != -1 && mismatches.Has(according_to_peer_id))
			{
				return mismatches[according_to_peer_id].Get(peer_id, new Dictionary(){});
			}
			return input.Get(peer_id, new Dictionary(){});
	
		}
	
	public class FrameData:
		int frame
		int type
		Dictionary data
		int start_time
		int end_time
		
		public void _Init(int _frame, int _type, Dictionary _data)
		{	  
			frame = _frame;
			type = _type;
			data = _data;
		
		}
	
		public FrameData CloneWithOffset(int offset)
		{	  
			var clone = FrameData.new(frame, type, data)
			clone.start_time = start_time + offset;
			clone.end_time = end_time + offset;
			return clone;
	
		}
	
	public Array peer_ids  = new Array(){};
	public Array mismatches  = new Array(){};
	public int max_tick  = 0;
	public int max_frame  = 0;
	public Dictionary frame_counter  = new Dictionary(){};
	public int start_time
	public int end_time
	
	public Dictionary match_info  = new Dictionary(){};
	public Dictionary input  = new Dictionary(){};
	public Dictionary state  = new Dictionary(){};
	public Dictionary frames  = new Dictionary(){};
	
	public Dictionary peer_time_offsets  = new Dictionary(){};
	public Dictionary peer_start_times  = new Dictionary(){};
	public Dictionary peer_end_times  = new Dictionary(){};
	
	public bool _is_loading  = false;
	public Thread _loader_thread
	public Mutex _loader_mutex
	
	[Signal] delegate void LoadProgress (current, total);
	[Signal] delegate void LoadFinished ();
	[Signal] delegate void LoadError (msg);
	[Signal] delegate void DataUpdated ();
	
	public void _Init()
	{  
		_loader_mutex = new Mutex()
	
	}
	
	public void Clear()
	{  
		if(IsLoading())
		{
			GD.PushError("Cannot Clear() log data while loading");
			return;
		
		}
		peer_ids.Clear();
		mismatches.Clear();
		max_tick = 0;
		max_frame = 0;
		start_time = 0;
		end_time = 0;
		match_info.Clear();
		input.Clear();
		state.Clear();
		frames.Clear();
		peer_time_offsets.Clear();
	
	}
	
	public void LoadLogFile(String path)
	{  
		if(IsLoading())
		{
			GD.PushError("Attempting to load log file when one is already loading");
			return;
		
		}
		var file = new File()
		if(file.OpenCompressed(path, File.READ, File.COMPRESSION_FASTLZ) != OK)
		{
			EmitSignal("load_error", "Unable to open file for reading: %s" % path);
			return;
		
		}
		if(_loader_thread)
		{
			_loader_thread.WaitToFinish();
		}
		_loader_thread = new Thread()
		
		_is_loading = true;
		_loader_thread.Start(this, "_loader_thread_function", new Array(){file, path});
	
	}
	
	public void _SetLoading(bool _value)
	{  
		_loader_mutex.Lock();
		_is_loading = _value;
		_loader_mutex.Unlock();
	
	}
	
	public bool IsLoading()
	{  
		bool value
		_loader_mutex.Lock();
		value = _is_loading;
		_loader_mutex.Unlock();
		return value;
	
	}
	
	public void _ThreadPrint(__TYPE msg)
	{  
		GD.Print(msg);
	
	}
	
	public void _LoaderThreadFunction(Array input)
	{  
		File file = input[0];
		String path = input[1];
		
		var header;
		var file_size = file.GetLen();
		
		while(!file.EofReached())
		{
			var data = file.get_var()
			if(data == null || !data is Dictionary)
			{
				continue;
			
			}
			if(header == null)
			{
				if(data["log_type"] == Logger.LogType.HEADER)
				{
					header = data;
					
					header["peer_id"] = (int)(header["peer_id"])
					if(header["peer_id"] in peer_ids)
					{
						file.Close();
						CallDeferred("emit_signal", "data_updated");
						CallDeferred("emit_signal", "load_error", "Log file has data for peer_id %s, which is already loaded" % header["peer_id"]);
						_SetLoading(false);
						return;
					
					}
					var header_match_info = header.Get("match_info", new Dictionary(){});
					if(match_info.Size() > 0 && match_info.Hash() != header_match_info.Hash())
					{
						file.Close();
						CallDeferred("emit_signal", "data_updated");
						CallDeferred("emit_signal", "load_error", "Log file for peer_id %s has match info that doesn"t match already loaded data\" % header["peer_id"]);
						_SetLoading(false);
						return;
					}
					else
					{
						match_info = header_match_info;
					
					}
					var peer_id = header["peer_id"];
					peer_ids.Append(peer_id);
					peer_time_offsets[peer_id] = 0;
					peer_start_times[peer_id] = 0;
					peer_end_times[peer_id] = 0;
					frame_counter[peer_id] = 0;
					frames[peer_id] = new Array(){};
					continue;
				}
				else
				{
					file.Close();
					CallDeferred(\"emit_signal\", \"data_updated\");
					CallDeferred(\"emit_signal\", \"load_error\", \"No header at the top of log: %s\" % path);
					_SetLoading(false);
					return;
			
				}
			}
			_AddLogEntry(data, header["peer_id"]);
			CallDeferred(\"emit_signal\", \"load_progress\", file.GetPosition(), file_size);
		
		}
		file.Close();
		_UpdateStartEndTimes();
		CallDeferred(\"emit_signal\", \"data_updated\");
		CallDeferred(\"emit_signal\", \"load_finished\");
		_SetLoading(false);
	
	}
	
	public void _AddLogEntry(Dictionary log_entry, int peer_id)
	{  
		int tick = log_entry.Get("tick", 0);
		
		max_tick = (int)(Mathf.Max(max_tick, tick))
		
		switch( log_entry["log_type"] as int)
		{
			case Logger.LogType.INPUT:
				InputData input_data
				if(!input.Has(tick))
				{
					input_data = InputData.new(tick, log_entry["input"])
					input[tick] = input_data;
				}
				else
				{
					input_data = input[tick];
					if(!input_data.CompareInput(peer_id, log_entry["input"]) && !tick in mismatches)
					{
						mismatches.Append(tick);
			
					}
				}
				break;
			case Logger.LogType.STATE:
				StateData state_data
				if(!state.Has(tick))
				{
					state_data = StateData.new(tick, log_entry["state"])
					state[tick] = state_data;
				}
				else
				{
					state_data = state[tick];
					if(!state_data.CompareState(peer_id, log_entry["state"]) && !tick in mismatches)
					{
						mismatches.Append(tick);
			
					}
				}
				break;
			case Logger.LogType.FRAME:
				log_entry.Erase("log_type");
				var frame_number = frame_counter[peer_id];
				var frame_data  = FrameData.new(frame_number, log_entry["frame_type"], log_entry)
				frames[peer_id].Append(frame_data);
				frame_counter[peer_id] += 1;
				max_frame = (int)(Mathf.Max(max_frame, frame_number))
				if(log_entry.Has("start_time"))
				{
					frame_data.start_time = log_entry["start_time"];
					var peer_start_time = peer_start_times[peer_id];
					peer_start_times[peer_id] = peer_start_time > 0 ? (int)(Mathf.Min(peer_start_time, frame_data.start_time)) : frame_data.start_time
				}
				if(log_entry.Has("end_time"))
				{
					frame_data.end_time = log_entry["end_time'];
				}
				else
				{
					frame_data.end_time = frame_data.start_time;
				}
				peer_end_times[peer_id] = (int)(Mathf.Max(peer_end_times[peer_id], frame_data.end_time))
	
				break;
		}
	}
	
	public void _UpdateStartEndTimes()
	{  
		int peer_id 
		
		peer_id = peer_ids[0];
		start_time = peer_start_times[peer_id] + peer_time_offsets[peer_id];
		foreach(var i in GD.Range(1, peer_ids.Size()))
		{
			peer_id = peer_ids[i];
			start_time = Mathf.Min(start_time, peer_start_times[peer_id] + peer_time_offsets[peer_id]);
		
		}
		peer_id = peer_ids[0];
		end_time = peer_end_times[peer_id] + peer_time_offsets[peer_id];
		foreach(var i in GD.Range(1, peer_ids.Size()))
		{
			peer_id = peer_ids[i];
			end_time = Mathf.Max(end_time, peer_end_times[peer_id] + peer_time_offsets[peer_id]);
	
		}
	}
	
	public void SetPeerTimeOffset(int peer_id, int offset)
	{  
		peer_time_offsets[peer_id] = offset;
		_UpdateStartEndTimes();
		CallDeferred("emit_signal", "data_updated");
	
	}
	
	public int GetFrameCount(int peer_id)
	{  
		if(IsLoading())
		{
			GD.PushError("Cannot GetFrame() while loading");
			return 0;
		
		}
		return frames[peer_id].Size();
	
	}
	
	public FrameData GetFrame(int peer_id, int frame_number)
	{  
		if(IsLoading())
		{
			GD.PushError("Cannot GetFrame() while loading");
			return null;
		
		}
		if(!frames.Has(peer_id))
		{
			return null;
		}
		if(frame_number >= frames[peer_id].Size())
		{
			return null;
		}
		var frame = frames[peer_id][frame_number];
		
		if(peer_time_offsets[peer_id] != 0)
		{
			return frame.CloneWithOffset(peer_time_offsets[peer_id]);
		
		}
		return frame;
	
	}
	
	public __TYPE GetFrameData(int peer_id, int frame_number, String key, __TYPE default_value = null)
	{  
		if(IsLoading())
		{
			GD.PushError("Cannot GetFrameData() while loading");
			return null;
		
		}
		var frame  = GetFrame(peer_id, frame_number);
		if(frame)
		{
			return frame.data.Get(key, default_value);
		}
		return default_value;
	
	}
	
	public FrameData GetFrameByTime(int peer_id, int time)
	{  
		if(IsLoading())
		{
			GD.PushError("Cannot GetFrameByTime() while loading");
			return null;
		
		}
		if(!frames.Has(peer_id))
		{
			return null;
		
		}
		Array peer_frames = frames[peer_id];
		int peer_time_offset = peer_time_offsets[peer_id];
		FrameData last_matching_frame
		foreach(var i in GD.Range(peer_frames.Size()))
		{
			FrameData frame = peer_frames[i];
			if(frame.start_time != 0)
			{
				if(frame.start_time + peer_time_offset <= time)
				{
					last_matching_frame = frame;
				}
				else
				{
					break;
		
				}
			}
		}
		if(last_matching_frame != null && peer_time_offset != 0)
		{
			return last_matching_frame.CloneWithOffset(peer_time_offset);
		
		}
		return last_matching_frame;
	
	
	}
	
	
	
}