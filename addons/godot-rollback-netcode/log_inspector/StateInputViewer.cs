
using System;
using System.Collections.Generic;
using Fractural.GodotCodeGenerator.Attributes;
using Fractural.Utils;
using Godot;
using GDC = Godot.Collections;

namespace Fractural.RollbackNetcode
{
    [Tool]
    public partial class StateInputViewer : VBoxContainer
    {
        private const string JSON_INDENT = "    ";

        [OnReadyGet("HBoxContainer/TickNumber")]
        private SpinBox _tickNumberSpinBox;
        [OnReadyGet("HBoxContainer/StartButton")]
        private Button _startButton;
        [OnReadyGet("HBoxContainer/PreviousMismatchButton")]
        private Button _previousMismatchButton;
        [OnReadyGet("HBoxContainer/NextMismatchButton")]
        private Button _nextMismatchButton;
        [OnReadyGet("HBoxContainer/EndButton")]
        private Button _endButton;
        [OnReadyGet("GridContainer/InputPanel/InputDataTree")]
        private Tree _inputDataTree;
        [OnReadyGet("GridContainer/InputMismatchesPanel/InputMismatchesDataTree")]
        private Tree _inputMismatchesDataTree;
        [OnReadyGet("GridContainer/StatePanel/StateDataTree")]
        private Tree _stateDataTree;
        [OnReadyGet("GridContainer/StateMismatchesPanel/StateMismatchesDataTree")]
        private Tree _stateMismatchesDataTree;

        private LogData _logData;
        private ReplayServer _replayServer;
        private int _replayPeerId;

        [OnReady]
        public void RealReady()
        {
            foreach (var tree in new[] { _inputMismatchesDataTree, _stateMismatchesDataTree })
            {
                tree.SetColumnTitle(1, "Local");
                tree.SetColumnTitle(2, "Remote");
                tree.ColumnTitlesVisible = true;
            }

            _tickNumberSpinBox.Connect("value_changed", this, nameof(_OnTickNumberValueChanged));
            _startButton.Connect("pressed", this, nameof(_OnStartButtonPressed));
            _previousMismatchButton.Connect("pressed", this, nameof(_OnPreviousMismatchButtonPressed));
            _nextMismatchButton.Connect("pressed", this, nameof(_OnNextMismatchButtonPressed));
            _endButton.Connect("pressed", this, nameof(_OnEndButtonPressed));
        }

        public void Construct(LogData _log_data)
        {
            _logData = _log_data;
        }

        public void SetReplayServer(ReplayServer _replay_server)
        {
            _replayServer = _replay_server;
        }

        public void SetReplayPeerId(int _replay_peer_id)
        {
            _replayPeerId = _replay_peer_id;
        }

        public void RefreshFromLogData()
        {
            _tickNumberSpinBox.MaxValue = _logData.max_tick;
            _OnTickNumberValueChanged(_tickNumberSpinBox.Value);
        }

        public void RefreshReplay()
        {
            if (_logData.IsLoading())
            {
                return;

            }
            if (_replayServer != null && _replayServer.IsConnectedToGame())
            {
                int tick = (int)(_tickNumberSpinBox.Value);
                LogData.StateData state_frame = _logData.state.Get<LogData.StateData>(tick, null);
                if (state_frame != null)
                {
                    GDC.Dictionary state_data;

                    if (state_frame.mismatches.Contains(_replayPeerId))
                        state_data = state_frame.mismatches.Get<GDC.Dictionary>(_replayPeerId);
                    else
                        state_data = state_frame.state;

                    _replayServer.SendMessage(new GDC.Dictionary()
                    {
                        ["type"] = "load_state",
                        ["state"] = state_data,
                    });

                }
            }
        }

        public void Clear()
        {
            _tickNumberSpinBox.MaxValue = 0;
            _tickNumberSpinBox.Value = 0;
            _ClearTrees();
        }

        public void _ClearTrees()
        {
            _inputDataTree.Clear();
            _inputMismatchesDataTree.Clear();
            _stateDataTree.Clear();
            _stateMismatchesDataTree.Clear();
        }

        public void _OnTickNumberValueChanged(double value)
        {
            if (_logData.IsLoading())
                return;
            int tick = (int)(value);

            LogData.InputData input_frame = _logData.input.Get<LogData.InputData>(tick, null);
            LogData.StateData state_frame = _logData.state.Get<LogData.StateData>(tick, null);

            _ClearTrees();

            if (input_frame != null)
            {
                _CreateTreeItemsFromDictionary(_inputDataTree, _inputDataTree.CreateItem(), input_frame.input);
                _CreateTreeFromMismatches(_inputMismatchesDataTree, input_frame.input, input_frame.mismatches);
            }
            if (state_frame != null)
            {
                _CreateTreeItemsFromDictionary(_stateDataTree, _stateDataTree.CreateItem(), state_frame.state);
                _CreateTreeFromMismatches(_stateMismatchesDataTree, state_frame.state, state_frame.mismatches);
            }
            RefreshReplay();
        }

        public GDC.Dictionary _ConvertArrayToDictionary(GDC.Array a)
        {
            GDC.Dictionary d = new GDC.Dictionary() { };
            for (int i = 0; i < a.Count; i++)
                d[i] = a[i];
            return d;
        }

