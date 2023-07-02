
using System;
using Godot;
using GDC = Godot.Collections;


public class HashSerializer : Reference
{
    public GDC.Dictionary Serialize(object value)
    {
        if (value is GDC.Dictionary dict)
        {
            return SerializeDictionary(dict);
        }
        else if (value is GDC.Array array)
        {
            return SerializeArray(array);
        }
        else if (value is Resource resource)
        {
            return SerializeResource(resource);
        }
        else if (value)
        {
            return SerializeObject(value);
        }
        return SerializeOther(value);
    }

    public GDC.Dictionary SerializeDictionary(GDC.Dictionary value)
    {
        GDC.Dictionary serialized = new GDC.Dictionary() { };
        foreach (var key in value)
        {
            serialized[key] = Serialize(value[key]);
        }
        return serialized;

    }

    public GDC.Dictionary SerializeArray(GDC.Array value)
    {
        GDC.Array serialized = new GDC.Array() { };
        foreach (var item in value)
        {
            serialized.Append(Serialize(item));
        }
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

    public GDC.Dictionary SerializeOther(object value)
    {
        if (value is Vector2)
        {
            return new GDC.Dictionary()
            {
                _ = "Vector2",
                x = value.x,
                y = value.y,
            };
        }
        else if (value is Vector3)
        {
            return new GDC.Dictionary()
            {
                _ = "Vector3",
                x = value.x,
                y = value.y,
                z = value.z,
            };
        }
        else if (value is Transform2D)
        {
            return new GDC.Dictionary()
            {
                _ = "Transform2D",
                x = new GDC.Dictionary() { x = value.x.x, y = value.x.y },
                y = new GDC.Dictionary() { x = value.y.x, y = value.y.y },
                origin = new GDC.Dictionary() { x = value.origin.x, y = value.origin.y },
            };
        }
        else if (value is Transform)
        {
            return new GDC.Dictionary()
            {
                _ = "Transform",
                x = new GDC.Dictionary() { x = value.basis.x.x, y = value.basis.x.y, z = value.basis.x.z },
                y = new GDC.Dictionary() { x = value.basis.y.x, y = value.basis.y.y, z = value.basis.y.z },
                z = new GDC.Dictionary() { x = value.basis.z.x, y = value.basis.z.y, z = value.basis.z.z },
                origin = new GDC.Dictionary() { x = value.origin.x, y = value.origin.y, z = value.origin.z },
            };

        }
        return value;
    }

    public __TYPE Unserialize(__TYPE value)
    {
        if (value is GDC.Dictionary)
        {
            if (!value.Contains("_"))
            {
                return UnserializeDictionary(value);

            }
            if (value["_"] == "resource")
            {
                return UnserializeResource(value);
            }
            else if (value["_"] in ["Vector2", "Vector3", "Transform2D", "Transform"])
			{
                return UnserializeOther(value);

            }
            return UnserializeObject(value);
        }
        else if (value is GDC.Array)
        {
            return UnserializeArray(value);
        }
        return value;

    }

    public __TYPE UnserializeDictionary(GDC.Dictionary value)
    {
        GDC.Dictionary unserialized = new GDC.Dictionary() { };
        foreach (var key in value)
        {
            unserialized[key] = Unserialize(value[key]);
        }
        return unserialized;

    }

    public __TYPE UnserializeArray(GDC.Array value)
    {
        GDC.Array unserialized = new GDC.Array() { };
        foreach (var item in value)
        {
            unserialized.Append(Unserialize(item));
        }
        return unserialized;

    }

    public __TYPE UnserializeResource(GDC.Dictionary value)
    {
        return GD.Load(value["path"]);

    }

    public __TYPE UnserializeObject(GDC.Dictionary value)
    {
        if (value["_"] == "object")
        {
            return value["string"];
        }
        return null;

    }

    public __TYPE UnserializeOther(GDC.Dictionary value)
    {
        switch (value["_"])
        {

            {
            "Vector2",
				return new Vector2(value.x}, value.y);
        {
            "Vector3",
				return new Vector3(value.x}, value.y, value.z);
        {
            "Transform2D",
				return new Transform2D(
                    new Vector2(value.x.x}, value.x.y),
					new Vector2(value.y.x, value.y.y),
					new Vector2(value.origin.x, value.origin.y)
				);
        {
            "Transform",
				return new Transform(
                    new Vector3(value.x.x}, value.x.y, value.x.z),
					new Vector3(value.y.x, value.y.y, value.y.z),
					new Vector3(value.z.x, value.z.y, value.z.z),
					new Vector3(value.origin.x, value.origin.y, value.origin.z)
				);

    }
		return null;
	
	
	}
}