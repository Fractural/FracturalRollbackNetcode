
using System;
using Godot;
using Dictionary = Godot.Collections.Dictionary;
using Array = Godot.Collections.Array;


public class DebugOverlay : HBoxContainer
{
	 
	public const var PeerStatus = GD.Load("res://addons/godot-rollback-netcode/debugger/PeerStatus.tscn");
	
	public void _Ready()
	{  
		SyncManager.Connect("peer_removed", this, "_on_SyncManager_peer_removed");
	
	}
	
	public void _OnSyncManagerPeerRemoved(__TYPE peer_id)
	{  
		var peer_id_str = GD.Str(peer_id);
		var peer_status = GetNodeOrNull(peer_id_str);
		if(peer_status)
		{
			peer_status.QueueFree();
			RemoveChild(peer_status);
	
		}
	}
	
	public __TYPE _CreateOrGetPeerStatus(int peer_id)
	{  
		var peer_id_str = GD.Str(peer_id);
		var peer_status = GetNodeOrNull(peer_id_str);
		if(peer_status)
		{
			return GetNode(peer_id_str);
		
		}
		peer_status = PeerStatus.Instance();
		peer_status.name = peer_id_str;
		AddChild(peer_status);
		
		return peer_status;
	
	}
	
	public void UpdatePeer(SyncManager peer.Peer)
	{  
		var peer_status = _CreateOrGetPeerStatus(peer.peer_id);
		peer_status.UpdatePeer(peer);
	
	}
	
	public void AddMessage(int peer_id, String msg)
	{  
		var peer_status = _CreateOrGetPeerStatus(peer_id);
		peer_status.AddMessage(msg);
	
	
	}
	
	
	
}