
using System;
using Godot;
using GDC = Godot.Collections;
using Fractural.Utils;
using System.Collections.Generic;

namespace Fractural.RollbackNetcode
{
    public class DebugStateComparer
    {
        public const string JSON_INDENT = "    ";

        public enum MismatchType
        {
            MISSING,
            EXTRA,
            REORDER,
            DIFFERENCE,
        }

        public class Mismatch
        {
            public MismatchType type;
            public string path;

            public object local_state;
            public object remote_state;

            public Mismatch(MismatchType _type, string _path, object _local_state, object _remote_state)
            {
                type = _type;
                path = _path;
                local_state = _local_state;
                remote_state = _remote_state;

            }
        }

        public GDC.Array mismatches = new GDC.Array() { };

        public void FindMismatches(GDC.Dictionary local_state, GDC.Dictionary remote_state)
        {
            _FindMismatchesRecursive(
                _CleanUpState(local_state),
                _CleanUpState(remote_state));
        }

        public GDC.Dictionary _CleanUpState(GDC.Dictionary state)
        {
            state = state.Duplicate(true);

            // Remove hash.
            state.Remove("$");

            // Remove any keys that are ignored in the hash.
            foreach (var node_path in state)
            {
                // I think this happens when there's an error reading a GDC.Dictionary.
                if (node_path == null)
                    state.Remove(null);

                var dict = state.Get<GDC.Dictionary>(node_path);
                foreach (string key in dict.Keys)
                {
                    if (key is string && key.BeginsWith("_"))
                        dict.Remove(key);
                }
            }
            return state;
        }

        public void _FindMismatchesRecursive(GDC.Dictionary local_state, GDC.Dictionary remote_state, GDC.Array path = null)
        {
            if (path == null)
                path = new GDC.Array() { };
            bool missing_or_extra = false;

            foreach (string key in local_state)
            {
                if (!remote_state.Contains(key))
                {
                    missing_or_extra = true;
                    mismatches.Add(new Mismatch(
                        MismatchType.MISSING,
                        _GetDiffPathString(path, key),
                        local_state[key],
                        null
                    ));

                }
            }
            foreach (string key in remote_state)
            {
                if (!local_state.Contains(key))
                {
                    missing_or_extra = true;
                    mismatches.Add(new Mismatch(
                        MismatchType.EXTRA,
                        _GetDiffPathString(path, key),
                        null,
                        remote_state[key]
                    ));
                }
            }
            if (!missing_or_extra)
            {
                if (local_state.Keys != remote_state.Keys)
                {
                    mismatches.Add(new Mismatch(
                        MismatchType.REORDER,
                        _GetDiffPathString(path, "KEYS"),
                        local_state.Keys,
                        remote_state.Keys
                    ));
                }
            }
            foreach (string key in local_state)
            {
                var local_value = local_state[key];

                if (!remote_state.Contains(key))
                    continue;
                var remote_value = remote_state[key];

                if (local_value is GDC.Dictionary localDictValue)
                {
                    if (remote_value is GDC.Dictionary remoteDictValue)
                    {
                        if (GD.Hash(local_value) != GD.Hash(remote_value))
                            _FindMismatchesRecursive(localDictValue, remoteDictValue, _ExtendDiffPath(path, key));
                    }
                    else
                        _AddDiffMismatch(local_value, remote_value, path, key);
                }
                else if (local_value is GDC.Array localArray)
                {
                    if (remote_value is GDC.Array remoteArray)
                    {
                        if (local_value != remote_value)
                            _FindMismatchesRecursive(_ConvertArrayToDictionary(localArray), _ConvertArrayToDictionary(remoteArray), _ExtendDiffPath(path, key));
                    }
                    else
                        _AddDiffMismatch(local_value, remote_value, path, key);
                }
                else if ((local_value).GetType() != (remote_value).GetType() || local_value != remote_value)
                {
                    _AddDiffMismatch(local_value, remote_value, path, key);

                }
            }
        }

        public string _GetDiffPathString(GDC.Array path, string key)
        {
            if (path.Count > 0)
            {
                return string.Join(" -> ", path) + " -> " + key;
            }
            return key;
        }

        public GDC.Array _ExtendDiffPath(GDC.Array path, string key)
        {
            var new_path = path.Duplicate();
            new_path.Add(GD.Str(key));
            return new_path;
        }

        public void _AddDiffMismatch(object local_value, object remote_value, GDC.Array path, string key)
        {
            mismatches.Add(new Mismatch(
                MismatchType.DIFFERENCE,
                _GetDiffPathString(path, key),
                local_value,
                remote_value
            ));

        }

        public GDC.Dictionary _ConvertArrayToDictionary(GDC.Array a)
        {
            GDC.Dictionary d = new GDC.Dictionary() { };
            for (int i = 0; i < a.Count; i++)
                d[i] = a[i];
            return d;
        }

        public string PrintMismatches()
        {
            var data = new List<string>();

            foreach (Mismatch mismatch in mismatches)
            {
                switch (mismatch.type)
                {
                    case MismatchType.MISSING:
                        data.Add($" => [MISSING] {mismatch.path}");
                        data.Add(JSON.Print(mismatch.local_state, JSON_INDENT));
                        data.Add("");

                        break;
                    case MismatchType.EXTRA:
                        data.Add($" => [EXTRA] {mismatch.path}");
                        data.Add(JSON.Print(mismatch.remote_state, JSON_INDENT));
                        data.Add("");

                        break;
                    case MismatchType.REORDER:
                        data.Add($" => [REORDER] {mismatch.path}");
                        data.Add($"LOCAL:  {JSON.Print(mismatch.local_state, JSON_INDENT)}");
                        data.Add($"REMOTE: {JSON.Print(mismatch.remote_state, JSON_INDENT)}");
                        data.Add("");

                        break;
                    case MismatchType.DIFFERENCE:
                        data.Add($" => [DIFF] {mismatch.path}");
                        data.Add($"LOCAL:  {JSON.Print(mismatch.local_state, JSON_INDENT)}");
                        data.Add($"REMOTE: {JSON.Print(mismatch.remote_state, JSON_INDENT)}");
                        data.Add("");

                        break;
                }
            }
            return string.Join("\n", data);
        }
    }
}