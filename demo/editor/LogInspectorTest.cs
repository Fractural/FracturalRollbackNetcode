using Fractural.RollbackNetcode;
using Godot;
using System;

namespace Demo.Editor
{
    public class LogInspectorTest : Node
    {
        private LogInspector _logInspector;

        public override void _Ready()
        {
            _logInspector = GetNode<LogInspector>("LogInspector");
            _logInspector.PopupCenteredRatio();
        }
    }
}