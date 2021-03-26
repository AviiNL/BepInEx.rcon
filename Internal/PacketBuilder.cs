namespace rcon.Internal
{
    internal static class PacketBuilder
    {

        internal static byte[] CreatePacket(int requestId, PacketType type, string payload)
        {
            int size = (sizeof(int) * 3) + payload.Length + 2;
            int response_length = (sizeof(int) * 2) + payload.Length + 2;

            byte[] packet = new byte[size];
            packet[0] = (byte)response_length;
            packet[sizeof(int)] = (byte)requestId;
            packet[sizeof(int) * 2] = (byte)type;

            for (int i = 0; i < payload.Length; i++)
            {
                packet[(sizeof(int) * 3) + i] = (byte)payload[i];
            }

            return packet;
        }

    }
}
