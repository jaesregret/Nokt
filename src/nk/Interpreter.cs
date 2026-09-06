using System;
using System.Collections.Generic;

namespace Nokt;

public class Interpreter
{
    private readonly Dictionary<string, Variable> _variables = new();
    private readonly HashSet<string> _loadedModules = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<string, string, ModuleSource>? _moduleLoader;
    private readonly XUi _xui = new();
    private string _currentModulePath = string.Empty;

    public Interpreter(Func<string, string, ModuleSource>? moduleLoader = null)
    {
        _moduleLoader = moduleLoader;
    }

    public void Execute(List<Statement> statements, string modulePath = "")
    {
        string previousModulePath = _currentModulePath;
        if (!string.IsNullOrEmpty(modulePath))
        {
            _currentModulePath = modulePath;
            _loadedModules.Add(modulePath);
        }
        try
        {
            foreach (Statement statement in statements)
                ExecuteStatement(statement);
        }
        finally
        {
            _currentModulePath = previousModulePath;
        }
    }

    private void ExecuteStatement(Statement statement)
    {
        switch (statement)
        {
            case ImportStatement import:
                ExecuteImport(import.Path);
                break;
            case SayStatement say:
                Console.WriteLine(Evaluate(say.Value));
                break;
            case LetStatement let:
                object letValue = Evaluate(let.Value);
                ValueType type = let.DeclaredType is null
                    ? GetValueType(letValue)
                    : ParseType(let.DeclaredType, let.Name);
                EnsureType(type, letValue, let.Name);
                _variables[let.Name] = new Variable(type, letValue);
                break;
            case AssignmentStatement assignment:
                if (!_variables.TryGetValue(assignment.Name, out Variable? variable))
                    throw new NoktException($"cannot assign undefined variable '{assignment.Name}'");
                object assignmentValue = Evaluate(assignment.Value);
                EnsureType(variable.Type, assignmentValue, assignment.Name);
                variable.Value = assignmentValue;
                break;
            case IfStatement conditional:
                if (RequireBoolean(Evaluate(conditional.Condition), "if condition"))
                    Execute(conditional.ThenBranch);
                else if (conditional.ElseBranch is not null)
                    Execute(conditional.ElseBranch);
                break;
            case WhileStatement loop:
                while (RequireBoolean(Evaluate(loop.Condition), "while condition"))
                    Execute(loop.Body);
                break;
            case WindowStatement window:
                _xui.Show(window.Window, statements => Execute(statements));
                break;
            default:
                throw new NoktException("unknown statement type");
        }
    }

    private void ExecuteImport(string requestedPath)
    {
        if (_moduleLoader is null)
            throw new NoktException("modules require a file-based interpreter entry point");

        ModuleSource module = _moduleLoader(requestedPath, _currentModulePath);
        if (!_loadedModules.Add(module.Path)) return;
        Execute(module.Statements, module.Path);
    }

    private object Evaluate(Expression expression)
    {
        switch (expression)
        {
            case StringLiteral str:
                return str.Value;
            case NumberLiteral number:
                return number.Value;
            case BooleanLiteral boolean:
                return boolean.Value;
            case ListExpression list:
                return list.Items.Select(Evaluate).ToList();
            case VariableExpression variable:
                if (_variables.TryGetValue(variable.Name, out Variable? value)) return value.Value;
                throw new NoktException($"undefined variable '{variable.Name}'");
            case IndexExpression index:
                return EvaluateIndex(index);
            case UnaryExpression unary:
                return EvaluateUnary(unary);
            case BinaryExpression binary:
                return EvaluateBinary(binary);
            default:
                throw new NoktException("unknown expression type");
        }
    }

    private object EvaluateUnary(UnaryExpression expression)
    {
        object value = Evaluate(expression.Operand);
        if (expression.Operator == "not")
            return !RequireBoolean(value, "operator 'not'", expression.Token);
        if (expression.Operator == "-")
            return -RequireInteger(value, "unary '-'", expression.Token);
        throw OperatorError($"unknown unary operator '{expression.Operator}'", expression.Token);
    }

    private object EvaluateIndex(IndexExpression expression)
    {
        object collection = Evaluate(expression.Collection);
        object indexValue = Evaluate(expression.Index);
        if (indexValue is not int index)
            throw new NoktException("collection index must be an integer");
        if (collection is not List<object> values)
            throw new NoktException($"cannot index value of type '{TypeName(GetValueType(collection))}'");
        if (index < 0 || index >= values.Count)
            throw new NoktException($"collection index {index} is outside 0..{values.Count - 1}");
        return values[index];
    }

