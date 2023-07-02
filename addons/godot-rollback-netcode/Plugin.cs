
using System;
using Godot;
using Dictionary = Godot.Collections.Dictionary;
using Array = Godot.Collections.Array;

[Tool]
public class Plugin : EditorPlugin
{
	 
	public const var LogInspector = GD.Load("res://addons/godot-rollback-netcode/log_inspector/LogInspector.tscn");
	
	public __TYPE log_inspector;
	
	public void _EnterTree()
	{  
		var project_settings_node = GD.Load("res://addons/godot-rollback-netcode/ProjectSettings.gd").new()
		project_settings_node.AddProjectSettings();
		project_settings_node.Free();
		
		AddAutoloadSingleton("SyncManager", "res://addons/godot-rollback-netcode/SyncManager.gd");
		
		log_inspector = LogInspector.Instance();
		GetEditorInterface().GetBaseControl().AddChild(log_inspector);
		log_inspector.SetEditorInterface(GetEditorInterface());
		AddToolMenuItem("Log inspector...", this, "open_log_inspector");
		
		if(!ProjectSettings.HasSetting("input/sync_debug"))
		{
			var sync_debug = new InputEventKey()
			sync_debug.scancode = KEY_F11;
			
			ProjectSettings.SetSetting("input/sync_debug", new Dictionary(){
				deadzone = 0.5,
				events = new Array(){
					sync_debug,
				},
			});
			
			// Cause the ProjectSettingsEditor to reload the input map from the
			// ProjectSettings.
			GetTree().root.GetChild(0).PropagateNotification(EditorSettings.NOTIFICATION_EDITOR_SETTINGS_CHANGED);
	
		}
	}
	
	public void OpenLogInspector(__TYPE ud)
	{  
		log_inspector.PopupCenteredRatio();
	
	}
	
	public void _ExitTree()
	{  
		RemoveToolMenuItem("Log inspector...");
		if(log_inspector)
		{
			log_inspector.Free();
			log_inspector = null;
	
	
		}
	}
	
	
	
}