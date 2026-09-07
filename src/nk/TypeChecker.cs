using System.Collections.Generic;

namespace Nokt;

public sealed class TypeChecker
{
    public void Check(List<Statement> statements)
    {
        var scope = new TypeScope();
        foreach (Statement statement in statements)
            CheckStatement(statement, scope, null);
    }

    private void CheckStatement(Statement statement, TypeScope scope, ValueType? returnType)
    {
        switch (statement)
        {
            case SayStatement say:
                CheckExpression(say.Value, scope);
                break;
            case ExpressionStatement expression:
                CheckExpression(expression.Expression, scope);
                break;
            case LetStatement let:
            {
                ValueType? valueType = CheckExpression(let.Value, scope);
                ValueType? declaredType = let.DeclaredType is null ? null : ParseType(let.DeclaredType, let.Name);
                if (declaredType is not null && valueType is not null && declaredType != valueType)
                    throw Error($"variable '{let.Name}' is '{Name(declaredType.Value)}' but expression is '{Name(valueType.Value)}'");
                EnsureListLiteralType(let.DeclaredType, let.Value, scope, let.Name);
                scope.Define(let.Name, declaredType ?? valueType);
                break;
            }
            case AssignmentStatement assignment:
            {
                ValueType? valueType = CheckExpression(assignment.Value, scope);
                if (scope.TryGet(assignment.Name, out ValueType? existing) && existing is not null && valueType is not null && existing != valueType)
                    throw Error($"variable '{assignment.Name}' is '{Name(existing.Value)}' but expression is '{Name(valueType.Value)}'");
                break;
            }
            case FunctionStatement function:
                scope.Define(function.Name, ValueType.Function);
                var functionScope = new TypeScope(scope);
                foreach (FunctionParameter parameter in function.Parameters)
                    functionScope.Define(parameter.Name, parameter.DeclaredType is null ? null : ParseType(parameter.DeclaredType, parameter.Name));
                ValueType? expectedReturn = function.ReturnType is null ? null : ParseType(function.ReturnType, $"function '{function.Name}' return");
                foreach (Statement bodyStatement in function.Body)
                    CheckStatement(bodyStatement, functionScope, expectedReturn);
                break;
            case ReturnStatement returnStatement:
                if (returnType is null && returnStatement.Value is not null)
                    CheckExpression(returnStatement.Value, scope);
                else if (returnType is not null)
                {
                    ValueType actual = returnStatement.Value is null ? ValueType.Void : CheckRequiredExpression(returnStatement.Value, scope);
                    if (actual != returnType)
                        throw Error($"function return must be '{Name(returnType.Value)}', got '{Name(actual)}'");
                }
                break;
            case IfStatement conditional:
                EnsureBoolean(conditional.Condition, scope, "if condition");
                CheckBlock(conditional.ThenBranch, scope, returnType);
                if (conditional.ElseBranch is not null) CheckBlock(conditional.ElseBranch, scope, returnType);
                break;
            case WhileStatement loop:
                EnsureBoolean(loop.Condition, scope, "while condition");
                CheckBlock(loop.Body, scope, returnType);
                break;
        }
    }

    private void CheckBlock(List<Statement> statements, TypeScope parent, ValueType? returnType)
    {
        var scope = new TypeScope(parent);
        foreach (Statement statement in statements)
            CheckStatement(statement, scope, returnType);
    }

    private ValueType? CheckExpression(Expression expression, TypeScope scope)
    {
        switch (expression)
        {
            case StringLiteral: return ValueType.String;
            case NumberLiteral: return ValueType.Int;
            case BooleanLiteral: return ValueType.Bool;
            case VariableExpression variable:
                scope.TryGet(variable.Name, out ValueType? variableType);
                return variableType;
            case ListExpression list:
            {
                ValueType? elementType = null;
                foreach (Expression item in list.Items)
                {
                    ValueType? itemType = CheckExpression(item, scope);
                    if (itemType is null) continue;
                    if (elementType is null) elementType = itemType;
                    else if (elementType != itemType)
                        throw Error($"list elements must have the same type, got '{Name(elementType.Value)}' and '{Name(itemType.Value)}'");
                }
                return ValueType.List;
            }
            case IndexExpression index:
                CheckExpression(index.Collection, scope);
                EnsureType(index.Index, scope, ValueType.Int, "collection index");
                return null;
            case UnaryExpression unary:
                if (unary.Operator == "not") EnsureType(unary.Operand, scope, ValueType.Bool, "operator 'not'");
                if (unary.Operator == "-") EnsureType(unary.Operand, scope, ValueType.Int, "unary '-'");
                return unary.Operator == "not" ? ValueType.Bool : ValueType.Int;
            case BinaryExpression binary:
                return CheckBinary(binary, scope);
            case CallExpression call:
                CheckExpression(call.Callee, scope);
                foreach (Expression argument in call.Arguments) CheckExpression(argument, scope);
                return null;
            case MemberExpression member:
                CheckExpression(member.Object, scope);
                return null;
            default:
                return null;
        }
    }

