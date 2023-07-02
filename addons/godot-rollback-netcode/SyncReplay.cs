
using System;
using Godot;
using Dictionary = Godot.Collections.Dictionary;
using Array = Godot.Collections.Array;
using System.Linq;
using Fractural.Utils;

namespace Fractural.RollbackNetcode
{
    public class SyncReplay : Node
    {

        public const string GAME_PORT_SETTING = "network/rollback/log_inspector/replay_port";
        public const string MATCH_SCENE_PATH_SETTING = "network/rollback/log_inspector/replay_match_scene_path";
        public const string MATCH_SCENE_METHOD_SETTING = "network/rollback/log_inspector/replay_match_scene_method";

        public bool active = false;
        public StreamPeerTCP connection;
        public string match_scene_path;
        public string match_scene_method = "setup_match_for_replay";

        public bool _setting_up_match = false;

        public void _Ready()
        {
            if (OS.GetCmdlineArgs().Contains("replay"))
            {
                if (!ProjectSettings.HasSetting(MATCH_SCENE_PATH_SETTING))
                {
                    _ShowErrorAndQuit("Match scene !configured for replay");
                    return;
                }
                match_scene_path = ProjectSettingsUtils.GetSetting<string>(MATCH_SCENE_PATH_SETTING);

                if (ProjectSettings.HasSetting(MATCH_SCENE_METHOD_SETTING))
                {
                    match_scene_method = ProjectSettingsUtils.GetSetting<string>(MATCH_SCENE_METHOD_SETTING);
                }
                active = true;

                GD.Print("Connecting to replay server...");
                if (!ConnectToReplayServer())
                {
                    _ShowErrorAndQuit("Unable to connect to replay server");
                    return;
                }
            }
        }

        public void _ShowErrorAndQuit(String msg)
        {
            OS.Alert(msg);
            GetTree().Quit(1);
        }

        public bool ConnectToReplayServer()
        {
            if (IsConnectedToReplayServer())
            {
                return true;
            }
            if (connection != null)
            {
                connection.DisconnectFromHost();
                connection = null;
            }
            int port = 49111;
            if (ProjectSettings.HasSetting(GAME_PORT_SETTING))
            {
                port = ProjectSettingsUtils.GetSetting<int>(GAME_PORT_SETTING);

            }
            connection = new StreamPeerTCP()

            return connection.ConnectToHost("127.0.0.1", port) == OK;

        }

        public bool IsConnectedToReplayServer()
        {
            return connection && connection.IsConnectedToHost();

        }

        public void Poll()
        {
            if (!active)
            {
                return;
            }
            if (connection)
            {
                var status = connection.GetStatus();
                if (status == StreamPeerTCP.STATUS_CONNECTED)
                {
                    while (!_setting_up_match && connection.GetAvailableBytes() >= 4)
                    {
                        var data = connection.get_var()

                        if (data is Dictionary)
                        {
                            ProcessMessage(data);
                        }
                    }
                }
                else if (status == StreamPeerTCP.STATUS_NONE)
                {
                    GetTree().Quit();
                }
                else if (status == StreamPeerTCP.STATUS_ERROR)
                {
                    OS.Alert("Error in connection to replay server");
                    GetTree().Quit(1);

                }
            }
        }

        public void _Process(float delta)
        {
            Poll();

        }

        public void ProcessMessage(Dictionary msg)
        {
            if (!msg.Has("type"))
            {
                GD.PushError("SyncReplay message has no "type" property: %s" % msg);
                return;

            }
            var type = msg["type"];
            switch (type)
            {

            {
                "setup_match",
				var my_peer_id = msg.Get("my_peer_id"}, 1);
            var peer_ids = msg.Get("peer_ids", new Array() { });
            var match_info = msg.Get("match_info", new Dictionary() { });
            _DoSetupMatch1(my_peer_id, peer_ids, match_info)



            {
                "load_state",
				var state = msg.Get("state"}, new Dictionary() { });
            _DoLoadState(state)



            {
                "execute_frame",
				_DoExecuteFrame(msg)
				
			case _:
                GD.PushError("SyncReplay message has unknown type: %s" % type);

                break;
            }
        }
    }

    public void _DoSetupMatch1(int my_peer_id, Array peer_ids, Dictionary match_info)
    {
        SyncManager.Stop();
        SyncManager.ClearPeers();

        SyncManager.network_adaptor = DummyNetworkAdaptor.new(my_peer_id)

        SyncManager.mechanized = true;

        foreach (var peer_id in peer_ids)
        {
            SyncManager.AddPeer(peer_id);

        }
        if (GetTree().ChangeScene(match_scene_path) != OK)
        {
            _ShowErrorAndQuit("Unable to change scene to: %s" % match_scene_path);
            return;

        }
        _setting_up_match = true;
        CallDeferred("_do_setup_match2", my_peer_id, peer_ids, match_info);

    }

    public void _DoSetupMatch2(int my_peer_id, Array peer_ids, Dictionary match_info)
    {
        _setting_up_match = false;

        var match_scene = GetTree().current_scene;
        if (!Utils.HasInteropMethod(match_scene, match_scene_method))
        {
            _ShowErrorAndQuit("Match scene has no such method: %s" % match_scene_method);
            return;

            // Call the scene's setup method.
        }
        Utils.CallInteropMethod(match_scene, match_scene_method, new Array() { my_peer_id, peer_ids, match_info });

        SyncManager.Start();

    }

    public void _DoLoadState(Dictionary state)
    {
        state = SyncManager.hash_serializer.Unserialize(state);
        SyncManager._CallLoadState(state);

    }

    public void _DoExecuteFrame(Dictionary msg)
    {
        int frame_type = msg["frame_type"];
        Dictionary input_frames_received = msg.Get("input_frames_received", new Dictionary() { });
        int rollback_ticks = msg.Get("rollback_ticks", 0);

        input_frames_received = SyncManager.hash_serializer.Unserialize(input_frames_received);
        SyncManager.mechanized_input_received = input_frames_received;
        SyncManager.mechanized_rollback_ticks = rollback_ticks;

        switch (frame_type)
        {
            case Logger.FrameType.TICK:
                SyncManager.ExecuteMechanizedTick();

                break;
            case Logger.FrameType.INTERPOLATION_FRAME:
                SyncManager.ExecuteMechanizedInterpolationFrame(msg["delta"]);

                break;
            case Logger.FrameType.INTERFRAME:
                SyncManager.ExecuteMechanizedInterframe();

                break;
            case _:
                SyncManager.ResetMechanizedData();



                break;
        }
    }
}
}