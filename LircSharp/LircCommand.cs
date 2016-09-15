using System.Collections.Generic;

namespace LircSharp
{
    public class LircCommand
    {
        public string Command { get; set; }
        public bool Succeeded { get; set; }
        public List<string> Data { get; set; } = new List<string>();
    }

    public class LircVersionCommand : LircCommand
    {
        public LircVersionCommand()
        {
            Command = "Version";
        }

        public string Version => Data[0];
    }

    public class LircListRemotesCommand : LircCommand
    {
        public LircListRemotesCommand()
        {
            Command = "ListRemotes";
        }

        public List<string> Remotes => Data;
    }

    public class LircListRemoteCommand : LircCommand
    {
        public LircListRemoteCommand(string remote)
        {
            Command = "ListRemote";
            Remote = remote;
        }

        public string Remote { get; }
    }
}
