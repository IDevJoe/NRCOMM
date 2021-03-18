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
        public static async Task StartUDP()
        {
            _client = new UdpClient(2888);
            await UdpLoop();
        }

        public static async Task Transmit(IPEndPoint ep, byte[] bytes)
        {
            await _client.SendAsync(bytes, bytes.Length, ep);
            Log.Debug("Dispatched {Length} bytes to {Address} (Calculated ID {ID})", bytes.Length, ep, Packet.GenPID(bytes));
        }

        private static async Task UdpLoop()
        {
            Log.Information("UDP server started");
            while (true)
            {
                var udprr = await _client.ReceiveAsync();
                Packet pack = null;
                try
                {
                    pack = new Packet(udprr.Buffer, udprr.RemoteEndPoint);
                    CommsManager.Execute(pack);
                }
                catch (Exception e)
                {
                    Log.Warning(e, "Error while parsing a packet. Possibly corrupted in transport");
                }
            }
            // ReSharper disable once FunctionNeverReturns
        }
    }
}