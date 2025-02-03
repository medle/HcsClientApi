
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hcs.ClientApi
{
    /// <summary>
    /// Поток байтов который является частью другого потока байтов.
    /// https://stackoverflow.com/questions/60592147/partial-stream-of-filestream
    /// </summary>
    public class HcsPartialStream : Stream
    {
        public Stream Stream { get; private set; }
        public long StreamStart { get; private set; }
        public long StreamLength { get; private set; }
        public long StreamEnd { get; private set; }

        public HcsPartialStream(Stream stream, long offset, long size)
        {
            Stream = stream;
            StreamStart = offset;
            StreamLength = size;
            StreamEnd = offset + size;
            stream.Seek(offset, SeekOrigin.Begin);
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => Math.Min((Stream.Length - StreamStart), StreamLength);

        public override long Position
        {
            get => Stream.Position - StreamStart;
            set => Stream.Position = StreamStart + value;
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var p = Stream.Position;
            if (p < StreamStart) {
                Stream.Position = StreamStart;
            }
            if (p > StreamEnd)//EOF
            {
                return 0;
            }
            if (p + count > StreamEnd) {
                count = (int)(StreamEnd - p);
            }
            return Stream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            //Seek will be complicated as there are three origin types.
            //you can do it yourself
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
    }
}
