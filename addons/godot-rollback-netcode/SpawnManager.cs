
using System;
using System.Collections.Generic;
using System.Linq;
using Fractural.Utils;
using Godot;
using GDC = Godot.Collections;

namespace Fractural.RollbackNetcode
{
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

        public override void _Ready()
        {
            if (ProjectSettings.HasSetting(REUSE_DESPAWNED_NODES_SETTING))
                reuse_despawned_nodes = ProjectSettingsUtils.GetSetting<bool>(REUSE_DESPAWNED_NODES_SETTING);
            AddToGroup("network_sync");
        }

        public void Reset()
        {
            spawn_records.Clear();
            node_scenes.Clear();
            counter.Clear();

            foreach (Node node in spawned_nodes.Values)
                node.QueueFree();
            spawned_nodes.Clear();

            foreach (GDC.Array nodes in retired_nodes.Values)
            {
                foreach (Node node in nodes)
                    node.QueueFree();
            }
            retired_nodes.Clear();
        }

        public string _RenameNode(string name)
        {
            if (!counter.Contains(name))
                counter[name] = 0;
            counter[name] = counter.Get<int>(name) + 1;
            return name + GD.Str(counter[name]);
        }

        public void _RemoveCollidingNode(string name, Node parent)
        {
            var existing_node = parent.GetNodeOrNull(name);
            if (existing_node != null)
            {
                GD.PushWarning($"Removing node {existing_node} which is in the way of new spawn");
                parent.RemoveChild(existing_node);
                existing_node.QueueFree();
            }
        }

        public void _AlphabetizeChildren(Node parent)
        {
            var children = new List<Node>(parent.GetChildren().Cast<Node>());
            children.Sort((Node a, Node b) => a.Name.CasecmpTo(b.Name));
            for (int i = 0; i < children.Count; i++)
                parent.MoveChild(children[i], i);
        }

        public Node _InstanceScene(string resource_path)
        {
            if (retired_nodes.Contains(resource_path))
            {
                GDC.Array nodes = retired_nodes.Get<GDC.Array>(resource_path);
                Node node = null;
                while (nodes.Count > 0)
                {
                    node = retired_nodes.Get<GDC.Array>(resource_path).PopFrontList<Node>();
                    if (IsInstanceValid(node) && !node.IsQueuedForDeletion())
                        break;
                    else
                        node = null;
                }
                if (nodes.Count == 0)
                    retired_nodes.Remove(resource_path);
                if (node != null)
                {
                    //print ("Reusing %s" % resource_path)
                    return node;
                }
            }
            //print ("Instancing new %s" % resource_path)
            var scene = GD.Load<PackedScene>(resource_path);
            return scene.Instance();
        }

        public Node Spawn(string name, Node parent, PackedScene scene, GDC.Dictionary data, bool rename = true, string signal_name = "")
        {
            var spawned_node = _InstanceScene(scene.ResourcePath);
            if (signal_name == "")
                signal_name = name;
            if (rename)
                name = _RenameNode(name);
            _RemoveCollidingNode(name, parent);
            spawned_node.Name = name;
            parent.AddChild(spawned_node);
            _AlphabetizeChildren(parent);

            if (spawned_node is INetworkSpawnPreProcess spawnPreProcess)
                data = spawnPreProcess._NetworkSpawnPreProcess(data);
            if (spawned_node is INetworkSpawn spawn)
                spawn._NetworkSpawn(data);
            GDC.Dictionary spawn_record = new GDC.Dictionary()
            {
                ["name"] = spawned_node.Name,
                ["parent"] = parent.GetPath(),
                ["scene"] = scene.ResourcePath,
                ["data"] = data,
                ["signal_name"] = signal_name,
            };

            spawned_node.SetMeta("spawn_signal_name", signal_name);

            var node_path = GD.Str(spawned_node.GetPath());
            spawn_records[node_path] = spawn_record;
            spawned_nodes[node_path] = spawned_node;
            node_scenes[node_path] = scene.ResourcePath;

            //print ("[%s] spawned: %s" % [SyncManager.current_tick, spawned_node.name])

            SceneSpawned?.Invoke(signal_name, spawned_node, scene, data);

            return spawned_node;
        }

        public void Despawn(Node node)
        {
            _DoDespawn(node, GD.Str(node.GetPath()));
        }

