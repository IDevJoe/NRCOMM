using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace NRLib
{
    /// <summary>
    /// Represents an NRLib stream
    /// </summary>
    public class NRStream : Stream
    {
        internal List<byte> _buffer = new List<byte>();
        internal event Send _onSend;

        public delegate void Send(byte[] bytes);

        public NRStream()
        {
            CanRead = true;
            CanWrite = true;
            CanSeek = false;
            Length = -1;
        }
        
        public override void Flush()
        {
            _buffer.Clear();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_buffer.Count < count)
            {
                Task.Run(() =>
                {
                    while (true)
                    {
                        if (_buffer.Count >= count) break;
                        Task.Delay(50).GetAwaiter().GetResult();
                    }
                }).GetAwaiter().GetResult();
            }
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset + count > buffer.Length)
                throw new ArgumentException("The sum of offset and count is larger than the buffer length.");
            if (offset < 0 || count < 0)
                throw new ArgumentOutOfRangeException("buffer", "buffer or offset are negative");
            int read = 0;
            for (int i = offset; i < offset+count; i++)
            {
                buffer[i] = _buffer[0];
                _buffer.RemoveAt(0);
                read++;
            }

            return read;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new System.NotSupportedException("Seeking is not supported");
        }

        public override void SetLength(long value)
        {
            throw new System.NotSupportedException("Seeking is not supported");
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

            _onSend?.Invoke(read);
        }

        public override bool CanRead { get; }
        public override bool CanSeek { get; }
        public override bool CanWrite { get; }
        public override long Length { get; }
        public override long Position { get; set; }
    }
}