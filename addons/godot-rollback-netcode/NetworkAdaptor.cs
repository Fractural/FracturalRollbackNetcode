
using System;
using Godot;
using GDC = Godot.Collections;

namespace Fractural.RollbackNetcode
{
    public abstract class NetworkAdaptor : Node
    {
        public class PingMessage
        {
            public int LocalTime { get; set; }
            public int RemoteTime { get; set; }
        }

        public delegate void ReceivedPingDelegate(int peer_id, PingMessage msg);
        public event ReceivedPingDelegate ReceivedPing;
        public delegate void ReceivedPingBackDelegate(int peer_id, PingMessage msg);
        public event ReceivedPingBackDelegate ReceivedPingBack;
        public event Action ReceivedRemoteStart;
        public event Action ReceivedRemoteStop;
        public delegate void ReceivedInputTickDelegate(int peer_id, byte[] msg);
        public event ReceivedInputTickDelegate ReceivedInputTick;

        public virtual void AttachNetworkAdaptor(SyncManager sync_manager) { }
        public virtual void DetachNetworkAdaptor(SyncManager sync_manager) { }
        public virtual void StartNetworkAdaptor(SyncManager sync_manager) { }
        public virtual void StopNetworkAdaptor(SyncManager sync_manager) { }

        public abstract void SendPing(int peer_id, PingMessage msg);
        public abstract void SendPingBack(int peer_id, PingMessage msg);
        public abstract void SendRemoteStart(int peer_id);
        public abstract void SendRemoteStop(int peer_id);
        public abstract void SendInputTick(int peer_id, byte[] msg);
        public abstract bool IsNetworkHost();
        public abstract bool IsNetworkMasterForNode(Node node);
        public abstract int GetNetworkUniqueId();
        public abstract void Poll();
    }
}