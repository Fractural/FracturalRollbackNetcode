
using System;
using Godot;
using Dictionary = Godot.Collections.Dictionary;
using Array = Godot.Collections.Array;


public class Logger : Reference
{
	 
	enum LogType {
		HEADER,
		FRAME,
		STATE,
		INPUT,
	}
	
	enum FrameType {
		INTERFRAME,
		TICK,
		INTERPOLATION_FRAME,
	}
	
	enum SkipReason {
		ADVANTAGE_ADJUSTMENT,
		INPUT_BUFFER_UNDERRUN,
		WAITING_TO_REGAIN_SYNC,
	}
	
	public Dictionary data  = new Dictionary(){};
	
	public Dictionary _start_times  = new Dictionary(){};
	
	public Thread _writer_thread
	public Semaphore _writer_thread_semaphore
	public Mutex _writer_thread_mutex
	public Array _write_queue  = new Array(){};
	public File _log_file
	public bool _started  = false;
	
	public __TYPE SyncManager;
	
	public void _Init(__TYPE _sync_manager)
	{  
		// Inject the SyncManager to prevent cyclic reference.
		SyncManager = _sync_manager;
		
		_writer_thread_mutex = new Mutex()
		_writer_thread_semaphore = new Semaphore()
		_writer_thread = new Thread()
		_log_file = new File()
	
	}
	
	public int Start(String log_file_name, int peer_id, Dictionary match_info = new Dictionary(){})
	{  
		if(!_started)
		{
			int err
	
			err = _log_file.OpenCompressed(log_file_name, File.WRITE, File.COMPRESSION_FASTLZ);
			if(err != OK)
			{
				return err;
			
			}
			Dictionary header  = new Dictionary(){
				log_type = LogType.HEADER,
				peer_id = peer_id,
				match_info = match_info,
			};
			_log_file.store_var(header)
			
			_started = true;
			_writer_thread.Start(this, "_writer_thread_function");
		
		}
		return OK;
	
	}
	
	public void Stop()
	{  
		_writer_thread_mutex.Lock();
		var is_running = _started;
		_writer_thread_mutex.Unlock();
		
		if(is_running)
		{
			if(data.Size() > 0)
			{
				WriteCurrentData();
			
			}
			_writer_thread_mutex.Lock();
			_started = false;
			_writer_thread_mutex.Unlock();
			
			_writer_thread_semaphore.Post();
			_writer_thread.WaitToFinish();
			
			_log_file.Close();
			_write_queue.Clear();
			data.Clear();
			_start_times.Clear();
	
		}
	}
	
	public void _WriterThreadFunction(__TYPE _userdata)
	{  
		while(true)
		{
			_writer_thread_semaphore.Wait();
			
			var data_to_write;
			bool should_exit
			
			_writer_thread_mutex.Lock();
			data_to_write = _write_queue.PopFront();
			should_exit = !_started;
			_writer_thread_mutex.Unlock();
			
			if(data_to_write is Dictionary)
			{
				_log_file.store_var(data_to_write)
			}
			else if(should_exit)
			{
				break;
	
			}
		}
	}
	
	public void WriteCurrentData()
	{  
		if(data.Size() == 0)
		{
			return;
		
		}
		var copy  = data.Duplicate(true);
		copy["log_type"] = LogType.FRAME;
		
		if(!copy.Has("frame_type"))
		{
			copy["frame_type"] = FrameType.INTERFRAME;
		
		}
		_writer_thread_mutex.Lock();
		_write_queue.PushBack(copy);
		_writer_thread_mutex.Unlock();
		
		_writer_thread_semaphore.Post();
		
		data.Clear();
	
	}
	
	public void WriteState(int tick, Dictionary state)
	{  
		Dictionary data_to_write  = new Dictionary(){
			{"log_type", LogType.STATE},
			{"tick", tick},
			{"state", SyncManager.hash_serializer.Serialize(state.Duplicate(true))},
		};
		
		_writer_thread_mutex.Lock();
		_write_queue.PushBack(data_to_write);
		_writer_thread_mutex.Unlock();
		
		_writer_thread_semaphore.Post();
	
	}
	
	public void WriteInput(int tick, Dictionary input)
	{  
		Dictionary data_to_write  = new Dictionary(){
			log_type = LogType.INPUT,
			tick = tick,
			input = new Dictionary(){},
		};
		foreach(var key in input.Keys())
		{
			data_to_write["input"][key] = SyncManager.hash_serializer.Serialize(input[key].input.Duplicate(true));
		
		}
		_writer_thread_mutex.Lock();
		_write_queue.PushBack(data_to_write);
		_writer_thread_mutex.Unlock();
		
		_writer_thread_semaphore.Post();
	
	}
	
