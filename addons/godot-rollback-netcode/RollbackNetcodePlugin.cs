
using System;
using Fractural.Plugin;
using Godot;
using GDC = Godot.Collections;

namespace Fractural.RollbackNetcode
{
    [Tool]
    public class RollbackNetcodePlugin : ExtendedPlugin
    {
        public override string PluginName => "Fractural Rollback Netcode";

        private PackedScene _logInspectorPrefab = GD.Load<PackedScene>("res://addons/godot-rollback-netcode/log_inspector/LogInspector.tscn");
        private LogInspector _logInspector;

        protected override void Load()
        {
            CustomProjectSettings.AddProjectSettings();

            AddAutoloadSingleton("SyncManager", "res://addons/godot-rollback-netcode/SyncManager.cs");

            _logInspector = _logInspectorPrefab.Instance<LogInspector>();
            GetEditorInterface().GetBaseControl().AddChild(_logInspector);
            _logInspector.Construct(GetEditorInterface());
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

        protected override void Unload()
        {
            RemoveToolMenuItem("Log inspector...");
            if (_logInspector != null)
            {
                _logInspector.QueueFree();
                _logInspector = null;
            }
        }

        private void OpenLogInspector(object userData)
        {
            _logInspector.PopupCenteredRatio();
        }
    }
}