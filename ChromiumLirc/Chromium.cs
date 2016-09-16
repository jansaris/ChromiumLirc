using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using NLog;

namespace ChromiumLirc
{
    public class Chromium : IDisposable
    {
        static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        const int AbortTimeout = 1100;
        const int PollTime = 1000;

        readonly string _processName;
        Thread _thread;
        bool _disposed;

        public int ActivePid => _monitoredProcess?.Id ?? -1;
        public string ActiveProcessName => _monitoredProcess?.ProcessName ?? "";
        public bool IsAlive => !_monitoredProcess?.HasExited ?? false;

        Process _monitoredProcess = null;

        public Chromium(string processName)
        {
            _processName = processName;
        }

        public void Watch()
        {
            _thread = new Thread(StartWatcher);
            _thread.Start();
        }

        void StartWatcher()
        {
            try
            {
                MonitorProcesses();
            }
            catch (ThreadAbortException)
            {
                Logger.Warn("Thread has been aborted");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"An unhandled exception occured in thread: {ex.Message}");
            }
        }

        void MonitorProcesses()
        {
            while (!_disposed)
            {
                var list = Process.GetProcessesByName(_processName);
                if (list.Length <= 0)
                {
                    Logger.Debug($"Found no processes for {_processName}, poll after {PollTime}ms");
                    Thread.Sleep(PollTime);
                    continue;
                }
                Logger.Debug($"Found {list.Length} processes for {_processName}");
                Array.ForEach(list, process => Logger.Debug($"{process.Id}: {process.ProcessName}"));
                _monitoredProcess = list.FirstOrDefault(p => !p.HasExited);
                if (_monitoredProcess == null)
                {
                    Logger.Debug("None of these processes was alive");
                    continue;
                }
                Logger.Info($"Start monitoring {_monitoredProcess.Id}: {_monitoredProcess.ProcessName}");
                MonitorInstance();
            }
        }

        void MonitorInstance()
        {
            while (!_disposed && IsAlive)
            {
                if (_monitoredProcess?.WaitForExit(PollTime) ?? true)
                {
                    Logger.Info($"Process {ActivePid}: {ActiveProcessName} has exited");
                    _monitoredProcess = null;
                }
                Logger.Debug($"Process {ActivePid}: {ActiveProcessName} is still running, poll after {PollTime}ms");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Logger.Debug("Dispose");
            AbortThread();
            GC.SuppressFinalize(this);
        }

        void AbortThread(int msBeforeRealAbort = AbortTimeout)
        {
            if (_thread == null || !_thread.IsAlive) return;
            var abortInSeconds = msBeforeRealAbort > 0 ? msBeforeRealAbort / 1000 : 0;
            Logger.Warn($"Wait max {abortInSeconds} sec for thread to stop by itself");
            Thread.Sleep(msBeforeRealAbort);
            if (_thread.IsAlive) _thread.Abort();
            else Logger.Info("Thread stopped by itself");
        }
    }
}