using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using NLog;

namespace LircSharp
{
    public class LircSocketClient : LircClient
    {
        static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        Socket _socket;
        SocketAsyncEventArgs _readArgs;
        SocketAsyncEventArgs _writeArgs;

        readonly Queue<string> _writeCommandQueue = new Queue<string>();
        readonly ManualResetEvent _connectedEvent = new ManualResetEvent(false);
        readonly ManualResetEvent _disposeEvent = new ManualResetEvent(false);
        readonly AutoResetEvent _writeEvent = new AutoResetEvent(false);
        Timer _reconnectTimer;
        readonly object _reconnectLock = new object();

        readonly byte[] _readBuffer = new byte[1024];
        readonly byte[] _writeBuffer = new byte[1024];

        public LircSocketClient() 
        {
            SetupReadWriteThreads();
        }

        protected override string Address => UnixAddress ?? $"{Host}:{Port}";
        public string UnixAddress { get; private set; }

        public int Port { get; private set; }

        public string Host { get; private set; }

        protected override void Dispose(bool disposing)
        {
            if (!Disposed)
            {
                if (disposing)
                {
                    Logger.Info("Dispose");
                    // Kill the reader/writer threads
                    _disposeEvent.Set();
                }
            }

            base.Dispose(disposing);
        }

        public void Connect(string unixEndpoint)
        {
            UnixAddress = unixEndpoint;
            Host = null;
            Port = 0;
            Connect();
        }

        public void Connect(string host, int port)
        {
            Host = host;
            Port = port;
            UnixAddress = null;
            Connect();
        }

        void SetupReadWriteThreads()
        {
            Logger.Debug("SetupReadWriteThreads");
            ThreadPool.QueueUserWorkItem(DoRead, null);
            ThreadPool.QueueUserWorkItem(DoWrite, null);
        }

        protected override void ConnectInternal()
        {
            Logger.Debug("ConnectInternal");
            if (_socket != null)
            {
                throw new InvalidOperationException("You must call disconnect before calling connect.");
            }

            var connectArgs = CreateSocket();
            connectArgs.Completed += ConnectCompleted;
            if (_socket != null && !_socket.ConnectAsync(connectArgs))
            {
                ConnectCompleted(this, connectArgs);
            }
        }

        SocketAsyncEventArgs CreateSocket()
        {
            if (string.IsNullOrWhiteSpace(UnixAddress))
            {
                _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                return new SocketAsyncEventArgs { RemoteEndPoint = new DnsEndPoint(Host, Port) };
            }
            _socket = new Socket(AddressFamily.Unix,SocketType.Stream, ProtocolType.IP);
            return new SocketAsyncEventArgs { RemoteEndPoint = new UnixEndPoint(UnixAddress)};
        }

        protected override void DisconnectInternal()
        {
            Logger.Debug("DisconnectInternal");
            if (_socket == null)
            {
                Logger.Debug("Already disconnected");
                return;
            }
            Logger.Info("Disconnect from socket");
            if (_socket.Connected)
            {
                _socket.Shutdown(SocketShutdown.Both);
                _socket.Close();
            }

            _connectedEvent.Reset();
            _socket.Dispose();
            _socket = null;
        }

        protected override void SendCommandInternal(string command)
        {
            // Queue up the command
            _writeCommandQueue.Enqueue(command);
            // And signal the worker to start pumping
            _writeEvent.Set();
        }

        void ConnectCompleted(object sender, SocketAsyncEventArgs e)
        {
            Logger.Debug("ConnectCompleted");
            if (e.SocketError != SocketError.Success)
            {
                OnError("Unable to connect: " + e.SocketError);
                return;
            }
            Logger.Info("Connected to the socket");
            _readArgs = new SocketAsyncEventArgs();
            _readArgs.SetBuffer(_readBuffer, 0, _readBuffer.Length);
            _readArgs.Completed += SocketReadCompleted;

            _writeArgs = new SocketAsyncEventArgs();
            _writeArgs.SetBuffer(_writeBuffer, 0, _writeBuffer.Length);
            _writeArgs.Completed += SocketWriteCompleted;

            _connectedEvent.Set();

            OnConnected();
        }

        void AttemptReconnectIfRequired()
        {
            Logger.Debug("AttemptReconnectIfRequired");
            var s = _socket;

            if (s == null)
            {
                // We've disconnected, no automatic reconnect should be attempted
                Logger.Debug("Disconnected, no auto reconnect required");
                return;
            }

            if (s.Connected)
            {
                Logger.Debug("Connected, no auto reconnect required");
                return;
            }
            Logger.Info("Reconnect");
            // If we're not connected then let everyone know
            _connectedEvent.Reset();

            lock (_reconnectLock)
            {
                // Check to see if we should queue up a reconnect timer
                if (_reconnectTimer != null) return;
                OnMessage("Setting up for reconnect...");
                // Sleep 30 seconds and try again
                _reconnectTimer = new Timer(state =>
                {
                    try
                    {
                        // If the socket is null, a disconnect was performed
                        // and we should not just blindly reconnect
                        if (_socket == null || _socket.Connected) return;
                        OnMessage("Reconnecting...");
                        Reconnect();
                    }
                    finally
                    {
                        _reconnectTimer = null;
                    }
                }, null, TimeSpan.FromSeconds(30), TimeSpan.FromMilliseconds(-1));
            }
        }

