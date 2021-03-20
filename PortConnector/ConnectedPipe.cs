using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NRLib;
using Serilog;

namespace PortConnector
{
    public class ConnectedPipe
    {
        public TcpClient Stream1 { get; }
        public AppConnection Stream2 { get; }
        public ConnectedPipe(TcpClient stream1, AppConnection stream2)
        {
            Stream1 = stream1;
            Stream2 = stream2;
        }

        public async Task Run()
        {
            await Run(CancellationToken.None);
        }

        public async Task Run(CancellationToken token)
        {
            CancellationTokenSource src = new CancellationTokenSource();
            token.Register(() => src.Cancel());
            bool teardown = false;
            Log.Debug("Pipe launched");
            var netstr = Stream1.GetStream();
            var nrstr = Stream2.Stream;
            Task x = await Task.WhenAny(netstr.CopyToAsync(nrstr, src.Token), nrstr.CopyToAsync(netstr, src.Token));
            if(x.Exception != null)
                Log.Error(x.Exception, "exception");

            await Task.Run(async () =>
            {
                if(nrstr.IsOpen)
                    while (true)
                    {
                        if (!netstr.DataAvailable)
                        {
                            break;
                        }
                    }
            });
            
            src.Cancel();
            /*var t1 = Task.Run(async () =>
            {
                while (true)
                {
                    // NR to Net
                    try
                    {
                        bool read = await Convert(nrstr, netstr, src.Token);
                        if (!read) throw new Exception("Closed");
                    }
                    catch (Exception e)
                    {
                        Log.Error(e, "Exception while reading from NR");
                        src.Cancel();
                        break;
                    }
                }
            }, src.Token);
            var t2 = Task.Run(async () =>
            {
                while (true)
                {
                    // Net to NR
                    try
                    {
                        bool read = await Convert(netstr, nrstr, src.Token);
                        if (!read) throw new Exception("Closed");

                    }
                    catch (Exception e)
                    {
                        Log.Error(e, "Exception while reading from Net");
                        src.Cancel();
                        break;
                    }
                }
            }, src.Token);
            await t1;
            await t2;*/
            try
            {
                Log.Debug("Thread ended");
                netstr.Close();
                Stream1.Close();
                await Stream2.Close();
            }
            catch (Exception e)
            {
                Log.Error(e, "Error while cleaning up");
            }
        }

        private async Task<bool> Convert(Stream source, Stream dest, CancellationToken token)
        {
            byte[] buffer = new byte[1024 * 20];
            var x = await source.ReadAsync(buffer, 0, buffer.Length, token);
            if (x == 0) return false;
            await dest.WriteAsync(buffer, 0, x);
            return true;
        }
    }
}