    private ValueType CheckRequiredExpression(Expression expression, TypeScope scope) =>
        CheckExpression(expression, scope) ?? throw Error("return expression type cannot be determined statically");

    private ValueType? CheckBinary(BinaryExpression binary, TypeScope scope)
    {
        ValueType? left = CheckExpression(binary.Left, scope);
        ValueType? right = CheckExpression(binary.Right, scope);
        if (left is null || right is null) return null;

        if (binary.Operator is "and" or "or")
        {
            if (left != ValueType.Bool || right != ValueType.Bool) throw Error($"operator '{binary.Operator}' requires booleans");
            return ValueType.Bool;
        }
        if (binary.Operator is "+")
        {
            if (left == right && (left == ValueType.Int || left == ValueType.String)) return left;
            throw Error("operator '+' requires two integers or two strings");
        }
        if (binary.Operator is "-" or "*" or "/" or ">" or "<" or ">=" or "<=")
        {
            if (left == ValueType.Int && right == ValueType.Int) return binary.Operator is ">" or "<" or ">=" or "<=" ? ValueType.Bool : ValueType.Int;
            throw Error($"operator '{binary.Operator}' requires integers");
        }
        if (binary.Operator is "==" or "!=") return ValueType.Bool;
        return null;
    }

    private void EnsureBoolean(Expression expression, TypeScope scope, string context) => EnsureType(expression, scope, ValueType.Bool, context);

    private void EnsureListLiteralType(string? declaredType, Expression expression, TypeScope scope, string name)
    {
        if (declaredType is null || !declaredType.StartsWith("list[", StringComparison.Ordinal) || expression is not ListExpression list)
            return;

        ValueType expected = ParseType(declaredType[5..^1], name);
        foreach (Expression item in list.Items)
        {
            ValueType? actual = CheckExpression(item, scope);
            if (actual is not null && actual != expected)
                throw Error($"list '{name}' requires elements of type '{Name(expected)}', got '{Name(actual.Value)}'");
        }
    }

    private void EnsureType(Expression expression, TypeScope scope, ValueType expected, string context)
    {
        ValueType? actual = CheckExpression(expression, scope);
        if (actual is not null && actual != expected)
            throw Error($"{context} must be '{Name(expected)}', got '{Name(actual.Value)}'");
    }

    private static ValueType ParseType(string type, string name) => type switch
    {
        "int" => ValueType.Int,
        "string" => ValueType.String,
        "bool" => ValueType.Bool,
        "list" => ValueType.List,
        "function" => ValueType.Function,
        "void" => ValueType.Void,
        _ when type.StartsWith("list[", StringComparison.Ordinal) && type.EndsWith("]", StringComparison.Ordinal)
            => ParseType(type[5..^1], name) == ValueType.Void ? throw Error("list element type cannot be void") : ValueType.List,
        _ => throw Error($"unknown type '{type}' for '{name}'")
    };

    private static string Name(ValueType type) => type.ToString().ToLowerInvariant();
    private static NoktException Error(string message) => new($"static type error: {message}");

    private sealed class TypeScope
    {
        private readonly Dictionary<string, ValueType?> _types = new();
        private readonly TypeScope? _parent;
        public TypeScope(TypeScope? parent = null) => _parent = parent;
        public void Define(string name, ValueType? type) => _types[name] = type;
        public bool TryGet(string name, out ValueType? type) => _types.TryGetValue(name, out type) || (_parent is not null && _parent.TryGet(name, out type));
    }
}
