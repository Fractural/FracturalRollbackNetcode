
using System;
using Fractural.GodotCodeGenerator.Attributes;
using Fractural.Utils;
using Godot;
using GDC = Godot.Collections;

namespace Fractural.RollbackNetcode
{
    // TODO NOW: Finish this;
    [Tool]
    public partial class LogInspector : Control
    {
        public const var LogData = GD.Load("res://addons/godot-rollback-netcode/log_inspector/LogData.gd");
        public const var ReplayServer = GD.Load("res://addons/godot-rollback-netcode/log_inspector/ReplayServer.gd");

        [OnReadyGet("FileDialog")]
        public FileDialog file_dialog;
        [OnReadyGet("ProgressDialog")]
        public ProgressDialog progress_dialog;
        [OnReadyGet("MarginContainer/VBoxContainer/LoadToolbar/DataDescriptionLabel")]
        public Label data_description_label;
        public string data_description_label_default_text;
        [OnReadyGet("MarginContainer/VBoxContainer/LoadToolbar/ModeButton")]
        public Button mode_button;
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
        public GDC.Array _files_to_load = new GDC.Array() { };

        public override void _Ready()
        {
            data_description_label_default_text = data_description_label.Text;
            state_input_viewer.SetLogData(log_data);
            frame_viewer.SetLogData(log_data);

            log_data.Connect("load_error", this, "_on_log_data_load_error");
            log_data.Connect("load_progress", this, "_on_log_data_load_progress");
            log_data.Connect("load_finished", this, "_on_log_data_load_finished");
            log_data.Connect("data_updated", this, "refresh_from_log_data");

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
            data_description_label.text = data_description_label_default_text;
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
                {
                    _files_to_load.Add(path);
                }
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
            {
                return;

            }
            data_description_label.text = "%s Logs (peer ids: %s) && %s ticks" % [log_data.peer_ids.Size(), log_data.peer_ids, log_data.max_tick]


        if (log_data.mismatches.Size() > 0)
            {
                data_description_label.text += " with %s mismatches" % log_data.mismatches.Size();

            }
            show_peer_field.Clear();
            foreach (var peer_id in log_data.peer_ids)
            {
                show_peer_field.AddItem("Peer %s" % peer_id, peer_id);

            }
            RefreshReplay();
            state_input_viewer.RefreshFromLogData();
            frame_viewer.RefreshFromLogData();

        }

        public void _OnLogDataLoadError(__TYPE msg)
        {
            progress_dialog.Hide();
            _files_to_load.Clear();
            OS.Alert(msg);

        }

        public void _OnLogDataLoadProgress(__TYPE current, __TYPE total)
        {
            progress_dialog.UpdateProgress(current, total);

        }

        public void _OnLogDataLoadFinished()
        {
            if (_files_to_load.Size() > 0)
            {
                var next_file = _files_to_load.PopFront();
                progress_dialog.SetLabel(LOADING_LABEL % next_file.GetFile());
                log_data.LoadLogFile(next_file);
            }
            else
            {
                progress_dialog.Hide();

            }
        }

        public void _OnModeButtonItemSelected(int index)
        {
            state_input_viewer.visible = false;
            frame_viewer.visible = false;

            if (index == DataMode.STATE_INPUT)
            {
                state_input_viewer.visible = true;
            }
            else if (index == DataMode.FRAME)
            {
                frame_viewer.visible = true;

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
            {
                replay_server.DisconnectFromGame(false);
            }
            else
            {
                replay_server.StopListening();

            }
        }

        public void UpdateReplayServerStatus()
        {
            switch (replay_server.GetStatus())
            {
                case ReplayServer.Status.NONE:
                    replay_server_status_label.text = "Disabled.";
                    start_server_button.disabled = false;
                    stop_server_button.disabled = true;
                    disconnect_button.disabled = true;
                    launch_game_button.disabled = true;
                    break;
                case ReplayServer.Status.LISTENING:
                    replay_server_status_label.text = "Listening for connections...";
                    start_server_button.disabled = true;
                    stop_server_button.disabled = false;
                    disconnect_button.disabled = true;
                    launch_game_button.disabled = false;
                    break;
                case ReplayServer.Status.CONNECTED:
                    replay_server_status_label.text = "Connected to game.";
                    start_server_button.disabled = true;
                    stop_server_button.disabled = false;
                    disconnect_button.disabled = false;
                    launch_game_button.disabled = true;

                    break;
            }
        }

        public void RefreshReplay()
        {
            var replay_peer_id = show_peer_field.GetSelectedId();

            if (replay_server)
            {
                replay_server.SendMatchInfo(log_data, replay_peer_id);

            }
            state_input_viewer.SetReplayPeerId(replay_peer_id);
            frame_viewer.SetReplayPeerId(replay_peer_id);

            var mode = mode_button.selected;
            if (mode == DataMode.STATE_INPUT)
            {
                state_input_viewer.RefreshReplay();
            }
            else if (mode == DataMode.FRAME)
            {
                frame_viewer.RefreshReplay();

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