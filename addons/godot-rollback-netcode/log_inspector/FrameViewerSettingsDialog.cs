
using System;
using System.Collections.Generic;
using System.Linq;
using Fractural.GodotCodeGenerator.Attributes;
using Godot;
using GDC = Godot.Collections;
using Fractural.Utils;

namespace Fractural.RollbackNetcode
{
    [Tool]
    public partial class FrameViewerSettingsDialog : WindowDialog
    {
        public PackedScene TimeOffsetSettingPrefab = GD.Load<PackedScene>("res://addons/godot-rollback-netcode/log_inspector/FrameViewerTimeOffsetSetting.tscn");

        [OnReadyGet("MarginContainer/GridContainer/ShowNetworkArrows")]
        public CheckBox show_network_arrows_field;
        [OnReadyGet("MarginContainer/GridContainer/NetworkArrowsPeer1")]
        public OptionButton network_arrows_peer1_field;
        [OnReadyGet("MarginContainer/GridContainer/NetworkArrowsPeer2")]
        public OptionButton network_arrows_peer2_field;
        [OnReadyGet("MarginContainer/GridContainer/ShowRollbackTicks")]
        public CheckBox show_rollback_ticks_field;
        [OnReadyGet("MarginContainer/GridContainer/MaxRollbackTicks")]
        public LineEdit max_rollback_ticks_field;
        [OnReadyGet("MarginContainer/GridContainer/TimeOffsetContainer")]
        public VBoxContainer time_offset_container;

        public LogData log_data;
        public FrameDataGraph data_graph;
        public FrameDataGrid data_grid;

        public void SetupSettingsDialog(LogData _log_data, FrameDataGraph _data_graph, FrameDataGrid _data_grid)
        {
            log_data = _log_data;
            data_graph = _data_graph;
            data_grid = _data_grid;
            RefreshFromLogData();
        }

        public void RefreshFromLogData()
        {
            _RebuildPeerOptions(network_arrows_peer1_field);
            _RebuildPeerOptions(network_arrows_peer2_field);
            _RebuildPeerTimeOffsetFields();

            show_network_arrows_field.Pressed = data_graph.canvas.show_network_arrows;
            var network_arrow_peers = new List<int>(data_graph.canvas.network_arrow_peers.Cast<int>());
            network_arrow_peers.Sort();
            if (network_arrow_peers.Count > 0)
                network_arrows_peer1_field.Select(network_arrows_peer1_field.GetItemIndex(network_arrow_peers[0]));
            if (network_arrow_peers.Count > 1)
                network_arrows_peer2_field.Select(network_arrows_peer2_field.GetItemIndex(network_arrow_peers[1]));

            show_rollback_ticks_field.Pressed = data_graph.canvas.show_rollback_ticks;
            max_rollback_ticks_field.Text = GD.Str(data_graph.canvas.max_rollback_ticks);

        }

        public void _RebuildPeerOptions(OptionButton option_button)
        {
            var value = option_button.GetSelectedId();
            option_button.Clear();
            foreach (int peer_id in log_data.peer_ids)
                option_button.AddItem($"Peer {peer_id}", peer_id);
            if (option_button.GetSelectedId() != value)
                option_button.Select(option_button.GetItemIndex(value));
        }

        public void _RebuildPeerTimeOffsetFields()
        {
            // Remove all the old Fields (disconnect signals).
            foreach (FrameViewerTimeOffsetSetting child in time_offset_container.GetChildren())
            {
                child.TimeOffsetChanged -= _OnPeerTimeOffsetChanged;
                time_offset_container.RemoveChild(child);
                child.QueueFree();
            }
            // Re-create new fields && connect the signals.
            foreach (int peer_id in log_data.peer_ids)
            {
                var child = TimeOffsetSettingPrefab.Instance<FrameViewerTimeOffsetSetting>();
                child.Name = GD.Str(peer_id);
                time_offset_container.AddChild(child);
                child.SetupTimeOffsetSetting(peer_id, $"Peer {peer_id}", log_data.peer_time_offsets.Get<int>(peer_id));
                child.TimeOffsetChanged += _OnPeerTimeOffsetChanged;
            }
        }

        public void _OnPeerTimeOffsetChanged(int value, int peer_id)
        {
            log_data.SetPeerTimeOffset(peer_id, value);
        }

        public void UpdateNetworkArrows()
        {
            if (show_network_arrows_field.Pressed)
            {
                if (network_arrows_peer1_field.GetSelectedId() != network_arrows_peer2_field.GetSelectedId())
                {
                    data_graph.canvas.show_network_arrows = true;
                    data_graph.canvas.network_arrow_peers = new GDC.Array(){
                    network_arrows_peer1_field.GetSelectedId(),
                    network_arrows_peer2_field.GetSelectedId(),
                };
                    data_graph.canvas.Update();
                }
            }
            else
            {
                data_graph.canvas.show_network_arrows = false;
                data_graph.canvas.Update();
            }
        }

        public void _OnShowNetworkArrowsToggled(bool button_pressed)
        {
            UpdateNetworkArrows();
        }

        public void _OnNetworkArrowsPeer1ItemSelected(int index)
        {
            UpdateNetworkArrows();
        }

        public void _OnNetworkArrowsPeer2ItemSelected(int index)
        {
            UpdateNetworkArrows();
        }

        public void _OnShowRollbackTicksPressed()
        {
            data_graph.canvas.show_rollback_ticks = show_rollback_ticks_field.Pressed;
            data_graph.canvas.Update();
        }

        public void _OnMaxRollbackTicksTextChanged(string new_text)
        {
            var value = max_rollback_ticks_field.Text;
            if (value.IsValidInteger())
            {
                var value_int = value.ToInt();
                if (value_int > 0)
                {
                    data_graph.canvas.max_rollback_ticks = value_int;
                    data_graph.canvas.Update();
                }
            }
        }
    }
}