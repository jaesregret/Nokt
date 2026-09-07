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
    private readonly HashSet<string> _exports = new();

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

    public void Export(string name)
    {
        if (!_variables.ContainsKey(name))
            throw new NoktException($"cannot export undefined symbol '{name}'");
        _exports.Add(name);
    }

    public IEnumerable<KeyValuePair<string, Variable>> ExportedVariables =>
        _variables.Where(pair => _exports.Contains(pair.Key));
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

public sealed class BuiltinFunctionValue
{
    public string Name { get; }
    public Func<List<object>, object> Invoke { get; }

    public BuiltinFunctionValue(string name, Func<List<object>, object> invoke)
    {
        Name = name;
        Invoke = invoke;
    }
}

public sealed class ModuleValue
{
    private readonly IReadOnlyDictionary<string, Variable> _members;

    public ModuleValue(IEnumerable<KeyValuePair<string, Variable>> members)
    {
        _members = members.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    }

    public bool TryGet(string name, out Variable? variable) => _members.TryGetValue(name, out variable);
}

public sealed class ListValue : List<object>
{
    public ValueType? ElementType { get; }

    public ListValue(IEnumerable<object> values, ValueType? elementType) : base(values)
    {
        ElementType = elementType;
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
    Module,
    Void
}
