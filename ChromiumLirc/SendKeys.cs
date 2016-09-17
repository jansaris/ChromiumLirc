using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using NLog;

namespace ChromiumLirc
{
    public class SendKeys : IDisposable
    {
        static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        bool _disposed;
        readonly string _xdoTool;

        public SendKeys(string xdoTool)
        {
            _xdoTool = xdoTool;
        }

        public void SendKey(string key, int pid)
        {
            Logger.Info($"Send '{key}' to PID {pid}");
            ThreadPool.QueueUserWorkItem(SendToXdoTool, new Tuple<string, int>(key, pid));
        }

        void SendToXdoTool(object state)
        {
            try
            {
                var data = (Tuple < string, int> )state;
                var xdoTool = new FileInfo(_xdoTool);
                if(!xdoTool.Exists)throw new FileNotFoundException(xdoTool.FullName);
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo(xdoTool.FullName)
                    {
                        Arguments = $"key {data.Item1}",
                        UseShellExecute = true
                    }
                };
                process.Start();
                process.WaitForExit(1000);
                Logger.Info($"XdoTool returned with {process.ExitCode}");
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"Failed to send {state} to {_xdoTool}: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Logger.Debug("Dispose");
        }
    }
}