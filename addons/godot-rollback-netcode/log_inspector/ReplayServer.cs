
using System;
using System.Collections.Generic;
using Fractural.Utils;
using Godot;
using GDC = Godot.Collections;

namespace Fractural.RollbackNetcode
{
    [Tool]
    public class ReplayServer : Node
    {
        public const string GAME_ARGUMENTS_SETTING = "network/rollback/log_inspector/replay_arguments";
        public const string GAME_PORT_SETTING = "network/rollback/log_inspector/replay_port";
        public const string MAIN_RUN_ARGS_SETTING = "editor/main_run_args";

        public TCP_Server server;
        public StreamPeerTCP connection;

        public EditorInterface editor_interface = null;
        public int game_pid = 0;

        public enum Status
        {
            NONE,
            LISTENING,
            CONNECTED,
        }

        public event Action StartedListening;
        public event Action StoppedListening;
        public event Action GameConnected;
        public event Action GameDisconnected;

        public void Construct(EditorInterface _editor_interface)
        {
            editor_interface = _editor_interface;
        }

        public void StartListening()
        {
            if (server == null)
            {
                ushort port = 49111;
                if (ProjectSettings.HasSetting(GAME_PORT_SETTING))
                    port = ProjectSettingsUtils.GetSetting<ushort>(GAME_PORT_SETTING);

                server = new TCP_Server();
                server.Listen(port, "127.0.0.1");
                StartedListening?.Invoke();
            }
        }

        public void StopListening()
        {
            if (server != null)
            {
                server.Stop();
                server = null;
                StoppedListening?.Invoke();
            }
        }

        public void DisconnectFromGame(bool restart_listening = true)
        {
            if (connection != null)
            {
                connection.DisconnectFromHost();
                GameDisconnected?.Invoke();
                connection = null;
            }
            StopGame();
            if (restart_listening)
                StartListening();
        }

        public void _Notification(int what)
        {
            if (what == NotificationPredelete)
            {
                DisconnectFromGame(false);
                StopListening();
                StopGame();
            }
        }

        public void LaunchGame()
        {
            StopGame();

            string args_string = "replay";
            if (ProjectSettings.HasSetting(GAME_ARGUMENTS_SETTING))
                args_string = ProjectSettingsUtils.GetSetting<string>(GAME_ARGUMENTS_SETTING);
            if (editor_interface != null)
            {
                var old_main_run_args = ProjectSettings.GetSetting(MAIN_RUN_ARGS_SETTING);
                ProjectSettings.SetSetting(MAIN_RUN_ARGS_SETTING, old_main_run_args + " " + args_string);
                editor_interface.PlayMainScene();
                ProjectSettings.SetSetting(MAIN_RUN_ARGS_SETTING, old_main_run_args);
            }
            else
            {
                List<string> args = new List<string>();
                foreach (var arg in args_string.Split(" "))
                    args.PushBackList(arg);
                game_pid = OS.Execute(OS.GetExecutablePath(), args.ToArray(), false);
            }
        }

        public void StopGame()
        {
            if (editor_interface != null && editor_interface.IsPlayingScene())
                editor_interface.StopPlayingScene();
            else if (game_pid != 0)
            {
                OS.Kill(game_pid);
                game_pid = 0;
            }
        }

        public bool IsGameStarted()
        {
            if (editor_interface != null)
                return editor_interface.IsPlayingScene();
            return game_pid > 0;
        }

        public bool IsConnectedToGame()
        {
            return connection != null && connection.IsConnectedToHost();
        }

        public Status GetStatus()
        {
            if (IsConnectedToGame())
                return Status.CONNECTED;
            else if (server != null && server.IsListening())
                return Status.LISTENING;
            return Status.NONE;
        }

        public void SendMessage(GDC.Dictionary msg)
        {
            if (!IsConnectedToGame())
            {
                GD.PushError("Replay attempting server to send message when !connected to game");
                return;
            }
            connection.PutVar(msg);
        }

        public void SendMatchInfo(LogData log_data, int my_peer_id)
        {
            if (!IsConnectedToGame())
                return;

            if (log_data == null || log_data.peer_ids.Count == 0)
                return;

            GDC.Array peer_ids = new GDC.Array() { };
            foreach (int peer_id in log_data.peer_ids)
            {
                if (peer_id != my_peer_id)
                    peer_ids.Add(peer_id);
            }
            GDC.Dictionary msg = new GDC.Dictionary()
            {
                ["type"] = "setup_match",
                ["my_peer_id"] = my_peer_id,
                ["peer_ids"] = peer_ids,
                ["match_info"] = log_data.match_info,
            };
            SendMessage(msg);
        }

        public void Poll()
        {
            if (connection != null)
            {
                if (connection.GetStatus() == StreamPeerTCP.Status.None || connection.GetStatus() == StreamPeerTCP.Status.Error)
                    DisconnectFromGame();
            }
            if (server != null && connection == null && server.IsConnectionAvailable())
            {
                connection = server.TakeConnection();
                StopListening();
                GameConnected?.Invoke();
            }
        }

        public override void _Process(float delta)
        {
            Poll();
        }
    }
}