using System;
using NLog;
using System.Configuration;
using System.IO;
using System.Reflection;
using LircSharp;

namespace ChromiumLirc
{
    class Program
    {
        static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        string _host;
        int _port;
        string _unixEndpoint;
        string _processName;
        string _keymap;
        string _xdoTool;

        static void Main()
        {
            try
            {
                Logger.Info("Welcome to ChromiumLirc");
                Directory.SetCurrentDirectory(AssemblyDirectory);
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
            //Initialize
            var lirc = new LircSocketClient();
            var chromium = new Chromium(_processName);
            var sendKeys = new SendKeys(_xdoTool);
            var connect = new LircToChromium(lirc,chromium, sendKeys, _keymap);

            try
            {
                //Start threads
                if (string.IsNullOrWhiteSpace(_unixEndpoint)) lirc.Connect(_host, _port);
                else lirc.Connect(_unixEndpoint);
                chromium.Watch();
                connect.Run();
                Logger.Info("Wait for 'Enter' to exit");
                Console.ReadLine();
            }
            finally
            {
                lirc.Disconnect();
                lirc.Dispose();
                chromium.Dispose();
                sendKeys.Dispose();
                connect.Dispose();
            }
        }

        void ReadConfiguration()
        {
            Logger.Debug("Read configuration items");
            _host = ConfigurationManager.AppSettings["Host"];
            _unixEndpoint = ConfigurationManager.AppSettings["UnixEndpoint"];
            _processName = ConfigurationManager.AppSettings["ProcessName"];
            _keymap = ConfigurationManager.AppSettings["KeyMapFile"];
            _xdoTool = ConfigurationManager.AppSettings["XdoTool"];
            var port = ConfigurationManager.AppSettings["Port"];
            if (!string.IsNullOrWhiteSpace(port))
            {
                _port = int.Parse(port);
            }
        }

        static string AssemblyDirectory
        {
            get
            {
                var codeBase = Assembly.GetExecutingAssembly().CodeBase;
                var uri = new UriBuilder(codeBase);
                var path = Uri.UnescapeDataString(uri.Path);
                return Path.GetDirectoryName(path);
            }
        }
    }
}
