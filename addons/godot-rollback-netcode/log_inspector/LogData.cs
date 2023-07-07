
using System;
using System.Collections.Generic;
using System.Linq;
using Fractural.Utils;
using Godot;
using GDC = Godot.Collections;

namespace Fractural.RollbackNetcode
{
    [Tool]
    public class LogData : Reference
    {
        public class StateData : Reference
        {
            public int tick;
            public GDC.Dictionary state;
            public int state_hash;
            // [tick: int]: mismatch: GDC.Dictionary
            public GDC.Dictionary mismatches = new GDC.Dictionary() { };

            public StateData() { }
            public StateData(int _tick, GDC.Dictionary _state)
            {
                tick = _tick;
                state = _state;
                state_hash = _state.Get<int>("$");
            }

            public bool CompareState(int peer_id, GDC.Dictionary peer_state)
            {
                if (state_hash == peer_state.Get<int>("$"))
                    return true;
                mismatches[peer_id] = peer_state;
                return false;
            }
        }

        public class InputData : Reference
        {
            public int tick;
            // [peerId: int]: inputData: GDC.Dictionary
            public GDC.Dictionary input;
            public int input_hash;
            // [tick: int]: mismatch: GDC.Dictionary
            public GDC.Dictionary mismatches = new GDC.Dictionary();

            public InputData() { }
            public InputData(int _tick, GDC.Dictionary _input)
            {
                tick = _tick;
                input = SortDictionary(_input);
                input_hash = GD.Hash(input);
            }

            public GDC.Dictionary SortDictionary(GDC.Dictionary d)
            {
                var keys = new List<object>(d.Keys.Cast<object>());
                keys.Sort(Utils.GDKeyComparer);

                GDC.Dictionary ret = new GDC.Dictionary() { };
                foreach (var key in keys)
                {
                    var val = d[key];
                    if (val is GDC.Dictionary dict)
                        val = SortDictionary(dict);
                    ret[key] = val;
                }
                return ret;
            }

            public bool CompareInput(int peer_id, GDC.Dictionary peer_input)
            {
                var sorted_peer_input = SortDictionary(peer_input);
                if (GD.Hash(sorted_peer_input) == input_hash)
                    return true;
                mismatches[peer_id] = sorted_peer_input;
                return false;
            }

            public GDC.Dictionary GetInputForPeer(int peer_id, int according_to_peer_id = -1)
            {
                if (according_to_peer_id != -1 && mismatches.Contains(according_to_peer_id))
                    return mismatches.Get<GDC.Dictionary>(according_to_peer_id).Get(peer_id, new GDC.Dictionary() { });
                return input.Get(peer_id, new GDC.Dictionary() { });
            }
        }

        public class FrameData : Reference
        {
            public int frame;
            public Logger.FrameType type;
            public GDC.Dictionary data;
            public long start_time;
            public long end_time;

            public FrameData() { }
            public FrameData(int _frame, Logger.FrameType _type, GDC.Dictionary _data)
            {
                frame = _frame;
                type = _type;
                data = _data;
            }

            public FrameData CloneWithOffset(long offset)
            {
                var clone = new FrameData(frame, type, data);
                clone.start_time = start_time + offset;
                clone.end_time = end_time + offset;
                return clone;
            }
        }

        // peer_id: int[]
        public GDC.Array peer_ids = new GDC.Array() { };
        // mismatch_tick: int[]
        public GDC.Array mismatches = new GDC.Array() { };
        public int max_tick = 0;
        public int max_frame = 0;
        // [peer_id: int]: frame: int
        public GDC.Dictionary frame_counter = new GDC.Dictionary() { };
        public long start_time;
        public long end_time;

        // User-set data
        public GDC.Dictionary match_info = new GDC.Dictionary() { };
        // [tick: int]: data: InputData
        public GDC.Dictionary input = new GDC.Dictionary() { };
        // Combination of all GDC.Dictionary data from all serializable nodes
        // [node: NodePath]: nodeState: GDC.Dictionary
        public GDC.Dictionary state = new GDC.Dictionary() { };
        // [peer_id: int] data_array: GDC.Array<FrameData>
        public GDC.Dictionary frames = new GDC.Dictionary() { };

        // [peer_id: int]: offset: int
        public GDC.Dictionary peer_time_offsets = new GDC.Dictionary() { };
        // [peer_id: int]: start_time: int
        public GDC.Dictionary peer_start_times = new GDC.Dictionary() { };
        // [peer_id: int]: end_time: int
        public GDC.Dictionary peer_end_times = new GDC.Dictionary() { };

        public bool _is_loading = false;
        public Thread _loader_thread;
        public Mutex _loader_mutex;

        public delegate void LoadProgressDelegate(ulong current, ulong total);
        public event LoadProgressDelegate LoadProgress;
        public event Action LoadFinished;
        public delegate void LoadErrorDelegate(string msg);
        public event LoadErrorDelegate LoadError;
        public event Action DataUpdated;

