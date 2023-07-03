
using System;
using Godot;
using GDC = Godot.Collections;

namespace Fractural.RollbackNetcode
{
    [Tool]
    public class Plugin : EditorPlugin
    {
        public PackedScene LogInspectorPrefab = GD.Load<PackedScene>("res://addons/godot-rollback-netcode/log_inspector/LogInspector.tscn");

        public LogInspector log_inspector;

        public override void _EnterTree()
        {
            CustomProjectSettings.AddProjectSettings();

            //AddAutoloadSingleton("SyncManager", "res://addons/godot-rollback-netcode/SyncManager.gd");

            log_inspector = LogInspectorPrefab.Instance<LogInspector>();
            GetEditorInterface().GetBaseControl().AddChild(log_inspector);
            log_inspector.SetEditorInterface(GetEditorInterface());
            AddToolMenuItem("Log inspector...", this, nameof(OpenLogInspector));

            if (!ProjectSettings.HasSetting("input/sync_debug"))
            {
                var sync_debug = new InputEventKey();
                sync_debug.Scancode = (uint)KeyList.F11;

                ProjectSettings.SetSetting("input/sync_debug", new GDC.Dictionary()
                {
                    ["deadzone"] = 0.5f,
                    ["events"] = new GDC.Array(){
                        sync_debug,
                    },
                });

                // Cause the ProjectSettingsEditor to reload the input map from the
                // ProjectSettings.
                GetTree().Root.GetChild(0).PropagateNotification(EditorSettings.NotificationEditorSettingsChanged);
            }
        }

        public override void _ExitTree()
        {
            RemoveToolMenuItem("Log inspector...");
            if (log_inspector != null)
            {
                log_inspector.Free();
                log_inspector = null;
            }
        }

        public void OpenLogInspector(object userData)
        {
            log_inspector.PopupCenteredRatio();
        }
    }
}