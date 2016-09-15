using NLog;

namespace LircSharp
{
    public static class LircCommandFactory
    {
        static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static LircCommand Create(string command)
        {
            Logger.Debug($"Create LircCommand for: {command}");
            var commandTokens= command.Split(' ');
            switch (commandTokens[0])
            {
                case "VERSION":
                    return new LircVersionCommand();
                case "LIST":
                    if (commandTokens.Length == 1)
                    {
                        return new LircListRemotesCommand();
                    }
                    return new LircListRemoteCommand(commandTokens[1]);
                //case "SIGHUP":
                //case "SEND_ONCE":
                //case "SEND_START":
                //case "SEND_STOP":
                default:
                    return new LircCommand
                    {
                        Command = command,
                    };
            }
        }
    }

}
