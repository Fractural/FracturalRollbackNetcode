
using System;
using System.Collections.Generic;
using Fractural.Utils;
using Godot;
using static Fractural.RollbackNetcode.LogData;
using GDC = Godot.Collections;

namespace Fractural.RollbackNetcode
{
    public class Logger : Reference
    {
        public enum LogType
        {
            HEADER,
            FRAME,
            STATE,
            INPUT,
        }

        public enum FrameType
        {
            INTERFRAME,
            TICK,
            INTERPOLATION_FRAME,
        }

        public enum SkipReason
        {
            ADVANTAGE_ADJUSTMENT,
            INPUT_BUFFER_UNDERRUN,
            WAITING_TO_REGAIN_SYNC,
        }

        public GDC.Dictionary data = new GDC.Dictionary();

        // [timer: string] start_time: int
        private GDC.Dictionary _start_times = new GDC.Dictionary();
        private Thread _writer_thread;
        private Semaphore _writer_thread_semaphore;
        private Mutex _writer_thread_mutex;
        private IList<GDC.Dictionary> _write_queue = new List<GDC.Dictionary>();
        private File _log_file;
        private bool _started = false;
        private SyncManager _syncManager;

        public Logger(SyncManager _sync_manager)
        {
            // Inject the SyncManager to prevent cyclic reference.
            _syncManager = _sync_manager;

            _writer_thread_mutex = new Mutex();
            _writer_thread_semaphore = new Semaphore();
            _writer_thread = new Thread();
            _log_file = new File();
        }

        public Error Start(string log_file_name, int peer_id, GDC.Dictionary match_info = null)
        {
            if (match_info == null)
                match_info = new GDC.Dictionary() { };

            if (!_started)
            {
                Error err = _log_file.OpenCompressed(log_file_name, File.ModeFlags.Write, File.CompressionMode.Fastlz);
                if (err != Error.Ok)
                {
                    return err;
                }
                GDC.Dictionary header = new GDC.Dictionary()
                {
                    ["log_type"] = LogType.HEADER,
                    ["peer_id"] = peer_id,
                    ["match_info"] = match_info,
                };
                _log_file.StoreVar(header);

                _started = true;
                _writer_thread.Start(this, nameof(_WriterThreadFunction));
            }
            return Error.Ok;
        }

