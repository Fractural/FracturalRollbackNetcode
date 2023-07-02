
using System;
using Godot;
using Dictionary = Godot.Collections.Dictionary;
using Array = Godot.Collections.Array;


public class RPCNetworkAdaptor : "res://addons/godot-rollback-netcode/NetworkAdaptor.gd"
{
	 
	public void SendPing(int peer_id, Dictionary msg)
	{  
		RpcUnreliableId(peer_id, "_remote_ping", msg);
	
	}
	
	public void _RemotePing(Dictionary msg)
	{  
		var peer_id = GetTree().GetRpcSenderId();
		EmitSignal("received_ping", peer_id, msg);
	
	}
	
	public void SendPingBack(int peer_id, Dictionary msg)
	{  
		RpcUnreliableId(peer_id, "_remote_ping_back", msg);
	
	}
	
	public void _RemotePingBack(Dictionary msg)
	{  
		var peer_id = GetTree().GetRpcSenderId();
		EmitSignal("received_ping_back", peer_id, msg);
	
	}
	
	public void SendRemoteStart(int peer_id)
	{  
		RpcId(peer_id, "_remote_start");
	
	}
	
	public void _RemoteStart()
	{  
		EmitSignal("received_remote_start");
	
	}
	
	public void SendRemoteStop(int peer_id)
	{  
		RpcId(peer_id, "_remote_stop");
	
	}
	
	public void _RemoteStop()
	{  
		EmitSignal("received_remote_stop");
	
	}
	
	public void SendInputTick(int peer_id, PoolByteArray msg)
	{  
		RpcUnreliableId(peer_id, "_rit", msg);
	
	}
	
	public bool IsNetworkHost()
	{  
		return GetTree().IsNetworkServer();
	
	}
	
	public bool IsNetworkMasterForNode(Node node)
	{  
		return node.IsNetworkMaster();
	
	}
	
	public int GetNetworkUniqueId()
	{  
		return GetTree().GetNetworkUniqueId();
	
	// _rit is short for _receive_input_tick. The method name ends up in each message
	// so, we're trying to keep it short.
	}
	
	public void _Rit(PoolByteArray msg)
	{  
		EmitSignal("received_input_tick", GetTree().GetRpcSenderId(), msg);
	
	
	
	}
	
	
	
}