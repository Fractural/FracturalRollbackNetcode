
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
        private PackedScene TimeOffsetSettingPrefab = GD.Load<PackedScene>("res://addons/godot-rollback-netcode/log_inspector/FrameViewerTimeOffsetSetting.tscn");

        [OnReadyGet("MarginContainer/GridContainer/ShowNetworkArrows")]
        private CheckBox _showNetworkArrowsCheckBox;
        [OnReadyGet("MarginContainer/GridContainer/NetworkArrowsPeer1")]
        private OptionButton _networkArrowsPeer1OptionButton;
        [OnReadyGet("MarginContainer/GridContainer/NetworkArrowsPeer2")]
        private OptionButton _networkArrowsPeer2OptionButton;
        [OnReadyGet("MarginContainer/GridContainer/ShowRollbackTicks")]
        private CheckBox _showRollbackTicksCheckBox;
        [OnReadyGet("MarginContainer/GridContainer/MaxRollbackTicks")]
        private LineEdit _maxRollbackTicksLineEdit;
        [OnReadyGet("MarginContainer/GridContainer/TimeOffsetContainer")]
        private VBoxContainer time_offset_container;

        private LogData _logData;
        private FrameDataGraph _dataGraph;
        private FrameDataGrid _dataGrid;

        [OnReady]
        public void RealReady()
        {
            _showNetworkArrowsCheckBox.Connect("toggled", this, nameof(_OnShowNetworkArrowsToggled));
            _networkArrowsPeer1OptionButton.Connect("item_selected", this, nameof(_OnNetworkArrowsPeer1ItemSelected));
            _networkArrowsPeer2OptionButton.Connect("item_selected", this, nameof(_OnNetworkArrowsPeer2ItemSelected));
            _showRollbackTicksCheckBox.Connect("pressed", this, nameof(_OnShowRollbackTicksPressed));
            _maxRollbackTicksLineEdit.Connect("text_changed", this, nameof(_OnMaxRollbackTicksTextChanged));
        }

        public void Construct(LogData logData, FrameDataGraph dataGraph, FrameDataGrid dataGrid)
        {
            _logData = logData;
            _dataGraph = dataGraph;
            _dataGrid = dataGrid;
            RefreshFromLogData();
        }

        public void RefreshFromLogData()
        {
            _RebuildPeerOptions(_networkArrowsPeer1OptionButton);
            _RebuildPeerOptions(_networkArrowsPeer2OptionButton);
            _RebuildPeerTimeOffsetFields();

            _showNetworkArrowsCheckBox.Pressed = _dataGraph.canvas.show_network_arrows;
            var network_arrow_peers = new List<int>(_dataGraph.canvas.network_arrow_peers.Cast<int>());
            network_arrow_peers.Sort();
            if (network_arrow_peers.Count > 0)
                _networkArrowsPeer1OptionButton.Select(_networkArrowsPeer1OptionButton.GetItemIndex(network_arrow_peers[0]));
            if (network_arrow_peers.Count > 1)
                _networkArrowsPeer2OptionButton.Select(_networkArrowsPeer2OptionButton.GetItemIndex(network_arrow_peers[1]));

            _showRollbackTicksCheckBox.Pressed = _dataGraph.canvas.show_rollback_ticks;
            _maxRollbackTicksLineEdit.Text = GD.Str(_dataGraph.canvas.max_rollback_ticks);
        }

        public void _RebuildPeerOptions(OptionButton option_button)
        {
            var value = option_button.GetSelectedId();
            option_button.Clear();
            foreach (int peer_id in _logData.peer_ids)
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
            foreach (int peer_id in _logData.peer_ids)
            {
                var child = TimeOffsetSettingPrefab.Instance<FrameViewerTimeOffsetSetting>();
                child.Name = GD.Str(peer_id);
                time_offset_container.AddChild(child);
                child.Construct(peer_id, $"Peer {peer_id}", _logData.peer_time_offsets.Get<int>(peer_id));
                child.TimeOffsetChanged += _OnPeerTimeOffsetChanged;
            }
        }

        public void _OnPeerTimeOffsetChanged(int value, int peer_id)
        {
            _logData.SetPeerTimeOffset(peer_id, value);
        }

        public void UpdateNetworkArrows()
        {
            if (_showNetworkArrowsCheckBox.Pressed)
            {
                if (_networkArrowsPeer1OptionButton.GetSelectedId() != _networkArrowsPeer2OptionButton.GetSelectedId())
                {
                    _dataGraph.canvas.show_network_arrows = true;
                    _dataGraph.canvas.network_arrow_peers = new GDC.Array(){
                    _networkArrowsPeer1OptionButton.GetSelectedId(),
                    _networkArrowsPeer2OptionButton.GetSelectedId(),
                };
                    _dataGraph.canvas.Update();
                }
            }
            else
            {
                _dataGraph.canvas.show_network_arrows = false;
                _dataGraph.canvas.Update();
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
            _dataGraph.canvas.show_rollback_ticks = _showRollbackTicksCheckBox.Pressed;
            _dataGraph.canvas.Update();
        }

        public void _OnMaxRollbackTicksTextChanged(string new_text)
        {
            var value = _maxRollbackTicksLineEdit.Text;
            if (value.IsValidInteger())
            {
                var value_int = value.ToInt();
                if (value_int > 0)
                {
                    _dataGraph.canvas.max_rollback_ticks = value_int;
                    _dataGraph.canvas.Update();
                }
            }
        }
    }
}