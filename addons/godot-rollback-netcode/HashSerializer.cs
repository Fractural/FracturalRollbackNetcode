
using System;
using Godot;
using Dictionary = Godot.Collections.Dictionary;
using Array = Godot.Collections.Array;


public class HashSerializer : Reference
{
	 
	public __TYPE Serialize(__TYPE value)
	{  
		if(value is Dictionary)
		{
			return SerializeDictionary(value);
		}
		else if(value is Array)
		{
			return SerializeArray(value);
		}
		else if(value is Resource)
		{
			return SerializeResource(value);
		}
		else if(value is Object)
		{
			return SerializeObject(value);
		
		}
		return SerializeOther(value);
	
	}
	
	public Dictionary SerializeDictionary(Dictionary value)
	{  
		Dictionary serialized  = new Dictionary(){};
		foreach(var key in value)
		{
			serialized[key] = Serialize(value[key]);
		}
		return serialized;
	
	}
	
	public __TYPE SerializeArray(Array value)
	{  
		Array serialized  = new Array(){};
		foreach(var item in value)
		{
			serialized.Append(Serialize(item));
		}
		return serialized;
	
	}
	
	public __TYPE SerializeResource(Resource value)
	{  
		return new Dictionary(){
			_ = "resource",
			path = value.resource_path,
		};
	
	}
	
	public __TYPE SerializeObject(Object value)
	{  
		return new Dictionary(){
			_ = "object",
			string = value.ToString(),
		};
	
	}
	
	public __TYPE SerializeOther(__TYPE value)
	{  
		if(value is Vector2)
		{
			return new Dictionary(){
				_ = "Vector2",
				x = value.x,
				y = value.y,
			};
		}
		else if(value is Vector3)
		{
			return new Dictionary(){
				_ = "Vector3",
				x = value.x,
				y = value.y,
				z = value.z,
			};
		}
		else if(value is Transform2D)
		{
			return new Dictionary(){
				_ = "Transform2D",
				x = new Dictionary(){x = value.x.x, y = value.x.y},
				y = new Dictionary(){x = value.y.x, y = value.y.y},
				origin = new Dictionary(){x = value.origin.x, y = value.origin.y},
			};
		}
		else if(value is Transform)
		{
			return new Dictionary(){
				_ = "Transform",
				x = new Dictionary(){x = value.basis.x.x, y = value.basis.x.y, z = value.basis.x.z},
				y = new Dictionary(){x = value.basis.y.x, y = value.basis.y.y, z = value.basis.y.z},
				z = new Dictionary(){x = value.basis.z.x, y = value.basis.z.y, z = value.basis.z.z},
				origin = new Dictionary(){x = value.origin.x, y = value.origin.y, z = value.origin.z},
			};
		
		}
		return value;
	
	}
	
	public __TYPE Unserialize(__TYPE value)
	{  
		if(value is Dictionary)
		{
			if(!value.Has("_"))
			{
				return UnserializeDictionary(value);
			
			}
			if(value["_"] == "resource")
			{
				return UnserializeResource(value);
			}
			else if(value["_"] in ["Vector2", "Vector3", "Transform2D", "Transform"])
			{
				return UnserializeOther(value);
			
			}
			return UnserializeObject(value);
		}
		else if(value is Array)
		{
			return UnserializeArray(value);
		}
		return value;
	
	}
	
	public __TYPE UnserializeDictionary(Dictionary value)
	{  
		Dictionary unserialized  = new Dictionary(){};
		foreach(var key in value)
		{
			unserialized[key] = Unserialize(value[key]);
		}
		return unserialized;
	
	}
	
	public __TYPE UnserializeArray(Array value)
	{  
		Array unserialized  = new Array(){};
		foreach(var item in value)
		{
			unserialized.Append(Unserialize(item));
		}
		return unserialized;
	
	}
	
	public __TYPE UnserializeResource(Dictionary value)
	{  
		return GD.Load(value["path"]);
	
	}
	
	public __TYPE UnserializeObject(Dictionary value)
	{  
		if(value["_"] == "object")
		{
			return value["string"];
		}
		return null;
	
	}
	
	public __TYPE UnserializeOther(Dictionary value)
	{  
		switch( value["_"])
		{
			{"Vector2",
				return new Vector2(value.x}, value.y);
			{"Vector3",
				return new Vector3(value.x}, value.y, value.z);
			{"Transform2D",
				return new Transform2D(
					new Vector2(value.x.x}, value.x.y),
					new Vector2(value.y.x, value.y.y),
					new Vector2(value.origin.x, value.origin.y)
				);
			{"Transform",
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