    private object EvaluateBinary(BinaryExpression expression)
    {
        object left = Evaluate(expression.Left);
        if (expression.Operator == "and")
        {
            bool leftValue = RequireBoolean(left, "left operand of 'and'", expression.Token);
            return leftValue && RequireBoolean(Evaluate(expression.Right), "right operand of 'and'", expression.Token);
        }
        if (expression.Operator == "or")
        {
            bool leftValue = RequireBoolean(left, "left operand of 'or'", expression.Token);
            return leftValue || RequireBoolean(Evaluate(expression.Right), "right operand of 'or'", expression.Token);
        }

        object right = Evaluate(expression.Right);
        return expression.Operator switch
        {
            "+" => Add(left, right, expression.Token),
            "-" => Arithmetic(left, right, expression.Token, (a, b) => a - b),
            "*" => Arithmetic(left, right, expression.Token, (a, b) => a * b),
            "/" => Divide(left, right, expression.Token),
            "==" => Equals(left, right),
            "!=" => !Equals(left, right),
            ">" => Compare(left, right, expression.Token, (a, b) => a > b),
            "<" => Compare(left, right, expression.Token, (a, b) => a < b),
            ">=" => Compare(left, right, expression.Token, (a, b) => a >= b),
            "<=" => Compare(left, right, expression.Token, (a, b) => a <= b),
            _ => throw OperatorError($"unknown binary operator '{expression.Operator}'", expression.Token)
        };
    }

    private static object Add(object left, object right, Token token)
    {
        if (left is int leftNumber && right is int rightNumber) return leftNumber + rightNumber;
        if (left is string leftString && right is string rightString) return leftString + rightString;
        throw OperatorError("operator '+' requires two integers or two strings", token);
    }

    private static ValueType GetValueType(object value) => value switch
    {
        int => ValueType.Int,
        string => ValueType.String,
        bool => ValueType.Bool,
        List<object> => ValueType.List,
        _ => throw new NoktException($"unsupported value type '{value.GetType().Name}'")
    };

    private static ValueType ParseType(string type, string variableName) => type switch
    {
        "int" => ValueType.Int,
        "string" => ValueType.String,
        "bool" => ValueType.Bool,
        "list" => ValueType.List,
        _ => throw new NoktException($"unknown type '{type}' for variable '{variableName}'")
    };

    private static void EnsureType(ValueType expected, object value, string variableName)
    {
        ValueType actual = GetValueType(value);
        if (expected != actual)
            throw new NoktException($"variable '{variableName}' is '{TypeName(expected)}' but received '{TypeName(actual)}'");
    }

    private static string TypeName(ValueType type) => type switch
    {
        ValueType.Int => "int",
        ValueType.String => "string",
        ValueType.Bool => "bool",
        ValueType.List => "list",
        _ => "unknown"
    };

    private sealed class Variable
    {
        public ValueType Type { get; }
        public object Value { get; set; }

        public Variable(ValueType type, object value)
        {
            Type = type;
            Value = value;
        }
    }

    private enum ValueType
    {
        Int,
        String,
        Bool,
        List
    }

    private static int Arithmetic(object left, object right, Token token, Func<int, int, int> operation) =>
        operation(RequireInteger(left, "left operand", token), RequireInteger(right, "right operand", token));

    private static int Divide(object left, object right, Token token)
    {
        int divisor = RequireInteger(right, "right operand of '/'", token);
        if (divisor == 0) throw OperatorError("division by zero", token);
        return RequireInteger(left, "left operand of '/'", token) / divisor;
    }

    private static bool Compare(object left, object right, Token token, Func<int, int, bool> comparison) =>
        comparison(RequireInteger(left, "left comparison operand", token), RequireInteger(right, "right comparison operand", token));

    private static int RequireInteger(object value, string context, Token token)
    {
        if (value is int number) return number;
        throw OperatorError($"{context} must be an integer, got '{value}'", token);
    }

    private static bool RequireBoolean(object value, string context, Token? token = null)
    {
        if (value is bool boolean) return boolean;
        string location = token is null ? string.Empty : $" at line {token.Line}, column {token.Column}";
        throw new NoktException($"{context} must be boolean, got '{value}'{location}");
    }

    private static NoktException OperatorError(string message, Token token) =>
        new($"{message} at line {token.Line}, column {token.Column}; token/value '{token.Value}'");
}

public sealed record ModuleSource(string Path, List<Statement> Statements);