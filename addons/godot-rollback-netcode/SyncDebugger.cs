
using System;
using Godot;
using GDC = Godot.Collections;

namespace Fractural.RollbackNetcode
{

    public class SyncDebugger : Node
    {
        public SyncDebugger Global { get; private set; }

        public const var DebugOverlayPrefab = GD.Load("res://addons/godot-rollback-netcode/debugger/DebugOverlay.tscn");

        public const string JSON_INDENT = "    ";

        public bool print_previous_state = false;

        private CanvasLayer _canvas_layer;
        private DebugOverlay _debug_overlay;
        private bool _debug_pressed = false;
        private SyncManager _syncManager;

        public override void _Ready()
        {
            if (Global != null)
            {
                QueueFree();
                return;
            }
            Global = this;
            _syncManager = SyncManager.Global;

            _syncManager.RollbackFlagged += _OnSyncManagerRollbackFlagged;
            _syncManager.PredictionMissed += _OnSyncManagerPredictionMissed;
            _syncManager.SkipTicksFlagged += _OnSyncManagerSkipTicksFlagged;
            _syncManager.RemoteStateMismatch += _OnSyncManagerRemoteStateMismatch;
            _syncManager.PeerPingedBack += _OnSyncManagerPeerPingedBack;
            _syncManager.StateLoaded += _OnSyncManagerStateLoaded;
            _syncManager.TickFinished += _OnSyncManagerTickFinished;
        }

        public override void _Notification(int what)
        {
            if (what == NotificationPredelete)
            {
                if (Global == this)
                    Global = null;
                _syncManager.RollbackFlagged -= _OnSyncManagerRollbackFlagged;
                _syncManager.PredictionMissed -= _OnSyncManagerPredictionMissed;
                _syncManager.SkipTicksFlagged -= _OnSyncManagerSkipTicksFlagged;
                _syncManager.RemoteStateMismatch -= _OnSyncManagerRemoteStateMismatch;
                _syncManager.PeerPingedBack -= _OnSyncManagerPeerPingedBack;
                _syncManager.StateLoaded -= _OnSyncManagerStateLoaded;
                _syncManager.TickFinished -= _OnSyncManagerTickFinished;
            }
        }


        public void CreateDebugOverlay(DebugOverlay overlay_instance = null)
        {
            if (_debug_overlay != null)
            {
                _debug_overlay.QueueFree();
                _canvas_layer.RemoveChild(_debug_overlay);

            }
            if (overlay_instance == null)
                overlay_instance = DebugOverlayPrefab.Instance<DebugOverlay>();
            if (_canvas_layer == null)
            {
                _canvas_layer = new CanvasLayer();
                AddChild(_canvas_layer);
            }
            _debug_overlay = overlay_instance;
            _canvas_layer.AddChild(_debug_overlay);
        }

        public void ShowDebugOverlay(bool _visible = true)
        {
            if (_visible && _debug_overlay == null)
                CreateDebugOverlay();
            if (_debug_overlay != null)
                _debug_overlay.Visible = _visible;
        }

        public void HideDebugOverlay()
        {
            if (_debug_overlay != null)
                ShowDebugOverlay(false);
        }

        public bool IsDebugOverlayShown()
        {
            if (_debug_overlay != null)
                return _debug_overlay.Visible;
            return false;
        }

        public void _OnSyncManagerSkipTicksFlagged(int count)
        {
            GD.Print("-----");
            GD.Print($"Skipping {count} local Tick(s) to adjust for peer advantage");
        }

        public void _OnSyncManagerPredictionMissed(int tick, int peer_id, GDC.Dictionary local_input, GDC.Dictionary remote_input)
        {
            GD.Print("-----");
            GD.Print($"Prediction missed on tick {tick} for peer {peer_id}");
            GD.Print($"Received input: {_syncManager.hash_serializer.Serialize(remote_input)}");
            GD.Print($"Predicted input: {_syncManager.hash_serializer.Serialize(local_input)}");

            if (_debug_overlay != null)
                _debug_overlay.AddMessage(peer_id, "%Rollback s %s ticks" % [tick, _syncManager.rollback_ticks]);
        }

        public void _OnSyncManagerRollbackFlagged(int tick)
        {
            GD.Print("-----");
            GD.Print($"Rolling back to tick {tick} (rollback {_syncManager.rollback_ticks} Tick(s))");
        }

        public void _OnSyncManagerRemoteStateMismatch(int tick, int peer_id, int local_hash, int remote_hash)
        {
            GD.Print("-----");
            GD.Print($"On tick {tick}, remote void State ({remote_hash}) from {peer_id} doesn't match local State ({local_hash})");

            if (_debug_overlay != null)
                _debug_overlay.AddMessage(peer_id, $"{tick}: State mismatch");
        }

        public void _OnSyncManagerPeerPingedBack(SyncManager.Peer peer)
        {
            GD.Print("-----");
            GD.Print($"Peer {peer.peer_id}: RTT {peer.rtt} ms | local lag {peer.local_lag} | remote lag {peer.remote_lag} | advantage {peer.calculated_advantage}");
            if (_debug_overlay != null)
                _debug_overlay.UpdatePeer(peer);
        }

        public void _OnSyncManagerStateLoaded(int rollback_ticks)
        {

        }

        public void _OnSyncManagerTickFinished(bool is_rollback)
        {

        }

        public void _UnhandledInput(InputEvent @event)
        {
            var action_pressed = @event.IsActionPressed("sync_debug");
            if (action_pressed)
            {
                if (!_debug_pressed)
                {
                    _debug_pressed = true;
                    ShowDebugOverlay(!IsDebugOverlayShown());
                }
            }
            else
                _debug_pressed = false;
        }
    }
}