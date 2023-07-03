
using System;
using Godot;
using GDC = Godot.Collections;

[Tool]
public class ProgressDialog : PopupDialog
{

    public onready var label = GetNode("MarginContainer/VBoxContainer/Label");
    public onready var progress_bar = GetNode("MarginContainer/VBoxContainer/ProgressBar");

    public void SetLabel(string text)
    {
        label.text = text;

    }

    public void UpdateProgress(__TYPE value, __TYPE max_value)
    {
        progress_bar.max_value = max_value;
        progress_bar.value = value;


    }



}