        public void Stop()
        {
            _writer_thread_mutex.Lock();
            var is_running = _started;
            _writer_thread_mutex.Unlock();

            if (is_running)
            {
                if (data.Count > 0)
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

        public void _WriterThreadFunction(GDC.Dictionary _userdata)
        {
            while (true)
            {
                _writer_thread_semaphore.Wait();

                _writer_thread_mutex.Lock();
                var data_to_write = _write_queue.PopFront();
                var should_exit = !_started;
                _writer_thread_mutex.Unlock();

                if (data_to_write is GDC.Dictionary)
                    _log_file.StoreVar(data_to_write);
                else if (should_exit)
                    break;
            }
        }

        public void WriteCurrentData()
        {
            if (data.Count == 0)
                return;

            var copy = data.Duplicate(true);
            copy["log_type"] = LogType.FRAME;

            if (!copy.Contains("frame_type"))
                copy["frame_type"] = FrameType.INTERFRAME;

            _writer_thread_mutex.Lock();
            _write_queue.PushBack(copy);
            _writer_thread_mutex.Unlock();

            _writer_thread_semaphore.Post();
            data.Clear();
        }

        public void WriteState(int tick, GDC.Dictionary state)
        {
            GDC.Dictionary data_to_write = new GDC.Dictionary(){
            {"log_type", LogType.STATE},
            {"tick", tick},
            {"state", _syncManager.hash_serializer.Serialize(state.Duplicate(true))},
        };

            _writer_thread_mutex.Lock();
            _write_queue.PushBack(data_to_write);
            _writer_thread_mutex.Unlock();

            _writer_thread_semaphore.Post();

        }

        public void WriteInput(int tick, GDC.Dictionary input)
        {
            var inputDict = new GDC.Dictionary() { };
            foreach (var key in input.Keys)
                inputDict[key] = _syncManager.hash_serializer.Serialize(input.Get<InputData>(key).input.Duplicate(true));
            GDC.Dictionary data_to_write = new GDC.Dictionary()
            {
                ["log_type"] = LogType.INPUT,
                ["tick"] = tick,
                ["input"] = inputDict,
            };
            _writer_thread_mutex.Lock();
            _write_queue.PushBack(data_to_write);
            _writer_thread_mutex.Unlock();
            _writer_thread_semaphore.Post();
        }

        public void BeginInterframe()
        {
            if (!data.Contains("frame_type"))
                data["frame_type"] = FrameType.INTERFRAME;
            if (!data.Contains("start_time"))
                data["start_time"] = OS.GetSystemTimeMsecs();
        }

        public void EndInterframe()
        {
            if (!data.Contains("frame_type"))
                data["frame_type"] = FrameType.INTERFRAME;
            if (!data.Contains("start_time"))
                data["start_time"] = OS.GetSystemTimeMsecs() - 1;
            data["end_time"] = OS.GetSystemTimeMsecs();
            WriteCurrentData();
        }

        public void BeginTick(int tick)
        {
            if (data.Count > 0)
                EndInterframe();
            data["frame_type"] = FrameType.TICK;
            data["tick"] = tick;
            data["start_time"] = OS.GetSystemTimeMsecs();
        }

        public void EndTick(int start_ticks_usecs)
        {
            data["end_time"] = OS.GetSystemTimeMsecs();
            data["duration"] = (float)((int)Time.GetTicksUsec() - start_ticks_usecs) / 1000f;
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
            if (data.Count > 0)
                EndInterframe();
            data["frame_type"] = FrameType.INTERPOLATION_FRAME;
            data["tick"] = tick;
            data["start_time"] = OS.GetSystemTimeMsecs();
        }

        public void EndInterpolationFrame(int start_ticks_usecs)
        {
            data["end_time"] = OS.GetSystemTimeMsecs();
            data["duration"] = (float)((int)Time.GetTicksUsec() - start_ticks_usecs) / 1000.0f;
            WriteCurrentData();
        }

        public void LogFatalError(string msg)
        {
            if (!data.Contains("end_time"))
                data["end_time"] = OS.GetSystemTimeMsecs();
            data["fatal_error"] = true;
            data["fatal_error_message"] = msg;
            WriteCurrentData();
        }

        public void SetValue(string key, object value)
        {
            data[key] = value;
        }

        public void AddValue(string key, object value)
        {
            if (!data.Contains(key))
                data[key] = new GDC.Array() { };
            data.Get<GDC.Array>(key).Add(value);
        }

        public void MergeArrayValue(string key, GDC.Array value)
        {
            if (!data.Contains(key))
                data[key] = value;
            else
                data[key] = data.Get<GDC.Array>(key) + value;
        }

        public void IncrementValue(string key, int amount = 1)
        {
            if (!data.Contains(key))
                data[key] = amount;
            else
                data[key] = data.Get<int>(key) + amount;
        }

        public void StartTiming(string timer)
        {
            System.Diagnostics.Debug.Assert(!_start_times.Contains(timer), $"Timer already exists: {timer}");
            _start_times[timer] = (int)Time.GetTicksUsec();
        }

        public void StopTiming(string timer, bool accumulate = false)
        {
            System.Diagnostics.Debug.Assert(_start_times.Contains(timer), $"No such timer: {timer}");
            if (_start_times.Contains(timer))
            {
                AddTiming(timer, (float)((int)Time.GetTicksUsec() - _start_times.Get<int>(timer)) / 1000f, accumulate);
                _start_times.Remove(timer);
            }
        }

        public void AddTiming(string timer, float msecs, bool accumulate = false)
        {
            if (!data.Contains("timings"))
            {
                data["timings"] = new GDC.Dictionary() { };
            }
            var timingsDict = data.Get<GDC.Dictionary>("timings");
            if (timingsDict.Contains(timer) && accumulate)
            {
                var old_average = timingsDict.Get<float>(timer + ".average");
                var old_count = timingsDict.Get<int>(timer + ".count");

                timingsDict[timer] = timingsDict.Get<int>(timer) + msecs;
                timingsDict[timer + ".max"] = Mathf.Max(timingsDict.Get<int>(timer + ".max"), msecs);
                timingsDict[timer + ".average"] = ((old_average * old_count) + msecs) / (old_count + 1);
                timingsDict[timer + ".count"] = timingsDict.Get<int>(timer + ".count") + 1;
            }
            else
            {
                timingsDict[timer] = msecs;
                if (accumulate)
                {
                    timingsDict[timer + ".max"] = msecs;
                    timingsDict[timer + ".average"] = 0f;
                    timingsDict[timer + ".count"] = 1;
                }
            }
        }
    }
}