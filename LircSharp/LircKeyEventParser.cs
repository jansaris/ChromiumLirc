using System;
using System.Text;
using NLog;

namespace LircSharp
{
    public class LircKeyEventParser
    {
        static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public event EventHandler<LircKeyPressEventArs> CommandParsed;

        public void Parse(byte[] data, int index, int count)
        {
            Logger.Debug($"Parse {count - index} bytes");
            // Just serialize any data parsing
            var builder = new StringBuilder();
            for (var i = index; i < count; i++)
            {
                var d = data[i];
                if (data[i] != (byte)'\n')
                {
                    builder.Append((char) d);
                }
            }
            Parse(builder.ToString());
        }

        public void Parse(string keyEvent)
        {
            Logger.Debug($"Parse: {keyEvent}");
            var splitted = keyEvent.Split(' ');
            if(splitted.Length < 4) throw new LircParsingException($"Expected at least 4 parts in {keyEvent}");
            int index;
            if (!int.TryParse(splitted[1], out index))
            {
                Logger.Warn($"Failed to parse key index {splitted[1]}");
            }
            CommandParsed?.Invoke(this, new LircKeyPressEventArs(splitted[0], index, splitted[2], splitted[3]));
        }
    }
}