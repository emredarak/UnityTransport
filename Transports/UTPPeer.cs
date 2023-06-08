using System;
using System.IO;
using Unity.Collections;
using Unity.Networking.Transport;
using UnityEngine;

namespace Transports.UnityTransport
{
    internal class UTPPeer
    {
        public const string LogName = "UTP";

        public event EventHandler<DataReceivedEventArgs> DataReceived;

        protected void Receive(UTPConnection fromConnection, NetworkDriver networkDriver, DataStreamReader stream)
        {
            NativeArray<byte> nativeBuffer = new NativeArray<byte>(stream.Length - stream.GetBytesRead(), Allocator.Temp);
            stream.ReadBytes(nativeBuffer);

            byte[] buffer = new byte[nativeBuffer.Length];
            NativeArray<byte>.Copy(nativeBuffer, buffer, nativeBuffer.Length);
            nativeBuffer.Dispose();

            OnDataReceived(buffer, buffer.Length, fromConnection);
        }

        internal unsafe void Send(byte[] dataBuffer, int numBytes, NetworkConnection toConnection, NetworkDriver networkDriver)
        {
            if (!toConnection.IsCreated)
            {
                Debug.LogError("Player isn't connected. No Host client to send message to.");
                return;
            }

            if (networkDriver.BeginSend(toConnection, out var writer) == 0)
            {
                fixed (byte* bufferPtr = dataBuffer)
                {
                    writer.WriteBytes(bufferPtr, numBytes);
                }

                networkDriver.EndSend(writer);
            }
        }

        protected virtual void OnDataReceived(byte[] dataBuffer, int amount, UTPConnection fromConnection)
        {
            DataReceived?.Invoke(this, new DataReceivedEventArgs(dataBuffer, amount, fromConnection));
        }
    }
}