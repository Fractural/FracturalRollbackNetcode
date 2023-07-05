
using System;
using System.Collections.Generic;
using System.Linq;
using Fractural.Utils;
using Godot;
using static Fractural.RollbackNetcode.NetworkAdaptor;
using GDC = Godot.Collections;

namespace Fractural.RollbackNetcode
{
    public class SyncManager : Node
    {
        public static SyncManager Global { get; private set; }

        public class Peer : Reference
        {
            public int peer_id;
            public int rtt;
            public int last_ping_received;
            public float time_delta;

            public int last_remote_input_tick_received = 0;
            public int next_local_input_tick_requested = 1;
            public int last_remote_hash_tick_received = 0;
            public int next_local_hash_tick_requested = 1;

            public int remote_lag;
            public int local_lag;

            public float calculated_advantage;

            List<int> advantage_list = new List<int>();

            public Peer(int _peer_id)
            {
                peer_id = _peer_id;
            }

            public void RecordAdvantage(int ticks_to_calculate_advantage)
            {
                advantage_list.Add(local_lag - remote_lag);
                if (advantage_list.Count >= ticks_to_calculate_advantage)
                {
                    float total = 0;
                    foreach (var x in advantage_list)
                    {
                        total += x;
                    }
                    calculated_advantage = total / advantage_list.Count;
                    advantage_list.Clear();
                }
            }

            public void ClearAdvantage()
            {
                calculated_advantage = 0f;
                advantage_list.Clear();
            }

            public void Clear()
            {
                rtt = 0;
                last_ping_received = 0;
                time_delta = 0;
                last_remote_input_tick_received = 0;
                next_local_input_tick_requested = 0;
                last_remote_hash_tick_received = 0;
                next_local_hash_tick_requested = 0;
                remote_lag = 0;
                local_lag = 0;
                ClearAdvantage();
            }
        }

        public class InputForPlayer
        {
            public GDC.Dictionary input = new GDC.Dictionary();
            public bool predicted;

            public InputForPlayer(GDC.Dictionary _input, bool _predicted)
            {
                input = _input;
                predicted = _predicted;
            }
        }

        public class InputBufferFrame
        {
            public int tick;
            public IDictionary<int, InputForPlayer> players = new Dictionary<int, InputForPlayer>();

            public InputBufferFrame(int _tick)
            {
                tick = _tick;
            }

            public GDC.Dictionary GetPlayerInput(int peer_id)
            {
                if (players.ContainsKey(peer_id))
                    return players[peer_id].input;
                return new GDC.Dictionary() { };

            }

            public bool IsPlayerInputPredicted(int peer_id)
            {
                if (players.ContainsKey(peer_id))
                    return players[peer_id].predicted;
                return true;

            }

            public IList<int> GetMissingPeers(IDictionary<int, Peer> peers)
            {
                var missing = new List<int>();
                foreach (int peer_id in peers.Keys)
                {
                    if (!players.ContainsKey(peer_id) || players[peer_id].predicted)
                    {
                        missing.Add(peer_id);
                    }
                }
                return missing;
            }

