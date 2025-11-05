using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows_ServerTest.Logging;

namespace Windows_ServerTest.ServerCore
{
    public class EchoSession : Session
    {

        public EchoSession(ILogger logger)
           : base(logger)
        {
        }

        protected override void OnRecvPacket(byte[] packet)
        {
            string msg = Encoding.UTF8.GetString(packet);
#if DEBUG
            _logger.Info($"📩 수신: {msg}");
#endif
            SendAsync(Encoding.UTF8.GetBytes("서버 응답: " + msg));
        }

        protected override void OnDisconnected()
        {
#if DEBUG
            _logger.Info("❌ 세션 종료");
#endif
        }
    }

}
