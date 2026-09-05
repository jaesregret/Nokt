namespace Nokt;
public enum TokenType
{
    Say,
    String,
    EndOfFile,
    Unknown
}
public class Token
{
    public TokenType Type { get; }
    public string Value { get; }
    public int Line { get; }
    public Token(TokenType type, string value, int line)
    {
        Type = type;
        Value = value;
        Line = line;
    }
=
    public override string ToString() => $"{Type}('{Value}') Line {Line}";
}
