using System.IO;
using NRLib.Packets.Attributes;

namespace NRLib.Packets
{
    [PacketHandler(PackType.TCP_CS_SOCKET_DATA)]
    public class TcpCSSocketData : Packet
    {
        public byte[] SocketId;
        public byte[] SocketData;
        public TcpCSSocketData(Packet packet)
        {
            using(MemoryStream stream = new MemoryStream(packet.Data))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                SocketId = reader.ReadBytes(10);
                uint len = reader.ReadUInt32();
                SocketData = reader.ReadBytes((int)len);
            }
        }

        public TcpCSSocketData(byte[] socketId, byte[] data)
        {
            using(MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(socketId);
                writer.Write((uint)data.Length);
                writer.Write(data);
                Data = stream.ToArray();
            }

            PacketType = PackType.TCP_CS_SOCKET_DATA;
            Nonce = 0;
        }
    }
}