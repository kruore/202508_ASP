using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;

namespace Windows_ServerTest.ServerCore
{
    public class SendBuffer
    {
        public byte[] Buffer { get; private set; }
        public int Length { get; private set; }

        public SendBuffer(byte[] buffer)
        {
            Buffer = buffer;
            Length = 0;
        }

        public SendBuffer() : this(new byte[4096]) { }

        public void Write(byte[] data, int offset, int count)
        {
            if (Length + count > Buffer.Length)
                throw new InvalidOperationException("Buffer overflow");

            Array.Copy(data, offset, Buffer, Length, count);
            Length += count;
        }

        public byte[] Read(int offset, int count)
        {
            if (offset + count > Length)
                throw new ArgumentOutOfRangeException();

            byte[] result = new byte[count];
            Array.Copy(Buffer, offset, result, 0, count);
            return result;
        }

        public void Clear()
        {
            Length = 0;
        }
    }
}