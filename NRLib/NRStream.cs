using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace NRLib
{
    /// <summary>
    /// Represents an NRLib stream
    /// </summary>
    public class NRStream : Stream
    {
        internal Pipe Pipeline;

        internal AppConnection Connection;

        private CancellationTokenSource _cts = new CancellationTokenSource();

        public NRStream(AppConnection connection)
        {
            CanRead = true;
            CanWrite = true;
            CanSeek = false;
            Length = -1;
            Connection = connection;
            IsOpen = true;
            Pipeline = new Pipe();
        }

        public override void Close()
        {
            _cts.Cancel();
            IsOpen = false;
            if(Connection.Open) Connection.Close().GetAwaiter().GetResult();
            base.Close();
            //Dispose();
        }

        public override void Flush()
        {
            Pipeline.Reset();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return Read(buffer, offset, count, CancellationToken.None);
        }

        private int Read(byte[] buffer, int offset, int count, CancellationToken token)
        {
            if (token.IsCancellationRequested) return 0;
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset + count > buffer.Length)
                throw new ArgumentException("The sum of offset and count is larger than the buffer length.");
            if (offset < 0 || count < 0)
                throw new ArgumentOutOfRangeException("buffer", "buffer or offset are negative");
            while (true)
            {
                ReadResult rr = new ReadResult();
                if (_cts.IsCancellationRequested) throw new Exception("Socket was closed.");
                if (token.IsCancellationRequested) return 0;
                bool success = Pipeline.Reader.TryRead(out rr);
                if (!success)
                {
                    Task.Delay(50).GetAwaiter().GetResult();
                    continue;
                }
                byte[] b = rr.Buffer.ToArray();


                if (b.Length > count)
                {
                    for (int i = 0; i < count; i++)
                    {
                        buffer[i] = b[i];
                    }
                    Pipeline.Reader.AdvanceTo(rr.Buffer.GetPosition(count));
                    return count;
                }
                for (int i = 0; i < b.Length; i++)
                {
                    buffer[i] = b[i];
                }
                Pipeline.Reader.AdvanceTo(rr.Buffer.End);

                return b.Length;
            }

            /*ReadResult res = Pipeline.Reader.ReadAsync(token).GetAwaiter().GetResult();
            if (res.IsCanceled) return 0;
            
            
            read = buffer.Length;
            if (DebugFiles)
                _readDebug.Write(buffer);
            /*for (int i = offset; i < offset+count; i++)
            {
                if (Buffer.Length == 0) break;
                buffer[i] = Buffer[0];
                Buffer.RemoveAt(0);
                read++;
                if (DebugFiles)
                    _readDebug.WriteByte(buffer[i]);
            }#1#
            if(DebugFiles)
                Log.Debug("Debug Files Read Range End: {End}", _readDebug.Position);*/

            return 0;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException("Seeking is not supported");
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException("Seeking is not supported");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset + count > buffer.Length)
                throw new ArgumentException("offset and count are greater than buffer length");
            if (offset < 0 || count < 0)
                throw new ArgumentException("offset or count is negative");
            byte[] read = new byte[count];
            for (int i = offset; i < offset + count; i++)
            {
                read[i - offset] = buffer[i];
            }

            Connection.SendRaw(read).GetAwaiter().GetResult();
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await Task.Run(() => Write(buffer, offset, count));
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return await Task.Run(() => Read(buffer, offset, count, cancellationToken));
        }

        public override bool CanRead { get; }
        public override bool CanSeek { get; }
        public override bool CanWrite { get; }
        public bool IsOpen { get; private set; }
        public override long Length { get; }
        public override long Position { get; set; }
    }
}