namespace Nokt;

public enum TokenType
{
    Say,
    Fn,
    Return,
    Export,
    Import,
    As,
    Let,
    If,
    Else,
    While,
    True,
    False,
    And,
    Or,
    Not,
    Window,
    Text,
    Button,
    Input,
    Size,
    Colon,
    String,
    Number,
    Identifier,
    Equals,
    Arrow,
    EqualEqual,
    NotEqual,
    Greater,
    Less,
    GreaterEqual,
    LessEqual,
    Plus,
    Minus,
    Star,
    Slash,
    LeftParen,
    RightParen,
    LeftBrace,
    RightBrace,
    LeftBracket,
    RightBracket,
    Comma,
    Dot,
    NewLine,
    Indent,
    Dedent,
    EndOfFile,
    Unknown
}

public class Token
{
    public TokenType Type { get; }
    public string Value { get; }
    public int Line { get; }
    public int Column { get; }

    public Token(TokenType type, string value, int line, int column = 1)
    {
        Type = type;
        Value = value;
        Line = line;
        Column = column;
    }

    public override string ToString() => $"{Type}('{Value}') at line {Line}, column {Column}";
}