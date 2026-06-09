using Communication.Wpf.Models;
using Communication.Wpf.mySocket;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace Communication.Wpf.ViewModels
{
    public partial class CommunicationViewModel : ObservableObject, IDisposable
    {
        private const int SingleClientLimit = 1;
        private const int MaxLogLineCount = 10;

        private readonly ConcurrentDictionary<string, ClientInfo> _clientMap = new ConcurrentDictionary<string, ClientInfo>();
        private readonly ConcurrentQueue<string> _pendingLogLines = new ConcurrentQueue<string>();
        private readonly Queue<string> _logLines = new Queue<string>();
        private AsyncSocketServer _server;
        private int _isLogFlushScheduled;
        private bool _isDeviceConnected;

        [ObservableProperty]
        private string port = "8888";

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(StartListenCommand))]
        [NotifyCanExecuteChangedFor(nameof(StopListenCommand))]
        private bool isRunning;

        [ObservableProperty]
        private string clientName = "未连接";

        [ObservableProperty]
        private string logText;

        public Action<AsyncCommandExecutor> CommandBinder { get; set; }
        public Action ListenStartedAction { get; set; }
        public Action ListenStoppedAction { get; set; }
        public Action DeviceConnectedAction { get; set; }
        public Action DeviceDisconnectedAction { get; set; }

        public string ListenStatusText => IsRunning ? "监听中" : "未监听";
        public bool HasClient => _clientMap.Count > 0;

        partial void OnIsRunningChanged(bool value)
        {
            OnPropertyChanged(nameof(ListenStatusText));
        }

        [RelayCommand]
        private void ClearLog()
        {
            while (_pendingLogLines.TryDequeue(out _))
            {
            }

            _logLines.Clear();
            LogText = string.Empty;
        }

        [RelayCommand(CanExecute = nameof(CanStartListen))]
        private void StartListen()
        {
            if (!int.TryParse(Port, out int portValue))
            {
                AppendLog("端口格式不正确");
                return;
            }

            try
            {
                _server = new AsyncSocketServer(portValue, SingleClientLimit);
                _server.ClientConnected += ServerClientConnected;
                _server.ClientDisconnected += ServerClientDisconnected;
                _server.DataReceived += ServerDataReceived;
                _server.ServerError += ServerError;
                CommandBinder?.Invoke(_server.AsyncExecutor);
                _server.Start();

                IsRunning = true;
                ClientName = "等待客户端";
                ListenStartedAction?.Invoke();
                AppendLog($"开始监听端口 {portValue}，仅允许 1 个客户端连接");
            }
            catch (Exception ex)
            {
                AppendLog($"开始监听失败: {ex.Message}");
            }
        }

        private bool CanStartListen()
        {
            return !IsRunning;
        }

        [RelayCommand(CanExecute = nameof(CanStopListen))]
        private void StopListen()
        {
            try
            {
                bool wasRunning = IsRunning;
                _server?.Stop();
                _server = null;

                RunOnUi(() =>
                {
                    _clientMap.Clear();
                    IsRunning = false;
                    ClientName = "未连接";
                    AppendLog("监听已结束");
                    OnPropertyChanged(nameof(HasClient));
                });

                NotifyDeviceDisconnected();
                if (wasRunning)
                    ListenStoppedAction?.Invoke();
            }
            catch (Exception ex)
            {
                AppendLog($"结束监听时出错: {ex.Message}");
            }
        }

        private bool CanStopListen()
        {
            return IsRunning;
        }

        private void ServerClientConnected(object sender, ClientEventArgs e)
        {
            var clientInfo = new ClientInfo
            {
                ConnectionId = e.ConnectionId,
                IPAddress = e.RemoteEndPoint?.Address.ToString(),
                Port = e.RemoteEndPoint?.Port ?? 0,
                ConnectTime = DateTime.Now,
                Username = "客户端" + e.ConnectionId.Substring(0, 8)
            };

            _clientMap.TryAdd(e.ConnectionId, clientInfo);

            RunOnUi(() =>
            {
                ClientName = clientInfo.DisplayName;
                AppendLog($"客户端已连接: {clientInfo.DisplayName}");
                OnPropertyChanged(nameof(HasClient));
                NotifyDeviceConnected();
            });
        }

        private void ServerClientDisconnected(object sender, ClientEventArgs e)
        {
            _clientMap.TryRemove(e.ConnectionId, out var clientInfo);

            RunOnUi(() =>
            {
                AppendLog($"客户端已断开: {GetClientLogName(e, clientInfo)}");
                ClientName = IsRunning ? "等待客户端" : "未连接";
                OnPropertyChanged(nameof(HasClient));
                NotifyDeviceDisconnected();
            });
        }

        private void ServerDataReceived(object sender, DataReceivedEventArgs e)
        {
            try
            {
                string text = MessageProtocol.ParseContent(e.Data);
                RunOnUi(() => AppendLog($"收到[{GetClientLogName(e)}]: {text}"));
            }
            catch (Exception ex)
            {
                RunOnUi(() => AppendLog($"解析客户端消息失败: {ex.Message}"));
            }
        }

        private void ServerError(object sender, ServerErrorEventArgs e)
        {
            RunOnUi(() =>
            {
                AppendLog($"监听错误: {e.ErrorMessage}");
                if (e.Exception != null)
                    AppendLog($"异常详情: {e.Exception.Message}");
            });
        }

        private string GetClientLogName(ClientEventArgs e, ClientInfo clientInfo)
        {
            return clientInfo?.DisplayName ?? e.ConnectionId?.Substring(0, 8) ?? "未知客户端";
        }

        private string GetClientLogName(DataReceivedEventArgs e)
        {
            if (e.ConnectionId != null && _clientMap.TryGetValue(e.ConnectionId, out var clientInfo))
                return clientInfo.DisplayName;

            return e.ConnectionId?.Substring(0, 8) ?? "未知客户端";
        }

        private void AppendLog(string message)
        {
            _pendingLogLines.Enqueue($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            ScheduleLogFlush();
        }

        private void ScheduleLogFlush()
        {
            if (Interlocked.Exchange(ref _isLogFlushScheduled, 1) == 1)
                return;

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                FlushPendingLogs();
                return;
            }

            try
            {
                dispatcher.BeginInvoke((Action)FlushPendingLogs, DispatcherPriority.Background);
            }
            catch
            {
                Interlocked.Exchange(ref _isLogFlushScheduled, 0);
            }
        }

        private void FlushPendingLogs()
        {
            Interlocked.Exchange(ref _isLogFlushScheduled, 0);

            bool changed = false;
            while (_pendingLogLines.TryDequeue(out string line))
            {
                _logLines.Enqueue(line);
                changed = true;

                while (_logLines.Count > MaxLogLineCount)
                    _logLines.Dequeue();
            }

            if (changed)
            {
                var builder = new StringBuilder();
                foreach (string line in _logLines)
                    builder.Append(line);

                LogText = builder.ToString();
            }

            if (!_pendingLogLines.IsEmpty)
                ScheduleLogFlush();
        }

        private static void RunOnUi(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
                action();
            else
                dispatcher.BeginInvoke(action, DispatcherPriority.Background);
        }

        private void NotifyDeviceConnected()
        {
            if (_isDeviceConnected)
                return;

            _isDeviceConnected = true;
            DeviceConnectedAction?.Invoke();
        }

        private void NotifyDeviceDisconnected()
        {
            if (!_isDeviceConnected)
                return;

            _isDeviceConnected = false;
            DeviceDisconnectedAction?.Invoke();
        }

        public void Dispose()
        {
            StopListen();
        }

        public void StopListening()
        {
            StopListen();
        }
    }
}
