using System;

namespace LircSharp
{
    public class LircErrorEventArgs : EventArgs
    {
        public string Message { get; private set; }
        public Exception Exception { get; private set; }

        public LircErrorEventArgs(string message, Exception exception = null)
        {
            Message = message;
            Exception = exception;
        }
    }

}
