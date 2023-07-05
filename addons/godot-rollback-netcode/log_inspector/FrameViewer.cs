
using System;
using Fractural.GodotCodeGenerator.Attributes;
using Fractural.Utils;
using Godot;
using GDC = Godot.Collections;

namespace Fractural.RollbackNetcode
{
    [Tool]
    public partial class FrameViewer : Control
    {
        [OnReadyGet("VBoxContainer/HBoxContainer/Time")]
        public SpinBox time_field;
        [OnReadyGet("VBoxContainer/HBoxContainer/SeekOnReplayPeerField")]
        public CheckBox seek_on_replay_peer_field;
        [OnReadyGet("VBoxContainer/HBoxContainer/ReplayContainer/HBoxContainer/AutoReplayToCurrentField")]
        public CheckBox auto_replay_to_current_field;
        [OnReadyGet("VBoxContainer/HBoxContainer/ReplayContainer/HBoxContainer/ReplayToCurrentButton")]
        public Button replay_to_current_button;
        [OnReadyGet("VBoxContainer/VSplitContainer/DataGraph")]
        public FrameDataGraph data_graph;
        [OnReadyGet("VBoxContainer/VSplitContainer/DataGrid")]
        public FrameDataGrid data_grid;
        [OnReadyGet("SettingsDialog")]
        public FrameViewerSettingsDialog settings_dialog;

        public LogData log_data;
        public ReplayServer replay_server;
        public int replay_peer_id;

        public int replay_frame = -1;
        public int replay_last_interpolation_frame_time = 0;

        // [peerId: int]: currentFrame: int
        public GDC.Dictionary current_frames = new GDC.Dictionary() { };

        public void SetLogData(LogData _log_data)
        {
            log_data = _log_data;
            data_graph.SetLogData(log_data);
            data_grid.SetLogData(log_data);
            settings_dialog.SetupSettingsDialog(log_data, data_graph, data_grid);
        }

        public void RefreshFromLogData()
        {
            if (log_data.IsLoading())
                return;
            time_field.MaxValue = log_data.end_time - log_data.start_time;

            data_graph.RefreshFromLogData();
            data_grid.RefreshFromLogData();
            settings_dialog.RefreshFromLogData();

            replay_frame = -1;
            _OnTimeValueChanged(time_field.Value);
        }

        public void SetReplayServer(ReplayServer _replay_server)
        {
            if (replay_server != null)
                replay_server.GameDisconnected -= _OnReplayServerGameDisconnected;

            replay_server = _replay_server;
            if (replay_server != null)
                replay_server.GameDisconnected += _OnReplayServerGameDisconnected;
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
            if (auto_replay_to_current_field.Pressed)
                ReplayToCurrentFrame();
        }

        public void Clear()
        {
            current_frames.Clear();
            RefreshFromLogData();
        }

        public void _OnTimeValueChanged(double value)
        {
            if (log_data.IsLoading())
                return;
            var time = (int)(value);

            // Update our tracking of the current frame.
            foreach (int peer_id in log_data.peer_ids)
            {
                LogData.FrameData frame = log_data.GetFrameByTime(peer_id, log_data.start_time + time);
                if (frame != null)
                    current_frames[peer_id] = frame.frame;
                else
                    current_frames[peer_id] = 0;
            }
            data_graph.cursor_time = time;
            data_grid.cursor_time = time;

            if (auto_replay_to_current_field.Pressed)
                ReplayToCurrentFrame();
        }

        public void _OnPreviousFrameButtonPressed()
        {
            JumpToPreviousFrame();
        }

        public void JumpToPreviousFrame()
        {
            if (log_data.IsLoading())
                return;

            int frame_time = 0;

            if (seek_on_replay_peer_field.Pressed)
                frame_time = _GetPreviousFrameTimeForPeer(replay_peer_id);
            else
            {
                foreach (int peer_id in current_frames)
                    frame_time = (int)(Mathf.Max(frame_time, _GetPreviousFrameTimeForPeer(peer_id)));
            }
            if (frame_time > log_data.start_time)
                time_field.Value = frame_time - log_data.start_time;
            else
                time_field.Value = 0;
        }

        public int _GetPreviousFrameTimeForPeer(int peer_id)
        {
            var frame_id = current_frames.Get<int>(peer_id);
            if (frame_id > 0)
                frame_id -= 1;

            LogData.FrameData frame = log_data.GetFrame(peer_id, frame_id);
            return frame.start_time;

        }

        public void _OnNextFrameButtonPressed()
        {
            JumpToNextFrame();

        }

        public void JumpToNextFrame()
        {
            if (log_data.IsLoading())
            {
                return;

            }
            var frame_time = log_data.end_time;

            if (seek_on_replay_peer_field.Pressed)
            {
                frame_time = _GetNextFrameTimeForPeer(replay_peer_id);
            }
            else
            {
                foreach (int peer_id in current_frames.Keys)
                {
                    var peer_frame_time = _GetNextFrameTimeForPeer(peer_id);
                    if (peer_frame_time != 0)
                        frame_time = (int)(Mathf.Min(frame_time, _GetNextFrameTimeForPeer(peer_id)));
                }
            }
            if (frame_time > log_data.start_time)
                time_field.Value = frame_time - log_data.start_time;
            else
                time_field.Value = 0;
        }

