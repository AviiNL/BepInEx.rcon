using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace rcon.Internal
{
    internal class AsynchronousSocketListener
    {
        internal delegate void MessageReceived(Socket socket, int requestId, PacketType type, string payload);
        internal event MessageReceived OnMessage;
        private Socket listener;

        private List<StateObject> clients = new List<StateObject>();

        internal void StartListening(int port)
        {
            IPAddress ipAddress = IPAddress.Any;
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, port);

            // Create a TCP/IP socket.  
            listener = new Socket(ipAddress.AddressFamily,
                SocketType.Stream, ProtocolType.Tcp);

            // Bind the socket to the local endpoint and listen for incoming connections.  
            try
            {
                listener.Bind(localEndPoint);
                listener.Listen(100);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError(e.ToString());
            }
        }

        private bool isConnected(Socket c)
        {
            try
            {
                if (c != null && c != null && c.Connected)
                {
                    if (c.Poll(0, SelectMode.SelectRead))
                    {
                        return !(c.Receive(new byte[1], SocketFlags.Peek) == 0);
                    }
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        internal void Update()
        {

            for (int i = 0; i < clients.Count; i++)
            {
                StateObject state = clients[i];
                if (!isConnected(state.workSocket))
                {
                    UnityEngine.Debug.Log("Rcon client disconnected");
                    state.workSocket.Close();
                    clients.Remove(state);
                }
            }

            listener.BeginAccept(
                        new AsyncCallback(AcceptCallback),
                        listener);
        }

        internal void AcceptCallback(IAsyncResult ar)
        {
            // Signal the main thread to continue.  
            //allDone.Set();

            // Get the socket that handles the client request.  
            Socket listener = (Socket)ar.AsyncState;
            Socket handler = listener.EndAccept(ar);

            // Create the state object.  
            StateObject state = new StateObject();
            state.workSocket = handler;
            clients.Add(state);
            UnityEngine.Debug.Log("Rcon client connected");
            handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                new AsyncCallback(ReadCallback), state);
        }

        private void ReadCallback(IAsyncResult ar)
        {
            string content = string.Empty;

            // Retrieve the state object and the handler socket  
            // from the asynchronous state object.  
            StateObject state = (StateObject)ar.AsyncState;
            Socket handler = state.workSocket;

            // Read data from the client socket.
            int bytesRead = handler.EndReceive(ar);
            int length = BitConverter.ToInt32(state.buffer, 0);
            int requestId = BitConverter.ToInt32(state.buffer, sizeof(int));
            int type = BitConverter.ToInt32(state.buffer, sizeof(int) * 2);
            length -= (sizeof(int) * 3) - 2;
            byte[] payload = new byte[length];
            for (int i = 0; i < length; i++)
            {
                payload[i] = state.buffer[(sizeof(int) * 3) + i];
            }

            OnMessage?.Invoke(handler, requestId, (PacketType)type, Encoding.ASCII.GetString(payload));

            // read another packet probably?
            state.buffer = new byte[StateObject.BufferSize]; // clear the buffer
            handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                new AsyncCallback(ReadCallback), state);
        }

        private void Send(Socket handler, string data)
        {
            // Convert the string data to byte data using ASCII encoding.  
            byte[] byteData = Encoding.ASCII.GetBytes(data);

            // Begin sending the data to the remote device.  
            handler.BeginSend(byteData, 0, byteData.Length, 0,
                new AsyncCallback(SendCallback), handler);
        }

        private void SendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.  
                Socket handler = (Socket)ar.AsyncState;

                // Complete sending the data to the remote device.  
                int bytesSent = handler.EndSend(ar);
                UnityEngine.Debug.Log($"Sent {bytesSent} bytes to client.");

                handler.Shutdown(SocketShutdown.Both);
                handler.Close();

            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError(e.ToString());
            }
        }

        internal void Close()
        {
            if (listener.Connected)
            {
                foreach (var client in clients)
                {
                    if (isConnected(client.workSocket))
                    {
                        client.workSocket.Close();
                    }
                }
                listener.Close();
            }

        }
    }
}