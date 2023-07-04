
using System;
using Fractural.Utils;
using Godot;
using GDC = Godot.Collections;

namespace Fractural.RollbackNetcode
{
    public class NetworkTimer : Node
    {
        [Export] public bool autostart = false;
        [Export] public bool one_shot = false;
        [Export] public int wait_ticks = 0;
        [Export] public bool hash_state = true;

        public int ticks_left = 0;

        public bool _running = false;

        [Signal] delegate void Timeout();

        public override void _Ready()
        {
            SyncManager.Global.SyncStopped += _OnSyncManagerSyncStopped;
            AddToGroup("network_sync"); ;
            if (autostart)
                Start();
        }

        public override void _Notification(int what)
        {
            if (what == NotificationPredelete)
                SyncManager.Global.SyncStopped -= _OnSyncManagerSyncStopped;
        }

        public bool IsStopped()
        {
            return !_running;

        }

        public void Start(int ticks = -1)
        {
            if (ticks > 0)
            {
                wait_ticks = ticks;
            }
            ticks_left = wait_ticks;
            _running = true;

        }

        public void Stop()
        {
            _running = false;
            ticks_left = 0;

        }

        public void _OnSyncManagerSyncStopped()
        {
            Stop();
        }

        public void _NetworkProcess(GDC.Dictionary _input)
        {
            if (!_running)
            {
                return;
            }
            if (ticks_left <= 0)
            {
                _running = false;
                return;

            }
            ticks_left -= 1;

            if (ticks_left == 0)
            {
                if (!one_shot)
                {
                    ticks_left = wait_ticks;
                }
                EmitSignal("timeout");

            }
        }

        public GDC.Dictionary _SaveState()
        {
            if (hash_state)
            {
                return new GDC.Dictionary()
                {
                    ["running"] = _running,
                    ["wait_ticks"] = wait_ticks,
                    ["ticks_left"] = ticks_left,
                };
            }
            else
            {
                return new GDC.Dictionary()
                {
                    ["_running"] = _running,
                    ["_wait_ticks"] = wait_ticks,
                    ["_ticks_left"] = ticks_left,
                };
            }
        }

        public void _LoadState(GDC.Dictionary state)
        {
            if (hash_state)
            {
                _running = state.Get<bool>("running");
                wait_ticks = state.Get<int>("wait_ticks");
                ticks_left = state.Get<int>("ticks_left");
            }
            else
            {
                _running = state.Get<bool>("_running");
                wait_ticks = state.Get<int>("_wait_ticks");
                ticks_left = state.Get<int>("_ticks_left");
            }
        }
    }
}