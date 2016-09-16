using System;
using NLog;

namespace ChromiumLirc
{
    public class SendKeys : IDisposable
    {
        static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        bool _disposed;
        string _xdoTool;

        public SendKeys(string xdoTool)
        {
            _xdoTool = xdoTool;
        }

        public void SendKey(string key, int pid)
        {
            Logger.Info($"Send '{key}' to PID {pid}");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Logger.Debug("Dispose");
        }
    }
}