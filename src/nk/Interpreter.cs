using System;
using System.Collections.Generic;

namespace Nokt;

public class Interpreter
{
    private readonly Environment _globalEnvironment = new(ScopeKind.Global);
    private readonly HashSet<string> _loadedModules = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<string, string, ModuleSource>? _moduleLoader;
    private readonly IUiHost? _uiHost;
    private Environment _currentEnvironment;
    private string _currentModulePath = string.Empty;
    private int _functionDepth;

    public Interpreter(Func<string, string, ModuleSource>? moduleLoader = null, IUiHost? uiHost = null)
    {
        _moduleLoader = moduleLoader;
        _uiHost = uiHost;
        _currentEnvironment = _globalEnvironment;
    }

    public void Execute(List<Statement> statements, string modulePath = "")
    {
        ExecuteInEnvironment(statements, _globalEnvironment, modulePath);
    }

    private void ExecuteInEnvironment(List<Statement> statements, Environment environment, string modulePath = "")
    {
        Environment previousEnvironment = _currentEnvironment;
        string previousModulePath = _currentModulePath;
        _currentEnvironment = environment;
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
            _currentEnvironment = previousEnvironment;
            _currentModulePath = previousModulePath;
        }
    }

    private void ExecuteStatement(Statement statement)
    {
        switch (statement)
        {
            case ImportStatement import:
                ExecuteImport(import);
                break;
            case SayStatement say:
                Console.WriteLine(Evaluate(say.Value));
                break;
            case ExpressionStatement expression:
                Evaluate(expression.Expression);
                break;
            case FunctionStatement function:
                _currentEnvironment.Define(function.Name, new Variable(ValueType.Function, new FunctionValue(function, _currentEnvironment)));
                if (function.IsExported)
                    _currentEnvironment.Export(function.Name);
                break;
            case ReturnStatement returnStatement:
                if (_functionDepth == 0)
                    throw new NoktException("return can only be used inside a function");
                throw new ReturnSignal(returnStatement.Value is null ? null : Evaluate(returnStatement.Value));
            case LetStatement let:
                object letValue = Evaluate(let.Value);
                ValueType type = let.DeclaredType is null
                    ? GetValueType(letValue)
                    : ParseType(let.DeclaredType, let.Name);
                EnsureType(type, letValue, let.Name);
                _currentEnvironment.Define(let.Name, new Variable(type, letValue));
                break;
            case AssignmentStatement assignment:
                object assignmentValue = Evaluate(assignment.Value);
                if (!_currentEnvironment.TryGet(assignment.Name, out Variable? variable) || variable is null)
                    throw new NoktException($"cannot assign undefined variable '{assignment.Name}'");
                EnsureType(variable.Type, assignmentValue, assignment.Name);
                _currentEnvironment.TryAssign(assignment.Name, assignmentValue, out _);
                break;
            case IfStatement conditional:
                if (RequireBoolean(Evaluate(conditional.Condition), "if condition"))
                    ExecuteLocalBlock(conditional.ThenBranch);
                else if (conditional.ElseBranch is not null)
                    ExecuteLocalBlock(conditional.ElseBranch);
                break;
            case WhileStatement loop:
                Environment loopEnvironment = _currentEnvironment.CreateChild(ScopeKind.Local);
                while (RequireBoolean(Evaluate(loop.Condition), "while condition"))
                    ExecuteInEnvironment(loop.Body, loopEnvironment);
                break;
            case WindowStatement window:
                if (_uiHost is null)
                    throw new NoktException("GUI support is unavailable in the core runtime; use Nokt.Ui");
                _uiHost.Show(window.Window, statements => ExecuteLocalBlock(statements));
                break;
            default:
                throw new NoktException("unknown statement type");
        }
    }

    private void ExecuteImport(ImportStatement import)
    {
        if (_moduleLoader is null)
            throw new NoktException("modules require a file-based interpreter entry point");

        ModuleSource module = _moduleLoader(import.Path, _currentModulePath);
        if (!_loadedModules.Add(module.Path)) return;
        Environment moduleEnvironment = _currentEnvironment.CreateChild(ScopeKind.Module);
        ExecuteInEnvironment(module.Statements, moduleEnvironment, module.Path);
        if (import.Alias is not null)
        {
            ModuleValue value = new(moduleEnvironment.ExportedVariables);
            _currentEnvironment.Define(import.Alias, new Variable(ValueType.Module, value));
        }
        else
        {
            foreach (KeyValuePair<string, Variable> variable in moduleEnvironment.LocalVariables)
                _currentEnvironment.Define(variable.Key, variable.Value);
        }
    }

    private void ExecuteLocalBlock(List<Statement> statements)
    {
        ExecuteInEnvironment(statements, _currentEnvironment.CreateChild(ScopeKind.Local));
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
                if (_currentEnvironment.TryGet(variable.Name, out Variable? value) && value is not null) return value.Value;
                throw new NoktException($"undefined variable '{variable.Name}'");
            case IndexExpression index:
                return EvaluateIndex(index);
            case CallExpression call:
                return EvaluateCall(call);
            case MemberExpression member:
                return EvaluateMember(member);
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

    private object EvaluateCall(CallExpression expression)
    {
        object callee = Evaluate(expression.Callee);
        if (callee is not FunctionValue function)
            throw new NoktException("only functions can be called");

        if (expression.Arguments.Count != function.Declaration.Parameters.Count)
        {
            throw new NoktException(
                $"function '{function.Declaration.Name}' expected {function.Declaration.Parameters.Count} argument(s), " +
                $"got {expression.Arguments.Count}");
        }

        var arguments = expression.Arguments.Select(Evaluate).ToList();
        Environment callEnvironment = function.Closure.CreateChild(ScopeKind.Local);
        for (int index = 0; index < function.Declaration.Parameters.Count; index++)
        {
            FunctionParameter parameter = function.Declaration.Parameters[index];
            object argument = arguments[index];
            ValueType type = parameter.DeclaredType is null
                ? GetValueType(argument)
                : ParseType(parameter.DeclaredType, parameter.Name);
            EnsureType(type, argument, parameter.Name);
            callEnvironment.Define(parameter.Name, new Variable(type, argument));
        }

        _functionDepth++;
        try
        {
            ExecuteInEnvironment(function.Declaration.Body, callEnvironment);
        }
        catch (ReturnSignal result)
        {
            object returnValue = result.Value ?? VoidValue.Instance;
            EnsureReturnType(function.Declaration, returnValue);
            return returnValue;
        }
        finally
        {
            _functionDepth--;
        }

        object implicitReturn = VoidValue.Instance;
        EnsureReturnType(function.Declaration, implicitReturn);
        return implicitReturn;
    }

    private static void EnsureReturnType(FunctionStatement declaration, object value)
    {
        if (declaration.ReturnType is null) return;

        ValueType expected = ParseType(declaration.ReturnType, $"function '{declaration.Name}' return");
        ValueType actual = GetValueType(value);
        if (expected != actual)
        {
            throw new NoktException(
                $"function '{declaration.Name}' must return '{TypeName(expected)}', got '{TypeName(actual)}'");
        }
    }

    private object EvaluateMember(MemberExpression expression)
    {
        object value = Evaluate(expression.Object);
        if (value is ModuleValue module && module.TryGet(expression.Name, out Variable? member) && member is not null)
            return member.Value;
        throw new NoktException($"module has no exported member '{expression.Name}'");
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
        FunctionValue => ValueType.Function,
        ModuleValue => ValueType.Module,
        VoidValue => ValueType.Void,
        _ => throw new NoktException($"unsupported value type '{value.GetType().Name}'")
    };

    private static ValueType ParseType(string type, string variableName) => type switch
    {
        "int" => ValueType.Int,
        "string" => ValueType.String,
        "bool" => ValueType.Bool,
        "list" => ValueType.List,
        "function" => ValueType.Function,
        "module" => ValueType.Module,
        "void" => ValueType.Void,
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
        ValueType.Function => "function",
        ValueType.Module => "module",
        ValueType.Void => "void",
        _ => "unknown"
    };

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

    private sealed class ReturnSignal : Exception
    {
        public object? Value { get; }
        public ReturnSignal(object? value) => Value = value;
    }
}

public sealed record ModuleSource(string Path, List<Statement> Statements);