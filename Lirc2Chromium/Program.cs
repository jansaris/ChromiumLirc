using System;
using Ninject;
using NLog;

namespace Lirc2Chromium
{
    class Program
    {
        private readonly LircClient _lirc;
        private readonly Chromium _chromium;
        private readonly LinuxSignal _signal;
        readonly ILogger _logger = LogManager.GetCurrentClassLogger();

        public Program(LircClient lirc, Chromium chromium, LinuxSignal signal)
        {
            _lirc = lirc;
            _chromium = chromium;
            _signal = signal;
        }

        static void Main()
        {
            try
            {
                IKernel kernel = new StandardKernel();
                var program = kernel.Get<Program>();
                program.Run();
            }
            catch (Exception ex)
            {
                LogManager.GetCurrentClassLogger().Fatal(ex, $"Panic: {ex.Message}");
            }
        }

        private void Run()
        {
            _logger.Info("Welcome to Lirc2Chromium");
            try
            {
                _signal.Listen();
                _lirc.KeyPressed += _chromium.SendKey;
                _lirc.Listen();

                if (Console.IsInputRedirected)
                {
                    _logger.Info("Wait for kill-signal");
                    _signal.WaitForListenThreadToComplete();
                }
                else
                {
                    _logger.Info("Wait for keyboard input");
                    Console.WriteLine("Press enter to exit");
                    Console.ReadLine();
                }
            }
            catch (Exception ex)
            {
                _logger.Fatal(ex, $"Error on main: {ex.Message}");
            }
            finally
            {
                _lirc.Dispose();
                _signal.Dispose();
            }
        }
    }
}
