
using System;
using Godot;
using Dictionary = Godot.Collections.Dictionary;
using Array = Godot.Collections.Array;

[Tool]
public class ProgressDialog : PopupDialog
{
	 
	public onready var label = GetNode("MarginContainer/VBoxContainer/Label");
	public onready var progress_bar = GetNode("MarginContainer/VBoxContainer/ProgressBar");
	
	public void SetLabel(String text)
	{  
		label.text = text;
	
	}
	
	public void UpdateProgress(__TYPE value, __TYPE max_value)
	{  
		progress_bar.max_value = max_value;
		progress_bar.value = value;
	
	
	}
	
	
	
}