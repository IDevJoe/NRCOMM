using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NREP.Managers;
using NRLib;
using Serilog;
using Serilog.Events;

namespace NREP
{
    class NREP
    {
        public const double VERSION = 1.0;
        public static NREPConfiguration Config;
        static void Main(string[] args)
        {
            if(!File.Exists("config.json")) File.Copy("config.default.json", "config.json");
            Console.WriteLine("Loading Configuration");
            ReloadConfiguration();
            Console.WriteLine("Starting Logger.");
            using var log = new LoggerConfiguration().WriteTo.Console().MinimumLevel.Is((LogEventLevel)Enum.Parse(typeof(LogEventLevel), Config.MinLogLevel, true)).CreateLogger();
            Log.Logger = log;
            Log.Debug("Minimum log level is DEBUG. This may get spammy");
            Log.Information("Node Router Entry Point v{Version:0.00}", VERSION);
            Log.Information("---------------------------");
            if (Config.IsDefault)
            {
                LoudMessage("The configuration has default values. Unset 'default' in config.json to remove this message");
            }

            if (Config.X509 == null)
            {
                LoudMessage("No X509 Certificate is loaded. This entry point will transmit information insecurely. DO NOT USE IN PRODUCTION.");
            } else if (Config.CA == null)
            {
                LoudMessage("No CA is defined. Identities of clients will not be verified. DO NOT USE IN PRODUCTION.");
            }
            SslManager.Initialize();
            Log.Information("Registering handlers");
            NRL.Initialize();
            Routines.RegisterRoutines();
            Log.Information("Starting TCP server");
            TcpManager.StartTCP();
            Log.Information("Starting UDP Listen on 2888");
            UdpManager.StartUDP();
            Task.Delay(-1).GetAwaiter().GetResult();
        }

        public static void LoudMessage(string message)
        {
            Log.Warning("*****************************");
            Log.Warning(message);
            Log.Warning("*****************************");
        }

        public static NREPConfiguration ReloadConfiguration()
        {
            string read = File.ReadAllText("config.json");
            return Config = JsonConvert.DeserializeObject<NREPConfiguration>(read);
        }
    }
}
