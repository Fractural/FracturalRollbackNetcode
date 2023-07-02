
using System;
using Godot;
using Dictionary = Godot.Collections.Dictionary;
using Array = Godot.Collections.Array;

[Tool]
public class FrameDataGrid : Tree
{
	 
	public const var Logger = GD.Load("res://addons/godot-rollback-netcode/Logger.gd");
	public const var LogData = GD.Load("res://addons/godot-rollback-netcode/log_inspector/LogData.gd");
	
	public LogData log_data
	public int cursor_time = -1 {set{SetCursorTime(value);}}
	
	enum PropertyType {
		BASIC,
		ENUM,
		TIME,
		SKIPPED,
	}
	
	public Dictionary _property_definitions  = new Dictionary(){};
	
	public void SetLogData(LogData _log_data)
	{  
		log_data = _log_data;
	
	}
	
	public void SetCursorTime(int _cursor_time)
	{  
		if(cursor_time != _cursor_time)
		{
			cursor_time = _cursor_time;
			RefreshFromLogData();
	
		}
	}
	
	public void _Ready()
	{  
		_property_definitions["frame_type"] = new Dictionary(){
			type = PropertyType.ENUM,
			values = Logger.FrameType.Keys(),
		};
		_property_definitions["tick"] = new Dictionary(){};
		_property_definitions["input_tick"] = new Dictionary(){};
		_property_definitions["duration"] = new Dictionary(){
			suffix = " ms",
		};
		_property_definitions["fatal_error"] = new Dictionary(){};
		_property_definitions["fatal_error_message"] = new Dictionary(){};
		_property_definitions["skipped"] = new Dictionary(){};
		_property_definitions["skip_reason"] = new Dictionary(){
			type = PropertyType.ENUM,
			values = Logger.SkipReason.Keys(),
		};
		_property_definitions["buffer_underrun_message"] = new Dictionary(){};
		_property_definitions["start_time"] = new Dictionary(){
			type = PropertyType.TIME,
		};
		_property_definitions["end_time"] = new Dictionary(){
			type = PropertyType.TIME,
		};
		_property_definitions["timings"] = new Dictionary(){
			type = PropertyType.SKIPPED,
		};
		
		RefreshFromLogData();
	
	}
	
	public void RefreshFromLogData()
	{  
		Clear();
		var root = CreateItem();
		
		if(log_data == null || log_data.IsLoading() || log_data.peer_ids.Size() == 0)
		{
			SetColumnTitlesVisible(false);
			var empty = CreateItem(root);
			empty.SetText(0, "No data.");
			return;
		
		}
		Dictionary frames  = new Dictionary(){};
		Array prop_names  = new Array(){};
		Array extra_prop_names  = new Array(){};
		int index
		
		columns = log_data.peer_ids.Size() + 1;
		SetColumnTitlesVisible(true);
		
		index = 1;
		foreach(var peer_id in log_data.peer_ids)
		{
			SetColumnTitle(index, "Peer %s" % peer_id);
			index += 1;
			
			LogData frame.FrameData = log_data.GetFrameByTime(peer_id, log_data.start_time + cursor_time);
			frames[peer_id] = frame;
			if(frame)
			{
				foreach(var prop_name in frame.data)
				{
					if(!_property_definitions.Has(prop_name))
					{
						if(!prop_name in extra_prop_names)
						{
							extra_prop_names.Append(prop_name);
						}
					}
					else if(!prop_name in prop_names)
					{
						prop_names.Append(prop_name);
		
					}
				}
			}
		}
		foreach(var prop_name in _property_definitions)
		{
			if(!prop_name in prop_names)
			{
				continue;
	
			}
			var prop_def = _property_definitions.Get(prop_name);
			if(prop_def.Get("type") == PropertyType.SKIPPED)
			{
				continue;
			}
			var row = CreateItem(root);
			row.SetText(0, prop_def.Get("label", prop_name.Capitalize()));
			
			index = 1;
			foreach(var peer_id in log_data.peer_ids)
			{
				var frame = frames[peer_id];
				if(frame)
				{
					row.SetText(index, _PropToString(frame.data, prop_name, prop_def));
				}
				index += 1;
		
			}
		}
		foreach(var prop_name in extra_prop_names)
		{
			var row = CreateItem(root);
			row.SetText(0, prop_name.Capitalize());
			
			index = 1;
			foreach(var peer_id in log_data.peer_ids)
			{
				var frame = frames[peer_id];
				if(frame)
				{
					row.SetText(index, _PropToString(frame.data, prop_name, new Dictionary(){}));
				}
				index += 1;
		
			}
		}
		if("timings" in prop_names)
		{
			var timings_root = CreateItem(root);
			timings_root.SetText(0, "Timings");
			_AddTimings(timings_root, frames);
	
		}
	}
	
