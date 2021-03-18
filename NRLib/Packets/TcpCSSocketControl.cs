using System;
using System.Collections.Generic;
using System.IO;
using NRLib.Packets.Attributes;

namespace NRLib.Packets
{
    [PacketHandler(PackType.TCP_CS_SOCKET_CONTROL)]
    public class TcpCSSocketControl : Packet
    {
        public const byte OpenAck = 1 << 7;
        public const byte OpenRequest = 1 << 6;
        public const byte AcceptConnection = 1 << 5;
        public const byte RefuseConnection = 1 << 4;
        public const byte Ready = 1 << 3;
        public const byte Close = 1 << 2;

        public byte[] SocketId { get; }
        public byte Flags { get; }
        public byte[] InstanceId { get; }

        public string ReadableFlags
        {
            get
            {
                List<string> flags = new List<string>();
                if(CheckFlag(OpenAck)) flags.Add("OA");
                if(CheckFlag(OpenRequest)) flags.Add("OR");
                if(CheckFlag(AcceptConnection)) flags.Add("A");
                if(CheckFlag(RefuseConnection)) flags.Add("REF");
                if(CheckFlag(Ready)) flags.Add("REA");
                if(CheckFlag(Close)) flags.Add("C");
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
                if ((Flags & OpenRequest) == OpenRequest)
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
            if (socketId.Length != 10) throw new ArgumentException("SocketId is not 10 bytes");
            if (instanceId != null && instanceId.Length != 10)
                throw new ArgumentException("InstanceId is not 10 bytes.");
            using(MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(socketId);
                writer.Write(flags);
                if((flags & OpenRequest) == OpenRequest)
                    writer.Write(instanceId);
                Data = stream.ToArray();
            }

            Nonce = nonce;
            PacketType = PackType.TCP_CS_SOCKET_CONTROL;
        }
    }
}