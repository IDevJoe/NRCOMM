using System.IO;
using System.Linq;
using NRLib.Packets.Attributes;

namespace NRLib.Packets
{
    [TcpOnly]
    [PacketHandler(PackType.TCP_S_APP_INSTANCE_REPLY)]
    public class TcpSAppInstanceReply : Packet
    {
        public byte[][] Instances { get; }
        public TcpSAppInstanceReply(Packet pa)
        {
            using(MemoryStream stream = new MemoryStream(pa.Data))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                byte len = reader.ReadByte();
                var x = new byte[len][];
                for (int i = 0; i < len; i++)
                {
                    x[i] = reader.ReadBytes(10);
                }

                Instances = x;
            }
        }

        public TcpSAppInstanceReply(byte[][] instances, uint nonce)
        {
            using(MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write((byte)instances.Length);
                for (var i = 0; i < instances.Length; i++)
                {
                    writer.Write(instances[i]);
                }

                Data = stream.ToArray();
            }

            PacketType = PackType.TCP_S_APP_INSTANCE_REPLY;
            Nonce = nonce;
        }
    }
}