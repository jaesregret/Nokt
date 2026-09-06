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

public class BooleanLiteral : Expression
{
    public bool Value { get; }
    public BooleanLiteral(bool value) => Value = value;
}

public class ListExpression : Expression
{
    public List<Expression> Items { get; }
    public ListExpression(List<Expression> items) => Items = items;
}

public class IndexExpression : Expression
{
    public Expression Collection { get; }
    public Expression Index { get; }

    public IndexExpression(Expression collection, Expression index)
    {
        Collection = collection;
        Index = index;
    }
}

public class VariableExpression : Expression
{
    public string Name { get; }
    public VariableExpression(string name) => Name = name;
}

public class UnaryExpression : Expression
{
    public string Operator { get; }
    public Expression Operand { get; }
    public Token Token { get; }

    public UnaryExpression(string @operator, Expression operand, Token token)
    {
        Operator = @operator;
        Operand = operand;
        Token = token;
    }
}

public class BinaryExpression : Expression
{
    public Expression Left { get; }
    public string Operator { get; }
    public Expression Right { get; }
    public Token Token { get; }

    public BinaryExpression(Expression left, string @operator, Expression right, Token token)
    {
        Left = left;
        Operator = @operator;
        Right = right;
        Token = token;
    }
}

public class SayStatement : Statement
{
    public Expression Value { get; }
    public SayStatement(Expression value) => Value = value;
}

public class ImportStatement : Statement
{
    public string Path { get; }
    public ImportStatement(string path) => Path = path;
}

public class LetStatement : Statement
{
    public string Name { get; }
    public string? DeclaredType { get; }
    public Expression Value { get; }

    public LetStatement(string name, string? declaredType, Expression value)
    {
        Name = name;
        DeclaredType = declaredType;
        Value = value;
    }
}

public class AssignmentStatement : Statement
{
    public string Name { get; }
    public Expression Value { get; }

    public AssignmentStatement(string name, Expression value)
    {
        Name = name;
        Value = value;
    }
}

public class IfStatement : Statement
{
    public Expression Condition { get; }
    public List<Statement> ThenBranch { get; }
    public List<Statement>? ElseBranch { get; }

    public IfStatement(Expression condition, List<Statement> thenBranch, List<Statement>? elseBranch)
    {
        Condition = condition;
        ThenBranch = thenBranch;
        ElseBranch = elseBranch;
    }
}

public class WhileStatement : Statement
{
    public Expression Condition { get; }
    public List<Statement> Body { get; }

    public WhileStatement(Expression condition, List<Statement> body)
    {
        Condition = condition;
        Body = body;
    }
}

public class WindowStatement : Statement
{
    public UiWindowDefinition Window { get; }
    public WindowStatement(UiWindowDefinition window) => Window = window;
}

public class UiWindowDefinition
{
    public string Title { get; }
    public int Width { get; set; } = 640;
    public int Height { get; set; } = 400;
    public List<UiElementDefinition> Elements { get; } = new();
    public UiWindowDefinition(string title) => Title = title;
}

public abstract class UiElementDefinition { }

public class UiTextDefinition : UiElementDefinition
{
    public string Value { get; }
    public UiTextDefinition(string value) => Value = value;
}

public class UiInputDefinition : UiElementDefinition
{
    public string Placeholder { get; }
    public UiInputDefinition(string placeholder) => Placeholder = placeholder;
}

public class UiButtonDefinition : UiElementDefinition
{
    public string Label { get; }
    public List<Statement> OnClick { get; }

    public UiButtonDefinition(string label, List<Statement> onClick)
    {
        Label = label;
        OnClick = onClick;
    }
}

public class Parser
{
    private readonly List<Token> _tokens;
    private int _current;

    public Parser(List<Token> tokens) => _tokens = tokens;

    public List<Statement> Parse()
    {
        var statements = new List<Statement>();
        SkipNewLines();
        while (!IsAtEnd())
        {
            statements.Add(ParseStatement());
            SkipNewLines();
        }
        return statements;
    }

    private Statement ParseStatement()
    {
        if (Match(TokenType.Import))
        {
            Token path = Consume(TokenType.String, "expected module path after 'import'");
            RequireLineEnd("after import");
            return new ImportStatement(path.Value);
        }

        if (Match(TokenType.Say))
        {
            Expression value = ParseExpression();
            RequireLineEnd("after 'say'");
            return new SayStatement(value);
        }

        if (Match(TokenType.Let)) return ParseLet();
        if (Check(TokenType.Identifier) && CheckNext(TokenType.Equals)) return ParseAssignment();
        if (Match(TokenType.If)) return ParseIf();
        if (Match(TokenType.While)) return ParseWhile();
        if (Match(TokenType.Window)) return ParseWindow();

        Token unexpected = Peek();
        throw Error($"expected 'say', 'let', assignment, 'if' or 'while', found '{Display(unexpected)}'", unexpected);
    }

