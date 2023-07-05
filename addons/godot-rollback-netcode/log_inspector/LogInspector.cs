
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
        public FileDialog file_dialog;
        [OnReadyGet("ProgressDialog")]
        public ProgressDialog progress_dialog;
        [OnReadyGet("MarginContainer/VBoxContainer/LoadToolbar/DataDescriptionLabel")]
        public Label data_description_label;
        public string data_description_label_default_text;
        [OnReadyGet("MarginContainer/VBoxContainer/LoadToolbar/ModeButton")]
        public OptionButton mode_button;
        [OnReadyGet("MarginContainer/VBoxContainer/StateInputViewer")]
        public StateInputViewer state_input_viewer;
        [OnReadyGet("MarginContainer/VBoxContainer/FrameViewer")]
        public FrameViewer frame_viewer;
        [OnReadyGet("ReplayServer")]
        public ReplayServer replay_server;
        [OnReadyGet("MarginContainer/VBoxContainer/ReplayToolbar/ServerContainer/HBoxContainer/ReplayStatusLabel")]
        public Label replay_server_status_label;
        [OnReadyGet("MarginContainer/VBoxContainer/ReplayToolbar/ServerContainer/HBoxContainer/StartServerButton")]
        public Button start_server_button;
        [OnReadyGet("MarginContainer/VBoxContainer/ReplayToolbar/ServerContainer/HBoxContainer/StopServerButton")]
        public Button stop_server_button;
        [OnReadyGet("MarginContainer/VBoxContainer/ReplayToolbar/ServerContainer/HBoxContainer/DisconnectButton")]
        public Button disconnect_button;
        [OnReadyGet("MarginContainer/VBoxContainer/ReplayToolbar/ClientContainer/HBoxContainer/LaunchGameButton")]
        public Button launch_game_button;
        [OnReadyGet("MarginContainer/VBoxContainer/ReplayToolbar/ClientContainer/HBoxContainer/ShowPeerField")]
        public OptionButton show_peer_field;

        enum DataMode
        {
            STATE_INPUT,
            FRAME,
        }

        public LogData log_data = new LogData();
        // filePaths: string[]
        public GDC.Array _files_to_load = new GDC.Array() { };

        [OnReady]
        public void RealReady()
        {
            data_description_label_default_text = data_description_label.Text;
            state_input_viewer.SetLogData(log_data);
            frame_viewer.SetLogData(log_data);

            log_data.LoadError += _OnLogDataLoadError;
            log_data.LoadProgress += _OnLogDataLoadProgress;
            log_data.LoadFinished += _OnLogDataLoadFinished;
            log_data.DataUpdated += RefreshFromLogData;

            state_input_viewer.SetReplayServer(replay_server);
            frame_viewer.SetReplayServer(replay_server);

            file_dialog.CurrentDir = OS.GetUserDataDir() + "/detailed_logs/";

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
                log_data.LoadError -= _OnLogDataLoadError;
                log_data.LoadProgress -= _OnLogDataLoadProgress;
                log_data.LoadFinished -= _OnLogDataLoadFinished;
                log_data.DataUpdated -= RefreshFromLogData;
            }
        }

        public void _OnLogInspectorAboutToShow()
        {
            StartLogInspector();
        }

        public void StartLogInspector()
        {
            UpdateReplayServerStatus();
            replay_server.StartListening();

        }

        public void SetEditorInterface(EditorInterface editor_interface)
        {
            replay_server.SetEditorInterface(editor_interface);
        }

        public void _OnClearButtonPressed()
        {
            if (log_data.IsLoading())
                return;
            log_data.Clear();
            data_description_label.Text = data_description_label_default_text;
            state_input_viewer.Clear();
            frame_viewer.Clear();
        }

        public void _OnAddLogButtonPressed()
        {
            file_dialog.CurrentFile = "";
            file_dialog.CurrentPath = "";
            file_dialog.ShowModal();
            file_dialog.Invalidate();
        }

        public void _OnFileDialogFilesSelected(string[] paths)
        {
            if (paths.Length > 0)
            {
                bool already_loading = (_files_to_load.Count > 0) || log_data.IsLoading();
                foreach (var path in paths)
                    _files_to_load.Add(path);

                if (!already_loading)
                {
                    var first_file = _files_to_load.PopFrontList<string>();
                    UpdateProgressDialogLabel(first_file.GetFile());
                    progress_dialog.PopupCentered();
                    log_data.LoadLogFile(first_file);
                }
            }
        }

        public void RefreshFromLogData()
        {
            if (log_data.IsLoading())
                return;

            data_description_label.Text = $"{log_data.peer_ids.Count} Logs (peer ids: {log_data.peer_ids}) && {log_data.max_tick} ticks";

            if (log_data.mismatches.Count > 0)
                data_description_label.Text += $" with {log_data.mismatches.Count} mismatches";

            show_peer_field.Clear();
            foreach (int peer_id in log_data.peer_ids)
                show_peer_field.AddItem($"Peer {peer_id}", peer_id);

            RefreshReplay();
            state_input_viewer.RefreshFromLogData();
            frame_viewer.RefreshFromLogData();

        }

        public void _OnLogDataLoadError(string msg)
        {
            progress_dialog.Hide();
            _files_to_load.Clear();
            OS.Alert(msg);
        }

        public void _OnLogDataLoadProgress(ulong current, ulong total)
        {
            progress_dialog.UpdateProgress(current, total);
        }

        public void _OnLogDataLoadFinished()
        {
            if (_files_to_load.Count > 0)
            {
                var next_file = _files_to_load.PopFrontList<string>();
                UpdateProgressDialogLabel(next_file.GetFile());
                log_data.LoadLogFile(next_file);
            }
            else
                progress_dialog.Hide();
        }

        public void _OnModeButtonItemSelected(int index)
        {
            state_input_viewer.Visible = false;
            frame_viewer.Visible = false;

            switch ((DataMode)index)
            {
                case DataMode.STATE_INPUT:
                    state_input_viewer.Visible = true;
                    break;
                case DataMode.FRAME:
                    frame_viewer.Visible = true;
                    break;
            }
            RefreshReplay();
        }

        public void _OnStartServerButtonPressed()
        {
            replay_server.StartListening();
        }

        public void _OnStopServerButtonPressed()
        {
            if (replay_server.IsConnectedToGame())
                replay_server.DisconnectFromGame(false);
            else
                replay_server.StopListening();
        }

        public void UpdateReplayServerStatus()
        {
            switch (replay_server.GetStatus())
            {
                case ReplayServer.Status.NONE:
                    replay_server_status_label.Text = "Disabled.";
                    start_server_button.Disabled = false;
                    stop_server_button.Disabled = true;
                    disconnect_button.Disabled = true;
                    launch_game_button.Disabled = true;
                    break;
                case ReplayServer.Status.LISTENING:
                    replay_server_status_label.Text = "Listening for connections...";
                    start_server_button.Disabled = true;
                    stop_server_button.Disabled = false;
                    disconnect_button.Disabled = true;
                    launch_game_button.Disabled = false;
                    break;
                case ReplayServer.Status.CONNECTED:
                    replay_server_status_label.Text = "Connected to game.";
                    start_server_button.Disabled = true;
                    stop_server_button.Disabled = false;
                    disconnect_button.Disabled = false;
                    launch_game_button.Disabled = true;

                    break;
            }
        }

        public void RefreshReplay()
        {
            var replay_peer_id = show_peer_field.GetSelectedId();

            if (replay_server != null)
                replay_server.SendMatchInfo(log_data, replay_peer_id);

            state_input_viewer.SetReplayPeerId(replay_peer_id);
            frame_viewer.SetReplayPeerId(replay_peer_id);

            var mode = mode_button.Selected;
            switch ((DataMode)mode)
            {
                case DataMode.STATE_INPUT:
                    state_input_viewer.RefreshReplay();
                    break;
                case DataMode.FRAME:
                    frame_viewer.RefreshReplay();
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
            replay_server.LaunchGame();
        }

        public void _OnDisconnectButtonPressed()
        {
            replay_server.DisconnectFromGame();
        }

        public void _OnShowPeerFieldItemSelected(int index)
        {
            RefreshReplay();
        }

        private void UpdateProgressDialogLabel(string file)
        {
            progress_dialog.SetLabel($"Loading {file}...");
        }
    }
}