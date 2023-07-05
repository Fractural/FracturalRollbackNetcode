
using System;
using Fractural.GodotCodeGenerator.Attributes;
using Godot;
using GDC = Godot.Collections;

namespace Fractural.RollbackNetcode
{
    [Tool]
    public partial class FrameViewerTimeOffsetSetting : HBoxContainer
    {
        public delegate void TimeOffsetChangedDelegate(int value, int peerId);
        public event TimeOffsetChangedDelegate TimeOffsetChanged;

        [OnReadyGet("PeerLabel")]
        public Label peer_label;
        [OnReadyGet("OffsetValue")]
        public SpinBox offset_value_field;

        private int _peerId;

        public void SetupTimeOffsetSetting(int peerId, string _label, int _value)
        {
            _peerId = peerId;
            peer_label.Text = _label;
            offset_value_field.Value = _value;
        }

        public int GetTimeOffset()
        {
            return (int)offset_value_field.Value;
        }

        public void _OnOffsetValueValueChanged(float value)
        {
            TimeOffsetChanged?.Invoke((int)offset_value_field.Value, _peerId);
        }
    }
}