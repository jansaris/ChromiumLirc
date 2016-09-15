using System;

namespace LircSharp
{
    public class LircParsingException : Exception
    {
        public LircParsingException(string message)
            : base(message)
        {
        }

        public LircParsingException(string expectedToken, string actualToken)
            : base($"Expected {expectedToken} token, got {actualToken} token")
        {
        }
    }
}
