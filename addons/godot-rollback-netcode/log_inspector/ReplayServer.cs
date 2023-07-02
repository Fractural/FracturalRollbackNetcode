
using System;
using Godot;
using GDC = Godot.Collections;

[Tool]
public class ReplayServer : Node
{

    public const var LogData = GD.Load("res://addons/godot-rollback-netcode/log_inspector/LogData.gd");

    public const string GAME_ARGUMENTS_SETTING = "network/rollback/log_inspector/replay_arguments";
    public const string GAME_PORT_SETTING = "network/rollback/log_inspector/replay_port";
    public const string MAIN_RUN_ARGS_SETTING = "editor/main_run_args";

    public TCP_Server server

    public StreamPeerTCP connection

    public __TYPE editor_interface = null;
    public int game_pid = 0;

    enum Status
    {
        NONE,
        LISTENING,
        CONNECTED,
    }

    [Signal] delegate void StartedListening();
    [Signal] delegate void StoppedListening();
    [Signal] delegate void GameConnected();
    [Signal] delegate void GameDisconnected();

    public void SetEditorInterface(__TYPE _editor_interface)
    {
        editor_interface = _editor_interface;

    }

    public void StartListening()
    {
        if (!server)
        {
            int port = 49111;
            if (ProjectSettings.HasSetting(GAME_PORT_SETTING))
            {
                port = ProjectSettings.GetSetting(GAME_PORT_SETTING);

            }
            server = new TCPServer()

            server.Listen(port, "127.0.0.1");
            EmitSignal("started_listening");

        }
    }

    public void StopListening()
    {
        if (server)
        {
            server.Stop();
            server = null;
            EmitSignal("stopped_listening");

        }
    }

    public void DisconnectFromGame(bool restart_listening = true)
    {
        if (connection)
        {
            connection.DisconnectFromHost();
            EmitSignal("game_disconnected");
            connection = null;
        }
        StopGame();
        if (restart_listening)
        {
            StartListening();

        }
    }

    public void _Notification(int what)
    {
        if (what == NOTIFICATION_PREDELETE)
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
        {
            args_string = ProjectSettings.GetSetting(GAME_ARGUMENTS_SETTING);

        }
        if (editor_interface)
        {
            var old_main_run_args = ProjectSettings.GetSetting(MAIN_RUN_ARGS_SETTING);
            ProjectSettings.SetSetting(MAIN_RUN_ARGS_SETTING, old_main_run_args + " " + args_string);
            editor_interface.PlayMainScene();
            ProjectSettings.SetSetting(MAIN_RUN_ARGS_SETTING, old_main_run_args);
        }
        else
        {
            GDC.Array args = new GDC.Array() { };
            foreach (var arg in args_string.Split(" "))
            {
                args.PushBack(arg);
            }
            game_pid = OS.Execute(OS.GetExecutablePath(), args, false);

        }
    }

    public void StopGame()
    {
        if (editor_interface && editor_interface.IsPlayingScene())
        {
            editor_interface.StopPlayingScene();
        }
        else if (game_pid != 0)
        {
            OS.Kill(game_pid);
            game_pid = 0;

        }
    }

    public bool IsGameStarted()
    {
        if (editor_interface)
        {
            return editor_interface.IsPlayingScene();
        }
        return game_pid > 0;

    }

    public bool IsConnectedToGame()
    {
        return connection && connection.IsConnectedToHost();

    }

    public int GetStatus()
    {
        if (IsConnectedToGame())
        {
            return Status.CONNECTED;
        }
        else if (server && server.IsListening())
        {
            return Status.LISTENING;
        }
        return Status.NONE;

    }

    public void SendMessage(GDC.Dictionary msg)
    {
        if (!is_connected_to_game())
        {
            GD.PushError("Replay attempting server to send message when !connected to game");
            return;

        }
        connection.put_var(msg)


    }

    public void SendMatchInfo(LogData log_data, int my_peer_id)
    {
        if (!is_connected_to_game())
        {
            return;
        }
        if (!log_data || log_data.peer_ids.Size() == 0)
        {
            return;

        }
        GDC.Array peer_ids = new GDC.Array() { };
        foreach (var peer_id in log_data.peer_ids)
        {
            if (peer_id != my_peer_id)
            {
                peer_ids.Append(peer_id);

            }
        }
        GDC.Dictionary msg = new GDC.Dictionary()
        {
            type = "setup_match",
            my_peer_id = my_peer_id,
            peer_ids = peer_ids,
            match_info = log_data.match_info,
        };
        SendMessage(msg);

    }

    public void Poll()
    {
        if (connection)
        {
            if (connection.GetStatus() == StreamPeerTCP.STATUS_NONE || connection.GetStatus() == StreamPeerTCP.STATUS_ERROR)
            {
                DisconnectFromGame();
            }
        }
        if (server && !connection && server.IsConnectionAvailable())
        {
            connection = server.TakeConnection();
            StopListening();
            EmitSignal("game_connected");

        }
    }

    public void _Process(float delta)
    {
        Poll();


    }



}