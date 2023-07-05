
using System;
using Godot;
using GDC = Godot.Collections;

namespace Fractural.RollbackNetcode
{
    public static class CustomProjectSettings
    {
        public static int _property_order = 1000;

        public static void _AddProjectSetting(string name, Variant.Type type, object defaultValue, PropertyHint hint = PropertyHint.None, string hint_string = null)
        {
            if (!ProjectSettings.HasSetting(name))
            {
                ProjectSettings.SetSetting(name, defaultValue);

            }
            ProjectSettings.SetInitialValue(name, defaultValue);
            ProjectSettings.SetOrder(name, _property_order);

            _property_order += 1;

            GDC.Dictionary info = new GDC.Dictionary()
            {
                ["name"] = name,
                ["type"] = type,
            };
            if (hint != PropertyHint.None)
            {
                info["hint"] = hint;
            }
            if (hint_string != null)
            {
                info["hint_string"] = hint_string;
            }
            ProjectSettings.AddPropertyInfo(info);
        }

        public static void AddProjectSettings()
        {
            _AddProjectSetting("network/rollback/max_buffer_size", Variant.Type.Int, 20, PropertyHint.Range, "1, 60");
            _AddProjectSetting("network/rollback/ticks_to_calculate_advantage", Variant.Type.Int, 60, PropertyHint.Range, "1, 600");
            _AddProjectSetting("network/rollback/input_delay", Variant.Type.Int, 2, PropertyHint.Range, "0, 10");
            _AddProjectSetting("network/rollback/ping_frequency", Variant.Type.Real, 1.0, PropertyHint.Range, "0.01, 5.0");
            _AddProjectSetting("network/rollback/interpolation", Variant.Type.Bool, false);

            _AddProjectSetting("network/rollback/limits/max_input_frames_per_message", Variant.Type.Int, 5, PropertyHint.Range, "0, 60");
            _AddProjectSetting("network/rollback/limits/max_messages_at_once", Variant.Type.Int, 2, PropertyHint.Range, "0, 10");
            _AddProjectSetting("network/rollback/limits/max_ticks_to_regain_sync", Variant.Type.Int, 300, PropertyHint.Range, "0, 600");
            _AddProjectSetting("network/rollback/limits/min_lag_to_regain_sync", Variant.Type.Int, 5, PropertyHint.Range, "0, 60");
            _AddProjectSetting("network/rollback/limits/max_state_mismatch_count", Variant.Type.Int, 10, PropertyHint.Range, "0, 60");

            _AddProjectSetting("network/rollback/spawn_manager/reuse_despawned_nodes", Variant.Type.Bool, false);
            _AddProjectSetting("network/rollback/sound_manager/default_sound_bus", Variant.Type.String, "Master");

            _AddProjectSetting("network/rollback/classes/network_adaptor", Variant.Type.String, "", PropertyHint.File, "*.gd,*.cs");
            _AddProjectSetting("network/rollback/classes/message_serializer", Variant.Type.String, "", PropertyHint.File, "*.gd,*.cs");
            _AddProjectSetting("network/rollback/classes/hash_serializer", Variant.Type.String, "", PropertyHint.File, "*.gd,*.cs");

            _AddProjectSetting("network/rollback/debug/rollback_ticks", Variant.Type.Int, 0, PropertyHint.Range, "0, 60");
            _AddProjectSetting("network/rollback/debug/random_rollback_ticks", Variant.Type.Int, 0, PropertyHint.Range, "0, 60");
            _AddProjectSetting("network/rollback/debug/message_bytes", Variant.Type.Int, 0, PropertyHint.Range, "0, 2048");
            _AddProjectSetting("network/rollback/debug/skip_nth_message", Variant.Type.Int, 0, PropertyHint.Range, "0, 60");
            _AddProjectSetting("network/rollback/debug/physics_process_msecs", Variant.Type.Real, 10.0, PropertyHint.Range, "0.0, 60.0");
            _AddProjectSetting("network/rollback/debug/process_msecs", Variant.Type.Real, 10.0, PropertyHint.Range, "0.0, 60.0");
            _AddProjectSetting("network/rollback/debug/check_message_serializer_roundtrip", Variant.Type.Bool, false);
            _AddProjectSetting("network/rollback/debug/check_local_state_consistency", Variant.Type.Bool, false);

            _AddProjectSetting("network/rollback/log_inspector/replay_match_scene_path", Variant.Type.String, "", PropertyHint.File, "*.tscn,*.scn");
            _AddProjectSetting("network/rollback/log_inspector/replay_match_scene_method", Variant.Type.String, "setup_match_for_replay");
            _AddProjectSetting("network/rollback/log_inspector/replay_arguments", Variant.Type.String, "replay");
            _AddProjectSetting("network/rollback/log_inspector/replay_port", Variant.Type.Int, 49111);
        }
    }
}