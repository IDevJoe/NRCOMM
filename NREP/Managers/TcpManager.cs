using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using NRLib;
using NRLib.Packets;
using Serilog;

namespace NREP.Managers
{
    public class TcpManager
    {
        private static TcpListener _listener;
        public static int PortNumber { get; private set; }

        internal static Dictionary<Socket, List<AppManager.PublishedApp>> AppsByConnection =
            new Dictionary<Socket, List<AppManager.PublishedApp>>();
        public static void StartTCP()
        {
            Random rand = new Random();
            PortNumber = rand.Next(6000, 7000);
            _listener = new TcpListener(IPAddress.Any, PortNumber);
            _listener.Start();
            Task.Run(async () =>
            {
                try
                {
                    await TCPLoop();
                }
                catch (Exception e)
                {
                    Log.Fatal(e, "An error occurred while listening for connections");
                    Environment.Exit(-1);
                }
            });
        }

        private static async Task TCPLoop()
        {
            Log.Information("Now accepting TCP connections on {Port}", PortNumber);
            while (true)
            {
                Socket sock = await _listener.AcceptSocketAsync();
                Log.Debug("Accepted connection from {Address}", sock.RemoteEndPoint.ToString());
                AppsByConnection.Add(sock, new List<AppManager.PublishedApp>());
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await ConnectionHandler(sock);
                    }
                    catch (Exception e)
                    {
                        Log.Error(e, "An exception occurred while handling a TCP connection");
                        sock.Close();
                        foreach(AppManager.PublishedApp app in AppsByConnection[sock])
                        {
                            AppManager.PublishedApps.Remove(app);
                            Log.Debug("Removed app {App} ({AppID} - {InstId}) during cleanup operation", app.Description, app.AppId, app.InstanceId);
                        }

                        AppsByConnection.Remove(sock);
                    }
                });
            }
        }

        public static async Task ConnectionHandler(Socket sock)
        {
            TcpConnection conn = new TcpConnection()
            {
                Socket = sock
            };
            byte[] hello = new TcpSHello(0).Build();
            using (NetworkStream str = new NetworkStream(sock))
            {
                if (SslManager.Certificate == null)
                {
                    conn.Stream = str;
                    str.Write(hello, 0, hello.Length);
                    // Insecure Mode
                    while (true)
                    {
                        Packet pack = str.NextPacket(conn);
                        CommsManager.Execute(pack);
                    }
                }
                else
                {
                    using (SslStream str2 = new SslStream(str))
                    {
                        conn.Stream = str2;
                        SslServerAuthenticationOptions ao = new SslServerAuthenticationOptions()
                        {
                            ServerCertificate = SslManager.Certificate,
                            ClientCertificateRequired = true,
                            RemoteCertificateValidationCallback = RemoteCertificateValidationCallback,
                            CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                            EnabledSslProtocols = SslProtocols.Tls11 | SslProtocols.Tls12 | SslProtocols.Tls13
                        };
                        await str2.AuthenticateAsServerAsync(ao);
                        if (!str2.IsMutuallyAuthenticated && SslManager.CACertificate != null)
                        {
                            Log.Warning("Connection {IP} failed to authenticate in time", sock.RemoteEndPoint);
                            sock.Close();
                            return;
                        }
                        else
                        {
                            Log.Information("{Client} established a connection successfully", str2.RemoteCertificate?.Subject);
                        }
                        str2.Write(hello, 0, hello.Length);

                        while (true)
                        {
                            Packet pack = str2.NextPacket(conn);
                            CommsManager.Execute(pack);
                        }
                    }
                }
            }
            
        }

        private static bool RemoteCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslpolicyerrors)
        {
            if (SslManager.CACertificate == null) return true;
            if (certificate == null) return false;
            /*bool s1 = chain.Build((X509Certificate2) certificate);
            if (!s1)
            {
                Log.Error("SSL Validation failed for client: {@Errors}", chain.ChainStatus);
                return false;
            }*/

            bool s2 = SslManager.ValidateAgainstCA((X509Certificate2) certificate);
            if (!s2)
            {
                Log.Error("SSL Validation failed for client against master CA");
                return false;
            }

            return true;
        }
    }
}