    private LetStatement ParseLet()
    {
        Token name = Consume(TokenType.Identifier, "expected variable name after 'let'");
        string? declaredType = null;
        if (Match(TokenType.Colon))
        {
            Token type = Consume(TokenType.Identifier, "expected type name after ':'");
            declaredType = type.Value;
        }
        Consume(TokenType.Equals, "expected '=' after variable name");
        Expression value = ParseExpression();
        RequireLineEnd("after let assignment");
        return new LetStatement(name.Value, declaredType, value);
    }

    private AssignmentStatement ParseAssignment()
    {
        Token name = Advance();
        Consume(TokenType.Equals, "expected '=' after variable name");
        Expression value = ParseExpression();
        RequireLineEnd("after assignment");
        return new AssignmentStatement(name.Value, value);
    }

    private IfStatement ParseIf()
    {
        Expression condition = ParseExpression();
        RequireNewLine("after if condition");
        List<Statement> thenBranch = ParseIndentedBlock("if");
        List<Statement>? elseBranch = null;
        if (Match(TokenType.Else))
        {
            RequireNewLine("after 'else'");
            elseBranch = ParseIndentedBlock("else");
        }
        return new IfStatement(condition, thenBranch, elseBranch);
    }

    private WhileStatement ParseWhile()
    {
        Expression condition = ParseExpression();
        RequireNewLine("after while condition");
        return new WhileStatement(condition, ParseIndentedBlock("while"));
    }

    private WindowStatement ParseWindow()
    {
            Token title = Consume(TokenType.String, "expected window title after 'window'");
            Consume(TokenType.LeftBrace, "expected '{' after window title");
            var window = new UiWindowDefinition(title.Value);
            SkipUiLayout();

            while (!Check(TokenType.RightBrace) && !IsAtEnd())
            {
                if (Match(TokenType.Size))
                {
                    Token width = Consume(TokenType.Number, "expected width after 'size'");
                    Token height = Consume(TokenType.Number, "expected height after width");
                    window.Width = int.Parse(width.Value);
                    window.Height = int.Parse(height.Value);
                    RequireUiLineEnd("after size");
                }
                else if (Match(TokenType.Text))
                {
                    window.Elements.Add(new UiTextDefinition(Consume(TokenType.String, "expected text value after 'text'").Value));
                    RequireUiLineEnd("after text");
                }
                else if (Match(TokenType.Input))
                {
                    window.Elements.Add(new UiInputDefinition(Consume(TokenType.String, "expected placeholder after 'input'").Value));
                    RequireUiLineEnd("after input");
                }
                else if (Match(TokenType.Button))
                {
                    Token label = Consume(TokenType.String, "expected button label after 'button'");
                    Consume(TokenType.LeftBrace, "expected '{' after button label");
                    window.Elements.Add(new UiButtonDefinition(label.Value, ParseCodeBlock("button")));
                }
                else
                {
                    throw Error($"expected 'size', 'text', 'input' or 'button' in window, found '{Display(Peek())}'", Peek());
                }

                SkipUiLayout();
            }

            Consume(TokenType.RightBrace, "expected '}' after window contents");
            RequireLineEnd("after window");
            return new WindowStatement(window);
    }

    private List<Statement> ParseCodeBlock(string owner)
    {
            var statements = new List<Statement>();
            SkipUiLayout();
            while (!Check(TokenType.RightBrace) && !IsAtEnd())
            {
                statements.Add(ParseStatement());
                SkipNewLines();
                SkipUiLayout();
            }

            if (statements.Count == 0)
                throw Error($"expected at least one statement in '{owner}' block", Peek());

            Consume(TokenType.RightBrace, $"expected '}}' after {owner} block");
            return statements;
    }

    private void RequireUiLineEnd(string context)
    {
            if (!Check(TokenType.NewLine) && !Check(TokenType.RightBrace) && !IsAtEnd())
                throw Error($"expected end of line {context}", Peek());
            Match(TokenType.NewLine);
    }

    private void SkipUiLayout()
    {
            while (Match(TokenType.NewLine, TokenType.Indent, TokenType.Dedent)) { }
    }

    private List<Statement> ParseIndentedBlock(string owner)
    {
        Consume(TokenType.Indent, $"expected an indented block after '{owner}'");
        var statements = new List<Statement>();
        SkipNewLines();
        while (!Check(TokenType.Dedent) && !IsAtEnd())
        {
            statements.Add(ParseStatement());
            SkipNewLines();
        }
        if (statements.Count == 0)
            throw Error($"expected at least one statement in '{owner}' block", Peek());
        Consume(TokenType.Dedent, $"expected end of '{owner}' block");
        return statements;
    }

    private Expression ParseExpression() => ParseOr();

