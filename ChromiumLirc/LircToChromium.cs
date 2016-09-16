using System;
using System.Collections.Generic;
using System.IO;
using LircSharp;
using NLog;

namespace ChromiumLirc
{
    class LircToChromium : IDisposable
    {
        static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        readonly LircClient _lirc;
        readonly Chromium _chromium;
        readonly string _keymap;
        readonly SendKeys _keySender;
        readonly Dictionary<string,string> _dictionary = new Dictionary<string, string>();

        string ChromiumAliveString => _chromium.IsAlive ? string.Empty : "not ";
        bool _disposed;

        public LircToChromium(LircClient lirc, Chromium chromium, SendKeys keySender, string keymap)
        {
            _lirc = lirc;
            _chromium = chromium;
            _keymap = keymap;
            _keySender = keySender;
        }

        public void Run()
        {
            ReadKeyMap();
            _lirc.KeyPressed += Lirc_KeyPressed;
            _lirc.Connected += Lirc_Connected;
            _lirc.Error += Lirc_Error;
        }

        void ReadKeyMap()
        {
            var file = new FileInfo(_keymap);
            Logger.Info($"Read keymap file: {file.FullName}");
            var lines = File.ReadAllLines(file.FullName);
            Logger.Debug($"Read {lines.Length} lines");
            foreach(var line in lines)
            {
                try
                {
                    var index = line.IndexOf("=", StringComparison.Ordinal);
                    if(index == -1) throw new Exception($"No = on line {line}");
                    var key = line.Substring(0, index);
                    index++;
                    var value = line.Substring(index, line.Length - index);
                    _dictionary.Add(key,value);
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, $"Failed to read keymap line {line}: {ex.Message}, SKIP");
                }
            }
        }

        void Lirc_KeyPressed(object sender, LircKeyPressEventArs e)
        {
            Logger.Info($"Lirc key pressed: {e.Key} - Chromium is{ChromiumAliveString} alive");
            if (_chromium.IsAlive && _dictionary.ContainsKey(e.Key))
            {
                _keySender.SendKey(_dictionary[e.Key], _chromium.ActivePid);
            }
        }

        void Lirc_Error(object sender, LircErrorEventArgs e)
        {
            Logger.Warn($"Lirc error: {e.Message} - Chromium is{ChromiumAliveString} alive");
        }

        void Lirc_Connected(object sender, EventArgs e)
        {
            Logger.Info($"Connected to Lirc - Chromium is{ChromiumAliveString} alive");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Logger.Debug("Dispose");
        }
    }
}