        public void _CreateTreeItemsFromDictionary(Tree tree, TreeItem parent_item, GDC.Dictionary data, int data_column = 1)
        {
            foreach (string key in data.Keys)
            {
                var value = data[key];

                var item = tree.CreateItem(parent_item);
                item.SetText(0, GD.Str(key));

                if (value is GDC.Dictionary valueDict)
                    _CreateTreeItemsFromDictionary(tree, item, valueDict);
                else if (value is GDC.Array valueArray)
                    _CreateTreeItemsFromDictionary(tree, item, _ConvertArrayToDictionary(valueArray));
                else
                    item.SetText(data_column, GD.Str(value));

                if (key is string && key.BeginsWith("/root/SyncManager/"))
                    item.Collapsed = true;
            }
        }

        // mistmatches = [tick: int]: mistmatch: GDC.Dictionary
        public void _CreateTreeFromMismatches(Tree tree, GDC.Dictionary data, GDC.Dictionary mismatches)
        {
            if (mismatches.Count == 0)
                return;

            var root = tree.CreateItem();
            foreach (var peer_id in mismatches.Keys)
            {
                var peer_data = mismatches.Get<GDC.Dictionary>(peer_id);

                var peer_item = tree.CreateItem(root);
                peer_item.SetText(0, $"Peer {peer_id}");

                var comparer = new DebugStateComparer();
                comparer.FindMismatches(data, peer_data);

                foreach (DebugStateComparer.Mismatch mismatch in comparer.mismatches)
                {
                    var mismatch_item = tree.CreateItem(peer_item);
                    mismatch_item.SetExpandRight(0, true);
                    mismatch_item.SetExpandRight(1, true);

                    TreeItem child = null;

                    switch (mismatch.type)
                    {
                        case DebugStateComparer.MismatchType.MISSING:
                            mismatch_item.SetText(0, $"[MISSING] {mismatch.path}");

                            if (mismatch.local_state is GDC.Dictionary localDict)
                            {
                                _CreateTreeItemsFromDictionary(tree, mismatch_item, localDict);
                            }
                            else if (mismatch.local_state is GDC.Array localArray)
                            {
                                _CreateTreeItemsFromDictionary(tree, mismatch_item, _ConvertArrayToDictionary(localArray));
                            }
                            else
                            {
                                child = tree.CreateItem(mismatch_item);
                                child.SetText(1, JSON.Print(mismatch.local_state, JSON_INDENT));
                            }
                            break;
                        case DebugStateComparer.MismatchType.EXTRA:
                            mismatch_item.SetText(0, $"[EXTRA] {mismatch.path}");

                            if (mismatch.remote_state is GDC.Dictionary remoteDict)
                            {
                                _CreateTreeItemsFromDictionary(tree, mismatch_item, remoteDict, 2);
                            }
                            else if (mismatch.remote_state is GDC.Array remoteArray)
                            {
                                _CreateTreeItemsFromDictionary(tree, mismatch_item, _ConvertArrayToDictionary(remoteArray), 2);
                            }
                            else
                            {
                                child = tree.CreateItem(mismatch_item);
                                child.SetText(2, JSON.Print(mismatch.remote_state, JSON_INDENT));
                            }
                            break;
                        case DebugStateComparer.MismatchType.REORDER:
                            mismatch_item.SetText(0, $"[REORDER] {mismatch.path}");

                            if (!(mismatch.local_state is GDC.Array reorderLocalArray && mismatch.remote_state is GDC.Array reorderRemoteArray))
                                return;

                            for (int i = 0; i < Mathf.Max(reorderLocalArray.Count, reorderRemoteArray.Count); i++)
                            {
                                var order_item = tree.CreateItem(mismatch_item);
                                if (i < reorderLocalArray.Count)
                                {
                                    order_item.SetText(1, reorderLocalArray[i].ToString());
                                }
                                if (i < reorderRemoteArray.Count)
                                {
                                    order_item.SetText(2, reorderRemoteArray[i].ToString());
                                }
                            }
                            break;
                        case DebugStateComparer.MismatchType.DIFFERENCE:
                            mismatch_item.SetText(0, $"[DIFF] {mismatch.path}");

                            child = tree.CreateItem(mismatch_item);
                            child.SetText(1, JSON.Print(mismatch.local_state, JSON_INDENT));
                            child.SetText(2, JSON.Print(mismatch.remote_state, JSON_INDENT));
                            break;
                    }
                }
            }
        }

        public void _OnPreviousMismatchButtonPressed()
        {
            if (_logData.IsLoading())
                return;
            var current_tick = (int)(_tickNumberSpinBox.Value);
            int previous_mismatch = -1;
            foreach (int mismatch_tick in _logData.mismatches)
            {
                if (mismatch_tick < current_tick)
                    previous_mismatch = mismatch_tick;
                else
                    break;
            }
            if (previous_mismatch != -1)
                _tickNumberSpinBox.Value = previous_mismatch;
        }

        public void _OnNextMismatchButtonPressed()
        {
            if (_logData.IsLoading())
                return;
            var current_tick = (int)(_tickNumberSpinBox.Value);
            int next_mismatch = -1;
            foreach (int mismatch_tick in _logData.mismatches)
            {
                if (mismatch_tick > current_tick)
                {
                    next_mismatch = mismatch_tick;
                    break;
                }
            }
            if (next_mismatch != -1)
                _tickNumberSpinBox.Value = next_mismatch;
        }

        public void _OnStartButtonPressed()
        {
            _tickNumberSpinBox.Value = 0;
        }

        public void _OnEndButtonPressed()
        {
            _tickNumberSpinBox.Value = _tickNumberSpinBox.MaxValue;
        }
    }
}