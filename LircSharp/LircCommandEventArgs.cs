using System;

namespace LircSharp
{
    public class LircCommandEventArgs : EventArgs
    {
        public LircCommand Command { get; private set; }

        public LircCommandEventArgs(LircCommand command)
        {
            Command = command;
        }
    }

    public class LircMessageEventArgs : EventArgs
    {
        public string Message { get; private set; }

        public LircMessageEventArgs(string message)
        {
            Message = message;
        }
    }

    public class LircKeyPressEventArs : EventArgs
    {
        public string Key { get; private set; }
        public int Index { get; private set; }
        public string Code { get; private set; }
        public string Remote { get; private set; }

        public LircKeyPressEventArs(string code, int index, string key, string remote)
        {
            Code = code;
            Key = key;
            Index = index;
            Remote = remote;
        }
    }
}
