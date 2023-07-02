
using System;
using Godot;
using GDC = Godot.Collections;


public class DummyNetworkAdaptor : "res://addons/godot-rollback-netcode/NetworkAdaptor.gd"
{

    public int my_peer_id


    public void _Init(int _my_peer_id = 1)
    {
        my_peer_id = _my_peer_id;

    }

    public void SendPing(int peer_id, GDC.Dictionary msg)
    {

    }

    public void SendPingBack(int peer_id, GDC.Dictionary msg)
    {

    }

    public void SendRemoteStart(int peer_id)
    {

    }

    public void SendRemoteStop(int peer_id)
    {

    }

    public void SendInputTick(int peer_id, PoolByteArray msg)
    {

    }

    public bool IsNetworkHost()
    {
        return my_peer_id == 1;

    }

    public bool IsNetworkMasterForNode(Node node)
    {
        return node.GetNetworkMaster() == my_peer_id;

    }

    public int GetNetworkUniqueId()
    {
        return my_peer_id;


    }



}