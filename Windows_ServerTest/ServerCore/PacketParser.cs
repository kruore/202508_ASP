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

            // 1️ 헤더(2바이트) 읽기
            if (_buffer.DataSize < 2)
                return false;

            ushort packetSize = ReadUInt16();

            // 2️ 전체 패킷 크기 확인
            if (_buffer.DataSize < packetSize)
                return false;

            // 3️ 패킷 데이터 읽기 (wrap-around 처리)
            packet = new byte[packetSize - 2];
            _buffer.OnRead(2); // 헤더 제거

            ReadBytes(packet, 0, packetSize - 2);
            _buffer.OnRead(packetSize - 2);

            return true;
        }

        /// <summary>
        /// 링 버퍼에서 2바이트 읽기 (wrap-around 처리)
        /// </summary>
        private ushort ReadUInt16()
        {
            var segment = _buffer.ReadSegment;

            if (segment.Count >= 2)
            {
                // 연속된 2바이트 읽기
                return BitConverter.ToUInt16(segment.Array, segment.Offset);
            }
            else
            {
                // wrap-around: 1바이트씩 읽기
                byte[] temp = new byte[2];
                temp[0] = segment.Array[segment.Offset];

                // 버퍼 끝에서 시작으로 넘어감
                temp[1] = _buffer.ReadSegment.Array[0];

                return BitConverter.ToUInt16(temp, 0);
            }
        }

        /// <summary>
        /// 링 버퍼에서 N바이트 읽기 (wrap-around 처리)
        /// </summary>
        private void ReadBytes(byte[] destination, int destOffset, int count)
        {
            int remaining = count;
            int destPos = destOffset;

            while (remaining > 0)
            {
                var segment = _buffer.ReadSegment;

                if (segment.Count == 0)
                    throw new InvalidOperationException("버퍼에 데이터가 부족합니다.");

                int copySize = Math.Min(segment.Count, remaining);

                Array.Copy(
                    segment.Array,
                    segment.Offset,
                    destination,
                    destPos,
                    copySize
                );

                destPos += copySize;
                remaining -= copySize;
            }
        }
    }
}
