using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace rcon.Internal;

internal class AsynchronousSocketListener(IPAddress ipAddress, int port)
{
    internal delegate void MessageReceived(Socket socket, int requestId, PacketType type, string payload);
    internal event MessageReceived? OnMessage;

    // Create a TCP/IP socket.  
    private readonly Socket _listener = new(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
    private readonly List<StateObject> _clients = [];
    
    internal void StartListening()
    {
        // Bind the socket to the local endpoint and listen for incoming connections.  
        try
        {
            var localEndPoint = new IPEndPoint(ipAddress, port);

            _listener.Bind(localEndPoint);
            _listener.Listen(100);
            
            // start listening for the first client
            _listener.BeginAccept(AcceptCallback, _listener);
        }
        catch (Exception e)
        {
            Debug.LogError(e.ToString());
        }
    }
    
    private bool IsConnected(Socket? c)
    {
        try
        {
            if (c is { Connected: true })
            {
                if (c.Poll(0, SelectMode.SelectRead))
                {
                    return c.Receive(new byte[1], SocketFlags.Peek) != 0;
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

        // copy the original list to an array to prevent errors when removing from the original list.
        foreach (var state in _clients.ToArray()) 
            if (!IsConnected(state.WorkSocket))
            {
                Debug.Log("Rcon client disconnected");
                state.WorkSocket?.Close();
                _clients.Remove(state);
            }
        
        // memory leak when called inside the Update loop, instead call it after AcceptCallback
        //_listener?.BeginAccept(AcceptCallback, _listener);
    }

    internal void AcceptCallback(IAsyncResult ar)
    {
        // Signal the main thread to continue.  
        //allDone.Set();

        // Get the socket that handles the client request.  
        var listener = (Socket)ar.AsyncState;
        var handler = listener.EndAccept(ar);

        // Create the state object.  
        var state = new StateObject
        {
            WorkSocket = handler
        };
        _clients.Add(state);
        Debug.Log("Rcon client connected");
        handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, ReadCallback, state);
        
        // start listening for the next client
        _listener.BeginAccept(AcceptCallback, _listener);
    }

    private void ReadCallback(IAsyncResult ar)
    {
        // Retrieve the state object and the handler socket  
        // from the asynchronous state object.  
        var state = (StateObject)ar.AsyncState;
        var handler = state.WorkSocket;

        if (handler == null)
            return;

        // Read data from the client socket.
        int bytesRead = handler.EndReceive(ar);
        int length = BitConverter.ToInt32(state.buffer, 0);
        int requestId = BitConverter.ToInt32(state.buffer, sizeof(int));
        int type = BitConverter.ToInt32(state.buffer, sizeof(int) * 2);
        length -= sizeof(int) * 3 - 2;
        byte[] payload = new byte[length];
        for (var i = 0; i < length; i++)
        {
            payload[i] = state.buffer[(sizeof(int) * 3) + i];
        }

        OnMessage?.Invoke(handler, requestId, (PacketType)type, Encoding.ASCII.GetString(payload));

        // read another packet probably?
        state.buffer = new byte[StateObject.BufferSize]; // clear the buffer
        handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, ReadCallback, state);
    }

    private void Send(Socket handler, string data)
    {
        // Convert the string data to byte data using ASCII encoding.  
        byte[] byteData = Encoding.ASCII.GetBytes(data);

        // Begin sending the data to the remote device.  
        handler.BeginSend(byteData, 0, byteData.Length, 0, SendCallback, handler);
    }

    private void SendCallback(IAsyncResult ar)
    {
        try
        {
            // Retrieve the socket from the state object.  
            Socket handler = (Socket)ar.AsyncState;

            // Complete sending the data to the remote device.  
            int bytesSent = handler.EndSend(ar);
            Debug.Log($"Sent {bytesSent} bytes to client.");

            handler.Shutdown(SocketShutdown.Both);
            handler.Close();

        }
        catch (Exception e)
        {
            Debug.LogError(e.ToString());
        }
    }

    internal void Close()
    {
        if (_listener is { Connected: true })
        {
            foreach (var client in _clients)
            {
                if (IsConnected(client.WorkSocket))
                {
                    client.WorkSocket?.Close();
                }
            }
            _listener.Close();
        }

    }
}