using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NREP.Managers;
using NRLib;
using NRLib.Packets;
using Serilog;

namespace NREP
{
    public class Routines
    {
        public static void RegisterRoutines()
        {
            Packet.StorePacketRoutine(PackType.UDP_C_DISCOVER, UdpCDiscover);
            Packet.StorePacketRoutine(PackType.TCP_C_PUBLISH, Publish);
            Packet.StorePacketRoutine(PackType.TCP_C_DISCOVER_APP_INSTANCES, DiscoverApp);
            Packet.StorePacketRoutine(PackType.TCP_C_OPEN_SOCKET, OpenSocket);
        }

        public static void UdpCDiscover(Packet pack)
        {
            byte[] ts = new UdpSDiscoverReply((uint)TcpManager.PortNumber, pack.Nonce, SslManager.Certificate).Build();
            UdpManager.Transmit(pack.ClientAddress, ts);
        }

        public static void Publish(Packet pack)
        {
            var ap = new TcpCPublish(pack);
            byte[] reply = new byte[0];
            try
            {
                using (SHA1 sha1 = SHA1.Create())
                {
                    byte[] apByt = Encoding.UTF8.GetBytes(ap.Description);
                    byte[] appId = sha1.ComputeHash(apByt).Take(10).ToArray();
                    byte[] instByt = Encoding.UTF8.GetBytes(pack.Connection.Socket.RemoteEndPoint.ToString() + "-" + ap);
                    byte[] instId = sha1.ComputeHash(instByt).Take(10).ToArray();
                    var pa = new AppManager.PublishedApp()
                    {
                        AppId = appId,
                        Connection = pack.Connection,
                        InstanceId = instId,
                        Description = ap.Description
                    };
                    AppManager.PublishedApps.Add(pa);
                    TcpManager.AppsByConnection[pack.Connection.Socket].Add(pa);
                    reply = new TcpSPublishReply(true, appId, instId, pack.Nonce).Build();
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "Error while publishing an app");
                reply = new TcpSPublishReply(false, new byte[10], new byte[10], pack.Nonce).Build();
            }
            pack.Connection.Stream.Write(reply);
        }

        public static void DiscoverApp(Packet packet)
        {
            var ap = new TcpCDiscoverAppInstances(packet);
            var apps = AppManager.PublishedApps.FindAll(x => x.AppId.SequenceEqual(ap.AppId));
            var mapped = new byte[apps.Count][];
            for (var i = 0; i < apps.Count; i++)
            {
                mapped[i] = apps[i].InstanceId;
            }

            var reply = new TcpSAppInstanceReply(mapped, packet.Nonce);
            packet.Connection.Stream.Write(reply.Build());
        }

        public static void OpenSocket(Packet packet)
        {
            var ap = new TcpCOpenSocket(packet);
            var app = AppManager.PublishedApps.FirstOrDefault(x => x.InstanceId.SequenceEqual(ap.InstanceId));
            var reply = new byte[0];
            if (app == null)
            {
                reply = new TcpCSSocketControl(new byte[10],
                    TcpCSSocketControl.OPEN_ACK | TcpCSSocketControl.CLOSE, packet.Nonce).Build();
            }
            else
            {
                byte[] sockId = null;
                AppConnection connection = new AppConnection(packet.Connection, app);
                connection.SendInitialState(packet);
            }

            packet.Connection.Stream.Write(reply);
        }
    }
}