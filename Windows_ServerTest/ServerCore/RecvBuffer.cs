using System;

namespace Windows_ServerTest.ServerCore
{
    public class RecvBuffer
    {
        private byte[] _buffer;
        private int _readPos;   // 읽기 시작 위치
        private int _writePos;  // 쓰기 시작 위치
        private int _capacity;

        public RecvBuffer()
        {
            _buffer = new byte[4096];
            _capacity = 4096;
        }

        public int DataSize
        {
            get
            {
                if (_writePos >= _readPos)
                    return _writePos - _readPos;
                return _capacity - _readPos + _writePos;
            }
        }
        public int FreeSize
        {
            get { return _capacity - DataSize - 1; }
        }

        public ArraySegment<byte> ReadSegment
        {
            get
            {
                if (_writePos >= _readPos)
                {
                    return new ArraySegment<byte>(_buffer, _readPos, _writePos - _readPos);
                }
                else
                {
                    return new ArraySegment<byte>(_buffer, _readPos, _capacity - _readPos);
                }
            }
        }

        /// <summary>
        /// 쓸 수 있는 구간 반환 (tail~끝)
        /// </summary>
        public ArraySegment<byte> WriteSegment
        {
            get
            {
                if (_writePos >= _readPos)
                {

                    if (_readPos == 0)
                        return new ArraySegment<byte>(_buffer, _writePos, _capacity - _writePos - 1);
                    else
                        return new ArraySegment<byte>(_buffer, _writePos, _capacity - _writePos);
                }
                else
                {
                    return new ArraySegment<byte>(_buffer, _writePos, _readPos - _writePos - 1);
                }
            }
        }

        public void OnWrite(int bytes)
        {
            if (bytes < 0 || bytes > FreeSize)
                throw new ArgumentOutOfRangeException("bytes");

            _writePos = (_writePos + bytes) % _capacity;
        }
        public void OnRead(int bytes)
        {
            if (bytes < 0 || bytes > DataSize)
                throw new ArgumentOutOfRangeException("bytes");

            _readPos = (_readPos + bytes) % _capacity;
        }
        public void Clear()
        {
            _readPos = 0;
            _writePos = 0;
        }
    }
}
