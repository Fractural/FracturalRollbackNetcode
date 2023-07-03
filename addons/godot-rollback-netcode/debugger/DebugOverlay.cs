
using System;
using Godot;
using GDC = Godot.Collections;

namespace Fractural.RollbackNetcode
{
    public class DebugOverlay : HBoxContainer
    {
        public PackedScene PeerStatusPrefab = GD.Load<PackedScene>("res://addons/godot-rollback-netcode/debugger/PeerStatus.tscn");

        public void _Ready()
        {
            SyncManager.Global.Connect("peer_removed", this, "_on_SyncManager_peer_removed");
        }

        public void _OnSyncManagerPeerRemoved(int peer_id)
        {
            var peer_id_str = GD.Str(peer_id);
            var peer_status = GetNodeOrNull(peer_id_str);
            if (peer_status != null)
            {
                peer_status.QueueFree();
                RemoveChild(peer_status);
            }
        }

        public PeerStatus _CreateOrGetPeerStatus(int peer_id)
        {
            var peer_id_str = GD.Str(peer_id);
            var peer_status = GetNodeOrNull<PeerStatus>(peer_id_str);
            if (peer_status != null)
                return peer_status;
            peer_status = PeerStatusPrefab.Instance<PeerStatus>();
            peer_status.Name = peer_id_str;
            AddChild(peer_status);
            return peer_status;
        }

        public void UpdatePeer(SyncManager.Peer peer)
        {
            var peer_status = _CreateOrGetPeerStatus(peer.peer_id);
            peer_status.UpdatePeer(peer);
        }

        public void AddMessage(int peer_id, string msg)
        {
            var peer_status = _CreateOrGetPeerStatus(peer_id);
            peer_status.AddMessage(msg);
        }
    }
}