using System.Collections.Generic;

namespace Nokt;

public abstract class Statement { }

public class SayStatement : Statement
{
    public string Message { get; }

    public SayStatement(string message)
    {
        Message = message;
    }
}

public class Parser
{
    private readonly List<Token> _tokens;
    private int _current = 0;

    public Parser(List<Token> tokens)
    {
        _tokens = tokens;
    }

    public List<Statement> Parse()
    {
        var statements = new List<Statement>();

        while (!IsAtEnd())
        {
            statements.Add(ParseStatement());
        }

        return statements;
    }

    private Statement ParseStatement()
    {
        if (Match(TokenType.Say))
        {
            return ParseSay();
        }

        Token unexpected = Peek();
        throw new NoktException($"Invalid syntax: expected 'say', found '{unexpected.Value}' at line {unexpected.Line}");
    }

    private SayStatement ParseSay()
    {
        if (!Match(TokenType.String))
        {
            Token unexpected = Peek();
            throw new NoktException($"Invalid syntax: expected string after 'say', found '{unexpected.Value}' at line {unexpected.Line}");
        }

        string message = Previous().Value;
        return new SayStatement(message);
    }

    private bool Match(TokenType type)
    {
        if (Check(type))
        {
            Advance();
            return true;
        }

        return false;
    }

    private bool Check(TokenType type)
    {
        if (IsAtEnd()) return false;
        return Peek().Type == type;
    }

    private Token Advance()
    {
        if (!IsAtEnd()) _current++;
        return Previous();
    }

    private bool IsAtEnd() => Peek().Type == TokenType.EndOfFile;
    private Token Peek() => _tokens[_current];
    private Token Previous() => _tokens[_current - 1];
}
