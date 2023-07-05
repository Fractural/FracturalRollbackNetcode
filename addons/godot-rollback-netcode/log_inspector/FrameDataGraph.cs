
using System;
using Fractural.GodotCodeGenerator.Attributes;
using Godot;
using GDC = Godot.Collections;

namespace Fractural.RollbackNetcode
{
    [Tool]
    public partial class FrameDataGraph : VBoxContainer
    {
        [OnReadyGet("Canvas")]
        public FrameDataGraphCanvas canvas;
        [OnReadyGet("ScrollBar")]
        public HScrollBar scroll_bar;

        private int _cursor_time = -1;
        public int cursor_time
        {
            get => _cursor_time;
            set
            {
                if (_cursor_time != value)
                {
                    _cursor_time = value;
                    canvas.cursor_time = _cursor_time;
                    CursorTimeChanged?.Invoke(_cursor_time);
                }
            }
        }

        public LogData log_data;

        public delegate void CursorTimeChangedDelegate(int cursor_time);
        public event CursorTimeChangedDelegate CursorTimeChanged;

        public void SetLogData(LogData _log_data)
        {
            log_data = _log_data;
            canvas.SetLogData(log_data);
        }

        public void RefreshFromLogData()
        {
            if (log_data.IsLoading())
                return;
            scroll_bar.MaxValue = log_data.end_time - log_data.start_time;
            canvas.RefreshFromLogData();
        }

        public void _OnScrollBarValueChanged(float value)
        {
            canvas.start_time = (int)value;
        }

        public void _OnCanvasCursorTimeChanged(int _cursor_time)
        {
            cursor_time = _cursor_time;
        }

        public void _OnCanvasStartTimeChanged(int start_time)
        {
            scroll_bar.Value = start_time;
        }
    }
}