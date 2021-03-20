using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Serilog;

namespace NRLib
{
    public class PacketWorker
    {
        public struct QueuedPacket
        {
            public bool Rx;
            public Packet Packet;
            public EntryPoint Entry;
        }
        internal static List<QueuedPacket> Queue = new List<QueuedPacket>();

        internal static async Task Run()
        {
            try
            {
                while (true)
                {
                    if (Queue.Count == 0)
                    {
                        await Task.Delay(50);
                        continue;
                    }

                    try
                    {
                        if (Queue[0].Rx)
                        {
                            Log.Debug("Working on packet {Packet}", Queue[0].Packet.PacketId);
                            await Queue[0].Packet.ExecuteRoutine();
                        }
                        else
                        {
                            await Queue[0].Entry.TCPConnection.Stream.WriteAsync(Queue[0].Packet.Build());
                        }
                        Queue.RemoveAt(0);
                    }
                    catch (Exception e)
                    {
                        Log.Warning(e, "Error while processing routine");
                    }
                    
                }
                
            }
            catch (Exception e)
            {
                Log.Error(e, "Error in Packet Worker");
            }
        }
    }
}