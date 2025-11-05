using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Windows_ServerTest.Logging
{
    public interface ILogger
    {
        void Info(string message);
        void Error(string message, Exception ex = null);
        event Action<string> LogReceived;
    }

    public class SafeLogger : ILogger, IDisposable
    {
        private readonly BlockingCollection<string> _queue = new BlockingCollection<string>();
        private readonly Task _workerTask;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly string _baseDirectory;

        private StreamWriter _writer;
        private DateTime _currentDate;

        public event Action<string> LogReceived;

        public SafeLogger(string baseDirectory = "Logs")
        {
            _baseDirectory = baseDirectory;
            Directory.CreateDirectory(_baseDirectory);

            _currentDate = DateTime.Now.Date;
            OpenNewLogFile(_currentDate);

            _workerTask = Task.Run(() => ProcessQueueAsync(_cts.Token));
        }

        public void Info(string message) => Enqueue("INFO", message);

        public void Error(string message, Exception ex = null)
        {
            var full = ex == null ? message : $"{message} | {ex}";
            Enqueue("ERROR", full);
        }

        private void Enqueue(string level, string message)
        {
            try
            {
                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
                _queue.Add(line);
            }
            catch (InvalidOperationException)
            {
                // 큐가 닫혔을 때는 무시
            }
        }

        private async Task ProcessQueueAsync(CancellationToken token)
        {
            try
            {
                foreach (var line in _queue.GetConsumingEnumerable(token))
                {
                    try
                    {
                        LogReceived?.Invoke(line);

                        // 날짜 변경 감지 → 새 파일 열기
                        var now = DateTime.Now.Date;
                        if (now != _currentDate)
                        {
                            _writer?.Dispose();
                            _currentDate = now;
                            OpenNewLogFile(now);
                        }

                        await _writer.WriteLineAsync(line);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Logger] Write Error: {ex.Message}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 정상 종료
            }
        }

        private void OpenNewLogFile(DateTime date)
        {
            var fileName = $"{date:yyyy-MM-dd}.log";
            var fullPath = Path.Combine(_baseDirectory, fileName);

            _writer = new StreamWriter(fullPath, append: true)
            {
                AutoFlush = true
            };
        }

        public void Dispose()
        {
            _queue.CompleteAdding();
            _cts.Cancel();

            try
            {
                _workerTask.Wait(1000);
            }
            catch { }

            _writer?.Dispose();
            _cts.Dispose();
        }
    }
}
