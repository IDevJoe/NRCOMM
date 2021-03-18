using NRLib.Packets.Attributes;

namespace NRLib.Packets
{
    [TcpOnly]
    [PacketHandler(PackType.TCP_S_HELLO)]
    public class TcpSHello : Packet
    {
        // ReSharper disable once UnusedParameter.Local
        public TcpSHello(Packet packet)
        {
            
        }

        public TcpSHello(uint nonce)
        {
            PacketType = PackType.TCP_S_HELLO;
            Data = new byte[0];
            Nonce = nonce;
        }
    }
}