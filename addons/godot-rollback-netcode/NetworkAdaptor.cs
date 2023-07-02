
using System;
using Godot;
using Dictionary = Godot.Collections.Dictionary;
using Array = Godot.Collections.Array;


public class NetworkAdaptor : Node
{
	 
	[Signal] delegate void ReceivedPing (peer_id, msg);
	[Signal] delegate void ReceivedPingBack (peer_id, msg);
	[Signal] delegate void ReceivedRemoteStart ();
	[Signal] delegate void ReceivedRemoteStop ();
	[Signal] delegate void ReceivedInputTick (peer_id, msg);
	
	public void AttachNetworkAdaptor(__TYPE sync_manager)
	{  
	
	}
	
	public void DetachNetworkAdaptor(__TYPE sync_manager)
	{  
	
	}
	
	public void StartNetworkAdaptor(__TYPE sync_manager)
	{  
	
	}
	
	public void StopNetworkAdaptor(__TYPE sync_manager)
	{  
	
	}
	
	public void SendPing(int peer_id, Dictionary msg)
	{  
		GD.PushError("UNIMPLEMENTED NetworkAdaptor ERROR.SendPing()");
	
	}
	
	public void SendPingBack(int peer_id, Dictionary msg)
	{  
		GD.PushError("UNIMPLEMENTED NetworkAdaptor ERROR.SendPingBack()");
	
	}
	
	public void SendRemoteStart(int peer_id)
	{  
		GD.PushError("UNIMPLEMENTED NetworkAdaptor ERROR.SendRemoteStart()");
	
	}
	
	public void SendRemoteStop(int peer_id)
	{  
		GD.PushError("UNIMPLEMENTED NetworkAdaptor ERROR.SendRemoteStop()");
	
	}
	
	public void SendInputTick(int peer_id, PoolByteArray msg)
	{  
		GD.PushError("UNIMPLEMENTED NetworkAdaptor ERROR.SendInputTick()");
	
	}
	
	public bool IsNetworkHost()
	{  
		GD.PushError("UNIMPLEMENTED NetworkAdaptor ERROR.IsNetworkHost()");
		return true;
	
	}
	
	public bool IsNetworkMasterForNode(Node node)
	{  
		GD.PushError("UNIMPLEMENTED NetworkAdaptor ERROR.IsNetworkMasterForNode()");
		return true;
	
	}
	
	public int GetNetworkUniqueId()
	{  
		GD.PushError("UNIMPLEMENTED NetworkAdaptor ERROR.GetNetworkUniqueId()");
		return 1;
	
	}
	
	public void Poll()
	{  
	
	
	}
	
	
	
}