using System;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Communication.Wpf.mySocket
{
    public class SocketConnection : IDisposable
    {
        private readonly SocketAsyncEventArgs _ioEventArgs;
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        private readonly byte[] _lengthBuffer = new byte[4];

        public string ConnectionId { get; }
        public Socket Socket { get; private set; }
        public IPEndPoint RemoteEndPoint { get; }
        public DateTime ConnectTime { get; }
        public bool IsConnected => Socket?.Connected ?? false;

        public SocketConnection(string connectionId, Socket socket, SocketAsyncEventArgs ioEventArgs)
        {
            ConnectionId = connectionId;
            Socket = socket ?? throw new ArgumentNullException(nameof(socket));
            _ioEventArgs = ioEventArgs ?? throw new ArgumentNullException(nameof(ioEventArgs));
            RemoteEndPoint = socket.RemoteEndPoint as IPEndPoint;
            ConnectTime = DateTime.Now;

            socket.NoDelay = true;
            socket.ReceiveTimeout = 30000;
            socket.SendTimeout = 30000;
        }

        public async Task SendAsync(byte[] data)
        {
            if (!IsConnected)
                throw new SocketException((int)SocketError.NotConnected);

            await _sendLock.WaitAsync();

            try
            {
                BinaryPrimitives.WriteInt32BigEndian(_lengthBuffer, data.Length);
                await SendInternalAsync(_lengthBuffer, 0, _lengthBuffer.Length);
                await SendInternalAsync(data, 0, data.Length);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        private async Task SendInternalAsync(byte[] buffer, int offset, int count)
        {
            int totalSent = 0;
            while (totalSent < count)
            {
                _ioEventArgs.SetBuffer(buffer, offset + totalSent, count - totalSent);

                if (Socket.SendAsync(_ioEventArgs))
                {
                    await Task.Factory.FromAsync(
                        (callback, state) =>
                        {
                            void Completed(object s, SocketAsyncEventArgs e)
                            {
                                _ioEventArgs.Completed -= Completed;
                                callback?.Invoke(null);
                            }

                            _ioEventArgs.Completed += Completed;
                            return null;
                        },
                        result => { },
                        null);
                }

                if (_ioEventArgs.SocketError != SocketError.Success)
                    throw new SocketException((int)_ioEventArgs.SocketError);

                totalSent += _ioEventArgs.BytesTransferred;
            }
        }

        public void Dispose()
        {
            try
            {
                Socket?.Shutdown(SocketShutdown.Both);
                Socket?.Close();
                Socket?.Dispose();
            }
            catch
            {
            }

            _sendLock?.Dispose();
        }
    }
}
