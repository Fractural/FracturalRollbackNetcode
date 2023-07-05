
using System;
using Fractural.GodotCodeGenerator.Attributes;
using Fractural.Utils;
using Godot;
using GDC = Godot.Collections;

namespace Fractural.RollbackNetcode
{
    [Tool]
    public partial class LogInspector : WindowDialog
    {
        [OnReadyGet("FileDialog")]
        private FileDialog _fileDialog;
        [OnReadyGet("ProgressDialog")]
        private ProgressDialog _progressDialog;
        [OnReadyGet("MarginContainer/VBoxContainer/LoadToolbar/DataDescriptionLabel")]
        private Label _dataDescriptionLabel;
        private string _dataDescriptionLabelDefaultText;
        [OnReadyGet("MarginContainer/VBoxContainer/LoadToolbar/ClearButton")]
        private Button _clearButton;
        [OnReadyGet("MarginContainer/VBoxContainer/LoadToolbar/AddLogButton")]
        private Button _addLogButton;
        [OnReadyGet("MarginContainer/VBoxContainer/LoadToolbar/ModeButton")]
        private OptionButton _modeButton;
        [OnReadyGet("MarginContainer/VBoxContainer/StateInputViewer")]
        private StateInputViewer _stateInputViewer;
        [OnReadyGet("MarginContainer/VBoxContainer/FrameViewer")]
        private FrameViewer _frameViewer;
        [OnReadyGet("ReplayServer")]
        private ReplayServer _replayServer;
        [OnReadyGet("MarginContainer/VBoxContainer/ReplayToolbar/ServerContainer/HBoxContainer/ReplayStatusLabel")]
        private Label _replayServerStatusLabel;
        [OnReadyGet("MarginContainer/VBoxContainer/ReplayToolbar/ServerContainer/HBoxContainer/StartServerButton")]
        private Button _startServerButton;
        [OnReadyGet("MarginContainer/VBoxContainer/ReplayToolbar/ServerContainer/HBoxContainer/StopServerButton")]
        private Button _stopServerButton;
        [OnReadyGet("MarginContainer/VBoxContainer/ReplayToolbar/ServerContainer/HBoxContainer/DisconnectButton")]
        private Button _disconnectButton;
        [OnReadyGet("MarginContainer/VBoxContainer/ReplayToolbar/ClientContainer/HBoxContainer/LaunchGameButton")]
        private Button _launchGameButton;
        [OnReadyGet("MarginContainer/VBoxContainer/ReplayToolbar/ClientContainer/HBoxContainer/ShowPeerField")]
        private OptionButton _showPeerOptionButton;

        private enum DataMode
        {
            STATE_INPUT,
            FRAME,
        }

        private LogData _logData = new LogData();
        // filePaths: string[]
        private GDC.Array _files_to_load = new GDC.Array() { };