	public String _PropToString(Dictionary data, String prop_name, __TYPE prop_def = null)
	{  
		if(prop_def == null)
		{
			prop_def = _property_definitions.Get(prop_name, new Dictionary(){});
		}
		var prop_type = prop_def.Get("type", PropertyType.BASIC);
		
		var value = data.Get(prop_name, prop_def.Get("default", null));
		
		switch( prop_type)
		{
			case PropertyType.ENUM:
				if(value != null && prop_def.Has("values"))
				{
					var values = prop_def["values"];
					if(value >= 0 && value < values.Size())
					{
						value = values[value];
			
					}
				}
				break;
			case PropertyType.BASIC:
				if(prop_def.Has("values"))
				{
					value = prop_def["values"].Get(value, value);
			
				}
				break;
			case PropertyType.TIME:
				if(value != null)
				{
					var datetime = OS.GetDatetimeFromUnixTime(value / 1000);
					value = "%04d-%02d-%02d %02d:%02d:%02d" % [
						datetime["year"],
						datetime["month"],
						datetime["day"],
						datetime["hour"],
						datetime["minute"],
						datetime["second"],
					]
		
				}
				break;
		}
		if(value == null)
		{
			return "";
		
		}
		value = GD.Str(value);
		if(prop_def.Has("suffix"))
		{
			value += prop_def["suffix"];
		
		}
		return value;
	
	}
	
	public void _AddTimings(TreeItem root, Dictionary frames)
	{  
		Dictionary all_timings  = new Dictionary(){};
		foreach(var peer_id in log_data.peer_ids)
		{
			var frame = frames[peer_id];
			if(frame)
			{
				foreach(var key in frame.data.Get("timings", new Dictionary(){}))
				{
					all_timings[key] = true;
		
				}
			}
		}
		var all_timings_names = all_timings.Keys();
		all_timings_names.Sort();
		
		Dictionary items  = new Dictionary(){};
		foreach(var timing_name in all_timings_names)
		{
			var timing_name_parts = timing_name.Split(".");
			var item = _CreateNestedItem(timing_name_parts, root, items);
			int index = 1;
			foreach(var peer_id in log_data.peer_ids)
			{
				var frame = frames[peer_id];
				if(frame)
				{
					var timing = frame.data.Get("timings", new Dictionary(){}).Get(timing_name);
					if(timing != null)
					{
						if(timing_name_parts[timing_name_parts.Size() - 1] != "count")
						{
							timing = GD.Str(timing) + " ms";
						}
						else
						{
							timing = GD.Str(timing);
						}
						item.SetText(index, timing);
					}
				}
				index += 1;
	
			}
		}
	}
	
	public TreeItem _CreateNestedItem(Array name_parts, TreeItem root, Dictionary items)
	{  
		if(name_parts.Size() == 0)
		{
			return null;
		
		}
		var name = PoolStringArray(name_parts).Join(".");
		if(items.Has(name))
		{
			return items[name];
		
		}
		TreeItem item
		if(name_parts.Size() == 1)
		{
			item = CreateItem(root);
		}
		else
		{
			var parent_parts = name_parts.Slice(0, name_parts.Size() - 2);
			TreeItem parent = _CreateNestedItem(parent_parts, root, items);
			item = CreateItem(parent);
		
		}
		item.SetText(0, name_parts[name_parts.Size() - 1].Capitalize());
		item.collapsed = true;
		items[name] = item;
		
		return item;
	
	
	}
	
	
	
}