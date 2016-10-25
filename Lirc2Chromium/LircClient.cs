using System;
using System.Net.Sockets;
using NLog;

namespace Lirc2Chromium
{
    class LircClient : IDisposable
    {
        readonly ILogger _logger = LogManager.GetCurrentClassLogger();
        readonly UnixEndPoint _endpoint;
        private bool _disposed;
        private Socket _socket;
        public event EventHandler<string> KeyPressed;

        public LircClient(UnixEndPoint endpoint)
        {
            _endpoint = endpoint;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
        }

        public void Listen()
        {
            _socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
            _socket.Connect(_endpoint);
            //_socket.Receive()
        }

        protected virtual void OnKeyPressed(string key)
        {
            KeyPressed?.Invoke(this, key);
        }
    }
}