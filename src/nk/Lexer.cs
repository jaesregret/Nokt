using System.Collections.Generic;
using System.Text;

namespace Nokt;

public class Lexer
{
    private static readonly IReadOnlyDictionary<string, TokenType> Keywords = new Dictionary<string, TokenType>
    {
        ["say"] = TokenType.Say,
        ["import"] = TokenType.Import,
        ["let"] = TokenType.Let,
        ["if"] = TokenType.If,
        ["else"] = TokenType.Else,
        ["while"] = TokenType.While,
        ["true"] = TokenType.True,
        ["false"] = TokenType.False,
        ["and"] = TokenType.And,
        ["or"] = TokenType.Or,
        ["not"] = TokenType.Not,
        ["window"] = TokenType.Window,
        ["text"] = TokenType.Text,
        ["button"] = TokenType.Button,
        ["input"] = TokenType.Input,
        ["size"] = TokenType.Size
    };

    private readonly string _source;
    private int _position;
    private int _line = 1;
    private int _column = 1;
    private bool _atLineStart = true;
    private readonly List<int> _indentation = new() { 0 };

    public Lexer(string source)
    {
        _source = source;
        _position = 0;
    }

    public List<Token> Tokenize()
    {
        var tokens = new List<Token>();

        while (!IsAtEnd())
        {
            if (_atLineStart)
            {
                ReadIndentation(tokens);
                if (IsAtEnd()) break;
            }

            if (Peek() == ' ' || Peek() == '\t' || Peek() == '\r')
            {
                SkipWhitespace();
                continue;
            }

            char c = Peek();

            if (c == '\n')
            {
                int line = _line;
                int column = _column;
                Advance();
                tokens.Add(new Token(TokenType.NewLine, "\\n", line, column));
                _line++;
                _column = 1;
                _atLineStart = true;
            }
            else if (c == '#')
            {
                while (!IsAtEnd() && Peek() != '\n') Advance();
            }
            else if (c == '"')
            {
                tokens.Add(ReadString());
            }
            else if (IsLetter(c))
            {
                tokens.Add(ReadIdentifier());
            }
            else if (IsDigit(c))
            {
                tokens.Add(ReadNumber());
            }
            else if (c == '=')
            {
                int line = _line;
                int column = _column;
                Advance();
                if (MatchCharacter('='))
                    tokens.Add(new Token(TokenType.EqualEqual, "==", line, column));
                else
                    tokens.Add(new Token(TokenType.Equals, "=", line, column));
            }
            else if (c == ':')
            {
                tokens.Add(ReadSingle(TokenType.Colon));
            }
            else if (c == '!')
            {
                int line = _line;
                int column = _column;
                Advance();
                if (!MatchCharacter('='))
                    throw Error("expected '=' after '!'", line, column, "!");
                tokens.Add(new Token(TokenType.NotEqual, "!=", line, column));
            }
            else if (c == '>')
            {
                tokens.Add(ReadComparison(TokenType.Greater, TokenType.GreaterEqual));
            }
            else if (c == '<')
            {
                tokens.Add(ReadComparison(TokenType.Less, TokenType.LessEqual));
            }
            else if (c == '+')
            {
                tokens.Add(ReadSingle(TokenType.Plus));
            }
            else if (c == '-')
            {
                tokens.Add(ReadSingle(TokenType.Minus));
            }
            else if (c == '*')
            {
                tokens.Add(ReadSingle(TokenType.Star));
            }
            else if (c == '/')
            {
                tokens.Add(ReadSingle(TokenType.Slash));
            }
            else if (c == '(')
            {
                tokens.Add(ReadSingle(TokenType.LeftParen));
            }
            else if (c == ')')
            {
                tokens.Add(ReadSingle(TokenType.RightParen));
            }
            else if (c == '{')
            {
                tokens.Add(ReadSingle(TokenType.LeftBrace));
            }
            else if (c == '}')
            {
                tokens.Add(ReadSingle(TokenType.RightBrace));
            }
            else if (c == '[')
            {
                tokens.Add(ReadSingle(TokenType.LeftBracket));
            }
            else if (c == ']')
            {
                tokens.Add(ReadSingle(TokenType.RightBracket));
            }
            else if (c == ',')
            {
                tokens.Add(ReadSingle(TokenType.Comma));
            }
            else
            {
                throw Error($"invalid character '{c}'", _line, _column, c.ToString());
            }
        }

        while (_indentation.Count > 1)
        {
            _indentation.RemoveAt(_indentation.Count - 1);
            tokens.Add(new Token(TokenType.Dedent, "", _line, _column));
        }

        tokens.Add(new Token(TokenType.EndOfFile, "", _line, _column));
        return tokens;
    }

