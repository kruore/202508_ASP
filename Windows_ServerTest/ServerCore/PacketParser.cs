using System;

namespace Windows_ServerTest.ServerCore
{
    public class PacketParser
    {
        private readonly RecvBuffer _buffer;

        public PacketParser(RecvBuffer buffer)
        {
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        }

        public bool TryParse(out byte[] packet)
        {
            packet = null;
            
            if (_buffer.DataSize < 2)
                return false;

            ushort packetSize = BitConverter.ToUInt16(_buffer.ReadSegment.Array, _buffer.ReadSegment.Offset);

            if (_buffer.DataSize < packetSize)
                return false;

            packet = new byte[packetSize - 2];
            Array.Copy(
                _buffer.ReadSegment.Array,
                _buffer.ReadSegment.Offset + 2,
                packet,
                0,
                packetSize - 2
            );

            _buffer.OnRead(packetSize);

            return true;
        }
    }
}
