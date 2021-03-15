using System.IO;
using NRLib.Packets.Attributes;

namespace NRLib.Packets
{
    [PacketHandler(PackType.TCP_C_OPEN_SOCKET)]
    public class TcpCOpenSocket : Packet
    {
        public byte[] InstanceId { get; }
        
        public TcpCOpenSocket(Packet packet)
        {
            using(MemoryStream stream = new MemoryStream(packet.Data))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                InstanceId = reader.ReadBytes(10);
            }
        }
        
        public TcpCOpenSocket(byte[] instanceId, uint nonce)
        {
            using(MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(instanceId);
                Data = stream.ToArray();
            }

            PacketType = PackType.TCP_C_OPEN_SOCKET;
            Nonce = nonce;
        }
    }
}