using System.Collections.Generic;

namespace Nokt;

public abstract class Statement { }

public abstract class Expression { }

public class StringLiteral : Expression
{
    public string Value { get; }
    public StringLiteral(string value) => Value = value;
}

public class NumberLiteral : Expression
{
    public int Value { get; }
    public NumberLiteral(int value) => Value = value;
}

public class VariableExpression : Expression
{
    public string Name { get; }
    public VariableExpression(string name) => Name = name;
}

public class SayStatement : Statement
{
    public Expression Value { get; }
    public SayStatement(Expression value) => Value = value;
}

public class LetStatement : Statement
{
    public string Name { get; }
    public Expression Value { get; }

    public LetStatement(string name, Expression value)
    {
        Name = name;
        Value = value;
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

        if (Match(TokenType.Let))
        {
            return ParseLet();
        }

        Token unexpected = Peek();
        throw new NoktException($"Invalid syntax: expected 'say' or 'let', found '{unexpected.Value}' at line {unexpected.Line}");
    }

    private SayStatement ParseSay()
    {
        Expression value = ParseExpression("say");
        return new SayStatement(value);
    }

    private LetStatement ParseLet()
    {
        if (!Match(TokenType.Identifier))
        {
            Token unexpected = Peek();
            throw new NoktException($"Invalid syntax: expected variable name after 'let', found '{unexpected.Value}' at line {unexpected.Line}");
        }

        string name = Previous().Value;

        if (!Match(TokenType.Equals))
        {
            Token unexpected = Peek();
            throw new NoktException($"Invalid syntax: expected '=' after variable name, found '{unexpected.Value}' at line {unexpected.Line}");
        }

        Expression value = ParseExpression("let");
        return new LetStatement(name, value);
    }

    private Expression ParseExpression(string context)
    {
        if (Match(TokenType.String))
        {
            return new StringLiteral(Previous().Value);
        }

        if (Match(TokenType.Number))
        {
            return new NumberLiteral(int.Parse(Previous().Value));
        }

        if (Match(TokenType.Identifier))
        {
            return new VariableExpression(Previous().Value);
        }

        Token unexpected = Peek();
        throw new NoktException($"Invalid syntax: expected a value after '{context}', found '{unexpected.Value}' at line {unexpected.Line}");
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