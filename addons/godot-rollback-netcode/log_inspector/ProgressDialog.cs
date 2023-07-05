
using System;
using Fractural.GodotCodeGenerator.Attributes;
using Godot;
using GDC = Godot.Collections;

namespace Fractural.RollbackNetcode
{
    [Tool]
    public partial class ProgressDialog : PopupDialog
    {
        [OnReadyGet("MarginContainer/VBoxContainer/Label")]
        public Label label;
        [OnReadyGet("MarginContainer/VBoxContainer/ProgressBar")]
        public ProgressBar progress_bar;

        public void SetLabel(string text)
        {
            label.Text = text;
        }

        public void UpdateProgress(ulong value, ulong max_value)
        {
            progress_bar.MaxValue = max_value;
            progress_bar.Value = value;
        }
    }
}