        [OnReady]
        public void RealReady()
        {
            Connect("about_to_show", this, nameof(_OnLogInspectorAboutToShow));
            _fileDialog.Connect("files_selected", this, nameof(_OnFileDialogFilesSelected));
            _clearButton.Connect("pressed", this, nameof(_OnClearButtonPressed));
            _addLogButton.Connect("pressed", this, nameof(_OnAddLogButtonPressed));
            _modeButton.Connect("item_selected", this, nameof(_OnModeButtonItemSelected));
            _startServerButton.Connect("pressed", this, nameof(_OnStartServerButtonPressed));
            _stopServerButton.Connect("pressed", this, nameof(_OnStopServerButtonPressed));
            _disconnectButton.Connect("pressed", this, nameof(_OnDisconnectButtonPressed));
            _launchGameButton.Connect("pressed", this, nameof(_OnLaunchGameButtonPressed));
            _showPeerOptionButton.Connect("item_selected", this, nameof(_OnShowPeerFieldItemSelected));

            _replayServer.GameConnected += _OnReplayServerGameConnected;
            _replayServer.GameDisconnected += _OnReplayServerGameDisconnected;
            _replayServer.StartedListening += _OnReplayServerStartedListening;
            _replayServer.StoppedListening += _OnReplayServerStoppedListening;

            _dataDescriptionLabelDefaultText = _dataDescriptionLabel.Text;
            _stateInputViewer.Construct(_logData);
            _frameViewer.Construct(_logData);

            _logData.LoadError += _OnLogDataLoadError;
            _logData.LoadProgress += _OnLogDataLoadProgress;
            _logData.LoadFinished += _OnLogDataLoadFinished;
            _logData.DataUpdated += RefreshFromLogData;

            _stateInputViewer.SetReplayServer(_replayServer);
            _frameViewer.SetReplayServer(_replayServer);

            _fileDialog.CurrentDir = OS.GetUserDataDir() + "/detailed_logs/";

            // Show && make full screen if the scene is being run on its own.
            if (GetParent() == GetTree().Root)
            {
                Visible = true;
                AnchorRight = 1;
                AnchorBottom = 1;
                MarginRight = 0;
                MarginBottom = 0;
                StartLogInspector();
            }
        }

        public override void _Notification(int what)
        {
            if (what == NotificationPredelete)
            {
                _replayServer.GameConnected -= _OnReplayServerGameConnected;
                _replayServer.GameDisconnected -= _OnReplayServerGameDisconnected;
                _replayServer.StartedListening -= _OnReplayServerStartedListening;
                _replayServer.StoppedListening -= _OnReplayServerStoppedListening;

                _logData.LoadError -= _OnLogDataLoadError;
                _logData.LoadProgress -= _OnLogDataLoadProgress;
                _logData.LoadFinished -= _OnLogDataLoadFinished;
                _logData.DataUpdated -= RefreshFromLogData;
            }
        }

        public void _OnLogInspectorAboutToShow()
        {
            StartLogInspector();
        }

        public void StartLogInspector()
        {
            UpdateReplayServerStatus();
            _replayServer.StartListening();

        }

        public void Construct(EditorInterface editor_interface)
        {
            _replayServer.Construct(editor_interface);
        }

        public void _OnClearButtonPressed()
        {
            if (_logData.IsLoading())
                return;
            _logData.Clear();
            _dataDescriptionLabel.Text = _dataDescriptionLabelDefaultText;
            _stateInputViewer.Clear();
            _frameViewer.Clear();
        }

        public void _OnAddLogButtonPressed()
        {
            _fileDialog.CurrentFile = "";
            _fileDialog.CurrentPath = "";
            _fileDialog.ShowModal();
            _fileDialog.Invalidate();
        }

        public void _OnFileDialogFilesSelected(string[] paths)
        {
            if (paths.Length > 0)
            {
                bool already_loading = (_files_to_load.Count > 0) || _logData.IsLoading();
                foreach (var path in paths)
                    _files_to_load.Add(path);

                if (!already_loading)
                {
                    var first_file = _files_to_load.PopFrontList<string>();
                    UpdateProgressDialogLabel(first_file.GetFile());
                    _progressDialog.PopupCentered();
                    _logData.LoadLogFile(first_file);
                }
            }
        }

        public void RefreshFromLogData()
        {
            if (_logData.IsLoading())
                return;

            _dataDescriptionLabel.Text = $"{_logData.peer_ids.Count} Logs (peer ids: {_logData.peer_ids}) && {_logData.max_tick} ticks";

            if (_logData.mismatches.Count > 0)
                _dataDescriptionLabel.Text += $" with {_logData.mismatches.Count} mismatches";

            _showPeerOptionButton.Clear();
            foreach (int peer_id in _logData.peer_ids)
                _showPeerOptionButton.AddItem($"Peer {peer_id}", peer_id);

            RefreshReplay();
            _stateInputViewer.RefreshFromLogData();
            _frameViewer.RefreshFromLogData();
        }

