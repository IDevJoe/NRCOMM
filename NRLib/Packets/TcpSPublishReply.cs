using System;
using System.IO;
using NRLib.Packets.Attributes;

namespace NRLib.Packets
{
    [TcpOnly]
    [PacketHandler(PackType.TCP_S_PUBLISH_REPLY)]
    public class TcpSPublishReply : Packet
    {
        public bool Success { get; }
        public byte[] AppId { get; }
        public byte[] InstanceId { get; }

        public string ReadableAppId
        {
            get
            {
                string s = "";
                foreach (var b in AppId)
                {
                    s += b.ToString("x2").ToUpper();
                }

                return s;
            }
        }
        
        public string ReadableInstanceId
        {
            get
            {
                string s = "";
                foreach (var b in InstanceId)
                {
                    s += b.ToString("x2").ToUpper();
                }

                return s;
            }
        }

        public TcpSPublishReply(Packet packet)
        {
            using(MemoryStream stream = new MemoryStream(packet.Data))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                Success = reader.ReadByte() == 1;
                AppId = reader.ReadBytes(10);
                InstanceId = reader.ReadBytes(10);
            }
        }

        public TcpSPublishReply(bool success, byte[] appId, byte[] instanceId, uint nonce)
        {
            if (appId.Length != 10) throw new ArgumentException("AppId is not 10 bytes");
            if (instanceId.Length != 10) throw new ArgumentException("InstanceId is not 10 bytes");
            Success = success;
            AppId = appId;
            InstanceId = instanceId;
            
            using(MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(Success ? (byte)1 : (byte)0);
                writer.Write(AppId);
                writer.Write(InstanceId);
                Data = stream.ToArray();
            }

            PacketType = PackType.TCP_S_PUBLISH_REPLY;
            Nonce = nonce;
        }
    }
}