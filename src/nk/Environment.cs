using System.Collections.Generic;

namespace Nokt;

public enum ScopeKind
{
    Global,
    Local,
    Module
}

public sealed class Environment
{
    private readonly Dictionary<string, Variable> _variables = new();

    public Environment? Parent { get; }
    public ScopeKind Kind { get; }

    public Environment(ScopeKind kind = ScopeKind.Global, Environment? parent = null)
    {
        Kind = kind;
        Parent = parent;
    }

    public Environment CreateChild(ScopeKind kind) => new(kind, this);

    public void Define(string name, Variable variable)
    {
        _variables[name] = variable;
    }

    public bool TryGet(string name, out Variable? variable)
    {
        if (_variables.TryGetValue(name, out variable)) return true;
        return Parent is not null && Parent.TryGet(name, out variable);
    }

    public bool TryAssign(string name, object value, out Variable? variable)
    {
        if (_variables.TryGetValue(name, out variable))
        {
            variable.Value = value;
            return true;
        }

        return Parent is not null && Parent.TryAssign(name, value, out variable);
    }

    public IEnumerable<KeyValuePair<string, Variable>> LocalVariables => _variables;
}

public sealed class Variable
{
    public ValueType Type { get; }
    public object Value { get; set; }

    public Variable(ValueType type, object value)
    {
        Type = type;
        Value = value;
    }
}

public sealed class FunctionValue
{
    public FunctionStatement Declaration { get; }
    public Environment Closure { get; }

    public FunctionValue(FunctionStatement declaration, Environment closure)
    {
        Declaration = declaration;
        Closure = closure;
    }
}

public sealed class VoidValue
{
    public static VoidValue Instance { get; } = new();
    private VoidValue() { }
    public override string ToString() => string.Empty;
}

public enum ValueType
{
    Int,
    String,
    Bool,
    List,
    Function,
    Void
}
