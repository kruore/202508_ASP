using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using Windows_ServerTest.Logging;

namespace Windows_ServerTest.ServerCore
{
    public abstract class Session
    {
        public long SessionID;

        protected TcpClient _client;
        protected NetworkStream _stream;
        public RecvBuffer _recvBuffer;
        public SendBuffer _sendBuffer;
        protected PacketParser _parser;

        public event Action<Session> Disconnected;
        public event Action PacketReceived;
        public event Action PacketSent;

        protected void RaisePacketReceived() => PacketReceived?.Invoke();
        protected void RaisePacketSent() => PacketSent?.Invoke();

        protected readonly ILogger _logger;
        private bool _isConnected;

        public Session(ILogger logger)
        {
            _logger = logger;

        }


        protected virtual async Task SendAsync(byte[] data)
        {
            if (!_isConnected || data == null || data.Length == 0)
                return;

            try
            {
                await _stream.WriteAsync(data, 0, data.Length);
                RaisePacketSent();
            }
            catch (Exception ex)
            {
                _logger.Error("송신 중 오류", ex);
                Disconnect();
            }
        }

        /// <summary>
        /// 클라이언트 연결 초기화
        /// </summary>
        public virtual void Start(TcpClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _stream = client.GetStream();
            _isConnected = true;
            _parser = new PacketParser(_recvBuffer);
#if DEBUG
            _logger.Info($"🔌 클라이언트 접속: {client.Client.RemoteEndPoint}");
#endif

            // 비동기 수신 루프 시작
            Task.Run(() => StartRecv());
        }

        /// <summary>
        /// 비동기 수신 루프
        /// </summary>
        private async Task StartRecv()
        {
            try
            {
                while (_isConnected)
                {
                    // 빈 공간 가져오기
                    var segment = _recvBuffer.WriteSegment;
                    if (segment.Count == 0)
                    {
#if DEBUG
                        _logger.Error("RecvBuffer 공간 부족!");
#endif
                        Disconnect();
                        return;
                    }

                    // 비동기 수신
                    try
                    {
                        int recv = await _stream.ReadAsync(segment.Array, segment.Offset, segment.Count);
                        if (recv == 0)
                        {
#if DEBUG
                            _logger.Info("클라이언트 연결 종료 감지");
#endif
                            Disconnect();
                            break;
                        }
                        string msg = System.Text.Encoding.UTF8.GetString(segment.Array, segment.Offset, recv);
#if DEBUG
                        _logger.Info($"📩 수신 데이터: {msg}");
#endif
                        _recvBuffer.OnWrite(recv);
                    }
                    catch
                    {
#if DEBUG
                        _logger.Info("클라이언트 연결 종료 감지");
#endif
                        Disconnect();
                    }

                    while (_parser.TryParse(out var packet))
                    {
                        try
                        {
                            OnRecvPacket(packet);
                            RaisePacketReceived();
                        }
                        catch (Exception ex)
                        {
#if DEBUG
                            _logger.Error("패킷 처리 중 오류", ex);
#endif
                        }
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                // 스트림이 이미 닫혔을 경우 무시
            }
            catch (Exception ex)
            {
#if DEBUG
                _logger.Error("수신 루프 예외", ex);
#endif
            }
            finally
            {
                Disconnect();
            }
        }

        public virtual void Disconnect()
        {
            if (!_isConnected)
                return;

            _isConnected = false;

            try { _client.Client?.Shutdown(SocketShutdown.Both); } catch { }
            try { _stream?.Close(); } catch { }
            try { _client?.Close(); } catch { }
#if DEBUG
            _logger.Info($"❌ 세션 종료: {SessionID}");
#endif
            Disconnected?.Invoke(this);
            OnDisconnected();
        }



        /// <summary>
        /// 패킷 수신 시 호출
        /// </summary>
        protected abstract void OnRecvPacket(byte[] packet);

        /// <summary>
        /// 연결 종료 시 호출
        /// </summary>
        protected abstract void OnDisconnected();
    }
}