            public bool IsComplete(IDictionary<int, Peer> peers)
            {
                foreach (int peer_id in peers.Keys)
                {
                    if (!players.ContainsKey(peer_id) || players[peer_id].predicted)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        public class StateBufferFrame
        {
            public int tick;
            // [nodePath: string]: savedData: GDC.Dictionary
            public GDC.Dictionary data;

            public StateBufferFrame(int _tick, GDC.Dictionary _data)
            {
                tick = _tick;
                data = _data;
            }
        }

        public class StateHashFrame
        {
            public int tick;
            public int state_hash;

            public IDictionary<int, int> peer_hashes = new Dictionary<int, int>();
            public bool mismatch = false;

            public StateHashFrame(int _tick, int _state_hash)
            {
                tick = _tick;
                state_hash = _state_hash;
            }

            public bool RecordPeerHash(int peer_id, int peer_hash)
            {
                peer_hashes[peer_id] = peer_hash;
                if (peer_hash != state_hash)
                {
                    mismatch = true;
                    return false;
                }
                return true;
            }

            public bool HasPeerHash(int peer_id)
            {
                return peer_hashes.ContainsKey(peer_id);
            }

            public bool IsComplete(IDictionary<int, Peer> peers)
            {
                foreach (var peer_id in peers.Keys)
                {
                    if (!peer_hashes.ContainsKey(peer_id))
                    {
                        return false;
                    }
                }
                return true;
            }

            public IList<int> GetMissingPeers(IDictionary<int, Peer> peers)
            {
                var missing = new List<int>();
                foreach (var peer_id in peers.Keys)
                {
                    if (!peer_hashes.ContainsKey(peer_id))
                    {
                        missing.Add(peer_id);
                    }
                }
                return missing;
            }
        }

        public const string DEFAULT_NETWORK_ADAPTOR_PATH = "res://addons/godot-rollback-netcode/RPCNetworkAdaptor.cs";
        public const string DEFAULT_MESSAGE_SERIALIZER_PATH = "res://addons/godot-rollback-netcode/MessageSerializer.cs";
        public const string DEFAULT_HASH_SERIALIZER_PATH = "res://addons/godot-rollback-netcode/HashSerializer.cs";

        private NetworkAdaptor _network_adaptor;
        public NetworkAdaptor network_adaptor
        {
            get => _network_adaptor;
            set => SetNetworkAdaptor(value);
        }

        private MessageSerializer _message_serializer;
        public MessageSerializer message_serializer
        {
            get => _message_serializer;
            set => SetMessageSerializer(value);
        }

        private HashSerializer _hash_serializer;
        public HashSerializer hash_serializer
        {
            get => _hash_serializer;
            set => SetHashSerializer(value);
        }

        public Dictionary<int, Peer> peers = new Dictionary<int, Peer>();
        public List<InputBufferFrame> input_buffer = new List<InputBufferFrame>();
        public List<StateBufferFrame> state_buffer = new List<StateBufferFrame>();
        public List<StateHashFrame> state_hashes = new List<StateHashFrame>();

        private bool _mechanized = false;
        public bool mechanized
        {
            get => _mechanized;
            set => SetMechanized(value);
        }

        public GDC.Dictionary mechanized_input_received = new GDC.Dictionary() { };
        public int mechanized_rollback_ticks = 0;

        public int max_buffer_size = 20;
        public int ticks_to_calculate_advantage = 60;

        private int _input_delay = 2;
        public int input_delay
        {
            get => _input_delay;
            set => SetInputDelay(value);
        }

        public int max_input_frames_per_message = 5;
        public int max_messages_at_once = 2;
        public int max_ticks_to_regain_sync = 300;
        public int min_lag_to_regain_sync = 5;
        public bool interpolation = false;
        public int max_state_mismatch_count = 10;

        public int debug_rollback_ticks = 0;
        public int debug_random_rollback_ticks = 0;
        public int debug_message_bytes = 700;
        public int debug_skip_nth_message = 0;
        public float debug_physics_process_msecs = 10.0f;
        public float debug_process_msecs = 10.0f;
        public bool debug_check_message_serializer_roundtrip = false;
        public bool debug_check_local_state_consistency = false;

        // In seconds, because we don't want it to be dependent on the network tick.
        private float _ping_frequency = 1.0f;
        public float ping_frequency
        {
            get => _ping_frequency;
            set => SetPingFrequency(value);
        }

        public int input_tick { get; private set; } = 0;
        public int current_tick { get; private set; } = 0;
        public int skip_ticks { get; private set; } = 0;
        public int rollback_ticks { get; private set; } = 0;

        public int requested_input_complete_tick { get; private set; } = 0;
        public bool started { get; private set; } = false;
        public float tick_time { get; private set; } = 0f;

        public bool _host_starting = false;
        public Timer _ping_timer;
        public Logger _logger;

        private SpawnManager _spawn_manager;
        private SoundManager _sound_manager;
        public int _input_buffer_start_tick;

        public int _state_buffer_start_tick;
        public int _state_hashes_start_tick;

        public IList<byte[]> _input_send_queue = new List<byte[]>();
        public int _input_send_queue_start_tick;

        public int _ticks_spent_regaining_sync = 0;
        public Dictionary<string, GDC.Dictionary[]> _interpolation_state = new Dictionary<string, GDC.Dictionary[]>();
        public float _time_since_last_tick = 0.0f;
        public int _debug_skip_nth_message_counter = 0;
        public int _input_complete_tick = 0;
        public int _state_complete_tick = 0;
        public int _last_state_hashed_tick = 0;
        public int _state_mismatch_count = 0;
        public bool _in_rollback = false;
        public bool _ran_physics_process = false;
        public int _ticks_since_last_interpolation_frame = 0;
        public List<StateBufferFrame> _debug_check_local_state_consistency_buffer = new List<StateBufferFrame>();

        public event Action SyncStarted;
        public event Action SyncStopped;
        public event Action SyncLost;
        public event Action SyncRegained;
        public delegate void SyncErrorDelegate(string msg);
        public event SyncErrorDelegate SyncError;

        public delegate void SkipTicksFlaggedDelegate(int count);
        public event SkipTicksFlaggedDelegate SkipTicksFlagged;
        public delegate void RollbackFlaggedDelegate(int tick);
        public event RollbackFlaggedDelegate RollbackFlagged;
        public delegate void PredictionMissedDelegate(int tick, int peer_id, GDC.Dictionary local_input, GDC.Dictionary remote_input);
        public event PredictionMissedDelegate PredictionMissed;
        public delegate void RemoteStateMismatchDelegate(int tick, int peer_id, int local_hash, int remote_hash);
        public event RemoteStateMismatchDelegate RemoteStateMismatch;

        public delegate void PeerAddedDelegate(int peer_id);
        public event PeerAddedDelegate PeerAdded;
        public delegate void PeerRemovedDelegate(int peer_id);
        public event PeerRemovedDelegate PeerRemoved;
        public delegate void PeerPingedBackDelegate(Peer peer);
        public event PeerPingedBackDelegate PeerPingedBack;

        public delegate void StateLoadedDelegate(int rollback_ticks);
        public event StateLoadedDelegate StateLoaded;
        public delegate void TickFinishedDelegate(bool is_rollback);
        public event TickFinishedDelegate TickFinished;
        public delegate void TickRetiredDelegate(int tick);
        public event TickRetiredDelegate TickRetired;
        public delegate void TickInputCompleteDelegate(int tick);
        public event TickInputCompleteDelegate TickInputComplete;
        public delegate void SceneSpawnedDelegate(string name, Node spawned_node, PackedScene scene, GDC.Dictionary data);
        public event SceneSpawnedDelegate SceneSpawned;
        public delegate void SceneDespawnedDelegate(string name, Node node);
        public event SceneDespawnedDelegate SceneDespawned;
        public event Action InterpolationFrame;

        public override void _Ready()
        {
            if (Global != null)
            {
                QueueFree();
                return;
            }
            Global = this;
            //get_tree().Connect("network_peer_disconnected", this, "remove_peer")
            //get_tree().Connect("server_disconnected", this, "stop")

            IDictionary<string, string> project_settings = new Dictionary<string, string>()
            {
                [nameof(max_buffer_size)] = "network/rollback/max_buffer_size",
                [nameof(ticks_to_calculate_advantage)] = "network/rollback/ticks_to_calculate_advantage",
                [nameof(input_delay)] = "network/rollback/input_delay",
                [nameof(ping_frequency)] = "network/rollback/ping_frequency",
                [nameof(interpolation)] = "network/rollback/interpolation",
                [nameof(max_input_frames_per_message)] = "network/rollback/limits/max_input_frames_per_message",
                [nameof(max_messages_at_once)] = "network/rollback/limits/max_messages_at_once",
                [nameof(max_ticks_to_regain_sync)] = "network/rollback/limits/max_ticks_to_regain_sync",
                [nameof(min_lag_to_regain_sync)] = "network/rollback/limits/min_lag_to_regain_sync",
                [nameof(max_state_mismatch_count)] = "network/rollback/limits/max_state_mismatch_count",
                [nameof(debug_rollback_ticks)] = "network/rollback/debug/rollback_ticks",
                [nameof(debug_random_rollback_ticks)] = "network/rollback/debug/random_rollback_ticks",
                [nameof(debug_message_bytes)] = "network/rollback/debug/message_bytes",
                [nameof(debug_skip_nth_message)] = "network/rollback/debug/skip_nth_message",
                [nameof(debug_physics_process_msecs)] = "network/rollback/debug/physics_process_msecs",
                [nameof(debug_process_msecs)] = "network/rollback/debug/process_msecs",
                [nameof(debug_check_message_serializer_roundtrip)] = "network/rollback/debug/check_message_serializer_roundtrip",
                [nameof(debug_check_local_state_consistency)] = "network/rollback/debug/check_local_state_consistency",
            };
            foreach (var property_name in project_settings.Keys)
            {
                var setting_name = project_settings[property_name];
                if (ProjectSettings.HasSetting(setting_name))
                {
                    Set(property_name, ProjectSettings.GetSetting(setting_name));
                }
            }
            _ping_timer = new Timer();

            _ping_timer.Name = "PingTimer";
            _ping_timer.WaitTime = ping_frequency;
            _ping_timer.Autostart = true;
            _ping_timer.OneShot = false;
            _ping_timer.PauseMode = Node.PauseModeEnum.Process;
            _ping_timer.Connect("timeout", this, nameof(_OnPingTimerTimeout));
            AddChild(_ping_timer);

            _spawn_manager = new SpawnManager();
            _spawn_manager.Name = "SpawnManager";
            AddChild(_spawn_manager);
            _spawn_manager.SceneSpawned += _OnSpawnManagerSceneSpawned;
            _spawn_manager.SceneDespawned += _OnSpawnManagerSceneDespawned;

            _sound_manager = new SoundManager();
            _sound_manager.Name = "SoundManager";
            AddChild(_sound_manager);
            _sound_manager.SetupSoundManager(this);

            if (network_adaptor == null)
            {
                ResetNetworkAdaptor();
            }
            if (message_serializer == null)
            {
                SetMessageSerializer(_CreateClassFromProjectSettings<MessageSerializer>("network/rollback/classes/message_serializer", DEFAULT_MESSAGE_SERIALIZER_PATH));
            }
            if (hash_serializer == null)
            {
                SetHashSerializer(_CreateClassFromProjectSettings<HashSerializer>("network/rollback/classes/hash_serializer", DEFAULT_HASH_SERIALIZER_PATH));
            }
        }

        public override void _EnterTree()
        {
            CustomProjectSettings.AddProjectSettings();
        }

        public override void _ExitTree()
        {
            StopLogging();
        }

        public override void _Notification(int what)
        {
            if (what == NotificationPredelete)
            {
                if (Global == this)
                    Global = null;
            }
        }

        private T _CreateClassFromProjectSettings<T>(string setting_name, string default_path)
        {
            string class_path = "";
            if (ProjectSettings.HasSetting(setting_name))
            {
                class_path = ProjectSettingsUtils.GetSetting<string>(setting_name);
            }
            if (class_path == "")
            {
                class_path = default_path;
            }
            return (T)GD.Load<CSharpScript>(class_path).New();
        }

        public void SetNetworkAdaptor(NetworkAdaptor newNetworkAdaptor)
        {
            System.Diagnostics.Debug.Assert(!started, "Changing the network adaptor after SyncManager has started will probably break everything");

            if (_network_adaptor != null)
            {
                _network_adaptor.DetachNetworkAdaptor(this);
                _network_adaptor.ReceivedPing -= _OnReceivedPing;
                _network_adaptor.ReceivedPingBack -= _OnReceivedPingBack;
                _network_adaptor.ReceivedRemoteStart -= _OnReceivedRemoteStart;
                _network_adaptor.ReceivedRemoteStop -= _OnReceivedRemoteStop;
                _network_adaptor.ReceivedInputTick -= _OnReceivedInputTick;

                RemoveChild(_network_adaptor);
                _network_adaptor.QueueFree();

            }
            _network_adaptor = newNetworkAdaptor;
            _network_adaptor.Name = "NetworkAdaptor";
            AddChild(_network_adaptor);
            _network_adaptor.ReceivedPing += _OnReceivedPing;
            _network_adaptor.ReceivedPingBack += _OnReceivedPingBack;
            _network_adaptor.ReceivedRemoteStart += _OnReceivedRemoteStart;
            _network_adaptor.ReceivedRemoteStop += _OnReceivedRemoteStop;
            _network_adaptor.ReceivedInputTick += _OnReceivedInputTick;
            _network_adaptor.AttachNetworkAdaptor(this);
        }

        public void ResetNetworkAdaptor()
        {
            SetNetworkAdaptor(_CreateClassFromProjectSettings<NetworkAdaptor>("network/rollback/classes/network_adaptor", DEFAULT_NETWORK_ADAPTOR_PATH));
        }

        public void SetMessageSerializer(MessageSerializer newMessageSerializer)
        {
            System.Diagnostics.Debug.Assert(!started, "Changing the message serializer after SyncManager has started will probably break everything");
            _message_serializer = newMessageSerializer;
        }

        public void SetHashSerializer(HashSerializer newHashSerializer)
        {
            System.Diagnostics.Debug.Assert(!started, "Changing the hash serializer after SyncManager has started will probably break everything");
            _hash_serializer = newHashSerializer;
        }

        public void SetMechanized(bool newMechanized)
        {
            System.Diagnostics.Debug.Assert(!started, "Changing the mechanized flag after SyncManager has started will probably break everything");
            _mechanized = newMechanized;

            SetProcess(!mechanized);
            SetPhysicsProcess(!mechanized);
            _ping_timer.Paused = mechanized;

            if (mechanized)
                StopLogging();
        }

        public void SetPingFrequency(float newPingFrequency)
        {
            _ping_frequency = newPingFrequency;
            if (_ping_timer != null)
                _ping_timer.WaitTime = newPingFrequency;
        }

        public void SetInputDelay(int newInputDelay)
        {
            if (started)
                GD.PushWarning("Cannot change input delay after syncing has already started");
            _input_delay = newInputDelay;
        }

        public void AddPeer(int peer_id)
        {
            System.Diagnostics.Debug.Assert(!peers.ContainsKey(peer_id), "Peer with given id already exists");
            System.Diagnostics.Debug.Assert(peer_id != network_adaptor.GetNetworkUniqueId(), "Cannot add ourselves as a peer in SyncManager");

            if (peers.ContainsKey(peer_id))
                return;
            if (peer_id == network_adaptor.GetNetworkUniqueId())
                return;
            peers[peer_id] = new Peer(peer_id);

            PeerAdded?.Invoke(peer_id);
        }

        public bool HasPeer(int peer_id)
        {
            return peers.ContainsKey(peer_id);
        }

        public Peer GetPeer(int peer_id)
        {
            return peers[peer_id];
        }

        public void RemovePeer(int peer_id)
        {
            if (peers.ContainsKey(peer_id))
            {
                peers.Remove(peer_id);
                PeerRemoved?.Invoke(peer_id);
            }
            if (peers.Count == 0)
            {
                Stop();
            }
        }

        public void ClearPeers()
        {
            var peerIds = new List<int>(peers.Keys);
            foreach (var peerId in peerIds)
                RemovePeer(peerId);
        }

        public void _OnPingTimerTimeout()
        {
            if (peers.Count == 0)
                return;
            var msg = new PingMessage()
            {
                LocalTime = (int)OS.GetSystemTimeMsecs(),
            };
            foreach (var peer_id in peers.Keys)
            {
                System.Diagnostics.Debug.Assert(peer_id != network_adaptor.GetNetworkUniqueId(), "Cannot ping ourselves");
                network_adaptor.SendPing(peer_id, msg);
            }
        }

        public void _OnReceivedPing(int peer_id, PingMessage msg)
        {
            System.Diagnostics.Debug.Assert(peer_id != network_adaptor.GetNetworkUniqueId(), "Cannot ping back ourselves");

            msg.RemoteTime = (int)OS.GetSystemTimeMsecs();
            network_adaptor.SendPingBack(peer_id, msg);
        }

        public void _OnReceivedPingBack(int peer_id, PingMessage msg)
        {
            var system_time = (int)OS.GetSystemTimeMsecs();
            var peer = peers[peer_id];
            peer.last_ping_received = system_time;
            peer.rtt = system_time - msg.LocalTime;
            peer.time_delta = msg.RemoteTime - msg.LocalTime - (peer.rtt / 2.0f);
            PeerPingedBack?.Invoke(peer);
        }

        public void StartLogging(string log_file_path, GDC.Dictionary match_info = null)
        {
            if (match_info == null)
                match_info = new GDC.Dictionary() { };

            // Our logger needs threads!
            if (!OS.CanUseThreads())
                return;
            if (mechanized)
                return;
            if (_logger == null)
            {
                _logger = new Logger(this);
            }
            else
            {
                _logger.Stop();
            }
            if (_logger.Start(log_file_path, network_adaptor.GetNetworkUniqueId(), match_info) != Error.Ok)
            {
                StopLogging();
            }
        }

        public void StopLogging()
        {
            if (_logger != null)
            {
                _logger.Stop();
                _logger = null;
            }
        }

        public async void Start()
        {
            System.Diagnostics.Debug.Assert(network_adaptor.IsNetworkHost() || mechanized, "start() should only be called on the host");

            if (started || _host_starting)
                return;
            if (mechanized)
            {
                _OnReceivedRemoteStart();
                return;
            }
            if (network_adaptor.IsNetworkHost())
            {
                int highest_rtt = 0;
                foreach (var peer in peers.Values)
                {
                    highest_rtt = Math.Max(highest_rtt, peer.rtt);

                    // Call _RemoteStart() on all the other peers.
                }
                foreach (var peer_id in peers.Keys)
                {
                    network_adaptor.SendRemoteStart(peer_id);

                    // Attempt to prevent double starting on the host.
                }
                _host_starting = true;

                // Wait for half the highest RTT to start locally.
                GD.Print($"Delaying host start by {highest_rtt / 2}ms");

                await ToSignal(GetTree().CreateTimer(highest_rtt / 2000f), "timeout");

                _OnReceivedRemoteStart();
                _host_starting = false;
            }
        }

        public void _Reset()
        {
            input_tick = 0;
            current_tick = input_tick - input_delay;
            skip_ticks = 0;
            rollback_ticks = 0;
            input_buffer.Clear();
            state_buffer.Clear();
            state_hashes.Clear();
            _input_buffer_start_tick = 1;
            _state_buffer_start_tick = 0;
            _state_hashes_start_tick = 1;
            _input_send_queue.Clear();
            _input_send_queue_start_tick = 1;
            _ticks_spent_regaining_sync = 0;
            _interpolation_state.Clear();
            _time_since_last_tick = 0f;
            _debug_skip_nth_message_counter = 0;
            _input_complete_tick = 0;
            _state_complete_tick = 0;
            _last_state_hashed_tick = 0;
            _state_mismatch_count = 0;
            _in_rollback = false;
            _ran_physics_process = false;
            _ticks_since_last_interpolation_frame = 0;

        }

        public void _OnReceivedRemoteStart()
        {
            _Reset();
            tick_time = 1f / Engine.IterationsPerSecond;
            started = true;
            network_adaptor.StartNetworkAdaptor(this);
            _spawn_manager.Reset();
            SyncStarted?.Invoke();
        }

        public void Stop()
        {
            if (network_adaptor.IsNetworkHost() && !mechanized)
            {
                foreach (var peer_id in peers.Keys)
                    network_adaptor.SendRemoteStop(peer_id);
            }
            _OnReceivedRemoteStop();
        }

        public void _OnReceivedRemoteStop()
        {
            if (!(started || _host_starting))
                return;
            network_adaptor.StopNetworkAdaptor(this);
            started = false;
            _host_starting = false;
            _Reset();

            foreach (var peer in peers.Values)
                peer.Clear();

            SyncStopped?.Invoke();
            _spawn_manager.Reset();
        }

        public void _HandleFatalError(string msg)
        {
            SyncError?.Invoke(msg);
            GD.PushError("NETWORK SYNC LOST: " + msg);
            Stop();
            if (_logger != null)
                _logger.LogFatalError(msg);
        }

        public GDC.Dictionary _CallGetLocalInput()
        {
            var input = new GDC.Dictionary() { };
            var nodes = GetTree().GetNodesInGroup("network_sync");
            foreach (Node node in nodes)
            {
                if (network_adaptor.IsNetworkMasterForNode(node) && node is IGetLocalInput getLocalInput && node.IsInsideTree() && !node.IsQueuedForDeletion())
                {
                    var node_input = getLocalInput._GetLocalInput();
                    if (node_input.Count > 0)
                        input[GD.Str(node.GetPath())] = node_input;
                }
            }
            return input;
        }

        public void _CallNetworkProcess(InputBufferFrame inputFrame)
        {
            GDC.Array nodes = GetTree().GetNodesInGroup("network_sync");
            var processNodes = new List<INetworkProcess>();
            var postProcessNodes = new List<INetworkPostProcess>();

            // Call _NetworkPreprocess() && collect list of nodes with the other
            // virtual methods.
            for (int i = nodes.Count; i >= 0; i--)
            {
                var node = nodes[i] as Node;
                if (node.IsInsideTree() && !node.IsQueuedForDeletion())
                {
                    if (node is INetworkPreProcess preProcess)
                    {
                        var playerInput = inputFrame.GetPlayerInput(node.GetNetworkMaster());
                        var nodeInput = playerInput.Get(node.GetPath(), new GDC.Dictionary());
                        preProcess._NetworkPreprocess(nodeInput);
                    }
                    if (node is INetworkProcess process)
                    {
                        processNodes.Add(process);
                    }
                    if (node is INetworkPostProcess postProcess)
                    {
                        postProcessNodes.Add(postProcess);
                    }
                }
            }
            foreach (var processNode in processNodes)
            {
                var node = processNode as Node;
                var playerInput = inputFrame.GetPlayerInput(node.GetNetworkMaster());
                var nodeInput = playerInput.Get(node.GetPath(), new GDC.Dictionary());
                processNode._NetworkProcess(nodeInput);
            }
            foreach (var postProcessNode in postProcessNodes)
            {
                var node = postProcessNode as Node;
                var playerInput = inputFrame.GetPlayerInput(node.GetNetworkMaster());
                var nodeInput = playerInput.Get(node.GetPath(), new GDC.Dictionary());
                postProcessNode._NetworkPostprocess(nodeInput);
            }
        }

        public GDC.Dictionary _CallSaveState()
        {
            GDC.Dictionary state = new GDC.Dictionary();
            GDC.Array nodes = GetTree().GetNodesInGroup("network_sync");
            foreach (Node node in nodes)
            {
                if (node is INetworkSerializable networkSerializable && node.IsInsideTree() && !node.IsQueuedForDeletion())
                {
                    var nodePathStr = node.GetPath().ToString();
                    if (nodePathStr != "")
                        state[nodePathStr] = networkSerializable._SaveState();
                }
            }
            return state;
        }

        public void _CallLoadState(GDC.Dictionary state)
        {
            foreach (string nodePathStr in state.Keys)
            {
                if (nodePathStr == "$")
                    continue;

                var node = GetNodeOrNull(nodePathStr);
                System.Diagnostics.Debug.Assert(node != null, $"Unable to restore state to missing node: {nodePathStr}");

                if (node is INetworkSerializable networkSerializable)
                    networkSerializable._LoadState(state.Get<GDC.Dictionary>(nodePathStr));
            }
        }

        public void _CallInterpolateState(float weight)
        {
            foreach (string nodePathStr in _interpolation_state.Keys)
            {
                if (nodePathStr == "$")
                    continue;

                var node = GetNodeOrNull(nodePathStr);
                if (node is IInterpolateState interpolateState)
                {
                    var states = _interpolation_state[nodePathStr];
                    interpolateState._InterpolateState(states[0], states[1], weight);
                }
            }
        }

        public void _SaveCurrentState()
        {
            System.Diagnostics.Debug.Assert(current_tick >= 0, "Attempting to store state for negative tick");

            if (current_tick < 0)
                return;

            state_buffer.Add(new StateBufferFrame(current_tick, _CallSaveState()));

            // If the input for this state is complete, then update _state_complete_tick.
            if (_input_complete_tick > _state_complete_tick)
            {
                // Set to the current_tick so long as its less than || equal to the
                // _input_complete_tick, otherwise, cap it to the _input_complete_tick.
                _state_complete_tick = current_tick <= _input_complete_tick ? current_tick : _input_complete_tick;
            }
        }

        public void _UpdateInputCompleteTick()
        {
            while (input_tick >= _input_complete_tick + 1)
            {
                InputBufferFrame input_frame = GetInputFrame(_input_complete_tick + 1);
                if (input_frame == null)
                    break;

                if (!input_frame.IsComplete(peers))
                    break;
                // When we add debug rollbacks mark the input as !complete
                // so that the invariant \"a complete input frame cannot be rolled back\" is respected
                // NB: a complete input frame can still be loaded in a rollback for the incomplete input next frame
                if (debug_random_rollback_ticks > 0 && _input_complete_tick + 1 > current_tick - debug_random_rollback_ticks)
                    break;
                if (debug_rollback_ticks > 0 && _input_complete_tick + 1 > current_tick - debug_rollback_ticks)
                    break;

                if (_logger != null)
                    _logger.WriteInput(input_frame.tick, input_frame.players);
                _input_complete_tick += 1;

                // This tick should be recomputed with complete inputs, let"s roll back
                if (_input_complete_tick == requested_input_complete_tick)
                {
                    requested_input_complete_tick = 0;
                    var tick_delta = current_tick - _input_complete_tick;
                    if (tick_delta >= 0 && rollback_ticks <= tick_delta)
                    {
                        rollback_ticks = tick_delta + 1;
                        RollbackFlagged?.Invoke(_input_complete_tick);
                    }
                }
                TickInputComplete?.Invoke(_input_complete_tick);
            }
        }

        public void _UpdateStateHashes()
        {
            while (_state_complete_tick > _last_state_hashed_tick)
            {
                StateBufferFrame state_frame = _GetStateFrame(_last_state_hashed_tick + 1);
                if (state_frame == null)
                {
                    _HandleFatalError("Unable to hash state");
                    return;
                }
                _last_state_hashed_tick += 1;

                var state_hash = _CalculateDataHash(state_frame.data);
                state_hashes.Add(new StateHashFrame(_last_state_hashed_tick, state_hash));

                if (_logger != null)
                    _logger.WriteState(_last_state_hashed_tick, state_frame.data);
            }
        }

        public InputBufferFrame _PredictMissingInput(InputBufferFrame input_frame, InputBufferFrame previous_frame)
        {
            if (!input_frame.IsComplete(peers))
            {
                if (previous_frame == null)
                    previous_frame = new InputBufferFrame(-1);
                var missing_peers = input_frame.GetMissingPeers(peers);
                // [peer_id: int]: input: GDC.Dictionary
                IDictionary<int, GDC.Dictionary> missing_peers_predicted_input = new Dictionary<int, GDC.Dictionary>();
                // [peer_id: int]: missing_ticks: int
                IDictionary<int, int> missing_peers_ticks_since_real_input = new Dictionary<int, int>();
                foreach (var peer_id in missing_peers)
                {
                    missing_peers_predicted_input[peer_id] = new GDC.Dictionary() { };
                    Peer peer = peers[peer_id];
                    missing_peers_ticks_since_real_input[peer_id] = peer.last_remote_input_tick_received == 0 ? -1 : current_tick - peer.last_remote_input_tick_received;
                }
                GDC.Array nodes = GetTree().GetNodesInGroup("network_sync");
                foreach (Node node in nodes)
                {
                    int node_master = node.GetNetworkMaster();
                    if (!missing_peers.Contains(node_master))
                        continue;
                    var previous_input = previous_frame.GetPlayerInput(node_master);
                    var node_path_str = GD.Str(node.GetPath());
                    if (node is IPredictRemoteInput || previous_input.Contains(node_path_str))
                    {
                        var previous_input_for_node = previous_input.Get(node_path_str, new GDC.Dictionary());
                        int ticks_since_real_input = missing_peers_ticks_since_real_input[node_master];
                        var predicted_input_for_node = node is IPredictRemoteInput predictRemoteInput ? predictRemoteInput._PredictRemoteInput(previous_input_for_node, ticks_since_real_input) : previous_input_for_node.Duplicate();

                        if (predicted_input_for_node.Count > 0)
                            missing_peers_predicted_input[node_master][node_path_str] = predicted_input_for_node;
                    }
                }
                foreach (var peer_id in missing_peers_predicted_input.Keys)
                {
                    var predicted_input = missing_peers_predicted_input[peer_id];
                    _CalculateDataHash(predicted_input);
                    input_frame.players[peer_id] = new InputForPlayer(predicted_input, true);
                }
            }
            return input_frame;
        }

        public bool _DoTick(bool is_rollback = false)
        {
            var input_frame = GetInputFrame(current_tick);
            var previous_frame = GetInputFrame(current_tick - 1);

            System.Diagnostics.Debug.Assert(input_frame != null, "Input frame for current_tick is null");
            input_frame = _PredictMissingInput(input_frame, previous_frame);
            _CallNetworkProcess(input_frame);

            // If the game was stopped during the last network process, then we return
            // false here, to indicate that a full tick didn't complete && we need to
            // abort.
            if (!started)
                return false;
            _SaveCurrentState();

            // Debug check that states computed multiple times with complete inputs are the same
            if (debug_check_local_state_consistency && _last_state_hashed_tick >= current_tick)
                _DebugCheckConsistentLocalState(state_buffer[-1], "Recomputed");
            TickFinished?.Invoke(is_rollback);
            return true;
        }

        public InputBufferFrame _GetOrCreateInputFrame(int tick)
        {
            InputBufferFrame input_frame = null;
            if (input_buffer.Count == 0)
            {
                input_frame = new InputBufferFrame(tick);
                input_buffer.Add(input_frame);
            }
            else if (tick > input_buffer[input_buffer.Count - 1].tick)
            {
                var highest = input_buffer[input_buffer.Count - 1].tick;
                while (highest < tick)
                {
                    highest += 1;
                    input_frame = new InputBufferFrame(highest);
                    input_buffer.Add(input_frame);
                }
            }
            else
            {
                input_frame = GetInputFrame(tick);
                if (input_frame == null)
                {
                    _HandleFatalError($"Requested input Frame ({tick}) !found in buffer");
                    return null;
                }
            }
            return input_frame;
        }

        public bool _CleanupBuffers()
        {
            // Clean-up the input send queue.
            var min_next_input_tick_requested = _CalculateMinimumNextInputTickRequested();
            while (_input_send_queue_start_tick < min_next_input_tick_requested)
            {
                _input_send_queue.PopFront();
                _input_send_queue_start_tick += 1;

                // Clean-up old state buffer frames. We need to keep one extra frame of state
                // because when we rollback, we need to load the state for the frame before
                // the first one we need to run again.
            }
            while (state_buffer.Count > max_buffer_size + 1)
            {
                StateBufferFrame state_frame_to_retire = state_buffer[0];
                var input_frame = GetInputFrame(state_frame_to_retire.tick + 1);
                if (input_frame == null)
                {
                    string message = $"Attempting to retire state frame {state_frame_to_retire.tick}, but input frame {state_frame_to_retire.tick + 1} is missing";
                    GD.PushWarning(message);
                    if (_logger != null)
                        _logger.data["buffer_underrun_message"] = message;
                    return false;
                }
                if (!input_frame.IsComplete(peers))
                {
                    IList<int> missing = input_frame.GetMissingPeers(peers);
                    string message = $"Attempting to retire state frame {state_frame_to_retire.tick}, but input frame {input_frame.tick} is still missing Input (missing Peer(s): {missing})";
                    GD.PushWarning(message);
                    if (_logger != null)
                        _logger.data["buffer_underrun_message"] = message;
                    return false;

                }
                if (state_frame_to_retire.tick > _last_state_hashed_tick)
                {
                    string message = $"Unable to retire state frame {state_frame_to_retire.tick}, because we haven't hashed it yet";
                    GD.PushWarning(message);
                    if (_logger != null)
                        _logger.data["buffer_underrun_message"] = message;
                    return false;
                }
                state_buffer.PopFront();
                _state_buffer_start_tick += 1;

                TickRetired?.Invoke(state_frame_to_retire.tick);

                // Clean-up old input buffer frames. Unlike state frames, we can have many
                // frames from the future if we are running behind. We don"t want having too
                // many future frames to end up discarding input for the current frame, so we
                // only count input frames before the current frame towards the buffer size.
            }
            while ((current_tick - _input_buffer_start_tick) > max_buffer_size)
            {
                _input_buffer_start_tick += 1;
                input_buffer.PopFront();
            }
            while (state_hashes.Count > (max_buffer_size * 2))
            {
                StateHashFrame state_hash_to_retire = state_hashes[0];
                if (!state_hash_to_retire.IsComplete(peers) && !mechanized)
                {
                    IList<int> missing = state_hash_to_retire.GetMissingPeers(peers);
                    string message = $"Attempting to retire state hash frame {state_hash_to_retire.tick}, but we're still missing Hashes(missing Peer(s): {string.Join(", ", missing)})";
                    GD.PushWarning(message);
                    if (_logger != null)
                        _logger.data["buffer_underrun_message"] = message;
                    return false;
                }
                if (state_hash_to_retire.mismatch)
                    _state_mismatch_count += 1;
                else
                    _state_mismatch_count = 0;
                if (_state_mismatch_count > max_state_mismatch_count)
                {
                    _HandleFatalError("Fatal state mismatch");
                    return false;
                }
                _state_hashes_start_tick += 1;
                state_hashes.PopFront();
            }
            return true;
        }

        public InputBufferFrame GetInputFrame(int tick)
        {
            if (tick < _input_buffer_start_tick)
                return null;
            var index = tick - _input_buffer_start_tick;
            if (index >= input_buffer.Count)
                return null;
            var input_frame = input_buffer[index];
            System.Diagnostics.Debug.Assert(input_frame.tick == tick, "Input frame retreived from input buffer has mismatched tick number");
            return input_frame;
        }

        public GDC.Dictionary GetLatestInputFromPeer(int peer_id)
        {
            if (peers.ContainsKey(peer_id))
            {
                Peer peer = peers[peer_id];
                var input_frame = GetInputFrame(peer.last_remote_input_tick_received);
                if (input_frame != null)
                    return input_frame.GetPlayerInput(peer_id);
            }
            return new GDC.Dictionary() { };
        }

        public GDC.Dictionary GetLatestInputForNode(Node node)
        {
            return GetLatestInputFromPeerForPath(node.GetNetworkMaster(), node.GetPath().ToString());
        }

        public GDC.Dictionary GetLatestInputFromPeerForPath(int peer_id, string path)
        {
            return GetLatestInputFromPeer(peer_id).Get(path, new GDC.Dictionary() { });
        }

        public GDC.Dictionary GetCurrentInputForNode(Node node)
        {
            return GetCurrentInputFromPeerForPath(node.GetNetworkMaster(), node.GetPath().ToString());
        }

        public GDC.Dictionary GetCurrentInputFromPeerForPath(int peer_id, string path)
        {
            var input_frame = GetInputFrame(current_tick);
            if (input_frame != null)
                return input_frame.GetPlayerInput(peer_id).Get(path, new GDC.Dictionary() { });
            return new GDC.Dictionary() { };
        }

        public StateBufferFrame _GetStateFrame(int tick)
        {
            if (tick < _state_buffer_start_tick)
                return null;
            var index = tick - _state_buffer_start_tick;
            if (index >= state_buffer.Count)
                return null;
            var state_frame = state_buffer[index];
            System.Diagnostics.Debug.Assert(state_frame.tick == tick, "State frame retreived from state buffer has mismatched tick number");
            return state_frame;
        }

        public StateHashFrame _GetStateHashFrame(int tick)
        {
            if (tick < _state_hashes_start_tick)
                return null;
            var index = tick - _state_hashes_start_tick;
            if (index >= state_hashes.Count)
                return null;
            var state_hash_frame = state_hashes[index];
            System.Diagnostics.Debug.Assert(state_hash_frame.tick == tick, "State hash frame retreived from state hashes has mismatched tick number");
            return state_hash_frame;
        }

        public bool IsCurrentTickInputComplete()
        {
            return current_tick <= _input_complete_tick;
        }

        public IList<GDC.Dictionary> _GetInputMessagesFromSendQueueInRange(int first_index, int last_index, bool reverse = false)
        {
            var indexes = !reverse ? GD.Range(first_index, last_index + 1) : GD.Range(last_index, first_index - 1, -1);

            IList<GDC.Dictionary> all_messages = new List<GDC.Dictionary>() { };
            GDC.Dictionary msg = new GDC.Dictionary() { };
            foreach (var index in indexes)
            {
                msg[_input_send_queue_start_tick + index] = _input_send_queue[index];

                if (max_input_frames_per_message > 0 && msg.Count == max_input_frames_per_message)
                {
                    all_messages.Add(msg);
                    msg = new GDC.Dictionary() { };
                }
            }
            if (msg.Count > 0)
                all_messages.Add(msg);
            return all_messages;
        }

        public IList<GDC.Dictionary> _GetInputMessagesFromSendQueueForPeer(Peer peer)
        {
            var first_index = peer.next_local_input_tick_requested - _input_send_queue_start_tick;
            var last_index = _input_send_queue.Count - 1;
            var max_messages = (max_input_frames_per_message * max_messages_at_once);

            if ((last_index + 1) - first_index <= max_messages)
                return _GetInputMessagesFromSendQueueInRange(first_index, last_index, true);
            var new_messages = (int)(Mathf.Ceil(max_messages_at_once / 2f));
            var old_messages = (int)(Mathf.Floor(max_messages_at_once / 2f));

            return _GetInputMessagesFromSendQueueInRange(last_index - (new_messages * max_input_frames_per_message) + 1, last_index, true).Concat(
               _GetInputMessagesFromSendQueueInRange(first_index, first_index + (old_messages * max_input_frames_per_message) - 1)).ToList();
        }

        public GDC.Dictionary _GetStateHashesForPeer(Peer peer)
        {
            GDC.Dictionary ret = new GDC.Dictionary() { };
            if (peer.next_local_hash_tick_requested >= _state_hashes_start_tick)
            {
                var index = peer.next_local_hash_tick_requested - _state_hashes_start_tick;
                while (index < state_hashes.Count)
                {
                    StateHashFrame state_hash_frame = state_hashes[index];
                    ret[state_hash_frame.tick] = state_hash_frame.state_hash;
                    index += 1;
                }
            }
            return ret;
        }

        public void _RecordAdvantage(bool force_calculate_advantage = false)
        {
            foreach (var peer in peers.Values)
            {
                // Number of frames we are predicting for this peer.
                peer.local_lag = (input_tick + 1) - peer.last_remote_input_tick_received;
                // Calculate the advantage the peer has over us.
                peer.RecordAdvantage(!force_calculate_advantage ? ticks_to_calculate_advantage : 0);

                if (_logger != null)
                {
                    _logger.AddValue($"peer_{peer.peer_id}", new GDC.Dictionary()
                    {
                        ["local_lag"] = peer.local_lag,
                        ["remote_lag"] = peer.remote_lag,
                        ["advantage"] = peer.local_lag - peer.remote_lag,
                        ["calculated_advantage"] = peer.calculated_advantage,
                    });
                }
            }
        }

        public bool _CalculateSkipTicks()
        {
            // Attempt to find the greatest advantage.
            float max_advantage = 0;
            foreach (var peer in peers.Values)
                max_advantage = Mathf.Max(max_advantage, peer.calculated_advantage);
            if (max_advantage >= 2.0 && skip_ticks == 0)
            {
                skip_ticks = (int)(max_advantage / 2);
                SkipTicksFlagged?.Invoke(skip_ticks);
                return true;
            }
            return false;
        }

        public int _CalculateMaxLocalLag()
        {
            int max_lag = 0;
            foreach (var peer in peers.Values)
                max_lag = Mathf.Max(max_lag, peer.local_lag);
            return max_lag;
        }

        public int _CalculateMinimumNextInputTickRequested()
        {
            if (peers.Count == 0)
                return 1;
            var peer_list = new List<Peer>(peers.Values);
            int result = peer_list.PopFront().next_local_input_tick_requested;
            foreach (var peer in peer_list)
                result = Mathf.Min(result, peer.next_local_input_tick_requested);
            return result;
        }

        public void _SendInputMessagesToPeer(int peer_id)
        {
            System.Diagnostics.Debug.Assert(peer_id != network_adaptor.GetNetworkUniqueId(), "Cannot send input to ourselves");

            var peer = peers[peer_id];
            var state_hashes = _GetStateHashesForPeer(peer);
            var input_messages = _GetInputMessagesFromSendQueueForPeer(peer);

            if (_logger != null)
                _logger.data[$"messages_sent_to_peer_{peer_id}"] = input_messages.Count;
            foreach (var input in _GetInputMessagesFromSendQueueForPeer(peer))
            {
                GDC.Dictionary msg = new GDC.Dictionary()
                {
                    [MessageSerializer.InputMessageKey.NEXT_INPUT_TICK_REQUESTED] = peer.last_remote_input_tick_received + 1,
                    [MessageSerializer.InputMessageKey.INPUT] = input,
                    [MessageSerializer.InputMessageKey.NEXT_HASH_TICK_REQUESTED] = peer.last_remote_hash_tick_received + 1,
                    [MessageSerializer.InputMessageKey.STATE_HASHES] = state_hashes,
                };

                var bytes = message_serializer.SerializeMessage(msg);
                // See https://gafferongames.com/post/packet_fragmentation_and_reassembly/
                if (debug_message_bytes > 0)
                {
                    if (bytes.Length > debug_message_bytes)
                        GD.PushError($"Sending message w/ size {bytes.Length} bytes");
                }
                if (_logger != null)
                {
                    _logger.AddValue($"messages_sent_to_peer_{peer_id}_size", bytes.Length);
                    _logger.IncrementValue($"messages_sent_to_peer_{peer_id}_total_size", bytes.Length);
                    _logger.MergeArrayValue($"input_ticks_sent_to_peer_{peer_id}", new GDC.Array(input.Keys));

                    //var ticks = msg[InputMessageKey.INPUT].Keys;
                    //print (\"[%s] Sending ticks %s - %s\" % [current_tick, Mathf.Min(ticks[0], ticks[-1]), Mathf.Max(ticks[0], ticks[-1])])
                }
                network_adaptor.SendInputTick(peer_id, bytes);
            }
        }

        public void _SendInputMessagesToAllPeers()
        {
            if (debug_skip_nth_message > 1)
            {
                _debug_skip_nth_message_counter += 1;
                if (_debug_skip_nth_message_counter >= debug_skip_nth_message)
                {
                    GD.Print($"[{current_tick}] Skipping message to simulate packet loss");
                    _debug_skip_nth_message_counter = 0;
                    return;
                }
            }
            foreach (var peer_id in peers.Keys)
                _SendInputMessagesToPeer(peer_id);
        }

        public override void _PhysicsProcess(float _delta)
        {
            if (!started)
                return;
            if (_logger != null)
            {
                _logger.BeginTick(current_tick + 1);
                _logger.data["input_complete_tick"] = _input_complete_tick;
                _logger.data["state_complete_tick"] = _state_complete_tick;
            }
            var start_time = (uint)Time.GetTicksUsec();

            // @todo Is there a way we can move this to _RemoteStart()?
            // Store an initial state before any ticks.
            if (current_tick == 0)
            {
                _SaveCurrentState();
                if (_logger != null)
                {
                    _CalculateDataHash(state_buffer[0].data);
                    _logger.WriteState(0, state_buffer[0].data);
                }
            }

            //####
            // STEP 1: PERFORM ANY ROLLBACKS, IF NECESSARY.
            //####
            if (mechanized)
            {
                rollback_ticks = mechanized_rollback_ticks;
            }
            else
            {
                if (debug_random_rollback_ticks > 0)
                {
                    GD.Randomize();
                    debug_rollback_ticks = (int)GD.Randi() % debug_random_rollback_ticks;
                }
                if (debug_rollback_ticks > 0 && current_tick >= debug_rollback_ticks)
                    rollback_ticks = Mathf.Max(rollback_ticks, debug_rollback_ticks);
                // We need to reload the current tick since we did a partial rollback
                // to the previous tick in order to interpolate.
                if (interpolation && current_tick > 0 && rollback_ticks == 0)
                    _CallLoadState(state_buffer[state_buffer.Count - 1].data);
            }
            if (rollback_ticks > 0)
            {
                if (_logger != null)
                {
                    _logger.data["rollback_ticks"] = rollback_ticks;
                    _logger.StartTiming("rollback");
                }
                var original_tick = current_tick;

                // Rollback our internal state.
                System.Diagnostics.Debug.Assert(rollback_ticks + 1 <= state_buffer.Count, "Not enough state in buffer to rollback requested number of frames");
                if (rollback_ticks + 1 > state_buffer.Count)
                {
                    _HandleFatalError($"Not enough state in buffer to rollback {rollback_ticks} frames");
                    return;
                }
                _CallLoadState(state_buffer[-rollback_ticks - 1].data);

                current_tick -= rollback_ticks;
                if (debug_check_local_state_consistency)
                {
                    // Save already computed states for better logging in case of discrepancy
                    _debug_check_local_state_consistency_buffer = state_buffer.GetRange(state_buffer.Count - rollback_ticks - 1, state_buffer.Count - 1);
                    // Debug check that states computed multiple times with complete inputs are the same
                    if (_last_state_hashed_tick >= current_tick)
                    {
                        var state = new StateBufferFrame(current_tick, _CallSaveState());
                        _DebugCheckConsistentLocalState(state, "Loaded");
                    }
                }
                for (int i = 0; i < rollback_ticks; i++)
                    state_buffer.PopBack();

                // Invalidate sync ticks after this, they may be asked for again
                if (requested_input_complete_tick > 0 && current_tick < requested_input_complete_tick)
                    requested_input_complete_tick = 0;
                StateLoaded?.Invoke(rollback_ticks);

                _in_rollback = true;
                // Iterate forward until we"re at the same spot we left off.
                while (rollback_ticks > 0)
                {
                    current_tick += 1;
                    if (!_DoTick(true))
                        return;
                    rollback_ticks -= 1;
                }
                System.Diagnostics.Debug.Assert(current_tick == original_tick, "Rollback didn't return to the original tick");
                _in_rollback = false;

                if (_logger != null)
                    _logger.StopTiming("rollback");
            }

            //####
            // STEP 2: SKIP TICKS, IF NECESSARY.
            //####
            if (!mechanized)
            {
                _RecordAdvantage();

                if (_ticks_spent_regaining_sync > 0)
                {
                    _ticks_spent_regaining_sync += 1;
                    if (max_ticks_to_regain_sync > 0 && _ticks_spent_regaining_sync > max_ticks_to_regain_sync)
                    {
                        _HandleFatalError("Unable to regain synchronization");
                        return;
                    }
                    // Check again if we"re still getting input buffer undzerruns.
                    if (!_CleanupBuffers())
                    {
                        // This can happen if there's a fatal error in _CleanupBuffers().
                        if (!started)
                        {
                            return;
                        }
                        // Even when we're skipping ticks, still send input.
                        _SendInputMessagesToAllPeers();
                        if (_logger != null)
                            _logger.SkipTick(Logger.SkipReason.INPUT_BUFFER_UNDERRUN, start_time);
                        return;
                    }
                    // Check if our max lag is still greater than the min lag to regain sync.
                    if (min_lag_to_regain_sync > 0 && _CalculateMaxLocalLag() > min_lag_to_regain_sync)
                    {
                        //print ("REGAINING SYNC: wait for local lag to reduce")
                        // Even when we're skipping ticks, still send input.
                        _SendInputMessagesToAllPeers();
                        if (_logger != null)
                            _logger.SkipTick(Logger.SkipReason.WAITING_TO_REGAIN_SYNC, start_time);
                        return;
                    }
                    // If we've reach this point, that means we've regained sync!
                    _ticks_spent_regaining_sync = 0;
                    SyncRegained?.Invoke();

                    // We don't want to skip ticks through the normal mechanism, because
                    // any skips that were previously calculated don't apply anymore.
                    skip_ticks = 0;

                }
                // Attempt to clean up buffers, but if we can't, that means we've lost sync.
                else if (!_CleanupBuffers())
                {
                    // This can happen if there's a fatal error in _CleanupBuffers().
                    if (!started)
                        return;
                    SyncLost?.Invoke();
                    _ticks_spent_regaining_sync = 1;
                    // Even when we're skipping ticks, still send input.
                    _SendInputMessagesToAllPeers();
                    if (_logger != null)
                        _logger.SkipTick(Logger.SkipReason.INPUT_BUFFER_UNDERRUN, start_time);
                    return;
                }
                if (skip_ticks > 0)
                {
                    skip_ticks -= 1;
                    if (skip_ticks == 0)
                    {
                        foreach (var peer in peers.Values)
                            peer.ClearAdvantage();
                    }
                    else
                    {
                        // Even when we're skipping ticks, still send input.
                        _SendInputMessagesToAllPeers();
                        if (_logger != null)
                            _logger.SkipTick(Logger.SkipReason.ADVANTAGE_ADJUSTMENT, start_time);
                        return;
                    }
                }
                if (_CalculateSkipTicks())
                {
                    // This means we need to skip some ticks, so may as well start now!
                    if (_logger != null)
                        _logger.SkipTick(Logger.SkipReason.ADVANTAGE_ADJUSTMENT, start_time);
                    return;
                }
            }
            else
                _CleanupBuffers();

            //####
            // STEP 3: GATHER INPUT AND RUN CURRENT TICK
            //####
            input_tick += 1;
            current_tick += 1;

            if (!mechanized)
            {
                var input_frame = _GetOrCreateInputFrame(input_tick);
                // The underlying error would have already been reported in
                // _GetOrCreateInputFrame() so we can just return here.
                if (input_frame == null)
                    return;
                if (_logger != null)
                    _logger.data["input_tick"] = input_tick;
                var local_input = _CallGetLocalInput();
                _CalculateDataHash(local_input);
                input_frame.players[network_adaptor.GetNetworkUniqueId()] = new InputForPlayer(local_input, false);

                // Only serialize and send input when we have real remote peers.
                if (peers.Count > 0)
                {
                    byte[] serialized_input = message_serializer.SerializeInput(local_input);

                    // check that the serialized then unserialized input matches the original 
                    if (debug_check_message_serializer_roundtrip)
                    {
                        GDC.Dictionary unserialized_input = message_serializer.UnserializeInput(serialized_input);
                        _CalculateDataHash(unserialized_input);
                        if (local_input["GetNode("] != unserialized_input[")$"])
                        {
                            GD.PushError($"The input is different after being serialized && unserialized \n Original: {OrderedDict2str(local_input)} \n Unserialized: {OrderedDict2str(unserialized_input)}");
                        }
                    }
                    _input_send_queue.Append(serialized_input);
                    System.Diagnostics.Debug.Assert(input_tick == _input_send_queue_start_tick + _input_send_queue.Count - 1, "Input send queue ticks numbers are misaligned");
                    _SendInputMessagesToAllPeers();
                }
            }
            if (current_tick > 0)
            {
                if (_logger != null)
                    _logger.StartTiming("current_tick");
                if (!_DoTick())
                    return;
                if (_logger != null)
                    _logger.StopTiming("current_tick");
                if (interpolation)
                {
                    // Capture the state data to interpolate between.
                    var to_state = state_buffer[state_buffer.Count - 1].data;
                    var from_state = state_buffer[state_buffer.Count - 2].data;
                    _interpolation_state.Clear();
                    foreach (string path in to_state.Keys)
                    {
                        if (from_state.Contains(path))
                            _interpolation_state[path] = new[] { from_state.Get<GDC.Dictionary>(path), to_state.Get<GDC.Dictionary>(path) };
                    }
                    // Return to state from the previous frame, so we can interpolate
                    // towards the state of the current frame.
                    _CallLoadState(state_buffer[state_buffer.Count - 2].data);
                }
            }
            _time_since_last_tick = 0f;
            _ran_physics_process = true;
            _ticks_since_last_interpolation_frame += 1;

            var total_time_msecs = (float)((uint)Time.GetTicksUsec() - start_time) / 1000.0;
            if (debug_physics_process_msecs > 0 && total_time_msecs > debug_physics_process_msecs)
                GD.PushError($"[{current_tick}] SyncManager._PhysicsProcess() took {total_time_msecs.ToString("0.00")}ms");
            if (_logger != null)
                _logger.EndTick(start_time);
        }

        public override void _Process(float delta)
        {
            if (!started)
                return;
            var start_time = (uint)Time.GetTicksUsec();

            // These are things that we want to run during "interpolation frames", in
            // order to slim down the normal frames. Or, if interpolation is disabled,
            // we need to run these always. If we haven't managed to run this for more
            // one tick, we make sure to sneak it in just in case.
            if (!interpolation || !_ran_physics_process || _ticks_since_last_interpolation_frame > 1)
            {
                if (_logger != null)
                    _logger.BeginInterpolationFrame(current_tick);
                _time_since_last_tick += delta;

                // Don't interpolate if we are skipping ticks, || just ran physics process.
                if (interpolation && skip_ticks == 0 && !_ran_physics_process)
                {
                    float weight = _time_since_last_tick / tick_time;
                    if (weight > 1)
                    {
                        weight = 1f;
                    }
                    _CallInterpolateState(weight);
                }
                // If there are no other peers, then we'll never receive any new input,
                // so we need to update the _input_complete_tick elsewhere. Here's a fine
                // place to do it!
                if (peers.Count == 0)
                    _UpdateInputCompleteTick();
                _UpdateStateHashes();

                if (interpolation)
                    InterpolationFrame?.Invoke();
                // Do this last to catch any data that came in late.
                network_adaptor.Poll();

                if (_logger != null)
                    _logger.EndInterpolationFrame(start_time);
                // Clear counter, because we just did an interpolation frame.
                _ticks_since_last_interpolation_frame = 0;
            }
            // Clear flag so subsequent _Process() calls will know that they weren't
            // preceeded by _PhysicsProcess().
            _ran_physics_process = false;

            var total_time_msecs = (float)((uint)Time.GetTicksUsec() - start_time) / 1000.0;
            if (debug_process_msecs > 0 && total_time_msecs > debug_process_msecs)
                GD.PushError($"[{current_tick}] SyncManager._Process() took {total_time_msecs.ToString("0.00")}ms");
        }

        public GDC.Dictionary _CleanDataForHashing(GDC.Dictionary input)
        {
            GDC.Dictionary cleaned = new GDC.Dictionary() { };
            foreach (string path in input.Keys)
            {
                if (path == "$")
                    continue;
                cleaned[path] = _CleanDataForHashingRecursive(input.Get<GDC.Dictionary>(path));
            }
            return cleaned;
        }

        public GDC.Dictionary _CleanDataForHashingRecursive(GDC.Dictionary input)
        {
            GDC.Dictionary cleaned = new GDC.Dictionary() { };
            foreach (string key in input)
            {
                if (key.BeginsWith("_"))
                    continue;

                var value = input[key];
                if (value is GDC.Dictionary dictValue)
                    cleaned[key] = _CleanDataForHashingRecursive(dictValue);
                else
                    cleaned[key] = value;
            }
            return cleaned;
        }

        // Calculates the hash without any keys that start with '_' (if string)
        // || less than 0 (if integer) to allow some properties to !count when
        // comparing comparing data.
        //
        // This can be used for comparing Input (to prevent a difference betwen predicted
        // input && real input from causing a rollback) && State (for when a property
        // is only used for interpolation).
        public int _CalculateDataHash(GDC.Dictionary input)
        {
            var cleaned = _CleanDataForHashing(input);
            var serialized = hash_serializer.Serialize(cleaned);
            var serialized_hash = GD.Hash(serialized);
            input["$"] = serialized_hash;
            return serialized_hash;
        }

        public void _OnReceivedInputTick(int peer_id, byte[] serialized_msg)
        {
            if (!started)
                return;
            var msg = message_serializer.UnserializeMessage(serialized_msg);

            GDC.Dictionary all_remote_input = msg.Get<GDC.Dictionary>(MessageSerializer.InputMessageKey.INPUT);
            var all_remote_ticks = new List<int>(all_remote_input.Keys.Cast<int>());
            all_remote_ticks.Sort();

            var first_remote_tick = all_remote_ticks[0];
            var last_remote_tick = all_remote_ticks[all_remote_ticks.Count - 1];

            if (first_remote_tick >= input_tick + max_buffer_size)
            {
                // This either happens because we are really far Behind (but maybe, just
                // maybe could catch up) || we are receiving old ticks from a previous
                // round that hadn't yet arrived. Just discard the message && hope for
                // the best, but if we can't keep up, another one of the fail safes will
                // detect that we are out of sync.
                GD.Print("Discarding message from the future");
                // We return because we don't even want to do the accounting that happens
                // after integrating input, since the data in this message could be
                // totally Bunk (ie. if it's from a previous match).
                return;

            }
            if (_logger != null)
                _logger.BeginInterframe();
            Peer peer = peers[peer_id];

            // Only process if it contains ticks we haven't received yet.
            if (last_remote_tick > peer.last_remote_input_tick_received)
            {
                // Integrate the input we received into the input buffer.
                foreach (var remote_tick in all_remote_ticks)
                {
                    // Skip ticks we already have.
                    if (remote_tick <= peer.last_remote_input_tick_received)
                    {
                        continue;
                    }
                    // This means the input frame has already been retired, which can only
                    // happen if we already had all the input.
                    if (remote_tick < _input_buffer_start_tick)
                    {
                        continue;

                    }
                    var remote_input = message_serializer.UnserializeInput(all_remote_input.Get<byte[]>(remote_tick));

                    //			GD.Print("------ UNSERIALIZE REMOTE INPUT IN SYNC MANAGER: " + GD.Str(GetTree().GetNetworkUniqueId()) + " --------")
                    //			GD.Print(remote_input);

                    var input_frame = _GetOrCreateInputFrame(remote_tick);
                    if (input_frame == null)
                    {
                        // _GetOrCreateInputFrame() will have already flagged the error,
                        // so we can just return here.
                        return;
                    }
                    // If we already have non-predicted input for this peer, then skip it.
                    if (!input_frame.IsPlayerInputPredicted(peer_id))
                    {
                        continue;

                        //print ("Received remote tick %s from %s" % [remote_tick, peer_id])
                    }
                    if (_logger != null)
                    {
                        _logger.AddValue($"remote_ticks_received_from_{peer_id}", remote_tick);
                    }
                    // If we received a tick in the past && we aren't already setup to
                    // rollback earlier than that...
                    var tick_delta = current_tick - remote_tick;
                    if (tick_delta >= 0 && rollback_ticks <= tick_delta)
                    {
                        // Grab our predicted input, && store the remote input.
                        var local_input = input_frame.GetPlayerInput(peer_id);
                        input_frame.players[peer_id] = new InputForPlayer(remote_input, false);

                        // Check if the remote input matches what we had predicted, if not,
                        // flag that we need to rollback.
                        if (local_input["GetNode("] != remote_input[")$"])
                        {
                            rollback_ticks = tick_delta + 1;
                            PredictionMissed?.Invoke(remote_tick, peer_id, local_input, remote_input);
                            RollbackFlagged?.Invoke(remote_tick);
                        }
                    }
                    else
                    {
                        // Otherwise, just store it.
                        input_frame.players[peer_id] = new InputForPlayer(remote_input, false);
                    }
                }
                // Find what the last remote tick we received was after filling these in.
                var inputBuffeIndex = (peer.last_remote_input_tick_received - _input_buffer_start_tick) + 1;
                while (inputBuffeIndex < input_buffer.Count && !input_buffer[inputBuffeIndex].IsPlayerInputPredicted(peer_id))
                {
                    peer.last_remote_input_tick_received += 1;
                    inputBuffeIndex += 1;

                }
                // Update _input_complete_tick for new input.
                _UpdateInputCompleteTick();
            }
            // Record the next frame the other peer needs.
            peer.next_local_input_tick_requested = Math.Max(msg.Get<int>(MessageSerializer.InputMessageKey.NEXT_INPUT_TICK_REQUESTED), peer.next_local_input_tick_requested);

            // Number of frames the remote is predicting for us.
            peer.remote_lag = (peer.last_remote_input_tick_received + 1) - peer.next_local_input_tick_requested;

            // Process state hashes.
            var remote_state_hashes = msg.Get<GDC.Dictionary>(MessageSerializer.InputMessageKey.STATE_HASHES);
            foreach (int remote_tick in remote_state_hashes.Keys)
            {
                var state_hash_frame = _GetStateHashFrame(remote_tick);
                if (state_hash_frame != null && !state_hash_frame.HasPeerHash(peer_id))
                {
                    if (!state_hash_frame.RecordPeerHash(peer_id, remote_state_hashes.Get<int>(remote_tick)))
                        RemoteStateMismatch?.Invoke(remote_tick, peer_id, state_hash_frame.state_hash, remote_state_hashes.Get<int>(remote_tick));
                }
            }
            // Find what the last remote state hash we received was after filling these in.
            var stateHashesIndex = (peer.last_remote_hash_tick_received - _state_hashes_start_tick) + 1;
            while (stateHashesIndex < state_hashes.Count && state_hashes[stateHashesIndex].HasPeerHash(peer_id))
            {
                peer.last_remote_hash_tick_received += 1;
                stateHashesIndex += 1;
            }
            // Record the next state hash that the other peer needs.
            peer.next_local_hash_tick_requested = Mathf.Max(msg.Get<int>(MessageSerializer.InputMessageKey.NEXT_HASH_TICK_REQUESTED), peer.next_local_hash_tick_requested);
        }

        public void ResetMechanizedData()
        {
            mechanized_input_received.Clear();
            mechanized_rollback_ticks = 0;
        }

        public void _ProcessMechanizedInput()
        {
            foreach (var peer_id in mechanized_input_received)
            {
                var peer_input = mechanized_input_received.Get<GDC.Dictionary>(peer_id);
                foreach (var tick in peer_input)
                {
                    var input = peer_input.Get<GDC.Dictionary>(tick);
                    var input_frame = _GetOrCreateInputFrame((int)(tick));
                    input_frame.players[(int)(peer_id)] = new InputForPlayer(input, false);
                }
            }
        }

        public void ExecuteMechanizedTick()
        {
            _ProcessMechanizedInput();
            _PhysicsProcess(tick_time);
            ResetMechanizedData();
        }

        public void ExecuteMechanizedInterpolationFrame(float delta)
        {
            _UpdateInputCompleteTick();
            _ran_physics_process = false;
            _Process(delta);
            _ProcessMechanizedInput();
            ResetMechanizedData();
        }

        public void ExecuteMechanizedInterframe()
        {
            _ProcessMechanizedInput();
            ResetMechanizedData();
        }

        public GDC.Dictionary SortDictionaryKeys(GDC.Dictionary input)
        {
            GDC.Dictionary output = new GDC.Dictionary() { };

            var keys = new List<string>(input.Keys.Cast<string>());
            keys.Sort();
            foreach (var key in keys)
                output[key] = input[key];
            return output;
        }

        public Node Spawn(string name, Node parent, PackedScene scene, GDC.Dictionary data = null, bool rename = true, string signal_name = "")
        {
            if (data == null)
                data = new GDC.Dictionary() { };
            if (!started)
            {
                GD.PushError($"Refusing to spawn {name} before SyncManager has started");
                return null;
            }
            return _spawn_manager.Spawn(name, parent, scene, data, rename, signal_name);
        }

        public void Despawn(Node node)
        {
            _spawn_manager.Despawn(node);
        }

        public void _OnSpawnManagerSceneSpawned(string name, Node spawned_node, PackedScene scene, GDC.Dictionary data)
        {
            SceneSpawned?.Invoke(name, spawned_node, scene, data);
        }

        public void _OnSpawnManagerSceneDespawned(string name, Node node)
        {
            SceneDespawned?.Invoke(name, node);
        }

        public bool IsInRollback()
        {
            return _in_rollback;
        }

        public bool IsRespawning()
        {
            return _spawn_manager.is_respawning;
        }

        public async void SetDefaultSoundBus(string bus)
        {
            if (_sound_manager == null)
                await ToSignal(this, "ready");
            _sound_manager.default_bus = bus;
        }

        public void PlaySound(string identifier, AudioStream sound, GDC.Dictionary info = null)
        {
            if (info == null)
                info = new GDC.Dictionary() { };
            _sound_manager.PlaySound(identifier, sound, info);
        }

        public bool EnsureCurrentTickInputComplete()
        {
            if (IsCurrentTickInputComplete())
                return true;
            if (requested_input_complete_tick == 0 || requested_input_complete_tick > current_tick)
                requested_input_complete_tick = current_tick;
            return false;
        }

        public string OrderedDict2str(GDC.Dictionary dict)
        {
            string ret = "{";
            int i = 0;
            foreach (var key in dict.Keys)
            {
                var value = dict[key];
                var value_str = value is GDC.Dictionary dictValue ? OrderedDict2str(dictValue) : GD.Str(value);
                ret += $"{key}:{value_str}";

                if (i != dict.Count - 1)
                    ret += ", ";
                i++;
            }
            ret += "}";
            return ret;
        }

        public void _DebugCheckConsistentLocalState(StateBufferFrame state, string message = "Loaded")
        {
            var hashed_state = _CalculateDataHash(state.data);
            var previously_hashed_frame = _GetStateHashFrame(current_tick);
            var previous_state = _debug_check_local_state_consistency_buffer.PopFront();
            if (previously_hashed_frame != null && previously_hashed_frame.state_hash != hashed_state)
            {
                var comparer = new DebugStateComparer();
                comparer.FindMismatches(previous_state.data, state.data);
                GD.PushError($"{message} state is !consistent with saved state:\n {comparer.PrintMismatches()}");
            }
        }
    }
}