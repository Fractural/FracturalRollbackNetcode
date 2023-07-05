
using System;
using Godot;
using GDC = Godot.Collections;

namespace Fractural.RollbackNetcode
{
    public class DummyNetworkAdaptor : NetworkAdaptor
    {
        private int _myPeerId = 1;

        public DummyNetworkAdaptor() { }
        public DummyNetworkAdaptor(int myPeerId)
        {
            _myPeerId = myPeerId;
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
            return _myPeerId == 1;
        }

        public override bool IsNetworkMasterForNode(Node node)
        {
            return node.GetNetworkMaster() == _myPeerId;
        }

        public override int GetNetworkUniqueId()
        {
            return _myPeerId;
        }
    }
}