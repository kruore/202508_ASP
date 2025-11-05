using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows_ServerTest.Logging;

namespace Windows_ServerTest.ServerCore
{
    public class GameSession : Session
    {
        public GameSession(ILogger logger)
            : base(logger)
        {
        }

        protected override void OnRecvPacket(byte[] packet)
        {
#if DEBUG
            // 패킷 처리 로직
            _logger.Info($"패킷 수신: {packet.Length} bytes");
#endif
        }

        protected override void OnDisconnected()
        {
#if DEBUG
            _logger.Info("클라이언트 연결 종료 처리 완료");
#endif
        }
    }

}
