using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NRLib
{
    /// <summary>
    /// Represents an NRLib stream
    /// </summary>
    public class NRStream : Stream
    {
        internal List<byte> Buffer = new List<byte>();
        internal event Send OnSend;

        internal AppConnection Connection;

        public int Available
        {
            get
            {
                return Buffer.Count;
            }
        }

        public delegate void Send(byte[] bytes);

        private CancellationTokenSource _cts = new CancellationTokenSource();

        public NRStream(AppConnection connection)
        {
            CanRead = true;
            CanWrite = true;
            CanSeek = false;
            Length = -1;
            Connection = connection;
            IsOpen = true;
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
            Buffer.Clear();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return Read(buffer, offset, count, CancellationToken.None);
        }

        private int Read(byte[] buffer, int offset, int count, CancellationToken token)
        {
            if (Buffer.Count < count)
            {
                Task.Run(() =>
                {
                    while (true)
                    {
                        if (Buffer.Count > 0) break;
                        if (token.IsCancellationRequested) break;
                        if (_cts.IsCancellationRequested) break;
                        Task.Delay(50).GetAwaiter().GetResult();
                    }
                }).GetAwaiter().GetResult();
            }

            if (token.IsCancellationRequested && Available == 0) return 0;
            if (_cts.IsCancellationRequested) throw new Exception("Socket was closed.");
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset + count > buffer.Length)
                throw new ArgumentException("The sum of offset and count is larger than the buffer length.");
            if (offset < 0 || count < 0)
                throw new ArgumentOutOfRangeException("buffer", "buffer or offset are negative");
            int read = 0;
            for (int i = offset; i < offset+count; i++)
            {
                if (Buffer.Count == 0) break;
                buffer[i] = Buffer[0];
                Buffer.RemoveAt(0);
                read++;
            }

            return read;
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

            OnSend?.Invoke(read);
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