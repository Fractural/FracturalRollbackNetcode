
using System;
using Godot;
using GDC = Godot.Collections;

[Tool]
public class FrameDataGraph : VBoxContainer
{

    public const var LogData = GD.Load("res://addons/godot-rollback-netcode/log_inspector/LogData.gd");

    public onready var canvas = GetNode("Canvas");
    public onready var scroll_bar = GetNode("ScrollBar");

    public int cursor_time = -1 {set{SetCursorTime(value);
}}
	
	public LogData log_data

    [Signal] delegate void CursorTimeChanged(cursor_time);

public void SetLogData(LogData _log_data)
{
    log_data = _log_data;
    canvas.SetLogData(log_data);

}

public void RefreshFromLogData()
{
    if (log_data.IsLoading())
    {
        return;

    }
    scroll_bar.max_value = log_data.end_time - log_data.start_time;
    canvas.RefreshFromLogData();

}

public void SetCursorTime(int _cursor_time)
{
    if (cursor_time != _cursor_time)
    {
        cursor_time = _cursor_time;
        canvas.cursor_time = cursor_time;
        EmitSignal("cursor_time_changed", cursor_time);

    }
}

public void _OnScrollBarValueChanged(float value)
{
    canvas.start_time = (int)(value)


    }

public void _OnCanvasCursorTimeChanged(__TYPE _cursor_time)
{
    SetCursorTime(_cursor_time);

}

public void _OnCanvasStartTimeChanged(__TYPE start_time)
{
    scroll_bar.value = start_time;


}
	
	
	
}