using System.Collections.Generic;
using System.IO;
using System.Linq;
using NRLib.Packets.Attributes;

namespace NRLib.Packets
{
    [PacketHandler(PackType.TCP_CS_SOCKET_CONTROL)]
    public class TcpCSSocketControl : Packet
    {
        public const byte OPEN_ACK = 1 << 7;
        public const byte OPEN_REQUEST = 1 << 6;
        public const byte ACCEPT_CONNECTION = 1 << 5;
        public const byte REFUSE_CONNECTION = 1 << 4;
        public const byte READY = 1 << 3;
        public const byte CLOSE = 1 << 2;

        public byte[] SocketId { get; }
        public byte Flags { get; }
        public byte[] InstanceId { get; }

        public string ReadableFlags
        {
            get
            {
                List<string> flags = new List<string>();
                if(CheckFlag(OPEN_ACK)) flags.Add("OA");
                if(CheckFlag(OPEN_REQUEST)) flags.Add("OR");
                if(CheckFlag(ACCEPT_CONNECTION)) flags.Add("A");
                if(CheckFlag(REFUSE_CONNECTION)) flags.Add("REF");
                if(CheckFlag(READY)) flags.Add("REA");
                if(CheckFlag(CLOSE)) flags.Add("C");
                return string.Join(',', flags);
            }
        }

        public TcpCSSocketControl(Packet packet)
        {
            using(MemoryStream stream = new MemoryStream(packet.Data))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                SocketId = reader.ReadBytes(10);
                Flags = reader.ReadByte();
                if ((Flags & OPEN_REQUEST) == OPEN_REQUEST)
                {
                    InstanceId = reader.ReadBytes(10);
                }
            }
        }

        public bool CheckFlag(byte flag)
        {
            return ((Flags & flag) == flag);
        }

        public TcpCSSocketControl(byte[] socketId, byte flags, uint nonce = 0, byte[] instanceId = null)
        {
            using(MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(socketId);
                writer.Write(flags);
                if((flags & OPEN_REQUEST) == OPEN_REQUEST)
                    writer.Write(instanceId);
                Data = stream.ToArray();
            }

            Nonce = nonce;
            PacketType = PackType.TCP_CS_SOCKET_CONTROL;
        }
    }
}