        public void _DoDespawn(Node node, string node_path)
        {
            string signal_name = node.GetMeta<string>("spawn_signal_name");
            SceneDespawned?.Invoke(signal_name, node);

            if (node is INetworkDespawn despawn)
                despawn._NetworkDespawn();
            if (node.GetParent() != null)
                node.GetParent().RemoveChild(node);
            if (reuse_despawned_nodes && node_scenes.Contains(node_path) && IsInstanceValid(node) && !node.IsQueuedForDeletion())
            {
                var scene_path = node_scenes[node_path];
                if (!retired_nodes.Contains(scene_path))
                    retired_nodes[scene_path] = new GDC.Array() { };
                retired_nodes.Get<GDC.Array>(scene_path).Add(node);
            }
            else
                node.QueueFree();
            spawn_records.Remove(node_path);
            spawned_nodes.Remove(node_path);
            node_scenes.Remove(node_path);
        }

        public GDC.Dictionary _SaveState()
        {
            var nodePathKeys = new List<string>(spawned_nodes.Keys.Cast<string>());
            foreach (var node_path in nodePathKeys)
            {
                var node = spawned_nodes.Get<Node>(node_path);
                if (!IsInstanceValid(node))
                {
                    spawned_nodes.Remove(node_path);
                    spawn_records.Remove(node_path);
                    node_scenes.Remove(node_path);
                    //print ("[SAVE %s] removing invalid: %s" % [SyncManager.current_tick, node_path])
                }
                else if (node.IsQueuedForDeletion())
                {
                    if (node.GetParent() != null)
                        node.GetParent().RemoveChild(node);
                    spawned_nodes.Remove(node_path);
                    spawn_records.Remove(node_path);
                    node_scenes.Remove(node_path);
                    //print ("[SAVE %s] removing deleted: %s" % [SyncManager.current_tick, node_path])
                }
            }
            return new GDC.Dictionary()
            {
                ["spawn_records"] = spawn_records.Duplicate(),
                ["counter"] = counter.Duplicate(),
            };
        }

        public void _LoadState(GDC.Dictionary state)
        {
            spawn_records = state.Get<GDC.Dictionary>("spawn_records").Duplicate();
            counter = state.Get<GDC.Dictionary>("counter").Duplicate();

            // Remove nodes that aren't in the state we are loading.
            var keys = new List<string>(spawned_nodes.Keys.Cast<string>());
            foreach (string node_path in keys)
            {
                if (!spawn_records.Contains(node_path))
                {
                    _DoDespawn(spawned_nodes.Get<Node>(node_path), node_path);
                    //print ("[LOAD %s] de-spawned: %s" % [SyncManager.current_tick, node_path])
                }
            }

            // Spawn nodes that don't already exist.
            foreach (string node_path in spawn_records.Keys)
            {
                if (spawned_nodes.Contains(node_path))
                {
                    var old_node = spawned_nodes.Get<Node>(node_path);
                    if (!IsInstanceValid(old_node) || old_node.IsQueuedForDeletion())
                    {
                        spawned_nodes.Remove(node_path);
                        node_scenes.Remove(node_path);
                    }
                }
                is_respawning = true;

                if (!spawned_nodes.Contains(node_path))
                {
                    var spawn_record = spawn_records.Get<GDC.Dictionary>(node_path);
                    var parent = GetTree().CurrentScene.GetNode(spawn_record.Get<NodePath>("parent"));
                    System.Diagnostics.Debug.Assert(parent != null, "Can't re - spawn node when parent doesn''t exist");
                    var name = spawn_record.Get<string>("name");
                    _RemoveCollidingNode(name, parent);
                    var spawned_node = _InstanceScene(spawn_record.Get<string>("scene"));
                    spawned_node.Name = name;
                    parent.AddChild(spawned_node);
                    _AlphabetizeChildren(parent);

                    if (spawned_node is INetworkSpawn spawn)
                    {
                        spawn._NetworkSpawn(spawn_record.Get<GDC.Dictionary>("data"));
                    }
                    spawned_nodes[node_path] = spawned_node;
                    node_scenes[node_path] = spawn_record.Get<string>("scene");

                    spawned_node.SetMeta("spawn_signal_name", spawn_record.Get<string>("signal_name"));
                    // @todo Can we get rid of the GD.Load() && just use the path?
                    SceneSpawned?.Invoke(
                        spawn_record.Get<string>("signal_name"),
                        spawned_node,
                        GD.Load<PackedScene>(spawn_record.Get<string>("scene")),
                        spawn_record.Get<GDC.Dictionary>("data"));
                }
                //print ("[LOAD %s] re-spawned: %s" % [SyncManager.current_tick, node_path])
                is_respawning = false;
            }
        }
    }
}