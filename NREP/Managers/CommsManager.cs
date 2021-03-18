using System;
using System.Diagnostics;
using System.Reflection;
using NRLib;
using NRLib.Packets.Attributes;
using Serilog;

namespace NREP.Managers
{
    public class CommsManager
    {
        public static void Execute(Packet packet)
        {
            Log.Debug("Beginning execution of packet {Packet}", packet.PacketId);
            Type typ = null;
            bool success = PacketHandler.Associations.TryGetValue(packet.PacketType, out typ);
            if (!success)
            {
                Log.Error("Packet {Packet} failed processing as no handler is defined for packet type {PType}", packet.PacketId, packet.PacketType);
                return;
            }

            bool requireTcp = typ.GetCustomAttribute(typeof(TcpOnly)) != null;
            bool requireUdp = typ.GetCustomAttribute(typeof(UdpOnly)) != null;

            if (requireTcp && packet.TransType != Packet.TransportType.TCP)
            {
                Log.Error("Packet {Packet} failed processing as TCP is required, but {Type} was used", packet.PacketId, packet.TransType);
                return;
            } if (requireUdp && packet.TransType != Packet.TransportType.UDP)
            {
                Log.Error("Packet {Packet} failed processing as UDP is required, but {Type} was used", packet.PacketId, packet.TransType);
                return;
            }
            
            Stopwatch watch = new Stopwatch();
            watch.Start();
            packet.Handle();
            watch.Stop();
            Log.Debug("Packet {Packet} completed execution in {Time:000}ms using handler {Handler}", packet.PacketId, watch.ElapsedMilliseconds, typ);
        }
    }
}