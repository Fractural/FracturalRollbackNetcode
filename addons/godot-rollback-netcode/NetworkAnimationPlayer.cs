
using System;
using Fractural.Utils;
using Godot;
using GDC = Godot.Collections;

namespace Fractural.RollbackNetcode
{
    public class NetworkAnimationPlayer : AnimationPlayer
    {
        [Export] public bool auto_reset = true;

        public override void _Ready()
        {
            MethodCallMode = AnimationPlayer.AnimationMethodCallMode.Immediate;
            PlaybackProcessMode = AnimationPlayer.AnimationProcessMode.Manual;
            AddToGroup("network_sync");
        }

        public void _NetworkProcess(GDC.Dictionary input)
        {
            if (IsPlaying())
                Advance(SyncManager.Global.tick_time);
        }

        public GDC.Dictionary _SaveState()
        {
            if (IsPlaying() && (!auto_reset || CurrentAnimation != "RESET"))
            {
                return new GDC.Dictionary()
                {
                    ["is_playing"] = true,
                    ["current_animation"] = CurrentAnimation,
                    ["current_position"] = CurrentAnimationPosition,
                    ["current_speed"] = PlaybackSpeed,
                };
            }
            else
            {
                return new GDC.Dictionary()
                {
                    ["is_playing"] = false,
                    ["current_animation"] = "",
                    ["current_position"] = 0f,
                    ["current_speed"] = 1,
                };
            }
        }

        public void _LoadState(GDC.Dictionary state)
        {
            if (state.Get<bool>("is_playing"))
            {
                if (!IsPlaying() || CurrentAnimation != state.Get<string>("current_animation"))
                    Play(state.Get<string>("current_animation"));
                Seek(state.Get<int>("current_position"), true);
                PlaybackSpeed = state.Get<float>("current_speed");
            }
            else if (IsPlaying())
            {
                if (auto_reset && HasAnimation("RESET"))
                    Play("RESET");
                else
                    Stop();
            }
        }
    }
}