using System;
using UnityEngine;
using Unity.Networking.Transport;
using System.Collections.Generic;
using Unity.Services.Relay.Models;
using Unity.Services.Core;
using Unity.Services.Authentication;
using System.Threading.Tasks;
using Unity.Services.Relay;
using Unity.Networking.Transport.Relay;
using UnityEngine.Assertions;

namespace Transports.UnityTransport
{
    internal class UTPServer : UTPPeer, IServer
    {
        public event EventHandler<ConnectedEventArgs> Connected;
        public event EventHandler<DisconnectedEventArgs> Disconnected;

        public ushort Port { get; private set; }

        private NetworkDriver hostDriver;
        private Dictionary<int, UTPConnection> serverConnections;
        private Allocation hostAllocation;
        public string JoinCode;

        public void Start(ushort port)
        {
            var relayServerData = new RelayServerData(hostAllocation, "udp");

            var settings = new NetworkSettings();
            settings.WithRelayParameters(ref relayServerData);

            hostDriver = NetworkDriver.Create(settings);

            if (hostDriver.Bind(NetworkEndPoint.AnyIpv4) != 0)
            {
                Debug.LogError("Host client failed to bind");
            }
            else
            {
                if (hostDriver.Listen() != 0)
                {
                    Debug.LogError("Host client failed to listen");
                }
                else
                {
                    Debug.Log("Host client bound to Relay server");
                }
            }
        }

        public async Task PrepareStart(int maxPlayers)
        {
            await OnAllocate(maxPlayers);

            await OnJoinCode();
        }

        private async Task OnAllocate(int maxPlayers)
        {
            hostAllocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers);

            serverConnections = new Dictionary<int, UTPConnection>();
        }

        private async Task OnJoinCode()
        {
            try
            {
                JoinCode = await RelayService.Instance.GetJoinCodeAsync(hostAllocation.AllocationId);
            }
            catch (RelayServiceException ex)
            {
                Debug.LogError(ex.Message + "\n" + ex.StackTrace);
            }
        }

        void UpdateHost()
        {
            if (!hostDriver.IsCreated || !hostDriver.Bound)
            {
                return;
            }

            hostDriver.ScheduleUpdate().Complete();

            List<int> deleteKeys = new List<int>();

            foreach (KeyValuePair<int, UTPConnection> serverConnection in serverConnections)
            {
                if (!serverConnection.Value.NetworkConnection.IsCreated)
                {
                    Debug.Log("Stale connection removed");

                    deleteKeys.Add(serverConnection.Key);
                }
            }

            foreach (var deleteKey in deleteKeys)
            {
                serverConnections.Remove(deleteKey);
            }

            deleteKeys.Clear();

            NetworkConnection incomingConnection;
            while ((incomingConnection = hostDriver.Accept()) != default(NetworkConnection))
            {
                UTPConnection connection = new UTPConnection(incomingConnection, this, hostDriver);

                serverConnections.Add(incomingConnection.InternalId, connection);

                OnConnected(connection);
            }

            foreach (KeyValuePair<int, UTPConnection> serverConnection in serverConnections)
            {
                Assert.IsTrue(serverConnection.Value.NetworkConnection.IsCreated);

                NetworkEvent.Type eventType;
                while ((eventType = hostDriver.PopEventForConnection(serverConnection.Value.NetworkConnection, out var stream)) != NetworkEvent.Type.Empty)
                {
                    switch (eventType)
                    {
                        case NetworkEvent.Type.Data:

                            Receive(serverConnection.Value, hostDriver, stream);

                            break;
                        case NetworkEvent.Type.Disconnect:

                            if (serverConnections.TryGetValue(serverConnection.Value.NetworkConnection.InternalId, out UTPConnection connection))
                            {
                                OnDisconnected(connection, DisconnectReason.Disconnected);
                            }

                            serverConnection.Value.NetworkConnection = default(NetworkConnection);

                            break;
                    }
                }
            }
        }

        public void Close(Connection connection)
        {
            if (connection is UTPConnection uTPConnection)
            {
                if (serverConnections.TryGetValue(uTPConnection.NetworkConnection.InternalId, out UTPConnection serverConnection))
                {
                    hostDriver.Disconnect(serverConnection.NetworkConnection);

                    serverConnection.NetworkConnection = default(NetworkConnection);
                }
            }
        }

        public void Poll()
        {
            UpdateHost();
        }

        public void Shutdown()
        {
            if (serverConnections.Count == 0)
            {
                return;
            }

            foreach (KeyValuePair<int, UTPConnection> serverConnection in serverConnections)
            {
                hostDriver.Disconnect(serverConnection.Value.NetworkConnection);

                serverConnection.Value.NetworkConnection = default(NetworkConnection);
            }

            serverConnections.Clear();

            hostDriver.Dispose();
        }

        protected internal virtual void OnConnected(Connection connection)
        {
            Connected?.Invoke(this, new ConnectedEventArgs(connection));
        }

        protected virtual void OnDisconnected(UTPConnection connection, DisconnectReason reason)
        {
            Disconnected?.Invoke(this, new DisconnectedEventArgs(connection, reason));
        }
    }
}