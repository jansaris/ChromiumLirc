using System;
using System.Threading;
using Mono.Unix;
using Mono.Unix.Native;
using NLog;

namespace Lirc2Chromium
{
    public class LinuxSignal : IDisposable
    {
        static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        bool _disposing;
        bool _running;
        Thread _listeningThread;

        public void Listen()
        {
            _listeningThread = new Thread(ListenForSignal);
            _listeningThread.Start();
        }

        public void WaitForListenThreadToComplete()
        {
            while (_listeningThread?.IsAlive ?? false)
            {
                Thread.Sleep(1000);
            }
        }

        void ListenForSignal()
        {
            try
            {
                var intr = new UnixSignal(Signum.SIGINT);
                var term = new UnixSignal(Signum.SIGTERM);
                var hup = new UnixSignal(Signum.SIGHUP);
                var usr2 = new UnixSignal(Signum.SIGUSR2);
                UnixSignal[] signals = { intr, term, hup, usr2 };

                Logger.Info("Start listening for unix signals");

                for (_running = true; _running;)
                {
                    var idx = UnixSignal.WaitAny(signals, 1000);
                    if (idx < 0 || idx >= signals.Length) continue;
                    if (!_running) return;

                    Logger.Info("LinuxSignal: received signal " + signals[idx].Signum);

                    if ((intr.IsSet || term.IsSet))
                    {
                        intr.Reset();
                        term.Reset();

                        Logger.Info("LinuxSignal: stopping...");

                        _running = false;
                    }
                    else if (hup.IsSet)
                    {
                        // Ignore. Could be used to reload configuration.
                        hup.Reset();
                    }
                    else if (usr2.IsSet)
                    {
                        usr2.Reset();
                        // do something
                    }
                }
            }
            catch
            {
                Logger.Info("Unable to listen on unix signals");
            }
            Logger.Info("Finished listening to unix signals");
        }

        public void Dispose()
        {
            if (_disposing) return;
            _disposing = true;
            _running = false;
            WaitForListenThreadToComplete();
            _listeningThread = null;
        }
    }
}