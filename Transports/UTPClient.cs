using System;
using System.Threading.Tasks;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace Transports.UnityTransport
{
    public class ConnectResult
    {
        public bool Status;
        public Connection Connection;
        public string ConnectError;
    }

    internal class UTPClient : UTPPeer, IClient
    {
        public event EventHandler Connected;
        public event EventHandler ConnectionFailed;
        public event EventHandler<DisconnectedEventArgs> Disconnected;

        private JoinAllocation playerAllocation;
        private NetworkDriver playerDriver;
        private UTPConnection uTPConnection;

        public bool Connect(string hostAddress, out Connection connection, out string connectError)
        {
            bool status = false;

            UnityRelayConnect(out connection, out connectError, ref status);

            return status;
        }

        public async Task<bool> PrepareConnect(string joinCode)
        { 
            if (String.IsNullOrEmpty(joinCode))
            {
                Debug.LogError("Please input a join code.");

                return false;
            } 

            try
            {
                playerAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode); 
            }
            catch (RelayServiceException ex)
            {
                Debug.LogError(ex.Message + "\n" + ex.StackTrace);

                return false;
            } 
 
            var relayServerData = new RelayServerData(playerAllocation, "udp"); 

            var settings = new NetworkSettings();
            settings.WithRelayParameters(ref relayServerData);

            playerDriver = NetworkDriver.Create(settings);
 
            if (playerDriver.Bind(NetworkEndPoint.AnyIpv4) != 0)
            {
                Debug.LogError("Player client failed to bind");

                return false;
            }
            else
            {
                Debug.Log("Player client bound to Relay server");
            }

            return true;
        }

        private void UnityRelayConnect(out Connection connection, out string connectError, ref bool status)
        { 
            NetworkConnection clientConnection = playerDriver.Connect();

            if (clientConnection.IsCreated)
            {
                connection = uTPConnection = new UTPConnection(clientConnection, this, playerDriver);

                connectError = "";
                status = true;

                ConnectTimeout();
            }
            else
            {
                connection = null;
                connectError = "Failed to connect";
                status = false;
            }
        }

        private async void ConnectTimeout() 
        {
            Task timeOutTask = Task.Delay(6000); 
            await Task.WhenAny(timeOutTask);

            if (uTPConnection != null && !uTPConnection.IsConnected)
                OnConnectionFailed();
        }

        void UpdatePlayer()
        {
            if (!playerDriver.IsCreated || !playerDriver.Bound)
            {
                return;
            }

            playerDriver.ScheduleUpdate().Complete();

            NetworkEvent.Type eventType;
            while ((eventType = uTPConnection.NetworkConnection.PopEvent(playerDriver, out var stream)) != NetworkEvent.Type.Empty)
            {
                switch (eventType)
                {
                    case NetworkEvent.Type.Data:
                        Receive(uTPConnection, playerDriver, stream);
                        break;
                    case NetworkEvent.Type.Connect:
                        Debug.Log("Player connected to the Host");
                        OnConnected();
                        break;
                    case NetworkEvent.Type.Disconnect:
                        Debug.Log("Player got disconnected from the Host");
                        uTPConnection.NetworkConnection = default(NetworkConnection);

                        OnDisconnected(DisconnectReason.Disconnected);

                        break;
                }
            }
        }

        public void Poll()
        {
            if (uTPConnection != null)
                UpdatePlayer();
        }

        public void Disconnect()
        {
            playerDriver.Disconnect(uTPConnection.NetworkConnection);

            uTPConnection.NetworkConnection = default(NetworkConnection);

            uTPConnection = null;
        }

        protected virtual void OnConnected()
        {
            Connected?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnConnectionFailed()
        {
            ConnectionFailed?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnDisconnected(DisconnectReason reason)
        {
            Disconnected?.Invoke(this, new DisconnectedEventArgs(uTPConnection, reason));
        }
    }
}