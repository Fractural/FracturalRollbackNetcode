
using System;
using Fractural.Utils;
using Godot;
using GDC = Godot.Collections;

namespace Fractural.RollbackNetcode
{
    public class SoundManager : Node
    {
        public const string DEFAULT_SOUND_BUS_SETTING = "network/rollback/sound_manager/default_sound_bus";

        public string default_bus = "Master";
        // [tick: int]: data: GDC.Dictionary
        public GDC.Dictionary ticks = new GDC.Dictionary() { };

        private SyncManager _syncManager;

        public override void _Ready()
        {
            if (ProjectSettings.HasSetting(DEFAULT_SOUND_BUS_SETTING))
                default_bus = ProjectSettingsUtils.GetSetting<string>(DEFAULT_SOUND_BUS_SETTING);
        }

        public void SetupSoundManager(SyncManager _sync_manager)
        {
            _syncManager = _sync_manager;
            _syncManager.Connect("tick_retired", this, "_on_SyncManager_tick_retired");
            _syncManager.Connect("sync_stopped", this, "_on_SyncManager_sync_stopped");

        }

        public void PlaySound(string identifier, AudioStream sound, GDC.Dictionary info = null)
        {
            if (info == null)
                info = new GDC.Dictionary() { };
            if (_syncManager.IsRespawning())
                return;
            if (ticks.Contains(_syncManager.current_tick))
            {
                if (ticks.Get<GDC.Dictionary>(_syncManager.current_tick).Contains(identifier))
                    return;
            }
            else
                ticks[_syncManager.current_tick] = new GDC.Dictionary() { };
            ticks.Get<GDC.Dictionary>(_syncManager.current_tick)[identifier] = true;

            Node node;
            if (info.Contains("position"))
            {
                var player2D = new AudioStreamPlayer2D();
                player2D.Stream = sound;
                player2D.VolumeDb = info.Get("volume_db", 0f);
                player2D.PitchScale = info.Get("pitch_scale", 1f);
                player2D.Bus = info.Get("bus", default_bus);
                node = player2D;
                AddChild(player2D);
                player2D.Play();
                player2D.GlobalPosition = info.Get<Vector2>("position");
            }
            else
            {
                var player = new AudioStreamPlayer();
                player.Stream = sound;
                player.VolumeDb = info.Get("volume_db", 0f);
                player.PitchScale = info.Get("pitch_scale", 1f);
                player.Bus = info.Get("bus", default_bus);
                node = player;
                AddChild(player);
                player.Play();
            }

            node.Connect("finished", this, nameof(_OnAudioFinished), new GDC.Array() { node });
        }

        public void _OnAudioFinished(Node node)
        {
            RemoveChild(node);
            node.QueueFree();
        }

        public void _OnSyncManagerTickRetired(int tick)
        {
            ticks.Remove(tick);
        }

        public void _OnSyncManagerSyncStopped()
        {
            ticks.Clear();
        }
    }
}