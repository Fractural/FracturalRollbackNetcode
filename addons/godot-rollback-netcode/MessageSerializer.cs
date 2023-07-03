
using System;
using System.Linq;
using Fractural.Utils;
using Godot;
using GDC = Godot.Collections;

namespace Fractural.RollbackNetcode
{
    public interface IMessageSerializer
    {
        byte[] SerializeInput(GDC.Dictionary input);
        GDC.Dictionary UnserializeInput(byte[] serialized);
        byte[] SerializeMessage(GDC.Dictionary msg);
        GDC.Dictionary UnserializeMessage(byte[] serialized);
    }

    public class MessageSerializer : Godot.Reference, IMessageSerializer
    {
        const int DEFAULT_MESSAGE_BUFFER_SIZE = 1280;
        public enum InputMessageKey
        {
            NEXT_INPUT_TICK_REQUESTED = 0,
            INPUT = 1,
            NEXT_HASH_TICK_REQUESTED = 2,
            STATE_HASHES = 3,
        }

        private byte[] serialize_input(GDC.Dictionary input) => SerializeInput(input);

        public virtual byte[] SerializeInput(GDC.Dictionary input)
        {
            return GD.Var2Bytes(input);
        }

        private GDC.Dictionary unserialize_input(byte[] serialized) => UnserializeInput(serialized);

        public virtual GDC.Dictionary UnserializeInput(byte[] serialized)
        {
            return (GDC.Dictionary)GD.Bytes2Var(serialized);
        }

        private byte[] serialize_message(GDC.Dictionary msg) => SerializeMessage(msg);

        public virtual byte[] SerializeMessage(GDC.Dictionary msg)
        {
            var buffer = new StreamPeerBuffer();
            buffer.Resize(DEFAULT_MESSAGE_BUFFER_SIZE);

            buffer.Put32((int)msg[(int)InputMessageKey.NEXT_INPUT_TICK_REQUESTED]);

            GDC.Dictionary inputTicks = (GDC.Dictionary)msg[(int)InputMessageKey.INPUT];
            buffer.PutU8((byte)inputTicks.Count);
            if (inputTicks.Count > 0)
            {
                var inputKeys = inputTicks.Keys.OfType<int>().OrderBy((key) => key);
                buffer.Put32(inputKeys.First());
                foreach (var inputKey in inputKeys)
                {
                    var input = (byte[])inputTicks[inputKey];
                    buffer.PutU16(Convert.ToUInt16(input.Length));
                    buffer.PutData(input);
                }
            }

            buffer.Put32((int)msg[(int)InputMessageKey.NEXT_HASH_TICK_REQUESTED]);

            GDC.Dictionary stateHashes = (GDC.Dictionary)msg[(int)InputMessageKey.STATE_HASHES];
            buffer.PutU8(Convert.ToByte(stateHashes.Count));
            if (stateHashes.Count > 0)
            {
                var stateHashKeys = stateHashes.Keys.OfType<int>().OrderBy((key) => key);
                buffer.Put32(stateHashKeys.First());
                foreach (var stateHashKey in stateHashKeys)
                    // HACK: Currently Godot marshalls all GDScript ints into C# ints, even though they
                    //       have widly different ranges. GDScript ints are 64-bit, while C# ints are
                    //       32-bit.
                    //       https://github.com/godotengine/godot/issues/57141
                    buffer.Put32((int)stateHashes[stateHashKey]);
            }

            buffer.Resize(buffer.GetPosition());
            return buffer.DataArray;
        }

        private GDC.Dictionary unserialize_message(byte[] serialized) => UnserializeMessage(serialized);

        public virtual GDC.Dictionary UnserializeMessage(byte[] serialized)
        {
            var buffer = new StreamPeerBuffer();
            buffer.PutData(serialized);
            buffer.Seek(0);

            var msg = new GDC.Dictionary();
            msg[(int)InputMessageKey.INPUT] = new GDC.Dictionary();
            msg[(int)InputMessageKey.STATE_HASHES] = new GDC.Dictionary();

            msg[(int)InputMessageKey.NEXT_INPUT_TICK_REQUESTED] = buffer.GetU32();

            var inputTickCount = buffer.GetU8();
            if (inputTickCount > 0)
            {
                var inputTick = buffer.GetU32();
                for (int i = 0; i < inputTickCount; i++)
                {
                    var inputSize = buffer.GetU16();
                    ((GDC.Dictionary)msg[(int)InputMessageKey.INPUT])[inputTick] = buffer.GetData(inputSize)[1];
                    inputTick += 1;
                }
            }

            msg[(int)InputMessageKey.NEXT_HASH_TICK_REQUESTED] = buffer.GetU32();

            var hashTickCount = buffer.GetU8();
            if (hashTickCount > 0)
            {
                var hashTick = buffer.GetU32();
                for (int i = 0; i < hashTickCount; i++)
                {
                    ((GDC.Dictionary)msg[(int)InputMessageKey.STATE_HASHES])[hashTick] = buffer.GetU32();
                    hashTick += 1;
                }
            }

            return msg;
        }
    }
}