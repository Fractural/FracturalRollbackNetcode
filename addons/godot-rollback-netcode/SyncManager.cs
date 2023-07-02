
using System;
using Godot;
using Dictionary = Godot.Collections.Dictionary;
using Array = Godot.Collections.Array;


public class SyncManager : Node
{
	 
	public const var SpawnManager = GD.Load("res://addons/godot-rollback-netcode/SpawnManager.gd");
	public const var SoundManager = GD.Load("res://addons/godot-rollback-netcode/SoundManager.gd");
	public const var NetworkAdaptor = GD.Load("res://addons/godot-rollback-netcode/NetworkAdaptor.gd");
	public const var MessageSerializer = GD.Load("res://addons/godot-rollback-netcode/MessageSerializer.gd");
	public const var HashSerializer = GD.Load("res://addons/godot-rollback-netcode/HashSerializer.gd");
	public const var Logger = GD.Load("res://addons/godot-rollback-netcode/Logger.gd");
	public const var DebugStateComparer = GD.Load("res://addons/godot-rollback-netcode/DebugStateComparer.gd");
	public const var Utils = GD.Load("res://addons/godot-rollback-netcode/Utils.gd");
	
	class Peer extends Reference:
		int peer_id
		
		int rtt
		int last_ping_received
		float time_delta
		
		int last_remote_input_tick_received = 0;
		int next_local_input_tick_requested = 1;
		int last_remote_hash_tick_received = 0;
		int next_local_hash_tick_requested = 1;
		
		int remote_lag
		int local_lag
		
		float calculated_advantage
		Array advantage_list  = new Array(){};
		
		public void _Init(int _peer_id)
		{	  
			peer_id = _peer_id;
		
		}
	
		public void RecordAdvantage(int ticks_to_calculate_advantage)
		{	  
			advantage_list.Append(local_lag - remote_lag);
			if(advantage_list.Size() >= ticks_to_calculate_advantage)
			{
				float total = 0;
				foreach(var x in advantage_list)
				{
					total += x;
				}
				calculated_advantage = total / advantage_list.Size();
				advantage_list.Clear();
		
			}
		}
	
