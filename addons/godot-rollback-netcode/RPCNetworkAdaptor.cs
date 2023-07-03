
using System;
using Godot;
using GDC = Godot.Collections;

namespace Fractural.RollbackNetcode
{
    public class RPCNetworkAdaptor : NetworkAdaptor
    {
        public override void SendPing(int peer_id, PingMessage msg)
        {
            RpcUnreliableId(peer_id, nameof(_RemotePing), msg.ToGDDict());
        }

        public override void SendPingBack(int peer_id, PingMessage msg)
        {
            RpcUnreliableId(peer_id, nameof(_RemotePingBack), msg);
        }

        public override void SendRemoteStart(int peer_id)
        {
            RpcId(peer_id, nameof(_RemoteStart));
        }

        public override void SendRemoteStop(int peer_id)
        {
            RpcId(peer_id, nameof(_RemoteStop));
        }

        public override void SendInputTick(int peer_id, byte[] msg)
        {
            RpcUnreliableId(peer_id, nameof(_Rit), msg);
        }

        public override bool IsNetworkHost()
        {
            return GetTree().IsNetworkServer();
        }

        public override bool IsNetworkMasterForNode(Node node)
        {
            return node.IsNetworkMaster();
        }

        public override int GetNetworkUniqueId()
        {
            return GetTree().GetNetworkUniqueId();
        }

        protected void _RemotePing(GDC.Dictionary pingMessageDict)
        {
            var peer_id = GetTree().GetRpcSenderId();

            var pingMessage = new PingMessage();
            pingMessage.FromGDDict(pingMessageDict);
            InvokeReceivedPing(peer_id, pingMessage);
        }

        protected void _RemotePingBack(GDC.Dictionary pingMessageDict)
        {
            var peer_id = GetTree().GetRpcSenderId();

            var pingMessage = new PingMessage();
            pingMessage.FromGDDict(pingMessageDict);
            InvokeReceivedPingBack(peer_id, pingMessage);
        }

        protected void _RemoteStart()
        {
            InvokeReceivedRemoteStart();
        }

        protected void _RemoteStop()
        {
            InvokeReceivedRemoteStop();
        }

        // _rit is short for _receive_input_tick. The method name ends up in each message
        // so, we're trying to keep it short.
        protected void _Rit(byte[] msg)
        {
            InvokeReceivedInputTick(GetTree().GetRpcSenderId(), msg);
        }
    }
}