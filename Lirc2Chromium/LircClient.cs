using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using NLog;

namespace Lirc2Chromium
{
    class LircClient : IDisposable
    {
        readonly ILogger _logger = LogManager.GetCurrentClassLogger();
        readonly UnixEndPoint _endpoint;
        bool _disposed;
        Socket _socket;
        Thread _thread;
        public event EventHandler<string> KeyAction;

        public LircClient(UnixEndPoint endpoint)
        {
            _endpoint = endpoint;
        }

        public void Listen()
        {
            _logger.Info("Start listening to Lirc");
            _thread = new Thread(ListenThread);
            _thread.Start();
        }

        void ListenThread()
        {
            try
            {
                Connect();
                ReceiveDataLoop();
                _logger.Info("Stopped listening to Lirc");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Lirc main: {ex.Message}");
            }
        }

        void Connect()
        {
            _logger.Debug("Connect to lirc");
            _socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
            _socket.Connect(_endpoint);
            //_socket = new Socket(SocketType.Stream, ProtocolType.IP);
            //_socket.Connect(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8888));
            _logger.Info("Connected to Lirc");
        }

        void ReceiveDataLoop()
        {
            while (!_disposed && _socket.Connected)
            {
                try
                {
                    var buffer = new byte[_socket.ReceiveBufferSize];
                    var nrOfBytes = _socket.Receive(buffer);
                    _logger.Debug($"Received {nrOfBytes} from Lirc");
                    var lircEvent = LircEvent.Parse(buffer, nrOfBytes);
                    if (!string.IsNullOrWhiteSpace(lircEvent.Key)) SendOnKeyPressed(lircEvent.Key);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Error occured while receiving data from Lirc: {ex.Message}");
                }
            }
        }

        void SendOnKeyPressed(string key)
        {
            _logger.Debug($"Send {key} on a new thread");
            ThreadPool.QueueUserWorkItem(OnKeyPressed, key);
        }

        void OnKeyPressed(object key)
        {
            _logger.Debug($"Send {key} to the listeners");
            KeyAction?.Invoke(this, key.ToString());
        }

        public void Dispose()
        {
            if (_disposed) return;
            _logger.Info("Stop lirc client");
            _disposed = true;
            _socket?.Close(); //Will also cancel the current read
            Thread.Sleep(50); // Give the listen thread 50ms to close itself
            if(_thread?.IsAlive ?? false) _thread.Abort(); // If it is still running, abort the thread
        }
    }
}