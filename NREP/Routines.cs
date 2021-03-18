using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
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
            Packet.StorePacketRoutine(PackType.TCP_CS_SOCKET_CONTROL, ConnectionControl);
            Packet.StorePacketRoutine(PackType.TCP_CS_SOCKET_DATA, SocketData);
        }

        public static async Task UdpCDiscover(Packet pack)
        {
            byte[] ts = new UdpSDiscoverReply((uint)TcpManager.PortNumber, pack.Nonce, SslManager.Certificate).Build();
            await UdpManager.Transmit(pack.ClientAddress, ts);
        }

        public static async Task Publish(Packet pack)
        {
            var ap = new TcpCPublish(pack);
            byte[] reply = new byte[0];
            try
            {
                using (SHA1 sha1 = SHA1.Create())
                {
                    byte[] apByt = Encoding.UTF8.GetBytes(ap.Description);
                    byte[] appId = sha1.ComputeHash(apByt).Take(10).ToArray();
                    byte[] instByt = Encoding.UTF8.GetBytes(pack.Connection.Socket.RemoteEndPoint + "-" + ap);
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
            await pack.Connection.Stream.WriteAsync(reply);
        }

        public static async Task DiscoverApp(Packet packet)
        {
            var ap = new TcpCDiscoverAppInstances(packet);
            var apps = AppManager.PublishedApps.FindAll(x => x.AppId.SequenceEqual(ap.AppId));
            var mapped = new byte[apps.Count][];
            for (var i = 0; i < apps.Count; i++)
            {
                mapped[i] = apps[i].InstanceId;
            }

            var reply = new TcpSAppInstanceReply(mapped, packet.Nonce);
            await packet.Connection.Stream.WriteAsync(reply.Build());
        }

        public static async Task OpenSocket(Packet packet)
        {
            var ap = new TcpCOpenSocket(packet);
            var app = AppManager.PublishedApps.FirstOrDefault(x => x.InstanceId.SequenceEqual(ap.InstanceId));
            var reply = new byte[0];
            if (app == null)
            {
                reply = new TcpCSSocketControl(new byte[10],
                    TcpCSSocketControl.OpenAck | TcpCSSocketControl.Close, packet.Nonce).Build();
            }
            else
            {
                AppConnection connection = new AppConnection(packet.Connection, app);
                await connection.SendInitialState(packet);
            }

            await packet.Connection.Stream.WriteAsync(reply);
        }

        public static async Task ConnectionControl(Packet packet)
        {
            TcpCSSocketControl control = new TcpCSSocketControl(packet);
            var conn = AppConnection.Connections[AppConnection.IdToString(control.SocketId)];
            await conn.ProcessControl(packet);
        }

        public static async Task SocketData(Packet packet)
        {
            TcpCSSocketData data = new TcpCSSocketData(packet);
            var conn = AppConnection.Connections[AppConnection.IdToString(data.SocketId)];
            await conn.Send(packet.Connection.Socket, data.SocketData);
        }
    }
}