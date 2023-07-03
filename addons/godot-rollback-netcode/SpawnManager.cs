
using System;
using Godot;
using GDC = Godot.Collections;


public class SpawnManager : Node
{
    public const string REUSE_DESPAWNED_NODES_SETTING = "network/rollback/spawn_manager/reuse_despawned_nodes";

    public GDC.Dictionary spawn_records = new GDC.Dictionary() { };
    public GDC.Dictionary spawned_nodes = new GDC.Dictionary() { };
    public GDC.Dictionary node_scenes = new GDC.Dictionary() { };
    public GDC.Dictionary retired_nodes = new GDC.Dictionary() { };
    public GDC.Dictionary counter = new GDC.Dictionary() { };

    public bool reuse_despawned_nodes = false;

    public bool is_respawning = false;

    public delegate void SceneSpawnedDelegate(string name, Node spawned_node, PackedScene scene, GDC.Dictionary data);
    public event SceneSpawnedDelegate SceneSpawned;
    public delegate void SceneDespawnedDelegate(string name, Node node);
    public event SceneDespawnedDelegate SceneDespawned;

    public void _Ready()
    {
        if (ProjectSettings.HasSetting(REUSE_DESPAWNED_NODES_SETTING))
        {
            reuse_despawned_nodes = ProjectSettings.GetSetting<bool>(REUSE_DESPAWNED_NODES_SETTING);
        }
        AddToGroup("network_sync");

    }

    public void Reset()
    {
        spawn_records.Clear();
        node_scenes.Clear();
        counter.Clear();

        foreach (var node in spawned_nodes.Values())
        {
            node.QueueFree();
        }
        spawned_nodes.Clear();

        foreach (var nodes in retired_nodes.Values())
        {
            foreach (var node in nodes)
            {
                node.QueueFree();
            }
        }
        retired_nodes.Clear();

    }

    public string _RenameNode(string name)
    {
        if (!counter.Contains(name))
        {
            counter[name] = 0;
        }
        counter[name] += 1;
        return name + GD.Str(counter[name]);

    }

    public void _RemoveCollidingNode(string name, Node parent)
    {
        var existing_node = parent.GetNodeOrNull(name);
        if (existing_node)
        {
            GD.PushWarning("Removing node %s which is in the way of new spawn" % existing_node);
            parent.RemoveChild(existing_node);
            existing_node.QueueFree();

        }
    }

    public bool _NodeNameSortCallback(Node a, Node b)
    {
        return a.name.CasecmpTo(b.name) == -1;

    }

    public void _AlphabetizeChildren(Node parent)
    {
        var children = parent.GetChildren();
        children.SortCustom(this, "_node_name_sort_callback");
        foreach (var index in GD.Range(children.Size()))
        {
            var child = children[index];
            parent.MoveChild(child, index);

        }
    }

    public Node _InstanceScene(string resource_path)
    {
        if (retired_nodes.Contains(resource_path))
        {
            GDC.Array nodes = retired_nodes[resource_path];
            Node node


            while (nodes.Size() > 0)
            {
                node = retired_nodes[resource_path].PopFront();
                if (IsInstanceValid(node) && !node.IsQueuedForDeletion())
                {
                    break;
                }
                else
                {
                    node = null;

                }
            }
            if (nodes.Size() == 0)
            {
                retired_nodes.Erase(resource_path);

            }
            if (node)
            {
                //print ("Reusing %s" % resource_path)
                return node;

                //print ("Instancing new %s" % resource_path)
            }
        }
        var scene = GD.Load(resource_path);
        return scene.Instance();

    }

    public Node Spawn(string name, Node parent, PackedScene scene, GDC.Dictionary data, bool rename = true, string signal_name = "")
    {
        var spawned_node = _InstanceScene(scene.resource_path);
        if (signal_name == "")
        {
            signal_name = name;
        }
        if (rename)
        {
            name = _RenameNode(name);
        }
        _RemoveCollidingNode(name, parent);
        spawned_node.name = name;
        parent.AddChild(spawned_node);
        _AlphabetizeChildren(parent);

        if (Utils.HasInteropMethod(spawned_node, "_network_spawn_preprocess"))
        {
            data = Utils.CallInteropMethod(spawned_node, "_network_spawn_preprocess", new GDC.Array() { data });

        }
        if (Utils.HasInteropMethod(spawned_node, "_network_spawn"))
        {
            Utils.CallInteropMethod(spawned_node, "_network_spawn", new GDC.Array() { data });

        }
        GDC.Dictionary spawn_record = new GDC.Dictionary()
        {
            name = spawned_node.name,
            parent = parent.GetPath(),
            scene = scene.resource_path,
            data = data,
            signal_name = signal_name,
        };

        spawned_node.SetMeta("spawn_signal_name", signal_name);

        var node_path = GD.Str(spawned_node.GetPath());
        spawn_records[node_path] = spawn_record;
        spawned_nodes[node_path] = spawned_node;
        node_scenes[node_path] = scene.resource_path;

        //print ("[%s] spawned: %s" % [SyncManager.current_tick, spawned_node.name])

        EmitSignal("scene_spawned", signal_name, spawned_node, scene, data);

        return spawned_node;

    }