		public void ClearAdvantage()
		{	  
			calculated_advantage = 0.0;
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
	
	public class InputForPlayer:
		Dictionary input  = new Dictionary(){};
		bool predicted
		
		public void _Init(Dictionary _input, bool _predicted)
		{	  
			input = _input;
			predicted = _predicted;
	
		}
	
	public class InputBufferFrame:
		int tick
		Dictionary players  = new Dictionary(){};
		
		public void _Init(int _tick)
		{	  
			tick = _tick;
		
		}
	
		public Dictionary GetPlayerInput(int peer_id)
		{	  
			if(players.Has(peer_id))
			{
				return players[peer_id].input;
			}
			return new Dictionary(){};
		
		}
	
		public bool IsPlayerInputPredicted(int peer_id)
		{	  
			if(players.Has(peer_id))
			{
				return players[peer_id].predicted;
			}
			return true;
		
		}
	
		public Array GetMissingPeers(Dictionary peers)
		{	  
			Array missing  = new Array(){};
			foreach(var peer_id in peers)
			{
				if(!players.Has(peer_id) || players[peer_id].predicted)
				{
					missing.Append(peer_id);
				}
			}
			return missing;
		
		}
	
		public bool IsComplete(Dictionary peers)
		{	  
			foreach(var peer_id in peers)
			{
				if(!players.Has(peer_id) || players[peer_id].predicted)
				{
					return false;
				}
			}
			return true;
	
		}
	
	public class StateBufferFrame:
		int tick
		Dictionary data
	
		public void _Init(__TYPE _tick, __TYPE _data)
		{	  
			tick = _tick;
			data = _data;
	
		}
	
	public class StateHashFrame:
		int tick
		int state_hash
		
		Dictionary peer_hashes  = new Dictionary(){};
		bool mismatch  = false;
		
		public void _Init(int _tick, int _state_hash)
		{	  
			tick = _tick;
			state_hash = _state_hash;
		
		}
	
		public bool RecordPeerHash(int peer_id, int peer_hash)
		{	  
			peer_hashes[peer_id] = peer_hash;
			if(peer_hash != state_hash)
			{
				mismatch = true;
				return false;
			}
			return true;
		
		}
	
		public bool HasPeerHash(int peer_id)
		{	  
			return peer_hashes.Has(peer_id);
		
		}
	
		public bool IsComplete(Dictionary peers)
		{	  
			foreach(var peer_id in peers)
			{
				if(!peer_hashes.Has(peer_id))
				{
					return false;
				}
			}
			return true;
		
		}
	
		public Array GetMissingPeers(Dictionary peers)
		{	  
			Array missing  = new Array(){};
			foreach(var peer_id in peers)
			{
				if(!peer_hashes.Has(peer_id))
				{
					missing.Append(peer_id);
				}
			}
			return missing;
	
		}
	
	public const string DEFAULT_NETWORK_ADAPTOR_PATH := "res://addons/godot-rollback-netcode/RPCNetworkAdaptor.gd"
	public const string DEFAULT_MESSAGE_SERIALIZER_PATH := "res://addons/godot-rollback-netcode/MessageSerializer.gd"
	public const string DEFAULT_HASH_SERIALIZER_PATH := "res://addons/godot-rollback-netcode/HashSerializer.gd"
	
	Object network_adaptor {set{SetNetworkAdaptor(value);}}
	Object message_serializer {set{SetMessageSerializer(value);}}
	Object hash_serializer {set{SetHashSerializer(value);}}
	
	public Dictionary peers  = new Dictionary(){};
	public Array input_buffer  = new Array(){};
	public Array state_buffer  = new Array(){};
	public Array state_hashes  = new Array(){};
	
	public bool mechanized  = false {set{SetMechanized(value);}}
	public Dictionary mechanized_input_received  = new Dictionary(){};
	public int mechanized_rollback_ticks  = 0;
	
	public int max_buffer_size  = 20;
	public int ticks_to_calculate_advantage  = 60;
	public int input_delay  = 2 {set{SetInputDelay(value);}}
	public int max_input_frames_per_message  = 5;
	public int max_messages_at_once  = 2;
	public int max_ticks_to_regain_sync  = 300;
	public int min_lag_to_regain_sync  = 5;
	public bool interpolation  = false;
	public int max_state_mismatch_count  = 10;
	
	public int debug_rollback_ticks  = 0;
	public int debug_random_rollback_ticks  = 0;
	public int debug_message_bytes  = 700;
	public int debug_skip_nth_message  = 0;
	public float debug_physics_process_msecs  = 10.0f;
	public float debug_process_msecs  = 10.0f;
	public bool debug_check_message_serializer_roundtrip  = false;
	public bool debug_check_local_state_consistency  = false;
	
	// In seconds, because we don't want it to be dependent on the network tick.
	public float ping_frequency  = 1.0f {set{SetPingFrequency(value);}}
	
	public int input_tick = 0 {set{_SetReadonlyVariable(value);}}
	public int current_tick = 0 {set{_SetReadonlyVariable(value);}}
	public int skip_ticks = 0 {set{_SetReadonlyVariable(value);}}
	public int rollback_ticks = 0 {set{_SetReadonlyVariable(value);}}
	public int requested_input_complete_tick = 0 {set{_SetReadonlyVariable(value);}}
	public bool started  = false {set{_SetReadonlyVariable(value);}}
	float tick_time {set{_SetReadonlyVariable(value);}}
	
	public bool _host_starting  = false;
	public Timer _ping_timer
	private __TYPE _spawn_manager;
	private __TYPE _sound_manager;
	private __TYPE _logger;
	public int _input_buffer_start_tick
	public int _state_buffer_start_tick
	public int _state_hashes_start_tick
	public Array _input_send_queue  = new Array(){};
	public int _input_send_queue_start_tick
	public int _ticks_spent_regaining_sync  = 0;
	public Dictionary _interpolation_state  = new Dictionary(){};
	public float _time_since_last_tick  = 0.0f;
	public int _debug_skip_nth_message_counter  = 0;
	public int _input_complete_tick  = 0;
	public int _state_complete_tick  = 0;
	public int _last_state_hashed_tick  = 0;
	public int _state_mismatch_count  = 0;
	public bool _in_rollback  = false;
	public bool _ran_physics_process  = false;
	public int _ticks_since_last_interpolation_frame  = 0;
	public Array _debug_check_local_state_consistency_buffer  = new Array(){};
	
	[Signal] delegate void SyncStarted ();
	[Signal] delegate void SyncStopped ();
	[Signal] delegate void SyncLost ();
	[Signal] delegate void SyncRegained ();
	[Signal] delegate void SyncError (msg);
	
	[Signal] delegate void SkipTicksFlagged (count);
	[Signal] delegate void RollbackFlagged (tick);
	[Signal] delegate void PredictionMissed (tick, peer_id, local_input, remote_input);
	[Signal] delegate void RemoteStateMismatch (tick, peer_id, local_hash, remote_hash);
	
	[Signal] delegate void PeerAdded (peer_id);
	[Signal] delegate void PeerRemoved (peer_id);
	[Signal] delegate void PeerPingedBack (peer);
	
	[Signal] delegate void StateLoaded (rollback_ticks);
	[Signal] delegate void TickFinished (is_rollback);
	[Signal] delegate void TickRetired (tick);
	[Signal] delegate void TickInputComplete (tick);
	[Signal] delegate void SceneSpawned (name, spawned_node, scene, data);
	[Signal] delegate void SceneDespawned (name, node);
	[Signal] delegate void InterpolationFrame ();
	
	public void _EnterTree()
	{  
		var project_settings_node = GD.Load("res://addons/godot-rollback-netcode/ProjectSettings.gd").new()
		project_settings_node.AddProjectSettings();
		project_settings_node.Free();
	
	}
	
	public void _ExitTree()
	{  
		StopLogging();
	
	}
	
	public void _Ready()
	{  
		//get_tree().Connect("network_peer_disconnected", this, "remove_peer")
		//get_tree().Connect("server_disconnected", this, "stop")
		
		Dictionary project_settings  = new Dictionary(){
			max_buffer_size = "network/rollback/max_buffer_size",
			ticks_to_calculate_advantage = "network/rollback/ticks_to_calculate_advantage",
			input_delay = "network/rollback/input_delay",
			ping_frequency = "network/rollback/ping_frequency",
			interpolation = "network/rollback/interpolation",
			max_input_frames_per_message = "network/rollback/limits/max_input_frames_per_message",
			max_messages_at_once = "network/rollback/limits/max_messages_at_once",
			max_ticks_to_regain_sync = "network/rollback/limits/max_ticks_to_regain_sync",
			min_lag_to_regain_sync = "network/rollback/limits/min_lag_to_regain_sync",
			max_state_mismatch_count = "network/rollback/limits/max_state_mismatch_count",
			debug_rollback_ticks = "network/rollback/debug/rollback_ticks",
			debug_random_rollback_ticks = "network/rollback/debug/random_rollback_ticks",
			debug_message_bytes = "network/rollback/debug/message_bytes",
			debug_skip_nth_message = "network/rollback/debug/skip_nth_message",
			debug_physics_process_msecs = "network/rollback/debug/physics_process_msecs",
			debug_process_msecs = "network/rollback/debug/process_msecs",
			debug_check_message_serializer_roundtrip = "network/rollback/debug/check_message_serializer_roundtrip",
			debug_check_local_state_consistency = "network/rollback/debug/check_local_state_consistency",
		};
		foreach(var property_name in project_settings)
		{
			var setting_name = project_settings[property_name];
			if(ProjectSettings.HasSetting(setting_name))
			{
				Set(property_name, ProjectSettings.GetSetting(setting_name));
		
			}
		}
		_ping_timer = new Timer()
		_ping_timer.name = "PingTimer";
		_ping_timer.wait_time = ping_frequency;
		_ping_timer.autostart = true;
		_ping_timer.one_shot = false;
		_ping_timer.pause_mode = Node.PAUSE_MODE_PROCESS;
		_ping_timer.Connect("timeout", this, "_on_ping_timer_timeout");
		AddChild(_ping_timer);
		
		_spawn_manager = new SpawnManager()
		_spawn_manager.name = "SpawnManager";
		AddChild(_spawn_manager);
		_spawn_manager.Connect("scene_spawned", this, "_on_SpawnManager_scene_spawned");
		_spawn_manager.Connect("scene_despawned", this, "_on_SpawnManager_scene_despawned");
		
		_sound_manager = new SoundManager()
		_sound_manager.name = "SoundManager";
		AddChild(_sound_manager);
		_sound_manager.SetupSoundManager(this);
		
		if(network_adaptor == null)
		{
			ResetNetworkAdaptor();
		}
		if(message_serializer == null)
		{
			SetMessageSerializer(_CreateClassFromProjectSettings("network/rollback/classes/message_serializer", DEFAULT_MESSAGE_SERIALIZER_PATH));
		}
		if(hash_serializer == null)
		{
			SetHashSerializer(_CreateClassFromProjectSettings("network/rollback/classes/hash_serializer", DEFAULT_HASH_SERIALIZER_PATH));
	
		}
	}
	
	public void _SetReadonlyVariable(__TYPE _value)
	{  
	
	}
	
	public __TYPE _CreateClassFromProjectSettings(String setting_name, String default_path)
	{  
		string class_path  = "";
		if(ProjectSettings.HasSetting(setting_name))
		{
			class_path = ProjectSettings.GetSetting(setting_name);
		}
		if(class_path == "")
		{
			class_path = default_path;
		}
		return GD.Load(class_path).new()
	
	}
	
	public void SetNetworkAdaptor(Object _network_adaptor)
	{  
		System.Diagnostics.Debug.Assert(!started, "Changing the network adaptor after SyncManager has started will probably break everything");
		
		if(network_adaptor != null)
		{
			network_adaptor.DetachNetworkAdaptor(this);
			network_adaptor.Disconnect("received_ping", this, "_on_received_ping");
			network_adaptor.Disconnect("received_ping_back", this, "_on_received_ping_back");
			network_adaptor.Disconnect("received_remote_start", this, "_on_received_remote_start");
			network_adaptor.Disconnect("received_remote_stop", this, "_on_received_remote_stop");
			network_adaptor.Disconnect("received_input_tick", this, "_on_received_input_tick");
			
			RemoveChild(network_adaptor);
			network_adaptor.QueueFree();
		
		}
		network_adaptor = _network_adaptor;
		network_adaptor.name = "NetworkAdaptor";
		AddChild(network_adaptor);
		network_adaptor.Connect("received_ping", this, "_on_received_ping");
		network_adaptor.Connect("received_ping_back", this, "_on_received_ping_back");
		network_adaptor.Connect("received_remote_start", this, "_on_received_remote_start");
		network_adaptor.Connect("received_remote_stop", this, "_on_received_remote_stop");
		network_adaptor.Connect("received_input_tick", this, "_on_received_input_tick");
		network_adaptor.AttachNetworkAdaptor(this);
	
	}
	
	public void ResetNetworkAdaptor()
	{  
		SetNetworkAdaptor(_CreateClassFromProjectSettings("network/rollback/classes/network_adaptor", DEFAULT_NETWORK_ADAPTOR_PATH));
	
	}
	
	public void SetMessageSerializer(Object _message_serializer)
	{  
		System.Diagnostics.Debug.Assert(!started, "Changing the message serializer after SyncManager has started will probably break everything");
		message_serializer = _message_serializer;
	
	}
	
	public void SetHashSerializer(Object _hash_serializer)
	{  
		System.Diagnostics.Debug.Assert(!started, "Changing the hash serializer after SyncManager has started will probably break everything");
		hash_serializer = _hash_serializer;
	
	}
	
	public void SetMechanized(bool _mechanized)
	{  
		System.Diagnostics.Debug.Assert(!started, "Changing the mechanized flag after SyncManager has started will probably break everything");
		mechanized = _mechanized;
		
		SetProcess(!mechanized);
		SetPhysicsProcess(!mechanized);
		_ping_timer.paused = mechanized;
		
		if(mechanized)
		{
			StopLogging();
	
		}
	}
	
	public void SetPingFrequency(__TYPE _ping_frequency)
	{  
		ping_frequency = _ping_frequency;
		if(_ping_timer)
		{
			_ping_timer.wait_time = _ping_frequency;
	
		}
	}
	
	public void SetInputDelay(int _input_delay)
	{  
		if(started)
		{
			GD.PushWarning("Cannot change input delay after sync"ing has already started\");
		}
		input_delay = _input_delay;
	
	}
	
	public void AddPeer(int peer_id)
	{  
		System.Diagnostics.Debug.Assert(!peers.Has(peer_id), \"Peer with given id already exists\");
		System.Diagnostics.Debug.Assert(peer_id != network_adaptor.GetNetworkUniqueId(), \"Cannot add ourselves as a peer in SyncManager\");
		
		if(peers.Has(peer_id))
		{
			return;
		}
		if(peer_id == network_adaptor.GetNetworkUniqueId())
		{
			return;
		
		}
		peers[peer_id] = Peer.new(peer_id)
		EmitSignal(\"peer_added\", peer_id);
	
	}
	
	public bool HasPeer(int peer_id)
	{  
		return peers.Has(peer_id);
	
	}
	
	public Peer GetPeer(int peer_id)
	{  
		return peers.Get(peer_id);
	
	}
	
	public void RemovePeer(int peer_id)
	{  
		if(peers.Has(peer_id))
		{
			peers.Erase(peer_id);
			EmitSignal(\"peer_removed\", peer_id);
		}
		if(peers.Size() == 0)
		{
			Stop();
	
		}
	}
	
	public void ClearPeers()
	{  
		foreach(var peer_id in peers.Keys().Duplicate())
		{
			RemovePeer(peer_id);
	
		}
	}
	
	public void _OnPingTimerTimeout()
	{  
		if(peers.Size() == 0)
		{
			return;
		}
		Dictionary msg = new Dictionary(){
			local_time = OS.GetSystemTimeMsecs(),
		};
		foreach(var peer_id in peers)
		{
			System.Diagnostics.Debug.Assert(peer_id != network_adaptor.GetNetworkUniqueId(), \"Cannot ping ourselves\");
			network_adaptor.SendPing(peer_id, msg);
	
		}
	}
	
	public void _OnReceivedPing(int peer_id, Dictionary msg)
	{  
		System.Diagnostics.Debug.Assert(peer_id != network_adaptor.GetNetworkUniqueId(), \"Cannot ping back ourselves\");
		msg["remote_time"] = OS.GetSystemTimeMsecs();
		network_adaptor.SendPingBack(peer_id, msg);
	
	}
	
	public void _OnReceivedPingBack(int peer_id, Dictionary msg)
	{  
		var system_time = OS.GetSystemTimeMsecs();
		var peer = peers[peer_id];
		peer.last_ping_received = system_time;
		peer.rtt = system_time - msg["local_time"];
		peer.time_delta = msg["remote_time"] - msg["local_time"] - (peer.rtt / 2.0);
		EmitSignal(\"peer_pinged_back\", peer);
	
	}
	
	public void StartLogging(String log_file_path, Dictionary match_info = new Dictionary(){})
	{  
		// Our logger needs threads!
		if(!OS.CanUseThreads())
		{
			return;
		}
		if(mechanized)
		{
			return;
		
		}
		if(!_logger)
		{
			_logger = Logger.new(this)
		}
		else
		{
			_logger.Stop();
		
		}
		if(_logger.Start(log_file_path, network_adaptor.GetNetworkUniqueId(), match_info) != OK)
		{
			StopLogging();
	
		}
	}
	
	public void StopLogging()
	{  
		if(_logger)
		{
			_logger.Stop();
			_logger = null;
	
		}
	}
	
	public async void Start()
	{  
		System.Diagnostics.Debug.Assert(network_adaptor.IsNetworkHost() || mechanized, \"start() should only be called on the host\");
		if(started || _host_starting)
		{
			return;
		}
		if(mechanized)
		{
			_OnReceivedRemoteStart();
			return;
		}
		if(network_adaptor.IsNetworkHost())
		{
			int highest_rtt = 0;
			foreach(var peer in peers.Values())
			{
				highest_rtt = Mathf.Max(highest_rtt, peer.rtt);
			
			// Call _RemoteStart() on all the other peers.
			}
			foreach(var peer_id in peers)
			{
				network_adaptor.SendRemoteStart(peer_id);
			
			// Attempt to prevent double starting on the host.
			}
			_host_starting = true;
			
			// Wait for half the highest RTT to start locally.
			Print (\"Delaying host start by %sms\" % (highest_rtt / 2));
			await ToSignal(GetTree().CreateTimer(highest_rtt / 2000.0), "timeout")
			
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
		_time_since_last_tick = 0.0;
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
		tick_time = (1.0 / Engine.iterations_per_second);
		started = true;
		network_adaptor.StartNetworkAdaptor(this);
		_spawn_manager.Reset();
		EmitSignal(\"sync_started\");
	
	}
	
	public void Stop()
	{  
		if(network_adaptor.IsNetworkHost() && !mechanized)
		{
			foreach(var peer_id in peers)
			{
				network_adaptor.SendRemoteStop(peer_id);
		
			}
		}
		_OnReceivedRemoteStop();
	
	}
	
	public void _OnReceivedRemoteStop()
	{  
		if(!(started || _host_starting))
		{
			return;
		
		}
		network_adaptor.StopNetworkAdaptor(this);
		started = false;
		_host_starting = false;
		_Reset();
		
		foreach(var peer in peers.Values())
		{
			peer.Clear();
		
		}
		EmitSignal(\"sync_stopped\");
		_spawn_manager.Reset();
	
	}
	
	public __TYPE _HandleFatalError(String msg)
	{  
		EmitSignal(\"sync_error\", msg);
		GD.PushError(\"NETWORK SYNC LOST: \" + msg);
		Stop();
		if(_logger)
		{
			_logger.LogFatalError(msg);
		}
		return null;
	
	}
	
	public Dictionary _CallGetLocalInput()
	{  
		Dictionary input  = new Dictionary(){};
		Array nodes = GetTree().GetNodesInGroup("network_sync");
		foreach(var node in nodes)
		{
			if(network_adaptor.IsNetworkMasterForNode(node) && Utils.HasInteropMethod(node, "_get_local_input") && node.IsInsideTree() && !node.IsQueuedForDeletion())
			{
				var node_input = Utils.CallInteropMethod(node, "_get_local_input");
				if(node_input.Size() > 0)
				{
					input[GD.Str(node.GetPath())] = node_input;
				}
			}
		}
		return input;
	
	}
	
	public void _CallNetworkProcess(InputBufferFrame input_frame)
	{  
		Array nodes = GetTree().GetNodesInGroup("network_sync");
		Array process_nodes  = new Array(){};
		Array postprocess_nodes  = new Array(){};
		
		Array csharp_process_nodes  = new Array(){};
		Array csharp_postprocess_nodes  = new Array(){};
		
		// Call _NetworkPreprocess() && collect list of nodes with the other
		// virtual methods.
		var i = nodes.Size();
		while(i > 0)
		{
			i -= 1;
			var node = nodes[i];
			if(node.IsInsideTree() && !node.IsQueuedForDeletion())
			{
				if(Utils.HasInteropMethod(node, "_network_preprocess"))
				{
					var player_input = input_frame.GetPlayerInput(node.GetNetworkMaster());
					node._NetworkPreprocess(player_input.Get(GD.Str(node.GetPath()), new Dictionary(){}));
				}
				if(Utils.HasInteropMethod(node, "_network_process"))
				{
					process_nodes.Append(node);
				}
				if(Utils.HasInteropMethod(node, "_network_postprocess"))
				{
					postprocess_nodes.Append(node);
	
		// Call _NetworkProcess().
				}
			}
		}
		foreach(var node in process_nodes)
		{
			if(node.IsInsideTree() && !node.IsQueuedForDeletion())
			{
				var player_input = input_frame.GetPlayerInput(node.GetNetworkMaster());
				Utils.CallInteropMethod(node, "_network_process", new Array(){player_input.Get(GD.Str(node.GetPath()), new Dictionary(){})});
			
		// Call _NetworkPostprocess().
			}
		}
		foreach(var node in postprocess_nodes)
		{
			if(node.IsInsideTree() && !node.IsQueuedForDeletion())
			{
				var player_input = input_frame.GetPlayerInput(node.GetNetworkMaster());
				Utils.CallInteropMethod(node, "_network_postprocess", new Array(){player_input.Get(GD.Str(node.GetPath()), new Dictionary(){})});
		
			}
		}
	}
	
	public Dictionary _CallSaveState()
	{  
		Dictionary state  = new Dictionary(){};
		Array nodes = GetTree().GetNodesInGroup("network_sync");
		foreach(var node in nodes)
		{
			if(Utils.HasInteropMethod(node, "_save_state") && node.IsInsideTree() && !node.IsQueuedForDeletion())
			{
				var node_path = GD.Str(node.GetPath());
				if node_path != \"\":
					state[node_path] = Utils.CallInteropMethod(node, "_save_state");
		
			}
		}
		return state;
	
	}
	
	public void _CallLoadState(Dictionary state)
	{  
		foreach(var node_path in state)
		{
			if(node_path == "$")
			{
				continue;
			}
			var node = GetNodeOrNull(node_path);
			System.Diagnostics.Debug.Assert(node != null, \"Unable to restore state to missing node: %s\" % node_path);
			if(node && Utils.HasInteropMethod(node, "_load_state"))
			{
				Utils.CallInteropMethod(node, "_load_state", [state[node_path]]);
	
			}
		}
	}
	
	public void _CallInterpolateState(float weight)
	{  
		foreach(var node_path in _interpolation_state)
		{
			if(node_path == "$")
			{
				continue;
			}
			var node = GetNodeOrNull(node_path);
			if(node)
			{
				if(Utils.HasInteropMethod(node, "_interpolate_state"))
				{
					var states = _interpolation_state[node_path];
					Utils.CallInteropMethod(node, "_interpolate_state", [states[0], states[1], weight]);
	
				}
			}
		}
	}
	
	public void _SaveCurrentState()
	{  
		System.Diagnostics.Debug.Assert(current_tick >= 0, \"Attempting to store state for negative tick\");
		if(current_tick < 0)
		{
			return;
		
		}
		state_buffer.Append(StateBufferFrame.new(current_tick, _CallSaveState()));
		
		// If the input for this state is complete, then update _state_complete_tick.
		if(_input_complete_tick > _state_complete_tick)
		{
			// Set to the current_tick so long as its less than || equal to the
			// _input_complete_tick, otherwise, cap it to the _input_complete_tick.
			_state_complete_tick = current_tick <= _input_complete_tick ? current_tick : _input_complete_tick
	
		}
	}
	
	public void _UpdateInputCompleteTick()
	{  
		while(input_tick >= _input_complete_tick + 1)
		{
			InputBufferFrame input_frame = GetInputFrame(_input_complete_tick + 1);
			if(!input_frame)
			{
				break;
			}
			if(!input_frame.IsComplete(peers))
			{
				break;
			// When we add debug rollbacks mark the input as !complete
			// so that the invariant \"a complete input frame cannot be rolled back\" is respected
			// NB: a complete input frame can still be loaded in a rollback for the incomplete input next frame
			}
			if(debug_random_rollback_ticks > 0 && _input_complete_tick + 1 > current_tick - debug_random_rollback_ticks)
			{
				break;
			}
			if(debug_rollback_ticks > 0 && _input_complete_tick + 1 > current_tick - debug_rollback_ticks)
			{
				break;
			
			}
			if(_logger)
			{
				_logger.WriteInput(input_frame.tick, input_frame.players);
			
			}
			_input_complete_tick += 1;
			
			// This tick should be recomputed with complete inputs, let"s roll back
			if(_input_complete_tick == requested_input_complete_tick)
			{
				requested_input_complete_tick = 0;
				var tick_delta = current_tick - _input_complete_tick;
				if(tick_delta >= 0 && rollback_ticks <= tick_delta)
				{
					rollback_ticks = tick_delta + 1;
					EmitSignal("rollback_flagged", _input_complete_tick);
			
				}
			}
			EmitSignal("tick_input_complete", _input_complete_tick);
	
		}
	}
	
	public void _UpdateStateHashes()
	{  
		while(_state_complete_tick > _last_state_hashed_tick)
		{
			StateBufferFrame state_frame = _GetStateFrame(_last_state_hashed_tick + 1);
			if(!state_frame)
			{
				_HandleFatalError("Unable to hash state");
				return;
			
			}
			_last_state_hashed_tick += 1;
			
			var state_hash = _CalculateDataHash(state_frame.data);
			state_hashes.Append(StateHashFrame.new(_last_state_hashed_tick, state_hash));
			
			if(_logger)
			{
				_logger.WriteState(_last_state_hashed_tick, state_frame.data);
	
			}
		}
	}
	
	public InputBufferFrame _PredictMissingInput(InputBufferFrame input_frame, InputBufferFrame previous_frame)
	{  
		if(!input_frame.IsComplete(peers))
		{
			if(!previous_frame)
			{
				previous_frame = InputBufferFrame.new(-1)
			}
			var missing_peers  = input_frame.GetMissingPeers(peers);
			Dictionary missing_peers_predicted_input  = new Dictionary(){};
			Dictionary missing_peers_ticks_since_real_input  = new Dictionary(){};
			foreach(var peer_id in missing_peers)
			{
				missing_peers_predicted_input[peer_id] = new Dictionary(){};
				Peer peer = peers[peer_id];
				missing_peers_ticks_since_real_input[peer_id] = -1 if peer.last_remote_input_tick_received == 0 \
					else current_tick - peer.last_remote_input_tick_received
			}
			Array nodes = GetTree().GetNodesInGroup("network_sync");
			foreach(var node in nodes)
			{
				int node_master = node.GetNetworkMaster();
				if(!node_master in missing_peers)
				{
					continue;
				
				}
				var previous_input  = previous_frame.GetPlayerInput(node_master);
				var node_path_str  = GD.Str(node.GetPath());
				bool has_predict_network_input = Utils.HasInteropMethod(node, "_predict_remote_input");
				if(has_predict_network_input || previous_input.Has(node_path_str))
				{
					var previous_input_for_node = previous_input.Get(node_path_str, new Dictionary(){});
					int ticks_since_real_input = missing_peers_ticks_since_real_input[node_master];
					var predicted_input_for_node = has_predict_network_input ? Utils.CallInteropMethod(node, "_predict_remote_input", new Array(){previous_input_for_node, ticks_since_real_input}) : previous_input_for_node.Duplicate()
					if(predicted_input_for_node.Size() > 0)
					{
						missing_peers_predicted_input[node_master][node_path_str] = predicted_input_for_node;
			
					}
				}
			}
			foreach(var peer_id in missing_peers_predicted_input.Keys())
			{
				var predicted_input = missing_peers_predicted_input[peer_id];
				_CalculateDataHash(predicted_input);
				input_frame.players[peer_id] = InputForPlayer.new(predicted_input, true)
		
			}
		}
		return input_frame;
	
	}
	
	public bool _DoTick(bool is_rollback = false)
	{  
		var input_frame  = GetInputFrame(current_tick);
		var previous_frame  = GetInputFrame(current_tick - 1);
		
		System.Diagnostics.Debug.Assert(input_frame != null, "Input frame for current_tick is null");
		
		input_frame = _PredictMissingInput(input_frame, previous_frame);
		
		_CallNetworkProcess(input_frame);
		
		// If the game was stopped during the last network process, then we return
		// false here, to indicate that a full tick didn't complete && we need to
		// abort.
		if(!started)
		{
			return false;
		
		}
		_SaveCurrentState();
		
		// Debug check that states computed multiple times with complete inputs are the same
		if(debug_check_local_state_consistency && _last_state_hashed_tick >= current_tick)
		{
			_DebugCheckConsistentLocalState(state_buffer[-1], "Recomputed");
		
		}
		EmitSignal("tick_finished", is_rollback);
		return true;
	
	}
	
	public InputBufferFrame _GetOrCreateInputFrame(int tick)
	{  
		InputBufferFrame input_frame
		if(input_buffer.Size() == 0)
		{
			input_frame = InputBufferFrame.new(tick)
			input_buffer.Append(input_frame);
		}
		else if(tick > input_buffer[-1].tick)
		{
			var highest = input_buffer[-1].tick;
			while(highest < tick)
			{
				highest += 1;
				input_frame = InputBufferFrame.new(highest)
				input_buffer.Append(input_frame);
			}
		}
		else
		{
			input_frame = GetInputFrame(tick);
			if(input_frame == null)
			{
				return _HandleFatalError("Requested input Frame (%s) !found in buffer" % tick);
		
			}
		}
		return input_frame;
	
	}
	
	public bool _CleanupBuffers()
	{  
		// Clean-up the input send queue.
		var min_next_input_tick_requested = _CalculateMinimumNextInputTickRequested();
		while(_input_send_queue_start_tick < min_next_input_tick_requested)
		{
			_input_send_queue.PopFront();
			_input_send_queue_start_tick += 1;
		
		// Clean-up old state buffer frames. We need to keep one extra frame of state
		// because when we rollback, we need to load the state for the frame before
		// the first one we need to run again.
		}
		while(state_buffer.Size() > max_buffer_size + 1)
		{
			StateBufferFrame state_frame_to_retire = state_buffer[0];
			var input_frame = GetInputFrame(state_frame_to_retire.tick + 1);
			if(input_frame == null)
			{
				string message = "Attempting to retire state frame %s, but input frame %s is missing" % [state_frame_to_retire.tick, state_frame_to_retire.tick + 1]
				GD.PushWarning(message);
				if(_logger)
				{
					_logger.data["buffer_underrun_message"] = message;
				}
				return false;
			}
			if(!input_frame.IsComplete(peers))
			{
				Array missing = input_frame.GetMissingPeers(peers);
				string message = "Attempting to retire state frame %s, but input frame %s is still missing Input (missing Peer(s): %s)" % [state_frame_to_retire.tick, input_frame.tick, missing]
				GD.PushWarning(message);
				if(_logger)
				{
					_logger.data["buffer_underrun_message"] = message;
				}
				return false;
			
			}
			if(state_frame_to_retire.tick > _last_state_hashed_tick)
			{
				string message = "Unable to retire state frame %s, because we haven"t hashed it yet\" % state_frame_to_retire.tick
				GD.PushWarning(message);
				if(_logger)
				{
					_logger.data["buffer_underrun_message"] = message;
				}
				return false;
			
			}
			state_buffer.PopFront();
			_state_buffer_start_tick += 1;
			
			EmitSignal(\"tick_retired\", state_frame_to_retire.tick);
		
		// Clean-up old input buffer frames. Unlike state frames, we can have many
		// frames from the future if we are running behind. We don"t want having too
		// many future frames to end up discarding input for the current frame, so we
		// only count input frames before the current frame towards the buffer size.
		}
		while((current_tick - _input_buffer_start_tick) > max_buffer_size)
		{
			_input_buffer_start_tick += 1;
			input_buffer.PopFront();
		
		}
		while(state_hashes.Size() > (max_buffer_size * 2))
		{
			StateHashFrame state_hash_to_retire = state_hashes[0];
			if(!state_hash_to_retire.IsComplete(peers) && !mechanized)
			{
				Array missing = state_hash_to_retire.GetMissingPeers(peers);
				string message = "Attempting to retire state hash frame %s, but we"re still missing Hashes (missing Peer(s): %s)\" % [state_hash_to_retire.tick, missing]
				GD.PushWarning(message);
				if(_logger)
				{
					_logger.data["buffer_underrun_message"] = message;
				}
				return false;
			
			}
			if(state_hash_to_retire.mismatch)
			{
				_state_mismatch_count += 1;
			}
			else
			{
				_state_mismatch_count = 0;
			}
			if(_state_mismatch_count > max_state_mismatch_count)
			{
				_HandleFatalError(\"Fatal state mismatch\");
				return false;
			
			}
			_state_hashes_start_tick += 1;
			state_hashes.PopFront();
		
		}
		return true;
	
	}
	
	public InputBufferFrame GetInputFrame(int tick)
	{  
		if(tick < _input_buffer_start_tick)
		{
			return null;
		}
		var index = tick - _input_buffer_start_tick;
		if(index >= input_buffer.Size())
		{
			return null;
		}
		var input_frame = input_buffer[index];
		System.Diagnostics.Debug.Assert(input_frame.tick == tick, \"Input frame retreived from input buffer has mismatched tick number\");
		return input_frame;
	
	}
	
	public Dictionary GetLatestInputFromPeer(int peer_id)
	{  
		if(peers.Has(peer_id))
		{
			Peer peer = peers[peer_id];
			var input_frame = GetInputFrame(peer.last_remote_input_tick_received);
			if(input_frame)
			{
				return input_frame.GetPlayerInput(peer_id);
			}
		}
		return new Dictionary(){};
	
	}
	
	public Dictionary GetLatestInputForNode(Node node)
	{  
		return GetLatestInputFromPeerForPath(node.GetNetworkMaster(), GD.Str(node.GetPath()));
	
	}
	
	public Dictionary GetLatestInputFromPeerForPath(int peer_id, String path)
	{  
		return GetLatestInputFromPeer(peer_id).Get(path, new Dictionary(){});
	
	}
	
	public Dictionary GetCurrentInputForNode(Node node)
	{  
		return GetCurrentInputFromPeerForPath(node.GetNetworkMaster(), GD.Str(node.GetPath()));
	
	}
	
	public Dictionary GetCurrentInputFromPeerForPath(int peer_id, String path)
	{  
		var input_frame = GetInputFrame(current_tick);
		if(input_frame)
		{
			return input_frame.GetInputForPlayer(peer_id).Get(path, new Dictionary(){});
		}
		return new Dictionary(){};
	
	}
	
	public StateBufferFrame _GetStateFrame(int tick)
	{  
		if(tick < _state_buffer_start_tick)
		{
			return null;
		}
		var index = tick - _state_buffer_start_tick;
		if(index >= state_buffer.Size())
		{
			return null;
		}
		var state_frame = state_buffer[index];
		System.Diagnostics.Debug.Assert(state_frame.tick == tick, \"State frame retreived from state buffer has mismatched tick number\");
		return state_frame;
	
	}
	
	public StateHashFrame _GetStateHashFrame(int tick)
	{  
		if(tick < _state_hashes_start_tick)
		{
			return null;
		}
		var index = tick - _state_hashes_start_tick;
		if(index >= state_hashes.Size())
		{
			return null;
		}
		var state_hash_frame = state_hashes[index];
		System.Diagnostics.Debug.Assert(state_hash_frame.tick == tick, \"State hash frame retreived from state hashes has mismatched tick number\");
		return state_hash_frame;
	
	}
	
	public bool IsCurrentTickInputComplete()
	{  
		return current_tick <= _input_complete_tick;
	
	}
	
	public Array _GetInputMessagesFromSendQueueInRange(int first_index, int last_index, bool reverse = false)
	{  
		var indexes = !reverse ? GD.Range(first_index, last_index + 1) : GD.Range(last_index, first_index - 1, -1)
		
		Array all_messages  = new Array(){};
		Dictionary msg  = new Dictionary(){};
		foreach(var index in indexes)
		{
			msg[_input_send_queue_start_tick + index] = _input_send_queue[index];
			
			if(max_input_frames_per_message > 0 && msg.Size() == max_input_frames_per_message)
			{
				all_messages.Append(msg);
				msg = new Dictionary(){};
		
			}
		}
		if(msg.Size() > 0)
		{
			all_messages.Append(msg);
		
		}
		return all_messages;
	
	}
	
	public Array _GetInputMessagesFromSendQueueForPeer(Peer peer)
	{  
		var first_index  = peer.next_local_input_tick_requested - _input_send_queue_start_tick;
		var last_index  = _input_send_queue.Size() - 1;
		var max_messages  = (max_input_frames_per_message * max_messages_at_once);
		
		if((last_index + 1) - first_index <= max_messages)
		{
			return _GetInputMessagesFromSendQueueInRange(first_index, last_index, true);
		
		}
		var new_messages = (int)(Mathf.Ceil(max_messages_at_once / 2.0))
		var old_messages = (int)(Mathf.Floor(max_messages_at_once / 2.0))
		
		return _GetInputMessagesFromSendQueueInRange(last_index - (new_messages * max_input_frames_per_message) + 1, last_index, true) + \
			   _GetInputMessagesFromSendQueueInRange(first_index, first_index + (old_messages * max_input_frames_per_message) - 1);
	
	}
	
	public Dictionary _GetStateHashesForPeer(Peer peer)
	{  
		Dictionary ret  = new Dictionary(){};
		if(peer.next_local_hash_tick_requested >= _state_hashes_start_tick)
		{
			var index = peer.next_local_hash_tick_requested - _state_hashes_start_tick;
			while(index < state_hashes.Size())
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
		foreach(var peer in peers.Values())
		{
			// Number of frames we are predicting for this peer.
			peer.local_lag = (input_tick + 1) - peer.last_remote_input_tick_received;
			// Calculate the advantage the peer has over us.
			peer.RecordAdvantage(!force_calculate_advantage ? ticks_to_calculate_advantage : 0);
			
			if(_logger)
			{
				_logger.AddValue(\"peer_%s\" % peer.peer_id, new Dictionary(){
					local_lag = peer.local_lag,
					remote_lag = peer.remote_lag,
					advantage = peer.local_lag - peer.remote_lag,
					calculated_advantage = peer.calculated_advantage,
				});
	
			}
		}
	}
	
	public bool _CalculateSkipTicks()
	{  
		// Attempt to find the greatest advantage.
		float max_advantage
		foreach(var peer in peers.Values())
		{
			max_advantage = Mathf.Max(max_advantage, peer.calculated_advantage);
		
		}
		if(max_advantage >= 2.0 && skip_ticks == 0)
		{
			skip_ticks = (int)(max_advantage / 2)
			EmitSignal(\"skip_ticks_flagged\", skip_ticks);
			return true;
		
		}
		return false;
	
	}
	
	public int _CalculateMaxLocalLag()
	{  
		int max_lag  = 0;
		foreach(var peer in peers.Values())
		{
			max_lag = Mathf.Max(max_lag, peer.local_lag);
		}
		return max_lag;
	
	}
	
	public int _CalculateMinimumNextInputTickRequested()
	{  
		if(peers.Size() == 0)
		{
			return 1;
		}
		var peer_list  = peers.Values().Duplicate();
		int result = peer_list.PopFront().next_local_input_tick_requested;
		foreach(var peer in peer_list)
		{
			result = Mathf.Min(result, peer.next_local_input_tick_requested);
		}
		return result;
	
	}
	
	public void _SendInputMessagesToPeer(int peer_id)
	{  
		System.Diagnostics.Debug.Assert(peer_id != network_adaptor.GetNetworkUniqueId(), \"Cannot send input to ourselves\");
		var peer = peers[peer_id];
		
		var state_hashes = _GetStateHashesForPeer(peer);
		var input_messages = _GetInputMessagesFromSendQueueForPeer(peer);
		
		if(_logger)
		{
			_logger.data["messages_sent_to_peer_%s" % peer_id] = input_messages.Size();
		
		}
		foreach(var input in _GetInputMessagesFromSendQueueForPeer(peer))
		{
			Dictionary msg = new Dictionary(){
				MessageSerializer.InputMessageKey.NEXT_INPUT_TICK_REQUESTED: peer.last_remote_input_tick_received + 1,
				MessageSerializer.InputMessageKey.INPUT: input,
				MessageSerializer.InputMessageKey.NEXT_HASH_TICK_REQUESTED: peer.last_remote_hash_tick_received + 1,
				MessageSerializer.InputMessageKey.STATE_HASHES: state_hashes,
			};
			
			var bytes = message_serializer.SerializeMessage(msg);
			
			// See https://gafferongames.com/post/packet_fragmentation_and_reassembly/
			if(debug_message_bytes > 0)
			{
				if(bytes.Size() > debug_message_bytes)
				{
					GD.PushError(\"Sending message w/ size %s bytes\" % bytes.Size());
			
				}
			}
			if(_logger)
			{
				_logger.AddValue(\"messages_sent_to_peer_%s_size\" % peer_id, bytes.Size());
				_logger.IncrementValue(\"messages_sent_to_peer_%s_total_size\" % peer_id, bytes.Size());
				_logger.MergeArrayValue(\"input_ticks_sent_to_peer_%s\" % peer_id, input.Keys());
			
			//var ticks = msg[InputMessageKey.INPUT].Keys();
			//print (\"[%s] Sending ticks %s - %s\" % [current_tick, Mathf.Min(ticks[0], ticks[-1]), Mathf.Max(ticks[0], ticks[-1])])
			
			}
			network_adaptor.SendInputTick(peer_id, bytes);
	
		}
	}
	
	public void _SendInputMessagesToAllPeers()
	{  
		if(debug_skip_nth_message > 1)
		{
			_debug_skip_nth_message_counter += 1;
			if(_debug_skip_nth_message_counter >= debug_skip_nth_message)
			{
				GD.Print(\"[%s] Skipping message to simulate packet loss\" % current_tick);
				_debug_skip_nth_message_counter = 0;
				return;
		
			}
		}
		foreach(var peer_id in peers)
		{
			_SendInputMessagesToPeer(peer_id);
	
		}
	}
	
	public void _PhysicsProcess(float _delta)
	{  
		if(!started)
		{
			return;
		
		}
		if(_logger)
		{
			_logger.BeginTick(current_tick + 1);
			_logger.data["input_complete_tick"] = _input_complete_tick;
			_logger.data["state_complete_tick"] = _state_complete_tick;
		
		}
		var start_time  = OS.GetTicksUsec();
		
		// @todo Is there a way we can move this to _RemoteStart()?
		// Store an initial state before any ticks.
		if(current_tick == 0)
		{
			_SaveCurrentState();
			if(_logger)
			{
				_CalculateDataHash(state_buffer[0].data);
				_logger.WriteState(0, state_buffer[0].data);
		
		//####
		// STEP 1: PERFORM ANY ROLLBACKS, IF NECESSARY.
		//####
		
			}
		}
		if(mechanized)
		{
			rollback_ticks = mechanized_rollback_ticks;
		}
		else
		{
			if(debug_random_rollback_ticks > 0)
			{
				GD.Randomize();
				debug_rollback_ticks = GD.Randi() % debug_random_rollback_ticks;
			}
			if(debug_rollback_ticks > 0 && current_tick >= debug_rollback_ticks)
			{
				rollback_ticks = Mathf.Max(rollback_ticks, debug_rollback_ticks);
			
			// We need to reload the current tick since we did a partial rollback
			// to the previous tick in order to interpolate.
			}
			if(interpolation && current_tick > 0 && rollback_ticks == 0)
			{
				_CallLoadState(state_buffer[-1].data);
		
			}
		}
		if(rollback_ticks > 0)
		{
			if(_logger)
			{
				_logger.data["rollback_ticks"] = rollback_ticks;
				_logger.StartTiming("rollback");
			
			}
			var original_tick = current_tick;
			
			// Rollback our internal state.
			System.Diagnostics.Debug.Assert(rollback_ticks + 1 <= state_buffer.Size(), \"Not enough state in buffer to rollback requested number of frames\");
			if(rollback_ticks + 1 > state_buffer.Size())
			{
				_HandleFatalError(\"Not enough state in buffer to rollback %s frames\" % rollback_ticks);
				return;
			
			}
			_CallLoadState(state_buffer[-rollback_ticks - 1].data);
			
			current_tick -= rollback_ticks;
			
			if(debug_check_local_state_consistency)
			{
				// Save already computed states for better logging in case of discrepancy
				_debug_check_local_state_consistency_buffer = state_buffer.Slice(state_buffer.Size() - rollback_ticks - 1, state_buffer.Size() - 1);
				// Debug check that states computed multiple times with complete inputs are the same
				if(_last_state_hashed_tick >= current_tick)
				{
					var state  = StateBufferFrame.new(current_tick, _CallSaveState())
					_DebugCheckConsistentLocalState(state, \"Loaded\");
			
				}
			}
			state_buffer.Resize(state_buffer.Size() - rollback_ticks);
			
			// Invalidate sync ticks after this, they may be asked for again
			if(requested_input_complete_tick > 0 && current_tick < requested_input_complete_tick)
			{
				requested_input_complete_tick = 0;
			
			}
			EmitSignal(\"state_loaded\", rollback_ticks);
			
			_in_rollback = true;
			
			// Iterate forward until we"re at the same spot we left off.
			while(rollback_ticks > 0)
			{
				current_tick += 1;
				if(!_do_tick(true))
				{
					return;
				}
				rollback_ticks -= 1;
			}
			System.Diagnostics.Debug.Assert(current_tick == original_tick, "Rollback didn"t return to the original tick\");
			
			_in_rollback = false;
			
			if(_logger)
			{
				_logger.StopTiming("rollback");
		
		//####
		// STEP 2: SKIP TICKS, IF NECESSARY.
		//####
		
			}
		}
		if(!mechanized)
		{
			_RecordAdvantage();
			
			if(_ticks_spent_regaining_sync > 0)
			{
				_ticks_spent_regaining_sync += 1;
				if(max_ticks_to_regain_sync > 0 && _ticks_spent_regaining_sync > max_ticks_to_regain_sync)
				{
					_HandleFatalError(\"Unable to regain synchronization\");
					return;
				
				// Check again if we"re still getting input buffer underruns.
				}
				if(!_cleanup_buffers())
				{
					// This can happen if there's a fatal error in _CleanupBuffers().
					if(!started)
					{
						return;
					// Even when we're skipping ticks, still send input.
					}
					_SendInputMessagesToAllPeers();
					if(_logger)
					{
						_logger.SkipTick(Logger.SkipReason.INPUT_BUFFER_UNDERRUN, start_time);
					}
					return;
				
				// Check if our max lag is still greater than the min lag to regain sync.
				}
				if(min_lag_to_regain_sync > 0 && _CalculateMaxLocalLag() > min_lag_to_regain_sync)
				{
					//print ("REGAINING SYNC: wait for local lag to reduce")
					// Even when we're skipping ticks, still send input.
					_SendInputMessagesToAllPeers();
					if(_logger)
					{
						_logger.SkipTick(Logger.SkipReason.WAITING_TO_REGAIN_SYNC, start_time);
					}
					return;
				
				// If we've reach this point, that means we've regained sync!
				}
				_ticks_spent_regaining_sync = 0;
				EmitSignal("sync_regained");
				
				// We don't want to skip ticks through the normal mechanism, because
				// any skips that were previously calculated don't apply anymore.
				skip_ticks = 0;
			
			// Attempt to clean up buffers, but if we can't, that means we've lost sync.
			}
			else if(!_cleanup_buffers())
			{
				// This can happen if there's a fatal error in _CleanupBuffers().
				if(!started)
				{
					return;
				}
				EmitSignal("sync_lost");
				_ticks_spent_regaining_sync = 1;
				// Even when we're skipping ticks, still send input.
				_SendInputMessagesToAllPeers();
				if(_logger)
				{
					_logger.SkipTick(Logger.SkipReason.INPUT_BUFFER_UNDERRUN, start_time);
				}
				return;
			
			}
			if(skip_ticks > 0)
			{
				skip_ticks -= 1;
				if(skip_ticks == 0)
				{
					foreach(var peer in peers.Values())
					{
						peer.ClearAdvantage();
					}
				}
				else
				{
					// Even when we're skipping ticks, still send input.
					_SendInputMessagesToAllPeers();
					if(_logger)
					{
						_logger.SkipTick(Logger.SkipReason.ADVANTAGE_ADJUSTMENT, start_time);
					}
					return;
			
				}
			}
			if(_CalculateSkipTicks())
			{
				// This means we need to skip some ticks, so may as well start now!
				if(_logger)
				{
					_logger.SkipTick(Logger.SkipReason.ADVANTAGE_ADJUSTMENT, start_time);
				}
				return;
			}
		}
		else
		{
			_CleanupBuffers();
		
		//####
		// STEP 3: GATHER INPUT AND RUN CURRENT TICK
		//####
		
		}
		input_tick += 1;
		current_tick += 1;
		
		if(!mechanized)
		{
			var input_frame  = _GetOrCreateInputFrame(input_tick);
			// The underlying error would have already been reported in
			// _GetOrCreateInputFrame() so we can just return here.
			if(input_frame == null)
			{
				return;
			
			}
			if(_logger)
			{
				_logger.data["input_tick"] = input_tick;
			
			}
			var local_input = _CallGetLocalInput();
			_CalculateDataHash(local_input);
			input_frame.players[network_adaptor.GetNetworkUniqueId()] = InputForPlayer.new(local_input, false)
			
			// Only serialize && send input when we have real remote peers.
			if(peers.Size() > 0)
			{
				PoolByteArray serialized_input = message_serializer.SerializeInput(local_input);
				
				// check that the serialized then unserialized input matches the original 
				if(debug_check_message_serializer_roundtrip)
				{
					Dictionary unserialized_input = message_serializer.UnserializeInput(serialized_input);
					_CalculateDataHash(unserialized_input);
					if(local_input["GetNode("] != unserialized_input[")$"])
					{
						GD.PushError("The input is different after being serialized && unserialized \n Original: %s \n Unserialized: %s" % [OrderedDict2str(local_input), OrderedDict2str(unserialized_input)]);
					
					}
				}
				_input_send_queue.Append(serialized_input);
				System.Diagnostics.Debug.Assert(input_tick == _input_send_queue_start_tick + _input_send_queue.Size() - 1, "Input send queue ticks numbers are misaligned");
				_SendInputMessagesToAllPeers();
		
			}
		}
		if(current_tick > 0)
		{
			if(_logger)
			{
				_logger.StartTiming("current_tick");
			
			}
			if(!_do_tick())
			{
				return;
			
			}
			if(_logger)
			{
				_logger.StopTiming("current_tick");
			
			}
			if(interpolation)
			{
				// Capture the state data to interpolate between.
				Dictionary to_state = state_buffer[-1].data;
				Dictionary from_state = state_buffer[-2].data;
				_interpolation_state.Clear();
				foreach(var path in to_state)
				{
					if(from_state.Has(path))
					{
						_interpolation_state[path] = [from_state[path], to_state[path]]
				
				// Return to state from the previous frame, so we can interpolate
				// towards the state of the current frame.
					}
				}
				_CallLoadState(state_buffer[-2].data);
		
			}
		}
		_time_since_last_tick = 0.0;
		_ran_physics_process = true;
		_ticks_since_last_interpolation_frame += 1;
		
		var total_time_msecs = (float)(OS.GetTicksUsec() - start_time) / 1000.0;
		if(debug_physics_process_msecs > 0 && total_time_msecs > debug_physics_process_msecs)
		{
			GD.PushError("[%s] SyncManager._PhysicsProcess() took %.02fms" % [current_tick, total_time_msecs]);
		
		}
		if(_logger)
		{
			_logger.EndTick(start_time);
	
		}
	}
	
	public void _Process(float delta)
	{  
		if(!started)
		{
			return;
		
		}
		var start_time = OS.GetTicksUsec();
		
		// These are things that we want to run during "interpolation frames", in
		// order to slim down the normal frames. Or, if interpolation is disabled,
		// we need to run these always. If we haven't managed to run this for more
		// one tick, we make sure to sneak it in just in case.
		if(!interpolation || !_ran_physics_process || _ticks_since_last_interpolation_frame > 1)
		{
			if(_logger)
			{
				_logger.BeginInterpolationFrame(current_tick);
			
			}
			_time_since_last_tick += delta;
			
			// Don't interpolate if we are skipping ticks, || just ran physics process.
			if(interpolation && skip_ticks == 0 && !_ran_physics_process)
			{
				float weight = _time_since_last_tick / tick_time;
				if(weight > 1.0)
				{
					weight = 1.0;
				}
				_CallInterpolateState(weight);
			
			// If there are no other peers, then we'll never receive any new input,
			// so we need to update the _input_complete_tick elsewhere. Here's a fine
			// place to do it!
			}
			if(peers.Size() == 0)
			{
				_UpdateInputCompleteTick();
			
			}
			_UpdateStateHashes();
			
			if(interpolation)
			{
				EmitSignal("interpolation_frame");
			
			// Do this last to catch any data that came in late.
			}
			network_adaptor.Poll();
			
			if(_logger)
			{
				_logger.EndInterpolationFrame(start_time);
			
			// Clear counter, because we just did an interpolation frame.
			}
			_ticks_since_last_interpolation_frame = 0;
		
		// Clear flag so subsequent _Process() calls will know that they weren't
		// preceeded by _PhysicsProcess().
		}
		_ran_physics_process = false;
		
		var total_time_msecs = (float)(OS.GetTicksUsec() - start_time) / 1000.0;
		if(debug_process_msecs > 0 && total_time_msecs > debug_process_msecs)
		{
			GD.PushError("[%s] SyncManager._Process() took %.02fms" % [current_tick, total_time_msecs]);
	
		}
	}
	
	public Dictionary _CleanDataForHashing(Dictionary input)
	{  
		Dictionary cleaned  = new Dictionary(){};
		foreach(var path in input)
		{
			if(path == "$")
			{
				continue;
			}
			cleaned[path] = _CleanDataForHashingRecursive(input[path]);
		}
		return cleaned;
	
	}
	
	public Dictionary _CleanDataForHashingRecursive(Dictionary input)
	{  
		Dictionary cleaned  = new Dictionary(){};
		foreach(var key in input)
		{
			if((key is String && key.BeginsWith("_")) || (key is int && key < 0))
			{
				continue;
			}
			var value = input[key];
			if(value is Dictionary)
			{
				cleaned[key] = _CleanDataForHashingRecursive(value);
			}
			else
			{
				cleaned[key] = value;
			}
		}
		return cleaned;
	
	// Calculates the hash without any keys that start with '_' (if string)
	// || less than 0 (if integer) to allow some properties to !count when
	// comparing comparing data.
	//
	// This can be used for comparing Input (to prevent a difference betwen predicted
	// input && real input from causing a rollback) && State (for when a property
	// is only used for interpolation).
	}
	
	public int _CalculateDataHash(Dictionary input)
	{  
		var cleaned = _CleanDataForHashing(input);
		var serialized = hash_serializer.Serialize(cleaned);
		var serialized_hash = serialized.Hash();
		input["$"] = serialized_hash;
		return serialized_hash;
	
	}
	
	public void _OnReceivedInputTick(int peer_id, PoolByteArray serialized_msg)
	{  
		if(!started)
		{
			return;
		
		}
		var msg = message_serializer.UnserializeMessage(serialized_msg);
		
		Dictionary all_remote_input = msg[MessageSerializer.InputMessageKey.INPUT];
		var all_remote_ticks = all_remote_input.Keys();
		all_remote_ticks.Sort();
		
		var first_remote_tick = all_remote_ticks[0];
		var last_remote_tick = all_remote_ticks[-1];
		
		if(first_remote_tick >= input_tick + max_buffer_size)
		{
			// This either happens because we are really far Behind (but maybe, just
			// maybe could catch up) || we are receiving old ticks from a previous
			// round that hadn't yet arrived. Just discard the message && hope for
			// the best, but if we can't keep up, another one of the fail safes will
			// detect that we are out of sync.
			Print ("Discarding message from the future");
			// We return because we don't even want to do the accounting that happens
			// after integrating input, since the data in this message could be
			// totally Bunk (ie. if it's from a previous match).
			return;
		
		}
		if(_logger)
		{
			_logger.BeginInterframe();
		
		}
		Peer peer = peers[peer_id];
		
		// Only process if it contains ticks we haven't received yet.
		if(last_remote_tick > peer.last_remote_input_tick_received)
		{
			// Integrate the input we received into the input buffer.
			foreach(var remote_tick in all_remote_ticks)
			{
				// Skip ticks we already have.
				if(remote_tick <= peer.last_remote_input_tick_received)
				{
					continue;
				// This means the input frame has already been retired, which can only
				// happen if we already had all the input.
				}
				if(remote_tick < _input_buffer_start_tick)
				{
					continue;
				
				}
				var remote_input = message_serializer.UnserializeInput(all_remote_input[remote_tick]);
				
	//			GD.Print("------ UNSERIALIZE REMOTE INPUT IN SYNC MANAGER: " + GD.Str(GetTree().GetNetworkUniqueId()) + " --------")
	//			GD.Print(remote_input);
				
				var input_frame  = _GetOrCreateInputFrame(remote_tick);
				if(input_frame == null)
				{
					// _GetOrCreateInputFrame() will have already flagged the error,
					// so we can just return here.
					return;
				
				// If we already have non-predicted input for this peer, then skip it.
				}
				if(!input_frame.IsPlayerInputPredicted(peer_id))
				{
					continue;
				
				//print ("Received remote tick %s from %s" % [remote_tick, peer_id])
				}
				if(_logger)
				{
					_logger.AddValue("remote_ticks_received_from_%s" % peer_id, remote_tick);
				
				// If we received a tick in the past && we aren't already setup to
				// rollback earlier than that...
				}
				var tick_delta = current_tick - remote_tick;
				if(tick_delta >= 0 && rollback_ticks <= tick_delta)
				{
					// Grab our predicted input, && store the remote input.
					var local_input = input_frame.GetPlayerInput(peer_id);
					input_frame.players[peer_id] = InputForPlayer.new(remote_input, false)
					
					// Check if the remote input matches what we had predicted, if not,
					// flag that we need to rollback.
					if(local_input["GetNode("] != remote_input[")$"])
					{
						rollback_ticks = tick_delta + 1;
						EmitSignal("prediction_missed", remote_tick, peer_id, local_input, remote_input);
						EmitSignal("rollback_flagged", remote_tick);
					}
				}
				else
				{
					// Otherwise, just store it.
					input_frame.players[peer_id] = InputForPlayer.new(remote_input, false)
			
			// Find what the last remote tick we received was after filling these in.
				}
			}
			var index = (peer.last_remote_input_tick_received - _input_buffer_start_tick) + 1;
			while(index < input_buffer.Size() && !input_buffer[index].IsPlayerInputPredicted(peer_id))
			{
				peer.last_remote_input_tick_received += 1;
				index += 1;
			
			// Update _input_complete_tick for new input.
			}
			_UpdateInputCompleteTick();
		
		// Record the next frame the other peer needs.
		}
		peer.next_local_input_tick_requested = Mathf.Max(msg[MessageSerializer.InputMessageKey.NEXT_INPUT_TICK_REQUESTED], peer.next_local_input_tick_requested);
		
		// Number of frames the remote is predicting for us.
		peer.remote_lag = (peer.last_remote_input_tick_received + 1) - peer.next_local_input_tick_requested;
		
		// Process state hashes.
		var remote_state_hashes = msg[MessageSerializer.InputMessageKey.STATE_HASHES];
		foreach(var remote_tick in remote_state_hashes)
		{
			var state_hash_frame  = _GetStateHashFrame(remote_tick);
			if(state_hash_frame && !state_hash_frame.HasPeerHash(peer_id))
			{
				if(!state_hash_frame.RecordPeerHash(peer_id, remote_state_hashes[remote_tick]))
				{
					EmitSignal("remote_state_mismatch", remote_tick, peer_id, state_hash_frame.state_hash, remote_state_hashes[remote_tick]);
		
		// Find what the last remote state hash we received was after filling these in.
				}
			}
		}
		var index = (peer.last_remote_hash_tick_received - _state_hashes_start_tick) + 1;
		while(index < state_hashes.Size() && state_hashes[index].HasPeerHash(peer_id))
		{
			peer.last_remote_hash_tick_received += 1;
			index += 1;
		
		// Record the next state hash that the other peer needs.
		}
		peer.next_local_hash_tick_requested = Mathf.Max(msg[MessageSerializer.InputMessageKey.NEXT_HASH_TICK_REQUESTED], peer.next_local_hash_tick_requested);
	
	}
	
	public void ResetMechanizedData()
	{  
		mechanized_input_received.Clear();
		mechanized_rollback_ticks = 0;
	
	}
	
	public void _ProcessMechanizedInput()
	{  
		foreach(var peer_id in mechanized_input_received)
		{
			var peer_input = mechanized_input_received[peer_id];
			foreach(var tick in peer_input)
			{
				var input = peer_input[tick];
				var input_frame  = _GetOrCreateInputFrame((int)(tick));
				input_frame.players[(int)(peer_id)] = InputForPlayer.new(input, false)
	
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
	
	public Dictionary SortDictionaryKeys(Dictionary input)
	{  
		Dictionary output  = new Dictionary(){};
		
		var keys = input.Keys();
		keys.Sort();
		foreach(var key in keys)
		{
			output[key] = input[key];
		
		}
		return output;
	
	}
	
	public Node Spawn(String name, Node parent, PackedScene scene, Dictionary data = new Dictionary(){}, bool rename = true, String signal_name = "")
	{  
		if(!started)
		{
			GD.PushError("Refusing to spawn %s before SyncManager has started" % name);
			return null;
		
		}
		return _spawn_manager.Spawn(name, parent, scene, data, rename, signal_name);
	
	}
	
	public void Despawn(Node node)
	{  
		_spawn_manager.Despawn(node);
	
	}
	
	public void _OnSpawnManagerSceneSpawned(String name, Node spawned_node, PackedScene scene, Dictionary data)
	{  
		EmitSignal("scene_spawned", name, spawned_node, scene, data);
	
	}
	
	public void _OnSpawnManagerSceneDespawned(String name, Node node)
	{  
		EmitSignal("scene_despawned", name, node);
	
	}
	
	public bool IsInRollback()
	{  
		return _in_rollback;
	
	}
	
	public bool IsRespawning()
	{  
		return _spawn_manager.is_respawning;
	
	}
	
	public async void SetDefaultSoundBus(String bus)
	{  
		if(_sound_manager == null)
		{
			await ToSignal(this, "ready")
		}
		_sound_manager.default_bus = bus;
	
	}
	
	public void PlaySound(String identifier, AudioStream sound, Dictionary info = new Dictionary(){})
	{  
		_sound_manager.PlaySound(identifier, sound, info);
	
	}
	
	public bool EnsureCurrentTickInputComplete()
	{  
		if(IsCurrentTickInputComplete())
		{
			return true;
		}
		if(requested_input_complete_tick == 0 || requested_input_complete_tick > current_tick)
		{
			requested_input_complete_tick = current_tick;
		}
		return false;
	
	}
	
	public String OrderedDict2str(Dictionary dict)
	{  
		string ret  = "{";
		foreach(var i in dict.Size())
		{
			var key = dict.Keys()[i];
			var value = dict[key];
			var value_str  = value is Dictionary ? OrderedDict2str(value) : GD.Str(value)
			ret += "%s:%s" % [key, value_str]
			if(i != dict.Size() - 1)
			{
				ret += ", ";
			}
		}
		ret += "}";
		return ret;
	
	}
	
	public void _DebugCheckConsistentLocalState(StateBufferFrame state, message := "Loaded")
	{  
		var hashed_state  = _CalculateDataHash(state.data);
		var previously_hashed_frame  = _GetStateHashFrame(current_tick);
		var previous_state = _debug_check_local_state_consistency_buffer.PopFront();
		if(previously_hashed_frame && previously_hashed_frame.state_hash != hashed_state)
		{
			var comparer = new DebugStateComparer()
			comparer.FindMismatches(previous_state.data, state.data);
			GD.PushError("%s state is !consistent with saved state:\n %s" % [
				message,
				comparer.PrintMismatches(),
				]);
	
	
		}
	}
	
	
	
}