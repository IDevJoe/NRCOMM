using System;
using System.IO;
using NRLib.Packets.Attributes;

namespace NRLib.Packets
{
    [PacketHandler(PackType.TCP_CS_SOCKET_DATA)]
    public class TcpCSSocketData : Packet
    {
        public byte[] SocketId { get; }
        public byte[] SocketData { get; }
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
            if (socketId.Length != 10) throw new ArgumentException("SocketId is not 10 bytes");
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