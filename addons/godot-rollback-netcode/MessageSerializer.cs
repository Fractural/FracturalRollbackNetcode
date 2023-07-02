
using System;
using Godot;
using Dictionary = Godot.Collections.Dictionary;
using Array = Godot.Collections.Array;


public class MessageSerializer : Reference
{
	 
	public const int DEFAULT_MESSAGE_BUFFER_SIZE = 1280;
	
	enum InputMessageKey {
		NEXT_INPUT_TICK_REQUESTED,
		INPUT,
		NEXT_HASH_TICK_REQUESTED,
		STATE_HASHES,
	}
	
	public PoolByteArray SerializeInput(Dictionary input)
	{  
		return GD.Var2Bytes(input);
	
	}
	
	public Dictionary UnserializeInput(PoolByteArray serialized)
	{  
		return GD.Bytes2Var(serialized);
	
	}
	
	public PoolByteArray SerializeMessage(Dictionary msg)
	{  
		var buffer  = new StreamPeerBuffer()
		buffer.Resize(DEFAULT_MESSAGE_BUFFER_SIZE);
	
		buffer.PutU32(msg[InputMessageKey.NEXT_INPUT_TICK_REQUESTED]);
		
		var input_ticks = msg[InputMessageKey.INPUT];
		buffer.PutU8(input_ticks.Size());
		if(input_ticks.Size() > 0)
		{
			var input_keys = input_ticks.Keys();
			input_keys.Sort();
			buffer.PutU32(input_keys[0]);
			foreach(var input_key in input_keys)
			{
				var input = input_ticks[input_key];
				buffer.PutU16(input.Size());
				buffer.PutData(input);
		
			}
		}
		buffer.PutU32(msg[InputMessageKey.NEXT_HASH_TICK_REQUESTED]);
		
		var state_hashes = msg[InputMessageKey.STATE_HASHES];
		buffer.PutU8(state_hashes.Size());
		if(state_hashes.Size() > 0)
		{
			var state_hash_keys = state_hashes.Keys();
			state_hash_keys.Sort();
			buffer.PutU32(state_hash_keys[0]);
			foreach(var state_hash_key in state_hash_keys)
			{
				buffer.PutU32(state_hashes[state_hash_key]);
		
			}
		}
		buffer.Resize(buffer.GetPosition());
		return buffer.data_array;
	
	}
	
	public Dictionary UnserializeMessage(__TYPE serialized)
	{  
		var buffer  = new StreamPeerBuffer()
		buffer.PutData(serialized);
		buffer.Seek(0);
		
		Dictionary msg  = new Dictionary(){
			InputMessageKey.INPUT: new Dictionary(){},
			InputMessageKey.STATE_HASHES: new Dictionary(){},
		};
		
		msg[InputMessageKey.NEXT_INPUT_TICK_REQUESTED] = buffer.GetU32();
		
		var input_tick_count = buffer.GetU8();
		if(input_tick_count > 0)
		{
			var input_tick = buffer.GetU32();
			foreach(var input_tick_index in GD.Range(input_tick_count))
			{
				var input_size = buffer.GetU16();
				msg[InputMessageKey.INPUT][input_tick] = buffer.GetData(input_size)[1];
				input_tick += 1;
		
			}
		}
		msg[InputMessageKey.NEXT_HASH_TICK_REQUESTED] = buffer.GetU32();
		
		var hash_tick_count = buffer.GetU8();
		if(hash_tick_count > 0)
		{
			var hash_tick = buffer.GetU32();
			foreach(var hash_tick_index in GD.Range(hash_tick_count))
			{
				msg[InputMessageKey.STATE_HASHES][hash_tick] = buffer.GetU32();
				hash_tick += 1;
		
			}
		}
		return msg;
	
	
	}
	
	
	
}