using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using NRLib.Packets;
using NRLib.Packets.Attributes;
using Serilog;

namespace NRLib
{
    public class NRL
    {
        private static bool _init = false;
        
        /// <summary>
        /// Initializes NRLib
        /// </summary>
        public static void Initialize()
        {
            if (_init) return;
            if (Log.Logger == null)
            {
                Log.Logger = new LoggerConfiguration().WriteTo.Console().MinimumLevel.Debug().CreateLogger();
            }
            PacketHandler.Associate();

            _init = true;
        }

        public delegate void EntryPointCallback(EntryPoint ep);

        /// <summary>
        /// Finds entry points on the local network
        /// </summary>
        /// <param name="callback">A callback to be called when new entry points are discovered, nullable</param>
        /// <returns>A list of entry points</returns>
        public static async Task<EntryPoint[]> FindEntryPoints(EntryPointCallback callback = null)
        {
            Initialize();
            Random rand = new Random();
            using (UdpClient cl = new UdpClient())
            {
                uint nonce = (uint)rand.Next(0, Int32.MaxValue);
                byte[] bc = new UdpCDiscover(nonce).Build();
                await cl.SendAsync(bc, bc.Length, new IPEndPoint(IPAddress.Broadcast, 2888));

                List<EntryPoint> epl = new List<EntryPoint>();
                while (true)
                {
                    var ar = cl.BeginReceive(null, null);
                    ar.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(500));
                    if (!ar.IsCompleted) break;
                    try
                    {
                        IPEndPoint ipep = null;
                        byte[] res = cl.EndReceive(ar, ref ipep);
                        Packet thisPack = new Packet(res, ipep);
                        if (thisPack.Nonce != nonce) continue;
                        if (thisPack.PacketType != PackType.UDP_S_DISCOVER_REPLY) continue;
                        UdpSDiscoverReply rep = new UdpSDiscoverReply(thisPack);
                        var ep = new EntryPoint(new IPEndPoint(ipep.Address, (int)rep.PortNumber), rep.Certificate);
                        if(callback != null) callback(ep);
                        epl.Add(ep);
                    }
                    catch (Exception e)
                    {
                        Console.Error.WriteLine(e);
                        break;
                    }
                }

                return epl.ToArray();
            }
        }
    }
}