
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
        private Label _peerLabel;
        [OnReadyGet("OffsetValue")]
        private SpinBox _offsetValueSpinBox;

        private int _peerId;

        [OnReady]
        public void RealReady()
        {
            _offsetValueSpinBox.Connect("value_cahnged", this, nameof(_OnOffsetValueValueChanged));
        }

        public void Construct(int peerId, string _label, int _value)
        {
            _peerId = peerId;
            _peerLabel.Text = _label;
            _offsetValueSpinBox.Value = _value;
        }

        public int GetTimeOffset()
        {
            return (int)_offsetValueSpinBox.Value;
        }

        public void _OnOffsetValueValueChanged(float value)
        {
            TimeOffsetChanged?.Invoke((int)_offsetValueSpinBox.Value, _peerId);
        }
    }
}