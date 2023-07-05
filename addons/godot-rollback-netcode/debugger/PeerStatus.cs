
using System;
using Fractural.GodotCodeGenerator.Attributes;
using Godot;
using GDC = Godot.Collections;

namespace Fractural.RollbackNetcode
{
    public partial class PeerStatus : Control
    {
        [OnReadyGet("VBoxContainer/GridContainer/PeerIdValue")]
        public Label peer_id_field;
        [OnReadyGet("VBoxContainer/GridContainer/RTTValue")]
        public Label rtt_field;
        [OnReadyGet("VBoxContainer/GridContainer/LocalLagValue")]
        public Label local_lag_field;
        [OnReadyGet("VBoxContainer/GridContainer/RemoteLagValue")]
        public Label remote_lag_field;
        [OnReadyGet("VBoxContainer/GridContainer/AdvantageValue")]
        public Label advantage_field;
        [OnReadyGet("VBoxContainer/MessagesValue")]
        public Label messages_field;

        public void UpdatePeer(SyncManager.Peer peer)
        {
            peer_id_field.Text = peer.peer_id.ToString();
            rtt_field.Text = peer.rtt.ToString() + " ms";
            local_lag_field.Text = peer.local_lag.ToString();
            remote_lag_field.Text = peer.remote_lag.ToString();
            advantage_field.Text = peer.calculated_advantage.ToString();
        }

        public void AddMessage(string msg)
        {
            messages_field.Text += "* " + msg + "\n";
        }
    }
}