    public void Despawn(Node node)
    {
        _DoDespawn(node, GD.Str(node.GetPath()));

    }

    public void _DoDespawn(Node node, string node_path)
    {
        string signal_name = node.GetMeta("spawn_signal_name");
        EmitSignal("scene_despawned", signal_name, node);

        if (Utils.HasInteropMethod(node, "_network_despawn"))
        {
            Utils.CallInteropMethod(node, "_network_despawn");
        }
        if (node.GetParent())
        {
            node.GetParent().RemoveChild(node);

        }
        if (reuse_despawned_nodes && node_scenes.Contains(node_path) && IsInstanceValid(node) && !node.IsQueuedForDeletion())
        {
            var scene_path = node_scenes[node_path];
            if (!retired_nodes.Contains(scene_path))
            {
                retired_nodes[scene_path] = new GDC.Array() { };
            }
            retired_nodes[scene_path].Append(node);
        }
        else
        {
            node.QueueFree();

        }
        spawn_records.Erase(node_path);
        spawned_nodes.Erase(node_path);
        node_scenes.Erase(node_path);

    }

    public GDC.Dictionary _SaveState()
    {
        foreach (var node_path in spawned_nodes.Keys().Duplicate())
        {
            var node = spawned_nodes[node_path];
            if (!is_instance_valid(node))
            {
                spawned_nodes.Erase(node_path);
                spawn_records.Erase(node_path);
                node_scenes.Erase(node_path);
                //print ("[SAVE %s] removing invalid: %s" % [SyncManager.current_tick, node_path])
            }
            else if (node.IsQueuedForDeletion())
            {
                if (node.GetParent())
                {
                    node.GetParent().RemoveChild(node);
                }
                spawned_nodes.Erase(node_path);
                spawn_records.Erase(node_path);
                node_scenes.Erase(node_path);
                //print ("[SAVE %s] removing deleted: %s" % [SyncManager.current_tick, node_path])

            }
        }
        return new GDC.Dictionary()
        {
            spawn_records = spawn_records.Duplicate(),
            counter = counter.Duplicate(),
        };

    }

    public void _LoadState(GDC.Dictionary state)
    {
        spawn_records = state["spawn_records"].Duplicate();
        counter = state["counter"].Duplicate();

        // Remove nodes that aren't in the state we are loading.
        foreach (var node_path in spawned_nodes.Keys().Duplicate())
        {
            if (!spawn_records.Contains(node_path))
            {
                _DoDespawn(spawned_nodes[node_path], node_path);
                //print ("[LOAD %s] de-spawned: %s" % [SyncManager.current_tick, node_path])

                // Spawn nodes that don't already exist.
            }
        }
        foreach (var node_path in spawn_records.Keys())
        {
            if (spawned_nodes.Contains(node_path))
            {
                var old_node = spawned_nodes[node_path];
                if (!is_instance_valid(old_node) || old_node.IsQueuedForDeletion())
                {
                    spawned_nodes.Erase(node_path);
                    node_scenes.Erase(node_path);

                }
            }
            is_respawning = true;

            if (!spawned_nodes.Contains(node_path))
            {
                var spawn_record = spawn_records[node_path];
                var parent = GetTree().current_scene.GetNode(spawn_record["parent"]);
                System.Diagnostics.Debug.Assert(parent != null, "Can"t re - spawn node when parent doesn"t exist");
                var name = spawn_record["name"];
                _RemoveCollidingNode(name, parent);
                var spawned_node = _InstanceScene(spawn_record["scene"]);
                spawned_node.name = name;
                parent.AddChild(spawned_node);
                _AlphabetizeChildren(parent);

                if (Utils.HasInteropMethod(spawned_node, "_network_spawn"))
                {
                    Utils.CallInteropMethod(spawned_node, "_network_spawn", [spawn_record["data"]]);

                }
                spawned_nodes[node_path] = spawned_node;
                node_scenes[node_path] = spawn_record["scene"];

                spawned_node.SetMeta("spawn_signal_name", spawn_record["signal_name"]);
                // @todo Can we get rid of the GD.Load() && just use the path?
                EmitSignal("scene_spawned", spawn_record["signal_name"], spawned_node, GD.Load(spawn_record["scene"]), spawn_record["data"]);
                //print ("[LOAD %s] re-spawned: %s" % [SyncManager.current_tick, node_path])

            }
            is_respawning = false;



        }
    }



}