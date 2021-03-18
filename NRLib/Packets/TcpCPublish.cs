using System.IO;
using System.Text;
using NRLib.Packets.Attributes;

namespace NRLib.Packets
{
    [PacketHandler(PackType.TCP_C_PUBLISH)]
    public class TcpCPublish : Packet
    {
        public string Description { get; }
        public TcpCPublish(Packet packet)
        {
            using(MemoryStream stream = new MemoryStream(packet.Data))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                uint len = reader.ReadUInt32();
                byte[] bytes = reader.ReadBytes((int) len);
                Description = Encoding.UTF8.GetString(bytes);
            }
        }

        public TcpCPublish(string description, uint nonce)
        {
            PacketType = PackType.TCP_C_PUBLISH;
            Nonce = nonce;
            using(MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                byte[] bytes = Encoding.UTF8.GetBytes(description);
                writer.Write((uint)bytes.Length);
                writer.Write(bytes);
                Data = stream.ToArray();
            }

            Description = description;
        }
    }
}