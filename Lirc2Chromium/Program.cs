using System;
using Ninject;
using NLog;

namespace Lirc2Chromium
{
    class Program
    {
        readonly LircClient _lirc;
        readonly Chromium _chromium;
        readonly LinuxSignal _signal;
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

        void Run()
        {
            _logger.Info("Welcome to Lirc2Chromium");
            try
            {
                _signal.Listen();
                _lirc.KeyAction += _chromium.SendKey;
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
                    _logger.Info("Received stop event, close application");
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
