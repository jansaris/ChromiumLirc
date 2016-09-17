using System;
using System.Collections.Generic;
using System.Threading;
using NLog;

namespace LircSharp
{
    public abstract class LircClient : IDisposable
    {
        static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        readonly LircKeyEventParser _parser;
        readonly object _connectLock = new object();

        protected bool Disposed;

        public event EventHandler Connected;
        public event EventHandler<LircMessageEventArgs> Message;
        public event EventHandler<LircKeyPressEventArs> KeyPressed;
        public event EventHandler<LircCommandEventArgs> CommandCompleted;
        public event EventHandler<LircErrorEventArgs> Error;

        public Dictionary<string, List<string>> RemoteCommands { get; set; }

        public static readonly List<LircClient> Clients = new List<LircClient>();

        protected abstract string Address { get; }

        protected LircClient()
        {
            _parser = new LircKeyEventParser();
            _parser.CommandParsed += CommandParsed;

            Clients.Add(this);
        }

        private void CommandParsed(object sender, LircKeyPressEventArs e)
        {
            KeyPressed?.Invoke(this, e);
        }

        public void Dispose()
        {
            if (!Disposed) return;
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            Disposed = true;
            Logger.Debug("Dispose");
        }

        public void Connect()
        {
            // Check if someone is already connecting
            if (!Monitor.TryEnter(_connectLock))
            {
                Logger.Warn($"Connect to {Address} failed, because the monitor didn't allow");
                return;
            }

            try
            {
                Logger.Info($"Connect to {Address}");
                ConnectInternal();
            }
            finally
            {
                Monitor.Exit(_connectLock);
            }
        }

        public void Reconnect()
        {
            Disconnect();
            Connect();
        }

        public void Disconnect()
        {
            Logger.Info($"Disconnect from {Address}");
            DisconnectInternal();
        }

        public void SendCommand(string remote, string command)
        {
            SendCommand($"SEND_ONCE {remote} {command}\n");
        }

        public void SendCommand(string command)
        {
            if (!command.EndsWith("\n", StringComparison.Ordinal))
            {
                command += '\n';
            }
            Logger.Info($"Send command {command}");
            SendCommandInternal(command);
        }

        public List<string> GetCommands(string remote)
        {
            Logger.Info($"Get command for remorte {remote}");
            if (remote == null)
            {
                Logger.Debug("Null remote");
                return null;
            }

            if (!RemoteCommands.ContainsKey(remote))
            {
                throw new ArgumentException("You must specify a valid remote name");
            }

            return RemoteCommands[remote] ?? new List<string>();
        }

        protected abstract void SendCommandInternal(string command);        

        protected abstract void ConnectInternal();

        protected abstract void DisconnectInternal();

        protected void ParseData(byte[] buffer, int index, int count)
        {
            try
            {
                _parser.Parse(buffer, index, count);
            }
            catch (LircParsingException e)
            {
                OnError("Exception while parsing data", e);
            }
        }

        void CommandParsed(object sender, LircCommandEventArgs e)
        {
            Logger.Debug($"Command parsed: {e?.Command?.Command}");
            var command = e?.Command?.Command ?? string.Empty;
            switch (command)
            {
                case "ListRemotes":
                    var listRemotes = e?.Command as LircListRemotesCommand ?? new LircListRemotesCommand();
                    RemoteCommands = new Dictionary<string, List<string>>();
                    foreach (var remote in listRemotes.Remotes)
                    {
                        RemoteCommands[remote] = null;
                        SendCommand("LIST " + remote);
                    }
                    break;
                case "ListRemote":
                    var listRemote = e?.Command as LircListRemoteCommand;
                    if (listRemote != null) RemoteCommands[listRemote.Remote] = listRemote.Data;
                    break;
            }

            OnCommandCompleted(e?.Command);
        }

        protected void OnConnected()
        {
            Logger.Info("Lirc connected");
            Connected?.Invoke(this, EventArgs.Empty);
        }
        
        protected void OnError(string message, Exception exception = null)
        {
            Logger.Info($"Lirc error: {message}");
            Error?.Invoke(this, new LircErrorEventArgs(message, exception));
        }

        void OnCommandCompleted(LircCommand command)
        {
            Logger.Info($"Lirc command completed: {command?.Command}");
            CommandCompleted?.Invoke(this, new LircCommandEventArgs(command));
        }

        protected void OnMessage(string message)
        {
            Logger.Info($"Lirc message: {message}");
            Message?.Invoke(this, new LircMessageEventArgs(message));
        }
    }
}
