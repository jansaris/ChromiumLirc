using System;
using NLog;
using System.Configuration;
using LircSharp;

namespace ChromiumLirc
{
    class Program
    {
        static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        string _host;
        int _port;
        string _unixEndpoint;

        static void Main()
        {
            try
            {
                Logger.Info("Welcome to ChromiumLirc");
                
                var program = new Program();
                program.ReadConfiguration();
                program.Run();
            }
            catch (Exception ex)
            {
                Logger.Fatal(ex, $"An unhandled exception occured: {ex.Message}");
            }
        }

        void Run()
        {
            Logger.Info($"Start listening to Lirc on {_host}:{_port}");
            var lirc = new LircSocketClient();
            lirc.Message += Lirc_Message;
            lirc.CommandCompleted += Lirc_CommandCompleted;
            lirc.KeyPressed += Lirc_KeyPressed;
            lirc.Connected += Lirc_Connected;
            lirc.Error += Lirc_Error;
            if (string.IsNullOrWhiteSpace(_unixEndpoint))
            {
                lirc.Connect(_host, _port);
            }
            else
            {
                lirc.Connect(_unixEndpoint);
            }
            //lirc.Reconnect();
            Logger.Info("Wait for 'Enter' to exit");
            Console.ReadLine();
            lirc.Disconnect();
            lirc.Dispose();
        }

        void Lirc_KeyPressed(object sender, LircKeyPressEventArs e)
        {
            Logger.Info($"Lirc key pressed: {e.Key}");
        }

        private void Lirc_Error(object sender, LircErrorEventArgs e)
        {
            Logger.Warn($"Lirc error: {e.Message}");
        }

        private void Lirc_Connected(object sender, EventArgs e)
        {
            Logger.Info("Connected to Lirc");
        }

        private void Lirc_CommandCompleted(object sender, LircCommandEventArgs e)
        {
            Logger.Info($"Command: {e.Command.Command} - {e.Command.Succeeded}");
        }

        private void Lirc_Message(object sender, LircMessageEventArgs e)
        {
            Logger.Info($"Received: {e.Message}");
        }

        void ReadConfiguration()
        {
            Logger.Debug("Read configuration items");
            _host = ConfigurationManager.AppSettings["Host"];
            _unixEndpoint = ConfigurationManager.AppSettings["UnixEndpoint"];
            var port = ConfigurationManager.AppSettings["Port"];
            if (!string.IsNullOrWhiteSpace(port))
            {
                _port = int.Parse(port);
            }
        }
    }
}
