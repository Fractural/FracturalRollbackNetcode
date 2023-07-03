using GDC = Godot.Collections;

namespace Fractural.RollbackNetcode
{
    public interface INetworkSync { }
    public interface IGetLocalInput : INetworkSync
    {
        GDC.Dictionary _GetLocalInput();
    }

    public interface INetworkProcess : INetworkSync
    {
        void _NetworkProcess(GDC.Dictionary input);
    }

    public interface INetworkPreProcess : INetworkSync
    {
        void _NetworkPreprocess(GDC.Dictionary input);
    }

    public interface INetworkPostProcess : INetworkSync
    {
        void _NetworkPostprocess(GDC.Dictionary input);
    }

    public interface IInterpolateState : INetworkSync
    {
        void _InterpolateState(GDC.Dictionary oldState, GDC.Dictionary newState, float weight);
    }

    public interface IPredictRemoteInput : INetworkSync
    {
        GDC.Dictionary _PredictRemoteInput(GDC.Dictionary previousInput, int ticksSinceRealInput);
    }

    public interface INetworkSerializable : INetworkSync
    {
        GDC.Dictionary _SaveState();
        void _LoadState(GDC.Dictionary state);
    }

    public interface INetworkSpawnPreProcess
    {
        GDC.Dictionary _NetworkSpawnPreProcess(GDC.Dictionary data);
    }

    public interface INetworkSpawn
    {
        void _NetworkSpawn(GDC.Dictionary data);
    }

    public interface INetworkDespawn
    {
        void _NetworkDespawn();
    }
}
