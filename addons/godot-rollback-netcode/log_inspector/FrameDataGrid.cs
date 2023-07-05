
using System;
using System.Collections.Generic;
using System.Linq;
using Fractural.Utils;
using Godot;
using GDC = Godot.Collections;

namespace Fractural.RollbackNetcode
{
    [Tool]
    public class FrameDataGrid : Tree
    {
        public LogData log_data;
        private int _cursor_time = -1;
        public int cursor_time
        {
            get => _cursor_time;
            set
            {
                if (_cursor_time != value)
                {
                    _cursor_time = value;
                    RefreshFromLogData();
                }
            }
        }

        enum PropertyType
        {
            BASIC,
            ENUM,
            TIME,
            SKIPPED,
        }

        public GDC.Dictionary _property_definitions = new GDC.Dictionary() { };

        public void Construct(LogData _log_data)
        {
            log_data = _log_data;
        }

        public override void _Ready()
        {
            _property_definitions["frame_type"] = new GDC.Dictionary()
            {
                ["type"] = PropertyType.ENUM,
                ["values"] = Enum.GetNames(typeof(Logger.FrameType)),
            };
            _property_definitions["tick"] = new GDC.Dictionary() { };
            _property_definitions["input_tick"] = new GDC.Dictionary() { };
            _property_definitions["duration"] = new GDC.Dictionary()
            {
                ["suffix"] = " ms",
            };
            _property_definitions["fatal_error"] = new GDC.Dictionary() { };
            _property_definitions["fatal_error_message"] = new GDC.Dictionary() { };
            _property_definitions["skipped"] = new GDC.Dictionary() { };
            _property_definitions["skip_reason"] = new GDC.Dictionary()
            {
                ["type"] = PropertyType.ENUM,
                ["values"] = Enum.GetNames(typeof(Logger.SkipReason)),
            };
            _property_definitions["buffer_underrun_message"] = new GDC.Dictionary() { };
            _property_definitions["start_time"] = new GDC.Dictionary()
            {
                ["type"] = PropertyType.TIME,
            };
            _property_definitions["end_time"] = new GDC.Dictionary()
            {
                ["type"] = PropertyType.TIME,
            };
            _property_definitions["timings"] = new GDC.Dictionary()
            {
                ["type"] = PropertyType.SKIPPED,
            };

            RefreshFromLogData();
        }

        public void RefreshFromLogData()
        {
            Clear();
            var root = CreateItem();

            if (log_data == null || log_data.IsLoading() || log_data.peer_ids.Count == 0)
            {
                ColumnTitlesVisible = false;
                var empty = CreateItem(root);
                empty.SetText(0, "No data.");
                return;
            }
            // [peerId: int]: data: LogData.FrameData
            GDC.Dictionary frames = new GDC.Dictionary() { };
            GDC.Array prop_names = new GDC.Array() { };
            GDC.Array extra_prop_names = new GDC.Array() { };
            int index = 0;

            int columns = log_data.peer_ids.Count + 1;
            ColumnTitlesVisible = true;

            index = 1;
            foreach (int peer_id in log_data.peer_ids)
            {
                SetColumnTitle(index, $"Peer {peer_id}");
                index += 1;

                LogData.FrameData frame = log_data.GetFrameByTime(peer_id, log_data.start_time + cursor_time);
                frames[peer_id] = frame;
                if (frame != null)
                {
                    foreach (var prop_name in frame.data)
                    {
                        if (!_property_definitions.Contains(prop_name))
                        {
                            if (!extra_prop_names.Contains(prop_name))
                                extra_prop_names.Add(prop_name);
                        }
                        else if (!prop_names.Contains(prop_name))
                            prop_names.Add(prop_name);
                    }
                }
            }
            foreach (string prop_name in _property_definitions.Keys)
            {
                if (!prop_names.Contains(prop_name))
                    continue;

                var prop_def = _property_definitions.Get<GDC.Dictionary>(prop_name);
                if (prop_def.Get<PropertyType>("type") == PropertyType.SKIPPED)
                    continue;

                var row = CreateItem(root);
                row.SetText(0, prop_def.Get("label", prop_name.Capitalize()));

                index = 1;
                foreach (int peer_id in log_data.peer_ids)
                {
                    var frame = frames.Get<LogData.FrameData>(peer_id);
                    if (frame != null)
                        row.SetText(index, _PropToString(frame.data, prop_name, prop_def));
                    index += 1;

                }
            }
            foreach (string prop_name in extra_prop_names)
            {
                var row = CreateItem(root);
                row.SetText(0, prop_name.Capitalize());

                index = 1;
                foreach (var peer_id in log_data.peer_ids)
                {
                    var frame = frames.Get<LogData.FrameData>(peer_id);
                    if (frame != null)
                        row.SetText(index, _PropToString(frame.data, prop_name, new GDC.Dictionary() { }));
                    index += 1;
                }
            }
            if (prop_names.Contains("timings"))
            {
                var timings_root = CreateItem(root);
                timings_root.SetText(0, "Timings");
                _AddTimings(timings_root, frames);
            }
        }

