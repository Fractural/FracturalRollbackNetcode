
using System;
using Fractural.Utils;
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

            public GDC.Dictionary ToGDDict()
            {
                return new GDC.Dictionary()
                {
                    [nameof(LocalTime)] = LocalTime,
                    [nameof(RemoteTime)] = RemoteTime,
                };
            }

            public void FromGDDict(GDC.Dictionary dict)
            {
                LocalTime = dict.Get<int>(nameof(LocalTime));
                RemoteTime = dict.Get<int>(nameof(RemoteTime));
            }
        }

        public delegate void ReceivedPingDelegate(int peerId, PingMessage msg);
        public event ReceivedPingDelegate ReceivedPing;
        public delegate void ReceivedPingBackDelegate(int peerId, PingMessage msg);
        public event ReceivedPingBackDelegate ReceivedPingBack;
        public event Action ReceivedRemoteStart;
        public event Action ReceivedRemoteStop;
        public delegate void ReceivedInputTickDelegate(int peerId, byte[] msg);
        public event ReceivedInputTickDelegate ReceivedInputTick;

        protected void InvokeReceivedPing(int peerId, PingMessage msg) => ReceivedPing?.Invoke(peerId, msg);
        protected void InvokeReceivedPingBack(int peerId, PingMessage msg) => ReceivedPingBack?.Invoke(peerId, msg);
        protected void InvokeReceivedRemoteStart() => ReceivedRemoteStart?.Invoke();
        protected void InvokeReceivedRemoteStop() => ReceivedRemoteStop?.Invoke();
        protected void InvokeReceivedInputTick(int peerId, byte[] msg) => ReceivedInputTick?.Invoke(peerId, msg);

        public virtual void AttachNetworkAdaptor(SyncManager sync_manager) { }
        public virtual void DetachNetworkAdaptor(SyncManager sync_manager) { }
        public virtual void StartNetworkAdaptor(SyncManager sync_manager) { }
        public virtual void StopNetworkAdaptor(SyncManager sync_manager) { }
        public virtual void Poll() { }

        public abstract void SendPing(int peer_id, PingMessage msg);
        public abstract void SendPingBack(int peer_id, PingMessage msg);
        public abstract void SendRemoteStart(int peer_id);
        public abstract void SendRemoteStop(int peer_id);
        public abstract void SendInputTick(int peer_id, byte[] msg);
        public abstract bool IsNetworkHost();
        public abstract bool IsNetworkMasterForNode(Node node);
        public abstract int GetNetworkUniqueId();
    }
}