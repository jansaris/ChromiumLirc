using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NLog;
using static System.Int32;

namespace LircSharp
{
    public class LircCommandParser
    {
        static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        enum ParserState
        {
            Begin,
            Command,
            Result,
            DataOrEnd,
            DataCount,
            DataLine,
            End,
        }

        ParserState _state = ParserState.Begin;
        readonly object _parseLock = new object();
        StringBuilder _currentToken = new StringBuilder();
        LircCommand _currentCommand;
        int _dataLinesLeft;

        public event EventHandler<LircCommandEventArgs> CommandParsed;

        public void Parse(byte[] data, int index, int count)
        {
            Logger.Debug($"Parse {count - index} bytes");
            // Just serialize any data parsing
            lock (_parseLock)
            {
                for (var i = index; i < count; i++)
                {
                    var d = data[i];
                    if (d == (byte)'\n')
                    {
                        try
                        {
                            ParseToken(_currentToken.ToString());
                        }
                        finally
                        {
                            _currentToken = new StringBuilder();
                        }
                    }
                    else
                    {
                        _currentToken.Append((char)d);
                    }
                }
            }
        }

        public void ParseToken(string token)
        {
            try
            {
                Logger.Debug($"Parse token: {token}");
                switch (_state)
                {
                    case ParserState.Begin:
                        AssertToken("BEGIN", token);
                        _state = ParserState.Command;
                        break;
                    case ParserState.Command:
                        _currentCommand = LircCommandFactory.Create(token);
                        _state = ParserState.Result;
                        break;
                    case ParserState.Result:
                        AssertAnyToken(new[] { "SUCCESS", "ERROR" }, token);
                        _currentCommand.Succeeded = token == "SUCCESS";
                        _state = ParserState.DataOrEnd;
                        break;
                    case ParserState.DataOrEnd:
                        AssertAnyToken(new[] { "DATA", "END" }, token);
                        if (token == "END")
                        {
                            try
                            {
                                OnCommandParsed(_currentCommand);
                            }
                            finally
                            {
                                _currentCommand = null;
                                _state = ParserState.Begin;
                            }
                        }
                        else
                        {
                            _state = ParserState.DataCount;
                        }
                        break;
                    case ParserState.DataCount:
                        if (!TryParse(token, out _dataLinesLeft))
                        {
                            throw new LircParsingException("Unable to parse data line count from token " + token);
                        }
                        if(_dataLinesLeft == 0)
                        {
                            _state = ParserState.End;
                        }
                        else
                        {
                            _state = ParserState.DataLine;
                            _currentCommand.Data = new List<string>();
                        }
                        break;
                    case ParserState.DataLine:
                        _currentCommand.Data.Add(token);
                        if (--_dataLinesLeft == 0)
                        {
                            _state = ParserState.End;
                        }
                        break;
                    case ParserState.End:
                        AssertToken("END", token);
                        try
                        {
                            OnCommandParsed(_currentCommand);
                        }
                        finally
                        {
                            _currentCommand = null;
                            _state = ParserState.Begin;
                        }
                        break;
                    default:
                        throw new LircParsingException("Parsing engine in unknown state: " + _state);
                }
            }
            catch (LircParsingException)
            {
                if (_state != ParserState.Begin)
                {
                    _state = ParserState.Begin;
                    throw;
                }
            }
        }

        void OnCommandParsed(LircCommand command)
        {
            Logger.Debug($"Parsed command: {command.Command}-{command.Succeeded}");
            CommandParsed?.Invoke(this, new LircCommandEventArgs(command));
        }

        static void AssertToken(string expectedToken, string actualToken)
        {
            if (actualToken != expectedToken)
            {
                throw new LircParsingException($"Expected {expectedToken} token, got {actualToken} token");
            }
        }

        static void AssertAnyToken(ICollection<string> expectedTokens, string actualToken)
        {
            if (!expectedTokens.Contains(actualToken))
            {
                throw new LircParsingException(
                    $"Expected any of {expectedTokens.Aggregate((a, b) => a + ", " + b)} tokens, got {actualToken} token");
            }
        }
    }
}