    private void ReadIndentation(List<Token> tokens)
    {
        int startColumn = _column;
        int spaces = 0;

        while (!IsAtEnd() && (Peek() == ' ' || Peek() == '\t'))
        {
            spaces += Peek() == '\t' ? 4 : 1;
            Advance();
        }

        if (IsAtEnd() || Peek() == '\n' || Peek() == '#')
            return;

        _atLineStart = false;
        int currentIndentation = _indentation[^1];
        if (spaces > currentIndentation)
        {
            _indentation.Add(spaces);
            tokens.Add(new Token(TokenType.Indent, "", _line, startColumn));
        }
        else if (spaces < currentIndentation)
        {
            while (_indentation.Count > 1 && spaces < _indentation[^1])
            {
                _indentation.RemoveAt(_indentation.Count - 1);
                tokens.Add(new Token(TokenType.Dedent, "", _line, startColumn));
            }

            if (spaces != _indentation[^1])
                throw Error("inconsistent indentation", _line, startColumn, spaces.ToString());
        }
    }

    private Token ReadString()
    {
        int line = _line;
        int column = _column;
        Advance();
        var sb = new StringBuilder();

        while (!IsAtEnd() && Peek() != '"')
        {
            if (Peek() == '\n')
                throw Error("unterminated string", line, column, "\"");

            sb.Append(Advance());
        }

        if (IsAtEnd())
            throw Error("unterminated string", line, column, "\"");

        Advance();
        return new Token(TokenType.String, sb.ToString(), line, column);
    }

    private Token ReadIdentifier()
    {
        int line = _line;
        int column = _column;
        int start = _position;

        while (!IsAtEnd() && (IsLetter(Peek()) || IsDigit(Peek())))
        {
            Advance();
        }

        string text = _source[start.._position];

        return new Token(Keywords.GetValueOrDefault(text, TokenType.Identifier), text, line, column);
    }

    private Token ReadNumber()
    {
        int line = _line;
        int column = _column;
        int start = _position;

        while (!IsAtEnd() && IsDigit(Peek()))
        {
            Advance();
        }

        return new Token(TokenType.Number, _source[start.._position], line, column);
    }

    private void SkipWhitespace()
    {
        while (!IsAtEnd())
        {
            char c = Peek();

            if (c == ' ' || c == '\t' || c == '\r')
            {
                Advance();
            }
            else
            {
                break;
            }
        }
    }

    private Token ReadSingle(TokenType type)
    {
        int line = _line;
        int column = _column;
        string value = Advance().ToString();
        return new Token(type, value, line, column);
    }

    private Token ReadComparison(TokenType single, TokenType combined)
    {
        int line = _line;
        int column = _column;
        string value = Advance().ToString();
        if (MatchCharacter('=')) value += "=";
        return new Token(value == ">=" || value == "<=" ? combined : single, value, line, column);
    }

    private bool MatchCharacter(char expected)
    {
        if (IsAtEnd() || Peek() != expected) return false;
        Advance();
        return true;
    }

    private NoktException Error(string message, int line, int column, string value) =>
        new($"{message} at line {line}, column {column}; token/value '{value}'");

    private char Peek() => _source[_position];

    private char Advance()
    {
        char value = _source[_position++];
        _column++;
        return value;
    }
    private bool IsAtEnd() => _position >= _source.Length;

    private static bool IsLetter(char c) =>
        (c >= 'a' && c <= 'z') ||
        (c >= 'A' && c <= 'Z') ||
        c == '_';

    private static bool IsDigit(char c) => c >= '0' && c <= '9';
}