using System.Collections.Generic;
using System.Text;

namespace Nokt;

public class Lexer
{
    private readonly string _source;
    private int _position;
    private int _line = 1;

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
            SkipWhitespace();

            if (IsAtEnd()) break;

            char c = Peek();

            if (c == '"')
            {
                tokens.Add(ReadString());
            }
            else if (IsLetter(c))
            {
                tokens.Add(ReadIdentifier());
            }
            else
            {
                throw new NoktException($"Invalid character '{c}' at line {_line}");
            }
        }

        tokens.Add(new Token(TokenType.EndOfFile, "", _line));
        return tokens;
    }

    private Token ReadString()
    {
        Advance();
        var sb = new StringBuilder();

        while (!IsAtEnd() && Peek() != '"')
        {
            if (Peek() == '\n')
                throw new NoktException($"Unterminated string at line {_line}");

            sb.Append(Advance());
        }

        if (IsAtEnd())
            throw new NoktException($"Unterminated string at line {_line}");

        Advance();
        return new Token(TokenType.String, sb.ToString(), _line);
    }

    private Token ReadIdentifier()
    {
        var sb = new StringBuilder();

        while (!IsAtEnd() && (IsLetter(Peek()) || IsDigit(Peek())))
        {
            sb.Append(Advance());
        }

        string text = sb.ToString();

        if (text == "say")
            return new Token(TokenType.Say, text, _line);

        throw new NoktException($"Unknown command '{text}' at line {_line}");
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
            else if (c == '\n')
            {
                _line++;
                Advance();
            }
            else
            {
                break;
            }
        }
    }

    private char Peek() => _source[_position];
    private char Advance() => _source[_position++];
    private bool IsAtEnd() => _position >= _source.Length;

    private static bool IsLetter(char c) =>
        (c >= 'a' && c <= 'z') ||
        (c >= 'A' && c <= 'Z') ||
        c == '_';

    private static bool IsDigit(char c) => c >= '0' && c <= '9';
}
