using System.Net.Sockets;

namespace rcon.Internal
{
    internal class StateObject
    {
        // Size of receive buffer.  
        internal const int BufferSize = 4096;

        // Receive buffer.  
        internal byte[] buffer = new byte[BufferSize];

        // Client socket.
        internal Socket workSocket = null;
    }
}