	public void BeginInterframe()
	{  
		if(!data.Has("frame_type"))
		{
			data["frame_type"] = FrameType.INTERFRAME;
		}
		if(!data.Has("start_time"))
		{
			data["start_time"] = OS.GetSystemTimeMsecs();
	
		}
	}
	
	public void EndInterframe()
	{  
		if(!data.Has("frame_type"))
		{
			data["frame_type"] = FrameType.INTERFRAME;
		}
		if(!data.Has("start_time"))
		{
			data["start_time"] = OS.GetSystemTimeMsecs() - 1;
		}
		data["end_time"] = OS.GetSystemTimeMsecs();
		WriteCurrentData();
	
	}
	
	public void BeginTick(int tick)
	{  
		if(data.Size() > 0)
		{
			EndInterframe();
		
		}
		data["frame_type"] = FrameType.TICK;
		data["tick"] = tick;
		data["start_time"] = OS.GetSystemTimeMsecs();
	
	}
	
	public void EndTick(int start_ticks_usecs)
	{  
		data["end_time"] = OS.GetSystemTimeMsecs();
		data["duration"] = (float)(OS.GetTicksUsec() - start_ticks_usecs) / 1000.0;
		WriteCurrentData();
	
	}
	
	public void SkipTick(int skip_reason, int start_ticks_usecs)
	{  
		data["skipped"] = true;
		data["skip_reason"] = skip_reason;
		EndTick(start_ticks_usecs);
	
	}
	
	public void BeginInterpolationFrame(int tick)
	{  
		if(data.Size() > 0)
		{
			EndInterframe();
		
		}
		data["frame_type"] = FrameType.INTERPOLATION_FRAME;
		data["tick"] = tick;
		data["start_time"] = OS.GetSystemTimeMsecs();
	
	}
	
	public void EndInterpolationFrame(int start_ticks_usecs)
	{  
		data["end_time"] = OS.GetSystemTimeMsecs();
		data["duration"] = (float)(OS.GetTicksUsec() - start_ticks_usecs) / 1000.0;
		WriteCurrentData();
	
	}
	
	public void LogFatalError(String msg)
	{  
		if(!data.Has("end_time"))
		{
			data["end_time"] = OS.GetSystemTimeMsecs();
		}
		data["fatal_error"] = true;
		data["fatal_error_message"] = msg;
		WriteCurrentData();
	
	}
	
	public void SetValue(String key, __TYPE value)
	{  
		data[key] = value;
	
	}
	
	public void AddValue(String key, __TYPE value)
	{  
		if(!data.Has(key))
		{
			data[key] = new Array(){};
		}
		data[key].Append(value);
	
	}
	
	public void MergeArrayValue(String key, Array value)
	{  
		if(!data.Has(key))
		{
			data[key] = value;
		}
		else
		{
			data[key] = data[key] + value;
	
		}
	}
	
	public void IncrementValue(String key, int amount = 1)
	{  
		if(!data.Has(key))
		{
			data[key] = amount;
		}
		else
		{
			data[key] += amount;
	
		}
	}
	
	public void StartTiming(String timer)
	{  
		System.Diagnostics.Debug.Assert(!_start_times.Has(timer), "Timer already exists: %s" % timer);
		_start_times[timer] = OS.GetTicksUsec();
	
	}
	
	public void StopTiming(String timer, bool accumulate = false)
	{  
		System.Diagnostics.Debug.Assert(_start_times.Has(timer), "No such timer: %s" % timer);
		if(_start_times.Has(timer))
		{
			AddTiming(timer, (float)(OS.GetTicksUsec() - _start_times[timer]) / 1000.0, accumulate);
			_start_times.Erase(timer);
	
		}
	}
	
	public void AddTiming(String timer, float msecs, bool accumulate = false)
	{  
		if(!data.Has("timings"))
		{
			data["timings"] = new Dictionary(){};
		}
		if(data["timings"].Has(timer) && accumulate)
		{
			var old_average = data["timings"][timer + ".average"];
			var old_count = data["timings"][timer + ".count"];
			
			data["timings"][timer] += msecs;
			data["timings"][timer + ".max"] = Mathf.Max(data["timings"][timer + ".max"], msecs);
			data["timings"][timer + ".average"] = ((old_average * old_count) + msecs) / (old_count + 1);
			data["timings"][timer + ".count"] += 1;
		}
		else
		{
			data["timings"][timer] = msecs;
			if(accumulate)
			{
				data["timings"][timer + ".max"] = msecs;
				data["timings"][timer + ".average"] = 0.0;
				data["timings"][timer + ".count"] = 1;
				
	
	
			}
		}
	}
	
	
	
}