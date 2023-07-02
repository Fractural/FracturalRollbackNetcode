using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fractural.RollbackNetcode
{
    public class Peer
    {
        public int PeerID { get; set; }

        /// <summary>
        /// Round trip time in ticks?
        /// </summary>
        public int RoundTripTime { get; set; }
        public int LastPingReceived { get; set; }
        /// <summary>
        /// ???
        /// </summary>
        public float TimeDelta { get; set; }

        /// <summary>
        /// ???
        /// </summary>
        public int LastRemoteInputTickReceived { get; set; } = 0;
        /// <summary>
        /// ???
        /// </summary>
        public int NextLocalInputTickRequested { get; set; } = 1;
        /// <summary>
        /// ???
        /// </summary>
        public int LastRemoteHashTickReceived { get; set; } = 0;
        /// <summary>
        /// ???
        /// </summary>
        public int NextLocalHashTickRequested { get; set; } = 1;

        /// <summary>
        /// ???
        /// </summary>
        public int RemoteLag { get; set; }
        /// <summary>
        /// ???
        /// </summary>
        public int LocalLag { get; set; }

        //public float CalculatedAdvantage
    }

    public abstract class NetworkAdaptor
    {
        public delegate void RecievedPingDelegate
        public event Action ReceivedPing(int peerId, )
    }

    public class SyncManager : Node
    {
        NetworkAdaptor

        public void Start()
        {

        }
    }
}
