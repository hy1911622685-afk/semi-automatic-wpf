using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Communication.Wpf.mySocket
{
    public class AsyncSocketServer : IDisposable
    {
        private const int BufferSize = 8192;

        private readonly int _port;
        private readonly int _maxConnections;
        private readonly ConcurrentDictionary<string, Socket> _clients;
        private readonly ConcurrentDictionary<string, IPEndPoint> _clientEndpoints;
        private readonly SemaphoreSlim _connectionSemaphore;
        private volatile bool _isRunning;
        private Socket _listener;

        public event EventHandler<ClientEventArgs> ClientConnected;
        public event EventHandler<ClientEventArgs> ClientDisconnected;
        public event EventHandler<DataReceivedEventArgs> DataReceived;
        public event EventHandler<ServerErrorEventArgs> ServerError;

        public bool IsRunning => _isRunning;
        public int ClientCount => _clients.Count;
        public AsyncCommandExecutor AsyncExecutor { get; } = new AsyncCommandExecutor();

        public AsyncSocketServer(int port, int maxConnections = 100)
        {
            _port = port;
            _maxConnections = maxConnections;
            _clients = new ConcurrentDictionary<string, Socket>();
            _clientEndpoints = new ConcurrentDictionary<string, IPEndPoint>();
            _connectionSemaphore = new SemaphoreSlim(maxConnections, maxConnections);
            AsyncExecutor.SendCmdAction = BroadcastAsync;
        }

        public void Start()
        {
            AsyncExecutor.InitCmd();

            try
            {
                _listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                {
                    NoDelay = true,
                    ReceiveBufferSize = BufferSize,
                    SendBufferSize = BufferSize
                };

                _listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _listener.Bind(new IPEndPoint(IPAddress.Any, _port));
                _listener.Listen(_maxConnections);
                _isRunning = true;

                _ = StartAcceptAsync();
            }
            catch (Exception ex)
            {
                OnServerError("启动服务器失败", ex);
                throw;
            }
        }

        private async Task StartAcceptAsync()
        {
            while (_isRunning)
            {
                bool semaphoreTaken = false;
                try
                {
                    await _connectionSemaphore.WaitAsync();
                    semaphoreTaken = true;

                    Socket clientSocket = await _listener.AcceptAsync();
                    semaphoreTaken = false;

                    string connectionId = Guid.NewGuid().ToString();
                    IPEndPoint remoteEndPoint = clientSocket.RemoteEndPoint as IPEndPoint;

                    _clients.TryAdd(connectionId, clientSocket);
                    _clientEndpoints.TryAdd(connectionId, remoteEndPoint);

                    OnClientConnected(new ClientEventArgs
                    {
                        ConnectionId = connectionId,
                        RemoteEndPoint = remoteEndPoint
                    });

                    _ = Task.Run(() => ReceiveDataAsync(connectionId, clientSocket));
                }
                catch (ObjectDisposedException)
                {
                    if (semaphoreTaken)
                        _connectionSemaphore.Release();
                    break;
                }
                catch (SocketException ex)
                {
                    if (semaphoreTaken)
                        _connectionSemaphore.Release();
                    if (!_isRunning)
                        break;

                    OnServerError("接受连接时发生 Socket 异常", ex);
                }
                catch (Exception ex)
                {
                    if (semaphoreTaken)
                        _connectionSemaphore.Release();
                    if (!_isRunning)
                        break;

                    OnServerError("接受连接时发生未知错误", ex);
                }
            }
        }

        private async Task ReceiveDataAsync(string connectionId, Socket clientSocket)
        {
            try
            {
                while (_isRunning && clientSocket.Connected)
                {
                    var buffer = new byte[BufferSize];
                    int bytesRead = await clientSocket.ReceiveAsync(new ArraySegment<byte>(buffer), SocketFlags.None);

                    if (bytesRead <= 0)
                        break;

                    OnDataReceived(new DataReceivedEventArgs
                    {
                        ConnectionId = connectionId,
                        Data = buffer.Take(bytesRead).ToArray(),
                        RemoteEndPoint = clientSocket.RemoteEndPoint as IPEndPoint
                    });
                }
            }
            catch (Exception ex)
            {
                if (_isRunning)
                    OnServerError($"接收数据时出错 [{connectionId}]", ex);
            }
            finally
            {
                CleanupConnection(connectionId);
            }
        }

        public async Task SendAsync(string connectionId, byte[] data)
        {
            if (!_clients.TryGetValue(connectionId, out var clientSocket) || !clientSocket.Connected)
                throw new InvalidOperationException($"连接不存在或已断开: {connectionId}");

            try
            {
                await clientSocket.SendAsync(new ArraySegment<byte>(data), SocketFlags.None);
            }
            catch (Exception ex)
            {
                OnServerError($"发送数据失败 [{connectionId}]", ex);
                throw;
            }
        }

        public async Task BroadcastAsync(byte[] data, string excludeConnectionId = null)
        {
            var tasks = new List<Task>();

            foreach (var kvp in _clients)
            {
                if (kvp.Key != excludeConnectionId && kvp.Value.Connected)
                    tasks.Add(SendAsync(kvp.Key, data));
            }

            await Task.WhenAll(tasks);
        }

        public async Task BroadcastAsync(string msg, string excludeConnectionId = null)
        {
            await BroadcastAsync(MessageProtocol.PackMessage(msg), excludeConnectionId);
        }

        public void DisconnectClient(string connectionId)
        {
            if (!_clients.TryRemove(connectionId, out var clientSocket))
                return;

            try
            {
                clientSocket.Shutdown(SocketShutdown.Both);
                clientSocket.Close();
                clientSocket.Dispose();
            }
            catch
            {
            }

            _clientEndpoints.TryRemove(connectionId, out _);

            OnClientDisconnected(new ClientEventArgs
            {
                ConnectionId = connectionId
            });

            _connectionSemaphore.Release();
        }

        public void Stop()
        {
            _isRunning = false;

            try
            {
                foreach (var connectionId in _clients.Keys.ToList())
                    DisconnectClient(connectionId);

                _listener?.Close();
                _listener?.Dispose();
            }
            catch (Exception ex)
            {
                OnServerError("停止服务器时出错", ex);
            }
        }

        public string ParseContent(byte[] contentBytes)
        {
            return Encoding.UTF8.GetString(contentBytes);
        }

        private void CleanupConnection(string connectionId)
        {
            DisconnectClient(connectionId);
        }

        protected virtual void OnClientConnected(ClientEventArgs e)
        {
            ClientConnected?.Invoke(this, e);
        }

        protected virtual void OnClientDisconnected(ClientEventArgs e)
        {
            ClientDisconnected?.Invoke(this, e);
        }

        protected virtual void OnDataReceived(DataReceivedEventArgs e)
        {
            DataReceived?.Invoke(this, e);
            ServerDataReceived(e);
        }

        protected virtual void OnServerError(string message, Exception ex = null)
        {
            ServerError?.Invoke(this, new ServerErrorEventArgs
            {
                ErrorMessage = message,
                Exception = ex
            });
        }

        private async void ServerDataReceived(DataReceivedEventArgs e)
        {
            string command = ParseContent(e.Data);
            var result = await AsyncExecutor.ExecuteAsync(command);
            await AsyncExecutor.FeedbackCommand(result);
        }

        public void Dispose()
        {
            Stop();
            _connectionSemaphore?.Dispose();
        }
    }

    public class ClientEventArgs : EventArgs
    {
        public string ConnectionId { get; set; }
        public IPEndPoint RemoteEndPoint { get; set; }
    }

    public class DataReceivedEventArgs : EventArgs
    {
        public string ConnectionId { get; set; }
        public byte[] Data { get; set; }
        public IPEndPoint RemoteEndPoint { get; set; }
    }

    public class ServerErrorEventArgs : EventArgs
    {
        public string ErrorMessage { get; set; }
        public Exception Exception { get; set; }
    }
}
