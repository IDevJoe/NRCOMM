using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using NRLib;
using Serilog;

namespace NREP.Managers
{
    public class UdpManager
    {
        private static UdpClient _client;
        public static void StartUDP()
        {
            _client = new UdpClient(2888);
            UdpLoop();
        }

        public static void Transmit(IPEndPoint ep, byte[] bytes)
        {
            _client.Send(bytes, bytes.Length, ep);
            Log.Debug("Dispatched {Length} bytes to {Address} (Calculated ID {ID})", bytes.Length, ep, Packet.GenPID(bytes));
        }

        private static void UdpLoop()
        {
            Log.Information("UDP server started.");
            Task.Run(() =>
            {
                while (true)
                {
                    IPEndPoint source = null;
                    byte[] dgram = _client.Receive(ref source);
                    Packet pack = null;
                    try
                    {
                        pack = new Packet(dgram, source);
                        CommsManager.Execute(pack);
                    }
                    catch (Exception e)
                    {
                        Log.Warning(e, "Error while parsing a packet. Possibly corrupted in transport.");
                    }
                }
            });
        }
    }
}