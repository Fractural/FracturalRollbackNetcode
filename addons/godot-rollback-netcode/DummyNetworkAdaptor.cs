
using System;
using Godot;
using GDC = Godot.Collections;

namespace Fractural.RollbackNetcode
{
    public class DummyNetworkAdaptor : NetworkAdaptor
    {
        public int my_peer_id;

        public DummyNetworkAdaptor() { }
        public DummyNetworkAdaptor(int _my_peer_id = 1)
        {
            my_peer_id = _my_peer_id;
        }

        public override void SendPing(int peer_id, PingMessage msg)
        {

        }

        public override void SendPingBack(int peer_id, PingMessage msg)
        {

        }

        public override void SendRemoteStart(int peer_id)
        {

        }

        public override void SendRemoteStop(int peer_id)
        {

        }

        public override void SendInputTick(int peer_id, byte[] msg)
        {

        }

        public override bool IsNetworkHost()
        {
            return my_peer_id == 1;
        }

        public override bool IsNetworkMasterForNode(Node node)
        {
            return node.GetNetworkMaster() == my_peer_id;
        }

        public override int GetNetworkUniqueId()
        {
            return my_peer_id;
        }
    }
}