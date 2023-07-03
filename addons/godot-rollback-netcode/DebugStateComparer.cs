
using System;
using Godot;
using GDC = Godot.Collections;


public class DebugStateComparer : Reference
{

    public const string JSON_INDENT = "    ";

    enum MismatchType
    {
        MISSING,
        EXTRA,
        REORDER,
        DIFFERENCE,
    }

    public class Mismatch :
        int type

        string path

        public __TYPE local_state;
    public __TYPE remote_state;

    public void _Init(int _type, string _path, __TYPE _local_state, __TYPE _remote_state)
    {
        type = _type;
        path = _path;
        local_state = _local_state;
        remote_state = _remote_state;

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
        state.Erase("$");

        // Remove any keys that are ignored in the hash.
        foreach (var node_path in state)
        {
            // I think this happens when there's an error reading a GDC.Dictionary.
            if (node_path == null)
            {
                state.Erase(null);
            }
            foreach (var key in state[node_path].Keys())
            {
                var value = state[node_path];
                if (key is string)
                {
                    if (key.BeginsWith("_"))
                    {
                        value.Erase(key);
                    }
                }
                else if (key is int)
                {
                    if (key < 0)
                    {
                        value.Erase(key);

                    }
                }
            }
        }
        return state;

    }

    public void _FindMismatchesRecursive(GDC.Dictionary local_state, GDC.Dictionary remote_state, GDC.Array path = new GDC.Array() { })
    {
        bool missing_or_extra = false;

        foreach (var key in local_state)
        {
            if (!remote_state.Contains(key))
            {
                missing_or_extra = true;
                mismatches.Append(Mismatch.new(
                    MismatchType.MISSING,
                    _GetDiffPathString(path, key),
                    local_state[key],
                    null
                ));

            }
        }
        foreach (var key in remote_state)
        {
            if (!local_state.Contains(key))
            {
                missing_or_extra = true;
                mismatches.Append(Mismatch.new(
                    MismatchType.EXTRA,
                    _GetDiffPathString(path, key),
                    null,
                    remote_state[key]
                ));

            }
        }
        if (!missing_or_extra)
        {
            if (local_state.Keys() != remote_state.Keys())
            {
                mismatches.Append(Mismatch.new(
                    MismatchType.REORDER,
                    _GetDiffPathString(path, "KEYS"),
                    local_state.Keys(),
                    remote_state.Keys()
                ));

            }
        }
        foreach (var key in local_state)
        {
            var local_value = local_state[key];

            if (!remote_state.Contains(key))
            {
                continue;
            }
            var remote_value = remote_state[key];

            if (local_value is GDC.Dictionary)
            {
                if (remote_value is GDC.Dictionary)
                {
                    if (local_value.Hash() != remote_value.Hash())
                    {
                        _FindMismatchesRecursive(local_value, remote_value, _ExtendDiffPath(path, key));
                    }
                }
                else
                {
                    _AddDiffMismatch(local_value, remote_value, path, key);
                }
            }
            else if (local_value is GDC.Array)
            {
                if (remote_value is GDC.Array)
                {
                    if (local_value != remote_value)
                    {
                        _FindMismatchesRecursive(_ConvertArrayToDictionary(local_value), _ConvertArrayToDictionary(remote_value), _ExtendDiffPath(path, key));
                    }
                }
                else
                {
                    _AddDiffMismatch(local_value, remote_value, path, key);
                }
            }
            else if ((local_value).GetType() != (remote_value).GetType() || local_value != remote_value)
            {
                _AddDiffMismatch(local_value, remote_value, path, key);

            }
        }
    }

    public string _GetDiffPathString(GDC.Array path, __TYPE key)
    {
        if (path.Size() > 0)
        {
            return PoolStringArray(path).Join(" -> ") + " -> " + GD.Str(key);
        }
        return GD.Str(key);

    }

    public GDC.Array _ExtendDiffPath(GDC.Array path, __TYPE key)
    {
        var new_path = path.Duplicate();
        new_path.Append(GD.Str(key))

        return new_path;

    }

    public void _AddDiffMismatch(__TYPE local_value, __TYPE remote_value, GDC.Array path, __TYPE key)
    {
        mismatches.Append(Mismatch.new(
            MismatchType.DIFFERENCE,
            _GetDiffPathString(path, key),
            local_value,
            remote_value
        ));

    }

    public GDC.Dictionary _ConvertArrayToDictionary(GDC.Array a)
    {
        GDC.Dictionary d = new GDC.Dictionary() { };
        foreach (var i in GD.Range(a.Size()))
        {
            d[i] = a[i];
        }
        return d;

    }

    public string PrintMismatches()
    {
        var data = PoolStringArray();

        foreach (var mismatch in mismatches)
        {
            switch (mismatch.type)
            {
                case MismatchType.MISSING:
                    data.Append(" => [MISSING] %s" % mismatch.path);
                    data.Append(JSON.Print(mismatch.local_state, JSON_INDENT));
                    data.Append("");

                    break;
                case MismatchType.EXTRA:
                    data.Append(" => [EXTRA] %s" % mismatch.path);
                    data.Append(JSON.Print(mismatch.remote_state, JSON_INDENT));
                    data.Append("");

                    break;
                case MismatchType.REORDER:
                    data.Append(" => [REORDER] %s" % mismatch.path);
                    data.Append("LOCAL:  %s" % JSON.Print(mismatch.local_state, JSON_INDENT));
                    data.Append("REMOTE: %s" % JSON.Print(mismatch.remote_state, JSON_INDENT));
                    data.Append("");

                    break;
                case MismatchType.DIFFERENCE:
                    data.Append(" => [DIFF] %s" % mismatch.path);
                    data.Append("LOCAL:  %s" % JSON.Print(mismatch.local_state, JSON_INDENT));
                    data.Append("REMOTE: %s" % JSON.Print(mismatch.remote_state, JSON_INDENT));
                    data.Append("");

                    break;
            }
        }
        return data.Join("\n");


    }



}