    private Expression ParseOr()
    {
        Expression expression = ParseAnd();
        while (Match(TokenType.Or))
        {
            Token token = Previous();
            expression = new BinaryExpression(expression, "or", ParseAnd(), token);
        }
        return expression;
    }

    private Expression ParseAnd()
    {
        Expression expression = ParseEquality();
        while (Match(TokenType.And))
        {
            Token token = Previous();
            expression = new BinaryExpression(expression, "and", ParseEquality(), token);
        }
        return expression;
    }

    private Expression ParseEquality()
    {
        Expression expression = ParseComparison();
        while (Match(TokenType.EqualEqual, TokenType.NotEqual))
        {
            Token token = Previous();
            expression = new BinaryExpression(expression, token.Value, ParseComparison(), token);
        }
        return expression;
    }

    private Expression ParseComparison()
    {
        Expression expression = ParseTerm();
        while (Match(TokenType.Greater, TokenType.Less, TokenType.GreaterEqual, TokenType.LessEqual))
        {
            Token token = Previous();
            expression = new BinaryExpression(expression, token.Value, ParseTerm(), token);
        }
        return expression;
    }

    private Expression ParseTerm()
    {
        Expression expression = ParseFactor();
        while (Match(TokenType.Plus, TokenType.Minus))
        {
            Token token = Previous();
            expression = new BinaryExpression(expression, token.Value, ParseFactor(), token);
        }
        return expression;
    }

    private Expression ParseFactor()
    {
        Expression expression = ParseUnary();
        while (Match(TokenType.Star, TokenType.Slash))
        {
            Token token = Previous();
            expression = new BinaryExpression(expression, token.Value, ParseUnary(), token);
        }
        return expression;
    }

    private Expression ParseUnary()
    {
        if (Match(TokenType.Not, TokenType.Minus))
        {
            Token token = Previous();
            return new UnaryExpression(token.Value, ParseUnary(), token);
        }
        return ParsePrimary();
    }

    private Expression ParsePrimary()
    {
        Expression expression;
        if (Match(TokenType.String))
            expression = new StringLiteral(Previous().Value);
        else if (Match(TokenType.Number))
            expression = new NumberLiteral(int.Parse(Previous().Value));
        else if (Match(TokenType.True))
            expression = new BooleanLiteral(true);
        else if (Match(TokenType.False))
            expression = new BooleanLiteral(false);
        else if (Match(TokenType.Identifier))
            expression = new VariableExpression(Previous().Value);
        else if (Match(TokenType.LeftBracket))
        {
            var items = new List<Expression>();
            if (!Check(TokenType.RightBracket))
            {
                do
                {
                    items.Add(ParseExpression());
                } while (Match(TokenType.Comma));
            }
            Consume(TokenType.RightBracket, "expected ']' after list");
            expression = new ListExpression(items);
        }
        else if (Match(TokenType.LeftParen))
        {
            expression = ParseExpression();
            Consume(TokenType.RightParen, "expected ')' after expression");
        }
        else
        {
            Token unexpected = Peek();
            throw Error($"expected expression, found '{Display(unexpected)}'", unexpected);
        }

        while (Match(TokenType.LeftBracket))
        {
            Expression index = ParseExpression();
            Consume(TokenType.RightBracket, "expected ']' after index");
            expression = new IndexExpression(expression, index);
        }

        return expression;
    }

    private void RequireNewLine(string context)
    {
        if (!Match(TokenType.NewLine)) throw Error($"expected end of line {context}", Peek());
    }

    private void RequireLineEnd(string context)
    {
        if (!Check(TokenType.NewLine) && !IsAtEnd() && !Check(TokenType.Dedent))
            throw Error($"expected end of line {context}", Peek());
        Match(TokenType.NewLine);
    }

    private Token Consume(TokenType type, string message)
    {
        if (Check(type)) return Advance();
        throw Error(message + $", found '{Display(Peek())}'", Peek());
    }

    private bool Match(params TokenType[] types)
    {
        foreach (TokenType type in types)
        {
            if (Check(type))
            {
                Advance();
                return true;
            }
        }
        return false;
    }

    private bool Check(TokenType type) => !IsAtEnd() && Peek().Type == type;
    private bool CheckNext(TokenType type) => _current + 1 < _tokens.Count && _tokens[_current + 1].Type == type;

    private void SkipNewLines()
    {
        while (Match(TokenType.NewLine)) { }
    }

    private Token Advance()
    {
        if (!IsAtEnd()) _current++;
        return Previous();
    }

    private bool IsAtEnd() => Peek().Type == TokenType.EndOfFile;
    private Token Peek() => _tokens[_current];
    private Token Previous() => _tokens[_current - 1];
    private static string Display(Token token) => string.IsNullOrEmpty(token.Value) ? token.Type.ToString() : token.Value;
    private static NoktException Error(string message, Token token) =>
        new($"{message} at line {token.Line}, column {token.Column}; token/value '{Display(token)}'");
}