        void DoRead(object state)
        {
            // If we're not connected, wait until we are
            var signaledHandle = WaitHandle.WaitAny(new WaitHandle[] { _connectedEvent, _disposeEvent });

            if (signaledHandle == 1)
            {
                // The object has been disposed, stop reading
                return;
            }

            // If the socket is null that means we've disconnected.
            if (_socket == null)
            {
                // Stop performing reads, they will be restarted when a new connection is made
                //return;
            }

            try
            {
                if (_socket == null || !_socket.Connected)
                {
                    // We got disconnected somehow, just queue up a read
                    ThreadPool.QueueUserWorkItem(DoRead, state);
                    return;
                }

                if (!_socket.ReceiveAsync(_readArgs))
                {
                    // This means the read finished syncronously so parse the data
                    SocketReadCompleted(this, _readArgs);
                }
            }
            catch (Exception e)
            {
                OnError("Exception reading.", e);
                // There was an error starting up the read, so let's queue up another
                ThreadPool.QueueUserWorkItem(DoRead, state);
            }
        }

        void SocketReadCompleted(object sender, SocketAsyncEventArgs e)
        {
            Logger.Debug($"SocketReadCompleted: {e.SocketError}");
            switch (e.SocketError)
            {
                // This occurs when reconnecting and when tombstoning
                case SocketError.OperationAborted:
                // These can occur when the server is down or connecting somewhere non-existant
                case SocketError.ConnectionAborted:
                case SocketError.ConnectionRefused:
                case SocketError.ConnectionReset:
                case SocketError.NotConnected:
                    AttemptReconnectIfRequired();
                    break;
                case SocketError.Shutdown:
                    // Nothing to do, we're disconnecting, maybe we just shouldn't attempt to reconnect unless asked.
                    break;
                case SocketError.TryAgain:
                    // Okay, nothing to do here either
                    break;
                case SocketError.Success:
                    // Success, parse the message
                    ParseData(e.Buffer, e.Offset, e.BytesTransferred);
                    break;
                default:
                    // log the error
                    OnError($"Error while reading: {e.SocketError}, Operation: {e.LastOperation}");
                    break;
            }

            // Then kick of another read
            ThreadPool.QueueUserWorkItem(DoRead, e.UserToken);
        }

        void DoWrite(object state)
        {
            var commandProcessed = false;

            do
            {
                // Make sure we're in a state that we can do something
                // If we're not connected, wait until we are
                int signaledHandle = WaitHandle.WaitAny(new WaitHandle[] { _connectedEvent, _disposeEvent });

                if (signaledHandle == 1)
                {
                    // The object has been disposed, stop writing
                    return;
                }          

                if (_writeCommandQueue.Count <= 0)
                {
                    // Either wait until we get a write or we're being disposed
                    signaledHandle = WaitHandle.WaitAny(new WaitHandle[] { _writeEvent, _disposeEvent });

                    if (signaledHandle == 1)
                    {
                        // The object has been disposed, stop reading
                        return;
                    }          
                }

                string command = null;
                try
                {
                    command = _writeCommandQueue.Dequeue();
                }
                catch(InvalidOperationException)
                {
                    // Ignore, it just means the queue is empty
                }

                // If for some reason we didn't get a command, just start waiting again
                if (command == null)
                {
                    continue;
                }

                OnMessage("Sending command " + command.Trim());

                var bytesToWrite = Encoding.UTF8.GetBytes(command, 0, command.Length, _writeBuffer, 0);
                _writeArgs.SetBuffer(0, bytesToWrite);
                try
                {
                    _socket.SendAsync(_writeArgs);
                    commandProcessed = true;
                }
                catch (Exception e)
                {
                    OnError("Error sending command: " + command + "\r\n" + e.Message, e);
                }
            }
            while (!commandProcessed);
        }

        void SocketWriteCompleted(object sender, SocketAsyncEventArgs e)
        {
            Logger.Debug($"SocketWriteCompleted: {e.SocketError}");
            switch (e.SocketError)
            {
                // This occurs when reconnecting and when tombstoning
                case SocketError.OperationAborted:
                // These can occur when the server is down or connecting somewhere non-existant
                case SocketError.ConnectionAborted:
                case SocketError.ConnectionRefused:
                case SocketError.ConnectionReset:
                case SocketError.NotConnected:
                    AttemptReconnectIfRequired();
                    break;
                case SocketError.Shutdown:
                    // Nothing to do, we're disconnecting, maybe we just shouldn't attempt to reconnect unless asked.
                    break;
                case SocketError.TryAgain:
                    // Okay
                    try
                    {
                        _socket.SendAsync(_writeArgs);
                        // If this succeeded, then a write is pending 
                        // and we should not queue up another one right away
                        return;
                    }
                    catch (Exception ex)
                    {
                        OnError("Error trying again.", ex);
                    }
                    break;
                case SocketError.Success:
                    break;
                default:
                    // Display the error
                    OnError($"Error writing to socket: {e.SocketError}, Operation: {e.LastOperation}");
                    break;
            }

            // Kick off another write
            ThreadPool.QueueUserWorkItem(DoWrite, e.UserToken);
        }
    }
}
