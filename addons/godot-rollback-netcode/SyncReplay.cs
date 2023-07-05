
using System;
using Godot;
using GDC = Godot.Collections;
using System.Linq;
using Fractural.Utils;

namespace Fractural.RollbackNetcode
{
    public class SyncReplay : Node
    {
        public static SyncReplay Global { get; private set; }

        public const string GAME_PORT_SETTING = "network/rollback/log_inspector/replay_port";
        public const string MATCH_SCENE_PATH_SETTING = "network/rollback/log_inspector/replay_match_scene_path";
        public const string MATCH_SCENE_METHOD_SETTING = "network/rollback/log_inspector/replay_match_scene_method";

        public bool active = false;
        public StreamPeerTCP connection;
        public string match_scene_path;
        public string match_scene_method = "setup_match_for_replay";

        public bool _setting_up_match = false;

        private SyncManager _syncManager;

        public override void _Ready()
        {
            if (Global != null)
            {
                QueueFree();
                return;
            }
            Global = this;
            _syncManager = SyncManager.Global;

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

        public override void _Process(float delta)
        {
            Poll();
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
            connection = new StreamPeerTCP();
            return connection.ConnectToHost("127.0.0.1", port) == Error.Ok;
        }

        public bool IsConnectedToReplayServer()
        {
            return connection != null && connection.IsConnectedToHost();
        }

        public void Poll()
        {
            if (!active)
            {
                return;
            }
            if (connection != null)
            {
                var status = connection.GetStatus();
                if (status == StreamPeerTCP.Status.Connected)
                {
                    while (!_setting_up_match && connection.GetAvailableBytes() >= 4)
                    {
                        var data = connection.GetVar();
                        if (data is GDC.Dictionary dict)
                            ProcessMessage(dict);
                    }
                }
                else if (status == StreamPeerTCP.Status.None)
                {
                    GetTree().Quit();
                }
                else if (status == StreamPeerTCP.Status.Error)
                {
                    OS.Alert("Error in connection to replay server");
                    GetTree().Quit(1);
                }
            }
        }

        public void ProcessMessage(GDC.Dictionary msg)
        {
            if (!msg.Contains("type"))
            {
                GD.PushError($"SyncReplay message has no \"type\" property: {msg}");
                return;

            }
            var type = msg.Get<string>("type");
            switch (type)
            {
                case "setup_match":
                    var myPeerId = msg.Get("my_peer_id", 1);
                    var peerIds = msg.Get("peer_ids", new GDC.Array() { });
                    var matchInfo = msg.Get("match_info", new GDC.Dictionary() { });
                    _DoSetupMatch1(myPeerId, peerIds, matchInfo);
                    break;
                case "load_state":
                    var state = msg.Get("state", new GDC.Dictionary() { });
                    _DoLoadState(state);
                    break;
                case "execute_frame":
                    _DoExecuteFrame(msg);
                    break;
                default:
                    GD.PushError($"SyncReplay message has unknown type: {type}");
                    break;
            }
        }

        private void _ShowErrorAndQuit(string msg)
        {
            OS.Alert(msg);
            GetTree().Quit(1);
        }

        private void _DoSetupMatch1(int my_peer_id, GDC.Array peer_ids, GDC.Dictionary match_info)
        {
            _syncManager.Stop();
            _syncManager.ClearPeers();

            _syncManager.network_adaptor = new DummyNetworkAdaptor(my_peer_id);

            _syncManager.mechanized = true;

            foreach (int peer_id in peer_ids)
                _syncManager.AddPeer(peer_id);

            if (GetTree().ChangeScene(match_scene_path) != Error.Ok)
            {
                _ShowErrorAndQuit($"Unable to change scene to: {match_scene_path}");
                return;
            }
            _setting_up_match = true;
            CallDeferred("_do_setup_match2", my_peer_id, peer_ids, match_info);
        }

        private void _DoSetupMatch2(int my_peer_id, GDC.Array peer_ids, GDC.Dictionary match_info)
        {
            _setting_up_match = false;

            var match_scene = GetTree().CurrentScene;
            if (!Utils.HasInteropMethod(match_scene, match_scene_method))
            {
                _ShowErrorAndQuit($"Match scene has no such method: {match_scene_method}");
                return;
            }
            // Call the scene's setup method.
            Utils.CallInteropMethod(match_scene, match_scene_method, new GDC.Array() { my_peer_id, peer_ids, match_info });

            _syncManager.Start();
        }

        private void _DoLoadState(GDC.Dictionary state)
        {
            state = _syncManager.hash_serializer.Unserialize(state) as GDC.Dictionary;
            _syncManager._CallLoadState(state);
        }

        private void _DoExecuteFrame(GDC.Dictionary msg)
        {
            Logger.FrameType frame_type = (Logger.FrameType)msg.Get<int>("frame_type");
            GDC.Dictionary input_frames_received = msg.Get("input_frames_received", new GDC.Dictionary() { });
            int rollback_ticks = msg.Get("rollback_ticks", 0);

            input_frames_received = _syncManager.hash_serializer.Unserialize(input_frames_received) as GDC.Dictionary;
            _syncManager.mechanized_input_received = input_frames_received;
            _syncManager.mechanized_rollback_ticks = rollback_ticks;

            switch (frame_type)
            {
                case Logger.FrameType.TICK:
                    _syncManager.ExecuteMechanizedTick();
                    break;
                case Logger.FrameType.INTERPOLATION_FRAME:
                    _syncManager.ExecuteMechanizedInterpolationFrame(msg.Get<float>("delta"));
                    break;
                case Logger.FrameType.INTERFRAME:
                    _syncManager.ExecuteMechanizedInterframe();
                    break;
                default:
                    _syncManager.ResetMechanizedData();
                    break;
            }
        }
    }
}