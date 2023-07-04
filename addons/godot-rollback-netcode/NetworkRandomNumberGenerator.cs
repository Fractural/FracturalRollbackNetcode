
using System;
using Fractural.Utils;
using Godot;
using GDC = Godot.Collections;

namespace Fractural.RollbackNetcode
{
    public class NetworkRandomNumberGenerator : Node
    {
        public RandomNumberGenerator generator;

        public override void _Ready()
        {
            generator = new RandomNumberGenerator();
            AddToGroup("network_sync");
        }

        public ulong Seed
        {
            get => generator.Seed;
            set => generator.Seed = value;
        }

        public GDC.Dictionary _SaveState()
        {
            return new GDC.Dictionary()
            {
                ["state"] = generator.State.Serialize(),
            };

        }

        public void _LoadState(GDC.Dictionary state)
        {
            generator.State = state.Get<byte[]>("state").DeserializePrimitive<ulong>();
        }

        public void Randomize()
        {
            generator.Randomize();
        }

        public uint Randi()
        {
            return generator.Randi();
        }

        public int RandiRange(int from, int to)
        {
            return generator.RandiRange(from, to);
        }
    }
}