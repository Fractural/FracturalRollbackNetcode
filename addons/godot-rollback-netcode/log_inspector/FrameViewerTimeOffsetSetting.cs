
using System;
using Godot;
using GDC = Godot.Collections;

[Tool]
public class FrameViewerTimeOffsetSetting : HBoxContainer
{

    public onready var peer_label = GetNode("PeerLabel");
    public onready var offset_value_field = GetNode("OffsetValue");

    [Signal] delegate void TimeOffsetChanged(value);

    public void SetupTimeOffsetSetting(string _label, int _value)
    {
        peer_label.text = _label;
        offset_value_field.value = _value;

    }

    public int GetTimeOffset()
    {
        return offset_value_field.value;

    }

    public void _OnOffsetValueValueChanged(float value)
    {
        EmitSignal("time_offset_changed", (int)(offset_value_field.value));


    }



}