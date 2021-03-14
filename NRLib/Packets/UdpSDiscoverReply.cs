using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using NRLib.Packets.Attributes;

namespace NRLib.Packets
{
    [UdpOnly]
    [PacketHandler(PackType.UDP_S_DISCOVER_REPLY)]
    public class UdpSDiscoverReply : Packet
    {
        public uint PortNumber { get; }
        public X509Certificate2 Certificate { get; }
        public UdpSDiscoverReply(Packet packet)
        {
            using(MemoryStream str = new MemoryStream(packet.Data))
            using (BinaryReader reader = new BinaryReader(str))
            {
                PortNumber = reader.ReadUInt32();
                var certLen = reader.ReadUInt32();
                if (certLen > 0)
                {
                    byte[] cert = reader.ReadBytes((int)certLen);
                    Certificate = new X509Certificate2(cert);
                }
            }
        }
        public UdpSDiscoverReply(uint portNum, uint nonce, X509Certificate2 certificate)
        {
            PacketType = PackType.UDP_S_DISCOVER_REPLY;
            Nonce = nonce;
            using(MemoryStream str = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(str))
            {
                writer.Write(portNum);
                byte[] cert = new byte[0];
                if (certificate != null)
                {
                    cert = certificate.GetRawCertData();
                }
                writer.Write((uint)cert.Length);
                writer.Write(cert);
                Data = str.ToArray();
            }
        }
    }
}