        public int _GetNextFrameTimeForPeer(int peer_id)
        {
            var frame_id = current_frames.Get<int>(peer_id);
            if (frame_id < log_data.GetFrameCount(peer_id) - 1)
            {
                frame_id += 1;
                LogData.FrameData frame = log_data.GetFrame(peer_id, frame_id);
                return frame.start_time;
            }
            return 0;
        }

        public void ReplayToCurrentFrame()
        {
            if (replay_server == null && !replay_server.IsConnectedToGame())
                return;

            if (log_data.IsLoading())
                return;
            if (log_data.peer_ids.Count == 0)
                return;
            if (!current_frames.Contains(replay_peer_id))
                return;
            int current_frame_id = current_frames.Get<int>(replay_peer_id);

            // If replay_frame is ahead of current frame, we have to replay from the beginning.
            if (replay_frame > current_frame_id)
                replay_frame = -1;
            // Reset replay.
            if (replay_frame == -1)
            {
                replay_last_interpolation_frame_time = 0;
                replay_server.SendMatchInfo(log_data, replay_peer_id);
            }
            replay_frame += 1;
            foreach (var frame_id in GD.Range(replay_frame, log_data.frames.Get<GDC.Array>(replay_peer_id).Count))
            {
                if (frame_id > current_frame_id)
                {
                    break;
                }
                LogData.FrameData frame_data = log_data.GetFrame(replay_peer_id, frame_id);
                _SendReplayFrameData(frame_data);
            }
            replay_frame = current_frame_id;
        }

        public void _SendReplayFrameData(LogData.FrameData frame_data)
        {
            var frame_type = frame_data.data.Get<Logger.FrameType>("frame_type");

            GDC.Dictionary msg = new GDC.Dictionary()
            {
                ["type"] = "execute_frame",
                ["frame_type"] = frame_type,
                ["rollback_ticks"] = frame_data.data.Get("rollback_ticks", 0),
            };

            GDC.Dictionary input_frames_received = new GDC.Dictionary() { };

            if (frame_type == Logger.FrameType.TICK)
            {
                var tick = (int)(frame_data.data["tick"]);
                if (tick > 0)
                {
                    // Get input for local peer.
                    input_frames_received[replay_peer_id] = new GDC.Dictionary()
                    {
                        ["tick"] = log_data.input.Get<LogData.InputData>(tick).GetInputForPeer(replay_peer_id, replay_peer_id),
                    };
                }
                replay_last_interpolation_frame_time = frame_data.data.Get<int>("end_time");
            }
            else if (frame_type == Logger.FrameType.INTERPOLATION_FRAME)
            {
                var start_time = frame_data.data.Get<int>("start_time");
                if (replay_last_interpolation_frame_time > 0)
                    msg["delta"] = (start_time - replay_last_interpolation_frame_time) / 1000.0;
                else
                {
                    // If we can't know the actual delta, let's use a small value that's
                    // bigger than zero, arbitrarily 1.0/120.0
                    msg["delta"] = 0.00833333;
                }
                replay_last_interpolation_frame_time = start_time;
            }
            // Get input received from each of the peers.
            foreach (int peer_id in log_data.peer_ids)
            {
                GDC.Array ticks = frame_data.data.Get($"remote_ticks_received_from_{peer_id}", new GDC.Array() { });
                if (ticks.Count > 0)
                {
                    GDC.Dictionary peer_input_ticks = new GDC.Dictionary() { };
                    foreach (int tick in ticks)
                    {
                        peer_input_ticks[tick] = log_data.input.Get<LogData.InputData>(tick).GetInputForPeer(peer_id, replay_peer_id);
                    }
                    input_frames_received[peer_id] = peer_input_ticks;
                }
            }
            msg["input_frames_received"] = input_frames_received;

            replay_server.SendMessage(msg);
        }

        public override void _UnhandledKeyInput(InputEventKey @event)
        {
            if (@event.Pressed)
            {
                switch ((KeyList)@event.Scancode)
                {
                    case KeyList.Pageup:
                        JumpToNextFrame();
                        break;
                    case KeyList.Pagedown:
                        JumpToPreviousFrame();
                        break;
                    case KeyList.Up:
                        time_field.Value += 1;
                        break;
                    case KeyList.Down:
                        time_field.Value -= 1;
                        break;
                }
            }
        }

        public void _OnStartButtonPressed()
        {
            time_field.Value = 0;
        }

        public void _OnEndButtonPressed()
        {
            time_field.Value = time_field.MaxValue;
        }

        public void _OnDataGraphCursorTimeChanged(float cursor_time)
        {
            time_field.Value = cursor_time;
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
            replay_to_current_button.Disabled = button_pressed;
            if (button_pressed)
                ReplayToCurrentFrame();
        }
    }
}