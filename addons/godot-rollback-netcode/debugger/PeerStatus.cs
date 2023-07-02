
using System;
using Godot;
using GDC = Godot.Collections;


public class PeerStatus : Control
{

    public onready var peer_id_field = GetNode("VBoxContainer/GridContainer/PeerIdValue");
    public onready var rtt_field = GetNode("VBoxContainer/GridContainer/RTTValue");
    public onready var local_lag_field = GetNode("VBoxContainer/GridContainer/LocalLagValue");
    public onready var remote_lag_field = GetNode("VBoxContainer/GridContainer/RemoteLagValue");
    public onready var advantage_field = GetNode("VBoxContainer/GridContainer/AdvantageValue");
    public onready var messages_field = GetNode("VBoxContainer/MessagesValue");

    public void UpdatePeer(SyncManager peer.Peer)
    {
        peer_id_field.text = GD.Str(peer.peer_id);
        rtt_field.text = GD.Str(peer.rtt) + " ms";
        local_lag_field.text = GD.Str(peer.local_lag);
        remote_lag_field.text = GD.Str(peer.remote_lag);
        advantage_field.text = GD.Str(peer.calculated_advantage);

    }

    public void AddMessage(String msg)
    {
        messages_field.text += "* " + msg + "\n";


    }



}