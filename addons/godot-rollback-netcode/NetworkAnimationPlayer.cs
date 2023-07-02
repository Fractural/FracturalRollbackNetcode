
using System;
using Godot;
using GDC = Godot.Collections;


public class NetworkAnimationPlayer : AnimationPlayer
{

    //class_name NetworkAnimationPlayer

    [Export] public bool auto_reset = true;

    public void _Ready()
    {
        method_call_mode = AnimationPlayer.ANIMATION_METHOD_CALL_IMMEDIATE;
        playback_process_mode = AnimationPlayer.ANIMATION_PROCESS_MANUAL;
        AddToGroup("network_sync");

    }

    public void _NetworkProcess(GDC.Dictionary input)
    {
        if (IsPlaying())
        {
            Advance(SyncManager.tick_time);

        }
    }

    public GDC.Dictionary _SaveState()
    {
        if (IsPlaying() && (!auto_reset || current_animation != "RESET"))
        {
            return new GDC.Dictionary()
            {
                is_playing = true,
                current_animation = current_animation,
                current_position = current_animation_position,
                current_speed = playback_speed,
            };
        }
        else
        {
            return new GDC.Dictionary()
            {
                is_playing = false,
                current_animation = "",
                current_position = 0.0,
                current_speed = 1;
        };

    }
}

public void _LoadState(GDC.Dictionary state)
{
    if (state["is_playing"])
    {
        if (!is_playing() || current_animation != state["current_animation"])
        {
            Play(state["current_animation"]);
        }
        Seek(state["current_position"], true);
        playback_speed = state["current_speed"];
    }
    else if (IsPlaying())
    {
        if (auto_reset && HasAnimation("RESET"))
        {
            Play("RESET");
        }
        else
        {
            Stop();


        }
    }
}
	
	
	
}