        public void _OnLogDataLoadError(string msg)
        {
            _progressDialog.Hide();
            _files_to_load.Clear();
            OS.Alert(msg);
        }

        public void _OnLogDataLoadProgress(ulong current, ulong total)
        {
            _progressDialog.UpdateProgress(current, total);
        }

        public void _OnLogDataLoadFinished()
        {
            if (_files_to_load.Count > 0)
            {
                var next_file = _files_to_load.PopFrontList<string>();
                UpdateProgressDialogLabel(next_file.GetFile());
                _logData.LoadLogFile(next_file);
            }
            else
                _progressDialog.Hide();
        }

        public void _OnModeButtonItemSelected(int index)
        {
            _stateInputViewer.Visible = false;
            _frameViewer.Visible = false;

            switch ((DataMode)index)
            {
                case DataMode.STATE_INPUT:
                    _stateInputViewer.Visible = true;
                    break;
                case DataMode.FRAME:
                    _frameViewer.Visible = true;
                    break;
            }
            RefreshReplay();
        }

        public void _OnStartServerButtonPressed()
        {
            _replayServer.StartListening();
        }

        public void _OnStopServerButtonPressed()
        {
            if (_replayServer.IsConnectedToGame())
                _replayServer.DisconnectFromGame(false);
            else
                _replayServer.StopListening();
        }

        public void UpdateReplayServerStatus()
        {
            switch (_replayServer.GetStatus())
            {
                case ReplayServer.Status.NONE:
                    _replayServerStatusLabel.Text = "Disabled.";
                    _startServerButton.Disabled = false;
                    _stopServerButton.Disabled = true;
                    _disconnectButton.Disabled = true;
                    _launchGameButton.Disabled = true;
                    break;
                case ReplayServer.Status.LISTENING:
                    _replayServerStatusLabel.Text = "Listening for connections...";
                    _startServerButton.Disabled = true;
                    _stopServerButton.Disabled = false;
                    _disconnectButton.Disabled = true;
                    _launchGameButton.Disabled = false;
                    break;
                case ReplayServer.Status.CONNECTED:
                    _replayServerStatusLabel.Text = "Connected to game.";
                    _startServerButton.Disabled = true;
                    _stopServerButton.Disabled = false;
                    _disconnectButton.Disabled = false;
                    _launchGameButton.Disabled = true;

                    break;
            }
        }

        public void RefreshReplay()
        {
            var replay_peer_id = _showPeerOptionButton.GetSelectedId();

            if (_replayServer != null)
                _replayServer.SendMatchInfo(_logData, replay_peer_id);

            _stateInputViewer.SetReplayPeerId(replay_peer_id);
            _frameViewer.SetReplayPeerId(replay_peer_id);

            var mode = _modeButton.Selected;
            switch ((DataMode)mode)
            {
                case DataMode.STATE_INPUT:
                    _stateInputViewer.RefreshReplay();
                    break;
                case DataMode.FRAME:
                    _frameViewer.RefreshReplay();
                    break;
            }
        }

        public void _OnReplayServerStartedListening()
        {
            UpdateReplayServerStatus();
        }

        public void _OnReplayServerStoppedListening()
        {
            UpdateReplayServerStatus();
        }

        public void _OnReplayServerGameConnected()
        {
            UpdateReplayServerStatus();
            RefreshReplay();
        }

        public void _OnReplayServerGameDisconnected()
        {
            UpdateReplayServerStatus();
        }

        public void _OnLaunchGameButtonPressed()
        {
            _replayServer.LaunchGame();
        }

        public void _OnDisconnectButtonPressed()
        {
            _replayServer.DisconnectFromGame();
        }

        public void _OnShowPeerFieldItemSelected(int index)
        {
            RefreshReplay();
        }

        private void UpdateProgressDialogLabel(string file)
        {
            _progressDialog.SetLabel($"Loading {file}...");
        }
    }
}