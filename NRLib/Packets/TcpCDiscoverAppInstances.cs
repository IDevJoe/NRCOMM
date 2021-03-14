using System.IO;
using NRLib.Packets.Attributes;

namespace NRLib.Packets
{
    [PacketHandler(PackType.TCP_C_DISCOVER_APP_INSTANCES)]
    public class TcpCDiscoverAppInstances : Packet
    {
        public byte[] AppId { get; private set; }
        public TcpCDiscoverAppInstances(Packet pack)
        {
            using(MemoryStream stream = new MemoryStream(pack.Data))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                AppId = reader.ReadBytes(10);
            }
        }

        public TcpCDiscoverAppInstances(byte[] appId, uint nonce)
        {
            using(MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(appId);
                Data = stream.ToArray();
            }

            Nonce = nonce;
            PacketType = PackType.TCP_C_DISCOVER_APP_INSTANCES;
        }
    }
}