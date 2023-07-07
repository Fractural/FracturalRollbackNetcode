
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
        private SpinBox _timeSpinBox;
        [OnReadyGet("VBoxContainer/HBoxContainer/StartButton")]
        private Button _startButton;
        [OnReadyGet("VBoxContainer/HBoxContainer/PreviousFrameButton")]
        private Button _previousFrameButton;
        [OnReadyGet("VBoxContainer/HBoxContainer/NextFrameButton")]
        private Button _nextFrameButton;
        [OnReadyGet("VBoxContainer/HBoxContainer/EndButton")]
        private Button _endButton;
        [OnReadyGet("VBoxContainer/HBoxContainer/SeekOnReplayPeerField")]
        private CheckBox _seekOnReplayPeerCheckBox;
        [OnReadyGet("VBoxContainer/HBoxContainer/ReplayContainer/HBoxContainer/AutoReplayToCurrentField")]
        private CheckBox _autoReplayToCurrentCheckbox;
        [OnReadyGet("VBoxContainer/HBoxContainer/ReplayContainer/HBoxContainer/ReplayToCurrentButton")]
        private Button _replayToCurrentButton;
        [OnReadyGet("VBoxContainer/HBoxContainer/SettingsButton")]
        private Button _settingsButton;
        [OnReadyGet("VBoxContainer/VSplitContainer/DataGraph")]
        private FrameDataGraph _dataGraph;
        [OnReadyGet("VBoxContainer/VSplitContainer/DataGrid")]
        private FrameDataGrid _dataGrid;
        [OnReadyGet("SettingsDialog")]
        private FrameViewerSettingsDialog _settingsDialog;

        private LogData _logData;
        private ReplayServer _replayServer;
        private int _replayPeerId;

        private long _replayFrame = -1;
        private long _replayLastInterpolationFrameTime = 0;

        // [peerId: int]: currentFrame: int
        private GDC.Dictionary _currentFrames = new GDC.Dictionary() { };

        [OnReady]
        public void RealReady()
        {
            _timeSpinBox.Connect("value_changed", this, nameof(_OnTimeValueChanged));
            _startButton.Connect("pressed", this, nameof(_OnStartButtonPressed));
            _previousFrameButton.Connect("pressed", this, nameof(_OnPreviousFrameButtonPressed));
            _nextFrameButton.Connect("pressed", this, nameof(_OnNextFrameButtonPressed));
            _endButton.Connect("pressed", this, nameof(_OnEndButtonPressed));
            _replayToCurrentButton.Connect("pressed", this, nameof(_OnReplayToCurrentButtonPressed));
            _autoReplayToCurrentCheckbox.Connect("toggled", this, nameof(_OnAutoReplayToCurrentFieldToggled));
            _settingsButton.Connect("pressed", this, nameof(_OnSettingsButtonPressed));
        }

        public void Construct(LogData _log_data)
        {
            _logData = _log_data;
            _dataGraph.Construct(_logData);
            _dataGrid.Construct(_logData);
            _settingsDialog.Construct(_logData, _dataGraph, _dataGrid);
        }

        public void RefreshFromLogData()
        {
            GD.Print("Refresh, is loading? ", _logData.IsLoading());
            if (_logData.IsLoading())
                return;
            _timeSpinBox.MaxValue = _logData.end_time - _logData.start_time;

            _dataGraph.RefreshFromLogData();
            _dataGrid.RefreshFromLogData();
            _settingsDialog.RefreshFromLogData();

            GD.Print("Time value changed? ", _timeSpinBox.Value);
            _replayFrame = -1;
            _OnTimeValueChanged(_timeSpinBox.Value);
        }

        public void SetReplayServer(ReplayServer _replay_server)
        {
            if (_replayServer != null)
                _replayServer.GameDisconnected -= _OnReplayServerGameDisconnected;

            _replayServer = _replay_server;
            if (_replayServer != null)
                _replayServer.GameDisconnected += _OnReplayServerGameDisconnected;
        }

        public void _OnReplayServerGameDisconnected()
        {
            _replayFrame = -1;
        }

        public void SetReplayPeerId(int _replay_peer_id)
        {
            _replayPeerId = _replay_peer_id;
        }

        public void RefreshReplay()
        {
            _replayFrame = -1;
            if (_autoReplayToCurrentCheckbox.Pressed)
                ReplayToCurrentFrame();
        }

        public void Clear()
        {
            _currentFrames.Clear();
            RefreshFromLogData();
        }

        public void _OnTimeValueChanged(double value)
        {
            GD.Print("Time value changed,  is loading: ", _logData.IsLoading());
            if (_logData.IsLoading())
                return;
            GD.Print("Seting time to new value");
            var time = (long)(value);

            // Update our tracking of the current frame.
            foreach (int peer_id in _logData.peer_ids)
            {
                LogData.FrameData frame = _logData.GetFrameByTime(peer_id, _logData.start_time + time);
                if (frame != null)
                    _currentFrames[peer_id] = frame.frame;
                else
                    _currentFrames[peer_id] = 0;
            }
            _dataGraph.cursor_time = time;
            _dataGrid.cursor_time = time;

            if (_autoReplayToCurrentCheckbox.Pressed)
                ReplayToCurrentFrame();
        }

        public void _OnPreviousFrameButtonPressed()
        {
            JumpToPreviousFrame();
        }

        public void JumpToPreviousFrame()
        {
            if (_logData.IsLoading())
                return;

            long frame_time = 0;

            if (_seekOnReplayPeerCheckBox.Pressed)
                frame_time = _GetPreviousFrameTimeForPeer(_replayPeerId);
            else
            {
                foreach (int peer_id in _currentFrames.Keys)
                    frame_time = Math.Max(frame_time, _GetPreviousFrameTimeForPeer(peer_id));
            }
            if (frame_time > _logData.start_time)
                _timeSpinBox.Value = frame_time - _logData.start_time;
            else
                _timeSpinBox.Value = 0;
        }

        public long _GetPreviousFrameTimeForPeer(int peer_id)
        {
            var frame_id = _currentFrames.Get<int>(peer_id);
            if (frame_id > 0)
                frame_id -= 1;

            LogData.FrameData frame = _logData.GetFrame(peer_id, frame_id);
            return frame.start_time;
        }

        public void _OnNextFrameButtonPressed()
        {
            JumpToNextFrame();
        }

        public void JumpToNextFrame()
        {
            if (_logData.IsLoading())
            {
                return;

            }
            var frame_time = _logData.end_time;

            if (_seekOnReplayPeerCheckBox.Pressed)
            {
                frame_time = _GetNextFrameTimeForPeer(_replayPeerId);
            }
            else
            {
                foreach (int peer_id in _currentFrames.Keys)
                {
                    var peer_frame_time = _GetNextFrameTimeForPeer(peer_id);
                    if (peer_frame_time != 0)
                        frame_time = Math.Min(frame_time, _GetNextFrameTimeForPeer(peer_id));
                }
            }
            if (frame_time > _logData.start_time)
                _timeSpinBox.Value = frame_time - _logData.start_time;
            else
                _timeSpinBox.Value = 0;
        }

        public long _GetNextFrameTimeForPeer(int peer_id)
        {
            var frame_id = _currentFrames.Get<int>(peer_id);
            if (frame_id < _logData.GetFrameCount(peer_id) - 1)
            {
                frame_id += 1;
                LogData.FrameData frame = _logData.GetFrame(peer_id, frame_id);
                return frame.start_time;
            }
            return 0;
        }

        public void ReplayToCurrentFrame()
        {
            if (_replayServer == null && !_replayServer.IsConnectedToGame())
                return;

            if (_logData.IsLoading())
                return;
            if (_logData.peer_ids.Count == 0)
                return;
            if (!_currentFrames.Contains(_replayPeerId))
                return;
            int current_frame_id = _currentFrames.Get<int>(_replayPeerId);

            // If replay_frame is ahead of current frame, we have to replay from the beginning.
            if (_replayFrame > current_frame_id)
                _replayFrame = -1;
            // Reset replay.
            if (_replayFrame == -1)
            {
                _replayLastInterpolationFrameTime = 0;
                _replayServer.SendMatchInfo(_logData, _replayPeerId);
            }
            _replayFrame += 1;
            for (long frame_id = _replayFrame; frame_id < _logData.frames.Get<GDC.Array>(_replayPeerId).Count; frame_id++)
            {
                if (frame_id > current_frame_id)
                    break;
                LogData.FrameData frame_data = _logData.GetFrame(_replayPeerId, frame_id);
                _SendReplayFrameData(frame_data);
            }
            _replayFrame = current_frame_id;
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
                    input_frames_received[_replayPeerId] = new GDC.Dictionary()
                    {
                        ["tick"] = _logData.input.Get<LogData.InputData>(tick).GetInputForPeer(_replayPeerId, _replayPeerId),
                    };
                }
                _replayLastInterpolationFrameTime = frame_data.data.GetSerializedPrimitive<long>("end_time");
            }
            else if (frame_type == Logger.FrameType.INTERPOLATION_FRAME)
            {
                var start_time = frame_data.data.GetSerializedPrimitive<long>("start_time");
                if (_replayLastInterpolationFrameTime > 0)
                    msg["delta"] = (start_time - _replayLastInterpolationFrameTime) / 1000.0;
                else
                {
                    // If we can't know the actual delta, let's use a small value that's
                    // bigger than zero, arbitrarily 1.0/120.0
                    msg["delta"] = 0.00833333;
                }
                _replayLastInterpolationFrameTime = start_time;
            }
            // Get input received from each of the peers.
            foreach (int peer_id in _logData.peer_ids)
            {
                GDC.Array ticks = frame_data.data.Get($"remote_ticks_received_from_{peer_id}", new GDC.Array() { });
                if (ticks.Count > 0)
                {
                    GDC.Dictionary peer_input_ticks = new GDC.Dictionary() { };
                    foreach (int tick in ticks)
                    {
                        peer_input_ticks[tick] = _logData.input.Get<LogData.InputData>(tick).GetInputForPeer(peer_id, _replayPeerId);
                    }
                    input_frames_received[peer_id] = peer_input_ticks;
                }
            }
            msg["input_frames_received"] = input_frames_received;

            _replayServer.SendMessage(msg);
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
                        _timeSpinBox.Value += 1;
                        break;
                    case KeyList.Down:
                        _timeSpinBox.Value -= 1;
                        break;
                }
            }
        }

        public void _OnStartButtonPressed()
        {
            _timeSpinBox.Value = 0;
        }

        public void _OnEndButtonPressed()
        {
            _timeSpinBox.Value = _timeSpinBox.MaxValue;
        }

        public void _OnDataGraphCursorTimeChanged(float cursor_time)
        {
            _timeSpinBox.Value = cursor_time;
        }

        public void _OnSettingsButtonPressed()
        {
            _settingsDialog.PopupCentered();
        }

        public void _OnReplayToCurrentButtonPressed()
        {
            ReplayToCurrentFrame();
        }

        public void _OnAutoReplayToCurrentFieldToggled(bool button_pressed)
        {
            _replayToCurrentButton.Disabled = button_pressed;
            if (button_pressed)
                ReplayToCurrentFrame();
        }
    }
}