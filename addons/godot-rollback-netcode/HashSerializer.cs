
using System;
using Fractural.Utils;
using Godot;
using GDC = Godot.Collections;

namespace Fractural.RollbackNetcode
{
    public class HashSerializer : Reference
    {
        public object Serialize(object value)
        {
            if (value is GDC.Dictionary dict)
                return SerializeDictionary(dict);
            else if (value is GDC.Array array)
                return SerializeArray(array);
            else if (value is Resource resource)
                return SerializeResource(resource);
            else if (value.GetType().IsValueType)
                return SerializeValueType(value);
            return SerializeObject(value);
        }

        public GDC.Dictionary SerializeDictionary(GDC.Dictionary value)
        {
            GDC.Dictionary serialized = new GDC.Dictionary() { };
            foreach (var key in value.Keys)
                serialized[key] = Serialize(value[key]);
            return serialized;
        }

        public GDC.Array SerializeArray(GDC.Array value)
        {
            GDC.Array serialized = new GDC.Array() { };
            foreach (var item in value)
                serialized.Add(Serialize(item));
            return serialized;
        }

        public GDC.Dictionary SerializeResource(Resource value)
        {
            return new GDC.Dictionary()
            {
                ["_"] = "resource",
                ["path"] = value.ResourcePath,
            };
        }

        public GDC.Dictionary SerializeObject(object value)
        {
            return new GDC.Dictionary()
            {
                ["_"] = "object",
                ["string"] = value.ToString(),
            };
        }

        public object SerializeValueType(object value)
        {
            if (value is Vector2 vector2)
            {
                return new GDC.Dictionary()
                {
                    ["_"] = "Vector2",
                    ["x"] = vector2.x,
                    ["y"] = vector2.y,
                };
            }
            else if (value is Vector3 vector3)
            {
                return new GDC.Dictionary()
                {
                    ["_"] = "Vector3",
                    ["x"] = vector3.x,
                    ["y"] = vector3.y,
                    ["z"] = vector3.z,
                };
            }
            else if (value is Transform2D transform2D)
            {
                return new GDC.Dictionary()
                {
                    ["_"] = "Transform2D",
                    ["x"] = new GDC.Dictionary() { ["x"] = transform2D.x.x, ["y"] = transform2D.x.y },
                    ["y"] = new GDC.Dictionary() { ["x"] = transform2D.y.x, ["y"] = transform2D.y.y },
                    ["origin"] = new GDC.Dictionary() { ["x"] = transform2D.origin.x, ["y"] = transform2D.origin.y },
                };
            }
            else if (value is Transform transform)
            {
                return new GDC.Dictionary()
                {
                    ["_"] = "Transform",
                    ["x"] = new GDC.Dictionary() { ["x"] = transform.basis.x.x, ["y"] = transform.basis.x.y, ["z"] = transform.basis.x.z },
                    ["y"] = new GDC.Dictionary() { ["x"] = transform.basis.y.x, ["y"] = transform.basis.y.y, ["z"] = transform.basis.y.z },
                    ["z"] = new GDC.Dictionary() { ["x"] = transform.basis.z.x, ["y"] = transform.basis.z.y, ["z"] = transform.basis.z.z },
                    ["origin"] = new GDC.Dictionary() { ["x"] = transform.origin.x, ["y"] = transform.origin.y, ["z"] = transform.origin.z },
                };
            }
            return value;
        }

        public object Unserialize(object value)
        {
            if (value is GDC.Dictionary dict)
            {
                var otherTypes = new[] { "Vector2", "Vector3", "Transform2D", "Transform" };
                if (!dict.Contains("_"))
                    return UnserializeDictionary(dict);

                if (dict.Get<string>("_") == "resource")
                    return UnserializeResource(dict);
                else if (otherTypes.Contains(dict.Get<string>("_")))
                    return UnserializeOther(dict);

                return UnserializeObject(dict);
            }
            else if (value is GDC.Array array)
                return UnserializeArray(array);
            return value;

        }

        public GDC.Dictionary UnserializeDictionary(GDC.Dictionary value)
        {
            GDC.Dictionary unserialized = new GDC.Dictionary() { };
            foreach (var key in value.Keys)
                unserialized[key] = Unserialize(value[key]);
            return unserialized;
        }

        public GDC.Array UnserializeArray(GDC.Array value)
        {
            GDC.Array unserialized = new GDC.Array() { };
            foreach (var item in value)
                unserialized.Add(Unserialize(item));
            return unserialized;
        }

        public Resource UnserializeResource(GDC.Dictionary value)
        {
            return GD.Load(value.Get<string>("path"));
        }

        public string UnserializeObject(GDC.Dictionary value)
        {
            if (value.Get<string>("_") == "object")
            {
                return value.Get<string>("string");
            }
            return null;

        }

        public object UnserializeOther(GDC.Dictionary value)
        {
            switch (value.Get<string>("_"))
            {
                case "Vector2":
                    return new Vector2(value.Get<float>("x"), value.Get<float>("y"));
                case "Vector3":
                    return new Vector3(value.Get<float>("x"), value.Get<float>("y"), value.Get<float>("z"));
                case "Transform2D":
                    return new Transform2D(
                        new Vector2(value.Get<float>("x.x"), value.Get<float>("x.y")),
                        new Vector2(value.Get<float>("y.x"), value.Get<float>("y.y")),
                        new Vector2(value.Get<float>("origin.x"), value.Get<float>("origin.y"))
                    );
                case "Transform":
                    return new Transform(
                        new Vector3(value.Get<float>("x.x"), value.Get<float>("x.y"), value.Get<float>("x.z")),
                        new Vector3(value.Get<float>("y.x"), value.Get<float>("y.y"), value.Get<float>("y.z")),
                        new Vector3(value.Get<float>("z.x"), value.Get<float>("z.y"), value.Get<float>("z.z")),
                        new Vector3(value.Get<float>("origin.x"), value.Get<float>("origin.y"), value.Get<float>("origin.z"))
                    );
            }
            return null;
        }
    }
}