        public LogData()
        {
            _loader_mutex = new Mutex();
        }

        public void Clear()
        {
            if (IsLoading())
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

        public void LoadLogFile(string path)
        {
            if (IsLoading())
            {
                GD.PushError("Attempting to load log file when one is already loading");
                return;
            }
            var file = new File();

            if (file.OpenCompressed(path, File.ModeFlags.Read, File.CompressionMode.Fastlz) != Error.Ok)
            {
                LoadError?.Invoke($"Unable to open file for reading: {path}");
                return;
            }
            if (_loader_thread != null)
                _loader_thread.WaitToFinish();
            _loader_thread = new Thread();
            _is_loading = true;
            _loader_thread.Start(this, nameof(_LoaderThreadFunction), new GDC.Array() { file, path });
        }

        public void _SetLoading(bool _value)
        {
            _loader_mutex.Lock();
            _is_loading = _value;
            _loader_mutex.Unlock();
        }

        public bool IsLoading()
        {
            _loader_mutex.Lock();
            bool value = _is_loading;
            _loader_mutex.Unlock();
            return value;
        }

        public void _ThreadPrint(string msg)
        {
            GD.Print(msg);
        }

        public void _LoaderThreadFunction(GDC.Array input)
        {
            File file = input.ElementAt<File>(0);
            string path = input.ElementAt<string>(1);

            GDC.Dictionary header = null;
            var file_size = file.GetLen();

            while (!file.EofReached())
            {
                //GD.Print("Log data reading data: ");
                var data = file.GetVar();
                //GD.Print("\t Got data ", data, " ", data.GetType());
                if (data == null || !(data is GDC.Dictionary dataDict))
                {
                    GD.Print("Get var failed, ", data);
                    continue;
                }

                if (header == null)
                {
                    if (dataDict.Get<Logger.LogType>("log_type") == Logger.LogType.HEADER)
                    {
                        header = dataDict;
                        header["peer_id"] = (int)(header["peer_id"]);
                        if (peer_ids.Contains(header["peer_id"]))
                        {
                            file.Close();
                            // TODO: Check if CallDeferred is needed for invoking C# events within 
                            //       multithreaded code that uses Godot threads.
                            _SetLoading(false);
                            DataUpdated?.Invoke();
                            LoadError?.Invoke($"Log file has data for peer_id {header["peer_id"]}, which is already loaded");
                            return;
                        }
                        var header_match_info = header.Get("match_info", new GDC.Dictionary() { });
                        if (match_info.Count > 0 && GD.Hash(match_info) != GD.Hash(header_match_info))
                        {
                            file.Close();
                            _SetLoading(false);
                            DataUpdated?.Invoke();
                            LoadError?.Invoke($"Log file for peer_id {header["peer_id"]} has match info that doesn't match already loaded data");
                            return;
                        }
                        else
                        {
                            match_info = header_match_info;
                        }
                        var peer_id = header["peer_id"];
                        peer_ids.Add(peer_id);
                        peer_time_offsets[peer_id] = 0;
                        peer_start_times[peer_id] = 0;
                        peer_end_times[peer_id] = 0;
                        frame_counter[peer_id] = 0;
                        frames[peer_id] = new GDC.Array() { };
                        continue;
                    }
                    else
                    {
                        file.Close();

                        _SetLoading(false);
                        DataUpdated?.Invoke();
                        LoadError?.Invoke($"No header at the top of log: {path}");
                        return;
                    }
                }
                _AddLogEntry(dataDict, header.Get<int>("peer_id"));
                LoadProgress?.Invoke(file.GetPosition(), file_size);
            }
            file.Close();
            _UpdateStartEndTimes();

            _SetLoading(false);
            DataUpdated?.Invoke();
            LoadFinished?.Invoke();

            GD.Print("Log data finished loading");
        }

        public void _AddLogEntry(GDC.Dictionary log_entry, int peer_id)
        {
            int tick = log_entry.Get("tick", 0);
            max_tick = (int)(Mathf.Max(max_tick, tick));

            switch (log_entry.Get<Logger.LogType>("log_type"))
            {
                case Logger.LogType.INPUT:
                    InputData input_data;
                    if (!input.Contains(tick))
                    {
                        input_data = new InputData(tick, log_entry.Get<GDC.Dictionary>("input"));
                        input[tick] = input_data;
                    }
                    else
                    {
                        input_data = input.Get<InputData>(tick);
                        if (!input_data.CompareInput(peer_id, log_entry.Get<GDC.Dictionary>("input")) && !mismatches.Contains(tick))
                            mismatches.Add(tick);
                    }
                    break;
                case Logger.LogType.STATE:
                    StateData state_data;
                    if (!state.Contains(tick))
                    {
                        state_data = new StateData(tick, log_entry.Get<GDC.Dictionary>("state"));
                        state[tick] = state_data;
                    }
                    else
                    {
                        state_data = state.Get<StateData>(tick);
                        if (!state_data.CompareState(peer_id, log_entry.Get<GDC.Dictionary>("state")) && !mismatches.Contains(tick))
                            mismatches.Add(tick);
                    }
                    break;
                case Logger.LogType.FRAME:
                    log_entry.Remove("log_type");
                    var frame_number = frame_counter.Get<int>(peer_id);
                    var frame_data = new FrameData(frame_number, log_entry.Get<Logger.FrameType>("frame_type"), log_entry);

                    frames.Get<GDC.Array>(peer_id).Add(frame_data);
                    frame_counter[peer_id] = frame_counter.Get<int>(peer_id) + 1;
                    max_frame = (int)(Mathf.Max(max_frame, frame_number));

                    if (log_entry.Contains("start_time"))
                    {
                        frame_data.start_time = log_entry.GetSerializedPrimitive<long>("start_time");
                        var peer_start_time = peer_start_times.Get<int>(peer_id);
                        peer_start_times[peer_id] = peer_start_time > 0 ? Math.Min(peer_start_time, frame_data.start_time) : frame_data.start_time;
                    }
                    if (log_entry.Contains("end_time"))
                    {
                        frame_data.end_time = log_entry.GetSerializedPrimitive<long>("end_time");
                    }
                    else
                    {
                        frame_data.end_time = frame_data.start_time;
                    }
                    peer_end_times[peer_id] = Math.Max(peer_end_times.Get<int>(peer_id), frame_data.end_time);
                    break;
            }
        }

        public void _UpdateStartEndTimes()
        {
            int peer_id = peer_ids.ElementAt<int>(0);
            start_time = peer_start_times.Get<long>(peer_id) + peer_time_offsets.Get<long>(peer_id);
            for (int i = 1; i < peer_ids.Count; i++)
            {
                peer_id = peer_ids.ElementAt<int>(i);
                start_time = Math.Min(start_time, peer_start_times.Get<long>(peer_id) + peer_time_offsets.Get<long>(peer_id));
            }
            peer_id = peer_ids.ElementAt<int>(0);
            end_time = peer_end_times.Get<long>(peer_id) + peer_time_offsets.Get<long>(peer_id);
            for (int i = 1; i < peer_ids.Count; i++)
            {
                peer_id = peer_ids.ElementAt<int>(i);
                end_time = Math.Max(end_time, peer_end_times.Get<long>(peer_id) + peer_time_offsets.Get<long>(peer_id));
            }
        }

        public void SetPeerTimeOffset(int peer_id, int offset)
        {
            peer_time_offsets[peer_id] = offset;
            _UpdateStartEndTimes();
            // TODO: Check if event needs to be deferred
            DataUpdated?.Invoke();
        }

        public int GetFrameCount(int peer_id)
        {
            if (IsLoading())
            {
                GD.PushError("Cannot GetFrame() while loading");
                return 0;

            }
            return frames.Get<GDC.Array>(peer_id).Count;
        }

        public FrameData GetFrame(int peer_id, long frame_number)
        {
            if (IsLoading())
            {
                GD.PushError("Cannot GetFrame() while loading");
                return null;
            }
            if (!frames.Contains(peer_id))
                return null;
            if (frame_number >= frames.Get<GDC.Array>(peer_id).Count)
                return null;
            var frame = frames.Get<GDC.Array>(peer_id).ElementAt<FrameData>((int)frame_number);
            if (peer_time_offsets.Get<int>(peer_id) != 0)
                return frame.CloneWithOffset(peer_time_offsets.Get<long>(peer_id));
            return frame;
        }

        public T GetFrameData<T>(int peer_id, int frame_number, string key, T default_value = default)
        {
            if (IsLoading())
            {
                GD.PushError("Cannot GetFrameData() while loading");
                return default;
            }
            var frame = GetFrame(peer_id, frame_number);
            if (frame != null)
                return frame.data.Get(key, default_value);
            return default_value;
        }

        public FrameData GetFrameByTime(int peer_id, long time)
        {
            if (IsLoading())
            {
                GD.PushError("Cannot GetFrameByTime() while loading");
                return null;
            }
            if (!frames.Contains(peer_id))
            {
                return null;
            }
            GDC.Array peer_frames = frames.Get<GDC.Array>(peer_id);
            long peer_time_offset = peer_time_offsets.Get<long>(peer_id);
            FrameData last_matching_frame = null;
            for (int i = 0; i < peer_frames.Count; i++)
            {
                FrameData frame = peer_frames.ElementAt<FrameData>(i);
                if (frame.start_time != 0)
                {
                    if (frame.start_time + peer_time_offset <= time)
                        last_matching_frame = frame;
                    else
                        break;
                }
            }
            if (last_matching_frame != null && peer_time_offset != 0)
                return last_matching_frame.CloneWithOffset(peer_time_offset);
            return last_matching_frame;
        }
    }
}