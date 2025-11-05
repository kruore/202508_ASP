using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Windows_ServerTest.Logging;
using Windows_ServerTest.ServerCore;

namespace Windows_ServerTest
{
    public partial class MainWindow : Window
    {
        private static readonly string LogPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logger", "logging.txt");

        private readonly ILogger _logger;
        private readonly TcpManager _tcpManager;
        private readonly DispatcherTimer _dashboardTimer;
        private DateTime _serverStartTime;

        public MainWindow()
        {
            InitializeComponent();

            // Logger 초기화
            try
            {
                string dir = Path.GetDirectoryName(LogPath);
                if (File.Exists(dir))
                    File.Delete(dir); // logger가 파일이면 삭제
                Directory.CreateDirectory(dir);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"로그 폴더 생성 실패: {ex.Message}");
            }
            _logger = new SafeLogger(LogPath);
            _logger.LogReceived += OnLogReceived;

            // TcpManager 생성
            _tcpManager = new TcpManager(_logger);

            // 상태 타이머 (1초마다 갱신)
            _dashboardTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _dashboardTimer.Tick += (s, e) => UpdateDashboard();
            _dashboardTimer.Start();

            // 버튼 초기 설정
            StopButton.Visibility = Visibility.Collapsed;

            // 초기 상태
            ServerStatusText.Text = "Stopped";
        }

        // 로그 수신 → UI 출력
        private void OnLogReceived(string line)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    LogTextBox.AppendText(line + Environment.NewLine);
                    LogTextBox.ScrollToEnd();
                }));
            }
            else
            {
                LogTextBox.AppendText(line + Environment.NewLine);
                LogTextBox.ScrollToEnd();
            }
        }

        // 서버 시작
        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (_tcpManager.IsRunning)
            {
                _logger.Info("이미 서버가 실행 중입니다.");
                return;
            }

            StartButton.Visibility = Visibility.Collapsed;
            StopButton.Visibility = Visibility.Visible;

            try
            {
                _logger.Info("-- 서버 시작 버튼 --");
                await _tcpManager.StartAsync(9000);
                _serverStartTime = DateTime.Now;

                ServerStatusText.Text = "Running";
            }
            catch (Exception ex)
            {
                _logger.Error("서버 시작 중 오류", ex);
                MessageBox.Show($"서버 시작 오류: {ex.Message}");
                StartButton.Visibility = Visibility.Visible;
                StopButton.Visibility = Visibility.Collapsed;

                ServerStatusText.Text = "Stopped";

            }
        }

        // 서버 정지
        private async void StopButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_tcpManager.IsRunning)
            {
                _logger.Info("이미 서버가 중지 상태입니다.");
                return;
            }

            StopButton.IsEnabled = false;

            try
            {
                _logger.Info("-- 서버 정지 버튼 --");
                await _tcpManager.StopAsync();

                ServerStatusText.Text = "Stopped";
            }
            catch (Exception ex)
            {
                _logger.Error("서버 정지 중 오류", ex);
                MessageBox.Show($"서버 정지 오류: {ex.Message}");
            }
            finally
            {
                StopButton.IsEnabled = true;
                StopButton.Visibility = Visibility.Collapsed;
                StartButton.Visibility = Visibility.Visible;
            }
        }

        //  창 닫기 시 Logger Dispose
        private void Window_Closed(object sender, EventArgs e)
        {
            if (_logger is IDisposable d)
                d.Dispose();
        }

        //  대시보드 갱신 루프
        private void UpdateDashboard()
        {
            if (_tcpManager?.SessionManager == null)
                return;

            var sm = _tcpManager.SessionManager;

            // 세션 수
            SessionCountText.Text = $"{sm.CurrentCount} / {sm.MaxSessions}";

            // 패킷 수
            RecvPacketsText.Text = sm.TotalRecvPackets.ToString("N0");
            SendPacketsText.Text = sm.TotalSendPackets.ToString("N0");

            // 업타임
            if (_tcpManager.IsRunning)
            {
                TimeSpan uptime = DateTime.Now - _serverStartTime;
                UptimeText.Text = $"{uptime:hh\\:mm\\:ss}";
            }
            else
            {
                UptimeText.Text = "00:00:00";
            }
        }
    }
}
