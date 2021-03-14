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
                    AppManager.PublishedApps.Add(new AppManager.PublishedApp()
                    {
                        AppId = appId,
                        Connection = pack.Connection,
                        InstanceId = instId,
                        Description = ap.Description
                    });
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
    }
}