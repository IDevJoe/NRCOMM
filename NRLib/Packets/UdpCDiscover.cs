using NRLib.Packets.Attributes;

namespace NRLib.Packets
{
    [UdpOnly]
    [PacketHandler(PackType.UDP_C_DISCOVER)]
    public class UdpCDiscover : Packet
    {
        public UdpCDiscover(uint nonce)
        {
            PacketType = PackType.UDP_C_DISCOVER;
            Nonce = nonce;
            Data = new byte[0];
        }
    }
}