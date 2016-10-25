using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NLog;

namespace Lirc2Chromium
{
    public class Chromium
    {
        readonly ILogger _logger = LogManager.GetCurrentClassLogger();
        readonly Configuration _configuration;

        public Chromium(Configuration configuration)
        {
            _configuration = configuration;
        }

        public void SendKey(object sender, string key)
        {
            _logger.Debug($"Send key {key}");
            if (!IsChromiumRunning())
            {
                _logger.Debug($"Chromium is not running, ignore {key}");
                return;
            }
            var translatedKey = TranslateKey(key);
            if(translatedKey != key) _logger.Info($"Translated key '{key}' into '{translatedKey}'");
            SendToXdoTool(translatedKey);
        }

        bool IsChromiumRunning()
        {
            _logger.Debug($"Find processes with name '{_configuration.ProcessName}'");
            var list = Process.GetProcessesByName(_configuration.ProcessName);
            _logger.Debug($"Found {list.Length} processes");
            return list.Any();
        }

        string TranslateKey(string key)
        {
            var file = new FileInfo(_configuration.KeyMapFile);
            _logger.Debug($"Read keymap file: {file.FullName}");
            try
            {
                foreach (var line in File.ReadLines(file.FullName))
                {
                    var index = line.IndexOf("=", StringComparison.Ordinal);
                    if (index == -1)
                    {
                        _logger.Warn($"No = on line {line}");
                        continue;
                    }

                    if (line.Substring(0, index) == key)
                    {
                        return line.Substring(index, line.Length - index);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"Failed to translate key '{key}': {ex.Message}");
            }

            return key;
        }

        void SendToXdoTool(string key)
        {
            try
            {
                _logger.Debug($"Send key {key} to xdo tool");
                var xdoTool = new FileInfo(_configuration.XdoTool);
                if (!xdoTool.Exists) throw new FileNotFoundException(xdoTool.FullName);
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo(xdoTool.FullName)
                    {
                        Arguments = $"key {key}",
                        UseShellExecute = true
                    }
                };
                process.Start();
                process.WaitForExit(1000);
                _logger.Info($"Sended key {key} to XdoTool with result {process.ExitCode}");
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, $"Failed to send {key} to {_configuration.XdoTool}: {ex.Message}");
            }
        }
    }
}