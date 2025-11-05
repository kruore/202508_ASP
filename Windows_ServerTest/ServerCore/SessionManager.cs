using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Windows_ServerTest.ServerCore
{
    public class SessionManager
    {
        private readonly ConcurrentDictionary<long, Session> _sessions = new ConcurrentDictionary<long, Session>();
        private long _nextSessionId = 0;
        private readonly int _maxSessions;
        private long _totalRecvPackets = 0;
        private long _totalSendPackets = 0;
        private MemoryPool<SendBuffer> SendBufferPool = new MemoryPool<SendBuffer>();
        private MemoryPool<RecvBuffer> RecvBufferPool = new MemoryPool<RecvBuffer>();

        private void OnSessionDisconnected(Session s)
        {
            Remove(s);
        }

        public SessionManager(int maxSessions = 5000)
        {
            _maxSessions = maxSessions;
        }

        public bool TryAdd(Session session)
        {
            if (_sessions.Count >= _maxSessions)
                return false;

            long id = Interlocked.Increment(ref _nextSessionId);
            session.SessionID = id;
            session.Disconnected += OnSessionDisconnected;
            session._sendBuffer = SendBufferPool.Rent();
            session._recvBuffer = RecvBufferPool.Rent();
            session.PacketReceived += OnPacketReceived;
            session.PacketSent += OnPacketSent;
            return _sessions.TryAdd(id, session);
        }

        public void Remove(Session session)
        {
            session.PacketReceived -= OnPacketReceived;
            session.PacketSent -= OnPacketSent;
            session.Disconnected -= OnSessionDisconnected;
            SendBufferPool.Return(session._sendBuffer);
            RecvBufferPool.Return(session._recvBuffer);
            _sessions.TryRemove(session.SessionID, out _);
        }

        private void OnPacketReceived() => Interlocked.Increment(ref _totalRecvPackets);
        private void OnPacketSent() => Interlocked.Increment(ref _totalSendPackets);

        public int CurrentCount => _sessions.Count;
        public int MaxSessions => _maxSessions;
        public long TotalRecvPackets => _totalRecvPackets;
        public long TotalSendPackets => _totalSendPackets;
    }

}
