
using System;
using Godot;
using Dictionary = Godot.Collections.Dictionary;
using Array = Godot.Collections.Array;


public class NakamaWebRTCNetworkAdaptor : "res://addons/godot-rollback-netcode/RPCNetworkAdaptor.gd"
{
	 
	//onready var OnlineMatch = GetNode('/root/OnlineMatch');
	
	public const int DATA_CHANNEL_ID := 42
	
	// If buffer exceeds this value, skip sending Messages (except ping backs).
	public int max_buffered_amount  = 0;
	// The max skipped input ticks in a row.
	public int max_skipped_input_in_a_row  = 1;
	// The number of messages of history to check for duplicates.
	public int max_duplicate_history  = 10;
	// The number of milliseconds to keep a message in the duplicate history.
	public int max_duplicate_msecs  = 100;
	// The maximum packet lifetime for WebRTC to try to redeliver messages.
	public int max_packet_lifetime  = 66;
	
	public class MessageHash:
		int value
		int time
		
		public void _Init(int _value, int _time)
		{	  
			value = _value;
			time = _time;
	
		}
	
	public Dictionary _data_channels  = new Dictionary(){};
	public Dictionary _last_messages  = new Dictionary(){};
	
	public int _last_skipped_tick  = 0;
	public int _skipped_tick_count  = 0;
	
