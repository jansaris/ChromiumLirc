using System;
using System.Text;
using NLog;

namespace Lirc2Chromium
{
    class LircEvent
    {
        static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static LircEvent Parse(byte[] data, int count)
        {
            Logger.Debug($"Parse {count} bytes");
            var keyEvent = Encoding.Default.GetString(data, 0, count);
            Logger.Debug($"Parse: {keyEvent}");
            var splitted = keyEvent.Split(' ');
            if (splitted.Length < 4) throw new FormatException($"Expected at least 4 parts in {keyEvent}");
            int index;
            if (!int.TryParse(splitted[1], out index))
            {
                Logger.Warn($"Failed to parse key index {splitted[1]}");
            }
            return new LircEvent
            {
                Code = splitted[0],
                Index = index,
                Key = splitted[2],
                Remote = splitted[3]
            };
        }

        public string Remote { get; private set; }

        public int Index { get; private set; }

        public string Key { get; private set; }

        public string Code { get; private set; }
    }
}