using System;
using System.IO;
using System.IO.Pipelines;
using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Mono.Options;
using NRLib;
using Serilog;
using Serilog.Events;

namespace PortConnector
{
    /**
     * This project is an example of what you can do with NRCOMM.
     * This example should not be considered reliable, as some packets
     * may arrive at the incorrect time, or not at all. However,
     * a reliable version can be made from this code if you wish to
     * tackle something like that.
     */
    class Program
    {
        internal EntryPoint EntryP;

        public static bool Publish { get; private set; }
        public static int Port { get; private set; }
        public static string AppName { get; private set; }
        public static string Host { get; private set; }

        static void Main(string[] args)
        {
            var verbosity = LogEventLevel.Information;
            bool showHelp = false;
            Host = "127.0.0.1";
            var options = new OptionSet()
            {
                {"publish", "publishes the app to the entry point", p => Publish = p != null},
                { "a|app=", "the app name [required]", n => AppName = n }, 
                { "p|port=", "the service port number [required]", (int r) => Port = r },
                { "ip=", "the ip address to connect to, defaults to 127.0.0.1", (string r) => Host = r }, 
                { "d", "show debug messages", v => { if (v != null) verbosity = LogEventLevel.Debug; } }, 
                { "h|help", "show this message and exit", h => showHelp = h != null },
            };
            options.Parse(args);
            if (AppName == null || Port == default || showHelp)
            {
                Console.WriteLine("Usage: PortConnector [--publish] [-a|-p|-d|-h]");
                using (var wr = new StringWriter())
                {
                    options.WriteOptionDescriptions(wr);
                    Console.WriteLine(wr.ToString());
                }

                return;
            }
            Log.Logger = new LoggerConfiguration().WriteTo.Console().MinimumLevel.Is(verbosity).CreateLogger();
            NRL.Initialize();
            new Program().Begin().GetAwaiter().GetResult();
        }

        public async Task Begin()
        {
            EntryPoint[] points = await NRL.FindEntryPoints();
            if (points.Length == 0)
            {
                Log.Error("Unable to locate an available entry point");
                return;
            }

            EntryP = points[0];
            Log.Information("Connecting to entry point {Id}", EntryP.Certificate.Subject);
            Packet.StorePacketRoutine(PackType.TCP_S_HELLO, OnConnect);
            Packet.StorePacketRoutine(PackType.TCP_CS_SOCKET_DATA, EntryPoint.StandardDataHandler);
            Packet.StorePacketRoutine(PackType.TCP_CS_SOCKET_CONTROL, EntryPoint.StandardSocketControlHandler);
            await EntryP.Connect(null);
            await Task.Delay(-1);
        }

        public async Task OnConnect(Packet packet)
        {
            if (Publish)
            {
                await EntryP.Publish(AppName, AppConnect);
            }
            else
            {
                _ = TcpListen();
            }
        }

        public async Task AppConnect(AppConnection connection)
        {
            TcpClient client = new TcpClient();
            try
            {
                Log.Debug("Attempting connect");
                await client.ConnectAsync(Host, Port);
            }
            catch (Exception e)
            {
                Log.Error(e, "Error while connecting to TCP socket");
                await connection.Refuse();
            }

            await connection.Accept();

            ConnectedPipe pipe = new ConnectedPipe(client, connection);
            _ = Task.Run(async () =>
            {
                await pipe.Run();
                client.Close();
            });
        }

        public async Task TcpListen()
        {
            Random rand = new Random();
            TcpListener listener = new TcpListener(IPAddress.Loopback, Port);
            listener.Start();
            try
            {
                while (true)
                {
                    var apps = await EntryP.DiscoverApps(AppName);
                    if (apps.Length < 1)
                    {
                        Log.Error("No suitable app found on entry point");
                        break;
                    }

                    var app = apps[rand.Next(0, apps.Length-1)];
                    var s = await listener.AcceptTcpClientAsync();
                    var appConn = new AppConnection(app, EntryP);
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            
                            await appConn.Connect();
                            ConnectedPipe pipe = new ConnectedPipe(s, appConn);
                            _ = pipe.Run();
                        }
                        catch (Exception e)
                        {
                            Log.Error(e, "Error while handling");
                            s.Dispose();
                        }
                    });
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "Error while accepting");
            }
            Environment.Exit(-1);
        }
    }
}