	public void AttachNetworkAdaptor(__TYPE sync_manager)
	{  
		if(OnlineMatch)
		{
			OnlineMatch.Connect("webrtc_peer_added", this, "_on_OnlineMatch_webrtc_peer_added");
			OnlineMatch.Connect("webrtc_peer_removed", this, "_on_OnlineMatch_webrtc_peer_removed");
			OnlineMatch.Connect("disconnected", this, "_on_OnlineMatch_disconnected");
		}
		else
		{
			GD.PushError("Can"t find OnlineMatch singleton that the NakamaWebRTCNetworkAdaptor depends on!\");
	
		}
	}
	
	public void DetachNetworkAdaptor(__TYPE sync_manager)
	{  
		if(OnlineMatch)
		{
			OnlineMatch.Disconnect(\"webrtc_peer_added\", this, "_on_OnlineMatch_webrtc_peer_added");
			OnlineMatch.Disconnect(\"webrtc_peer_removed\", this, "_on_OnlineMatch_webrtc_peer_removed");
			OnlineMatch.Disconnect(\"disconnected\", this, "_on_OnlineMatch_disconnected");
	
		}
	}
	
	public void StartNetworkAdaptor(__TYPE sync_manager)
	{  
		_last_messages.Clear();
		_last_skipped_tick = 0;
		_skipped_tick_count = 0;
	
	}
	
	public void StopNetworkAdaptor(__TYPE sync_manager)
	{  
	
	}
	
	public void _OnOnlineMatchWebrtcPeerAdded(WebRTCPeerConnection webrtc_peer, OnlineMatch player.Player)
	{  
		Print (\"Peer added -- trying to re-establish the data channel\");
		
		var peer_id  = player.peer_id;
		
		if(_data_channels.Has(peer_id))
		{
			_data_channels.Erase(peer_id);
		
		}
		var data_channel = webrtc_peer.CreateDataChannel("SyncManager", new Dictionary(){
			negotiated = true,
			id = DATA_CHANNEL_ID,
			maxPacketLifeTime = max_packet_lifetime,
			ordered = false,
		});
		// data_channel can be null if the peer has disconnected
		if(data_channel != null)
		{
			data_channel.write_mode = WebRTCDataChannel.WRITE_MODE_BINARY;
			_data_channels[peer_id] = data_channel;
			
			if(SyncManager._logger)
			{
				SyncManager._logger.data["nakama_webrtc_data_channel_created_for_peer_%s" % peer_id] = true;
	
			}
		}
	}
	
	public void _OnOnlineMatchWebrtcPeerRemoved(WebRTCPeerConnection webrtc_peer, OnlineMatch player.Player)
	{  
		var peer_id  = player.peer_id;
		if(_data_channels.Has(peer_id))
		{
			// Can this cause problems with re-establishing the connection?
			//_data_channels[peer_id].Close()
			_data_channels.Erase(peer_id);
	
		}
	}
	
	public void _OnOnlineMatchDisconnected()
	{  
		_data_channels.Clear();
	
	}
	
	public void SendInputTick(int peer_id, PoolByteArray msg)
	{  
		if(_data_channels.Has(peer_id) && _data_channels[peer_id].GetReadyState() == WebRTCDataChannel.STATE_OPEN)
		{
			WebRTCDataChannel data_channel = _data_channels[peer_id];
			
			// Skip sending if the data channel is over the max buffered amount.
			// Assuming the max_buffered_amount value is well tuned, this will kick
			// in when SCTP"s flow control turns on, && we want to wait until it
			// turns back off before sending any more data.
			if(max_buffered_amount > 0 && data_channel.GetBufferedAmount() > max_buffered_amount)
			{
				if(_last_skipped_tick == SyncManager.current_tick)
				{
					// We don't need to output the message multiple times per tick.
					return;
				
				}
				if(_last_skipped_tick == SyncManager.current_tick - 1)
				{
					_skipped_tick_count += 1;
				}
				else
				{
					_skipped_tick_count = 0;
				
				}
				if(_skipped_tick_count < max_skipped_input_in_a_row)
				{
					Print ("[%s] Skipping send because buffer is too Full (%s bytes)" % [SyncManager.current_tick, data_channel.GetBufferedAmount()]);
					if(SyncManager._logger)
					{
						SyncManager._logger.data["nakama_webrtc_send_skipped_to_peer_%s" % peer_id] = "Skipping send because buffer is too Full (%s bytes)" % data_channel.GetBufferedAmount();
					}
					_last_skipped_tick = SyncManager.current_tick;
					return;
				}
				else
				{
					_skipped_tick_count = 0;
			
				}
			}
			if(!_last_messages.Has(peer_id))
			{
				_last_messages[peer_id] = new Array(){};
			}
			var last_messages_for_peer = _last_messages[peer_id];
			
			// Clear out expired duplicate message records.
			var current_time = OS.GetTicksMsec();
			while(last_messages_for_peer.Size() > 0)
			{
				if(current_time - last_messages_for_peer[0].time >= max_duplicate_msecs)
				{
					//print ("[%s] Retiring duplicate from duplicate message history" % [SyncManager.current_tick])
					last_messages_for_peer.PopFront();
				}
				else
				{
					break;
			
			// Avoid sending duplicate messages. We'll let WebRTC's reliability
			// layer deal with making sure the message arrives, otherwise we can run
			// afoul of SCTP's flow control algorithm.
				}
			}
			var msg_hash_value = GD.Hash(msg);
			foreach(var msg_hash in last_messages_for_peer)
			{
				if(msg_hash.value == msg_hash_value)
				{
					Print ("[%s] Skipping duplicate message" % [SyncManager.current_tick]);
					if(SyncManager._logger)
					{
						SyncManager._logger.IncrementValue("nakama_webrtc_skipping_duplicate_messages_for_%s" % peer_id);
					}
					return;
			
				}
			}
			data_channel.PutPacket(msg);
			
			// Add message hash to duplicate history && push out old messages.
			//last_messages_for_peer.Append(MessageHash.new(msg_hash_value, current_time))
			_last_messages[peer_id].Append(MessageHash.new(msg_hash_value, current_time));
			while(last_messages_for_peer.Size() > max_duplicate_history)
			{
				last_messages_for_peer.PopFront();
	
			}
		}
	}
	
	public void Poll()
	{  
		foreach(var peer_id in _data_channels)
		{
			WebRTCDataChannel data_channel = _data_channels[peer_id];
			var data_channel_state = data_channel.GetReadyState();
			if(data_channel_state != WebRTCDataChannel.STATE_OPEN)
			{
				// Attempt to reconnect the data channel, if necessary.
				if(data_channel_state != WebRTCDataChannel.STATE_CONNECTING)
				{
					var player = OnlineMatch.GetPlayerByPeerId(peer_id);
					var webrtc_peer = OnlineMatch.GetWebrtcPeer(player.session_id);
					_OnOnlineMatchWebrtcPeerAdded(webrtc_peer, player);
				}
				continue;
			
			}
			data_channel.Poll();
			
			// Get all received messages.
			while(data_channel.GetAvailablePacketCount() > 0)
			{
				var msg = data_channel.GetPacket();
				EmitSignal("received_input_tick", peer_id, msg);
	
	
			}
		}
	}
	
	
	
}