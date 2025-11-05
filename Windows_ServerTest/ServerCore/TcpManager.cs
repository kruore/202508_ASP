using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows_ServerTest.Logging;

namespace Windows_ServerTest.ServerCore
{
    public class TcpManager
    {
        private TcpListener _listener;
        private CancellationTokenSource _cts;
        private Task _acceptLoopTask;
        private readonly SessionManager _sessionManager = new SessionManager(5000);
        public SessionManager SessionManager => _sessionManager;

        private readonly ILogger _logger;

        public bool IsRunning { get; private set; }

        public TcpManager(ILogger logger)
        {
            _logger = logger;
        }

        public Task StartAsync(int port)
        {
            if (IsRunning)
            {
                _logger.Info("이미 서버가 실행 중입니다.");
                return Task.CompletedTask;
            }

            _cts = new CancellationTokenSource();
            CancellationToken token = _cts.Token;

            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();
            IsRunning = true;

            _logger.Info(string.Format("✅ TCP SERVER 가 {0} 포트에서 실행 중입니다.", port));

            _acceptLoopTask = AcceptLoopAsync(token);

            return Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            if (!IsRunning)
            {
                _logger.Info("서버가 이미 중지 상태입니다.");
                return;
            }

            _logger.Info("⛔ 서버 중지 요청");

            try
            {
                if (_cts != null)
                    _cts.Cancel();
            }
            catch { }

            try
            {
                if (_listener != null)
                    _listener.Stop();
            }
            catch { }

            if (_acceptLoopTask != null)
            {
                try
                {
                    await _acceptLoopTask;
                }
                catch (Exception ex)
                {
                    _logger.Error("Accept 루프 종료 중 오류", ex);
                }
            }

            IsRunning = false;
            _logger.Info("✅ 서버 완전 종료");
        }

        private async Task AcceptLoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    Task<TcpClient> acceptTask = _listener.AcceptTcpClientAsync();
                    Task delayTask = Task.Delay(Timeout.Infinite, token);

                    Task completed = await Task.WhenAny(acceptTask, delayTask).ConfigureAwait(false);

                    if (completed != acceptTask)
                    {
                        break;
                    }

                    TcpClient client = acceptTask.Result;
#if DEBUG
                    _logger.Info("🔌 클라이언트 접속");
#endif

                    // fire-and-forget
                    HandleClientAsync(client, token);
                }
            }
            catch (ObjectDisposedException)
            {

            }
            catch (OperationCanceledException)
            {
                _logger.Info("Accept 루프 취소됨");
            }
            catch (Exception ex)
            {
                _logger.Error("Accept 루프 예외", ex);
            }
            finally
            {
                try
                {
                    if (_listener != null)
                        _listener.Stop();
                }
                catch { }

                IsRunning = false;
                _logger.Info("⛔ Accept 루프 종료");
            }
        }


        // 유저 등록
        private async Task HandleClientAsync(TcpClient client, CancellationToken token)
        {
            try
            {
#if DEBUG
                _logger.Info("🔌 새 클라이언트 접속");
#endif

                Session session = new EchoSession(_logger);
                if(SessionManager.TryAdd(session))
                {
#if DEBUG
                    _logger?.Info($"HandleClientAsync 진입 - client={client != null}");
#endif
                    session.Start(client);
                }
                else
                {
                    client.Client.Shutdown(SocketShutdown.Both);
                    client.Close();
                }
       
            }
            catch (Exception ex)
            {
#if DEBUG
                _logger.Error("세션 생성 오류", ex);
#endif
            }
        }

    }
}