        public string _PropToString(GDC.Dictionary data, string prop_name, GDC.Dictionary prop_def = null)
        {
            if (prop_def == null)
                prop_def = _property_definitions.Get(prop_name, new GDC.Dictionary() { });
            var prop_type = prop_def.Get("type", PropertyType.BASIC);
            var value = data.Get(prop_name, prop_def.Get<object>("default", null));
            var result = "";
            switch (prop_type)
            {
                case PropertyType.ENUM:
                    if (value != null && value is int intValue && prop_def.Contains("values"))
                    {
                        var values = prop_def.Get<string[]>("values");
                        if (intValue >= 0 && intValue < values.Length)
                            result = values[intValue];
                    }
                    break;
                case PropertyType.BASIC:
                    if (prop_def.Contains("values"))
                        result = prop_def.Get<GDC.Dictionary>("values").Get(value, value).ToString();
                    break;
                case PropertyType.TIME:
                    if (value != null && value is long longValue)
                    {
                        var datetime = OS.GetDatetimeFromUnixTime(longValue / 1000);
                        result = string.Format("{0:0000}-{1:00}-{2:00} {3:00}:{4:00}:{5:00}",
                            datetime["year"],
                            datetime["month"],
                            datetime["day"],
                            datetime["hour"],
                            datetime["minute"],
                            datetime["second"]
                        );
                    }
                    break;
            }
            if (value == null)
                return "";
            if (prop_def.Contains("suffix"))
                result += prop_def.Get<string>("suffix");
            return result;
        }

        // frames = [peerId: int]: data: LogData.FrameData
        public void _AddTimings(TreeItem root, GDC.Dictionary frames)
        {
            // [name: string]: bool
            GDC.Dictionary all_timings = new GDC.Dictionary() { };
            foreach (int peer_id in log_data.peer_ids)
            {
                var frame = frames.Get<LogData.FrameData>(peer_id);
                if (frame != null)
                {
                    foreach (var key in frame.data.Get("timings", new GDC.Dictionary() { }).Keys)
                        all_timings[key] = true;
                }
            }
            var all_timings_names = new List<string>(all_timings.Keys.Cast<string>());
            all_timings_names.Sort();

            GDC.Dictionary items = new GDC.Dictionary() { };
            foreach (var timing_name in all_timings_names)
            {
                var timing_name_parts = timing_name.Split(".");
                var item = _CreateNestedItem(timing_name_parts, root, items);
                int index = 1;
                foreach (var peer_id in log_data.peer_ids)
                {
                    var frame = frames.Get<LogData.FrameData>(peer_id);
                    if (frame != null)
                    {
                        var timing = frame.data.Get("timings", new GDC.Dictionary() { }).Get<object>(timing_name);
                        if (timing != null)
                        {
                            var timingString = "";
                            if (timing_name_parts[timing_name_parts.Length - 1] != "count")
                                timingString = timing.ToString() + " ms";
                            else
                                timingString = timing.ToString();
                            item.SetText(index, timingString);
                        }
                    }
                    index += 1;
                }
            }
        }

        // items = [name: string]: item: TreeItem
        public TreeItem _CreateNestedItem(string[] name_parts, TreeItem root, GDC.Dictionary items)
        {
            if (name_parts.Length == 0)
                return null;

            var name = string.Join(".", name_parts);
            if (items.Contains(name))
                return items.Get<TreeItem>(name);
            TreeItem item = null;
            if (name_parts.Length == 1)
                item = CreateItem(root);
            else
            {
                var parent_parts = name_parts.Slice(0, name_parts.Length - 2);
                TreeItem parent = _CreateNestedItem(parent_parts, root, items);
                item = CreateItem(parent);
            }
            item.SetText(0, name_parts[name_parts.Length - 1].Capitalize());
            item.Collapsed = true;
            items[name] = item;

            return item;
        }
    }
}