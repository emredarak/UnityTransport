using System;
using System.Collections.Generic;
using Unity.Networking.Transport;

namespace Transports.UnityTransport
{
    internal class UTPConnection : Connection, IEquatable<UTPConnection>
    {
        public readonly int InternalId;
        public NetworkConnection NetworkConnection;

        private readonly UTPPeer peer;
        private NetworkDriver networkDriver;

        internal UTPConnection(NetworkConnection networkConnection, UTPPeer peer, NetworkDriver networkDriver)
        {
            InternalId = networkConnection.InternalId;
            NetworkConnection = networkConnection;
            this.peer = peer;
            this.networkDriver = networkDriver;
        }

        protected override void Send(byte[] dataBuffer, int amount)
        {
            peer.Send(dataBuffer, amount, NetworkConnection, networkDriver);
        }
 
        public override string ToString() => NetworkConnection.ToString();
 
        public override bool Equals(object obj) => Equals(obj as UTPConnection);
 
        public bool Equals(UTPConnection other)
        {
            if (other is null)
                return false;

            if (ReferenceEquals(this, other))
                return true;

            return NetworkConnection.Equals(other.NetworkConnection);
        }
 
        public override int GetHashCode()
        {
            return -721414014 + EqualityComparer<NetworkConnection>.Default.GetHashCode(NetworkConnection);
        }

        public static bool operator ==(UTPConnection left, UTPConnection right)
        {
            if (left is null)
            {
                if (right is null)
                    return true;

                return false;
            }
 
            return left.Equals(right);
        }

        public static bool operator !=(UTPConnection left, UTPConnection right) => !(left == right);
    }
}
