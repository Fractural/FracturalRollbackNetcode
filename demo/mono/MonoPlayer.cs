using Fractural.RollbackNetcode;
using Fractural.Utils;
using Godot;
using GDC = Godot.Collections;
using System;

namespace Game
{
    public class MonoPlayer : Sprite, IGetLocalInput, INetworkProcess, INetworkSerializable, IPredictRemoteInput, IInterpolateState
    {
        [Export]
        public string InputPrefix { get; set; } = "player1_";

        public static class PlayerInputKey
        {
            public const string MovementVector = "0";
        }

        public GDC.Dictionary _SaveState()
        {
            return new GDC.Dictionary()
            {
                [nameof(Position)] = Position
            };
        }

        public void _LoadState(GDC.Dictionary state)
        {
            Position = state.Get<Vector2>(nameof(Position));
        }

        public void _InterpolateState(GDC.Dictionary oldState, GDC.Dictionary newState, float weight)
        {
            Position = oldState.Get<Vector2>(nameof(Position)).Lerp(
                newState.Get<Vector2>(nameof(Position)),
                weight);
        }

        public GDC.Dictionary _GetLocalInput()
        {
            var inputVector = Input.GetVector(InputPrefix + "left", InputPrefix + "right", InputPrefix + "up", InputPrefix + "down");
            var input = new GDC.Dictionary();
            if (inputVector != Vector2.Zero)
                input[PlayerInputKey.MovementVector] = inputVector;
            return input;
        }

        public GDC.Dictionary _PredictRemoteInput(GDC.Dictionary previousInput, int ticksSinceRealInput)
        {
            var input = previousInput.Duplicate();
            if (ticksSinceRealInput > 5)
                input.Remove(PlayerInputKey.MovementVector);
            return input;
        }

        public void _NetworkProcess(GDC.Dictionary input)
        {
            var inputVector = input.Get(PlayerInputKey.MovementVector, Vector2.Zero);
            Position += inputVector * 8;
        }

    }
}