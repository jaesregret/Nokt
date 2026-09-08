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
    private int _loopDepth;

    public Interpreter(Func<string, string, ModuleSource>? moduleLoader = null, IUiHost? uiHost = null)
    {
        _moduleLoader = moduleLoader;
        _uiHost = uiHost;
        _currentEnvironment = _globalEnvironment;
        _globalEnvironment.Define("len", new Variable(ValueType.Function, new BuiltinFunctionValue("len", BuiltinLen)));
        _globalEnvironment.Define("range", new Variable(ValueType.Function, new BuiltinFunctionValue("range", BuiltinRange)));
        _globalEnvironment.Define("readInt", new Variable(ValueType.Function, new BuiltinFunctionValue("readInt", BuiltinReadInt)));
        _globalEnvironment.Define("readFile", new Variable(ValueType.Function, new BuiltinFunctionValue("readFile", BuiltinReadFile)));
        _globalEnvironment.Define("writeFile", new Variable(ValueType.Function, new BuiltinFunctionValue("writeFile", BuiltinWriteFile)));
        _globalEnvironment.Define("appendFile", new Variable(ValueType.Function, new BuiltinFunctionValue("appendFile", BuiltinAppendFile)));
        _globalEnvironment.Define("fileExists", new Variable(ValueType.Function, new BuiltinFunctionValue("fileExists", BuiltinFileExists)));
        _globalEnvironment.Define("push", new Variable(ValueType.Function, new BuiltinFunctionValue("push", BuiltinPush)));
        _globalEnvironment.Define("pop", new Variable(ValueType.Function, new BuiltinFunctionValue("pop", BuiltinPop)));
        _globalEnvironment.Define("contains", new Variable(ValueType.Function, new BuiltinFunctionValue("contains", BuiltinContains)));
        _globalEnvironment.Define("join", new Variable(ValueType.Function, new BuiltinFunctionValue("join", BuiltinJoin)));
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
                EnsureListElementType(let.DeclaredType, letValue, let.Name);
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
                _loopDepth++;
                try
                {
                    while (RequireBoolean(Evaluate(loop.Condition), "while condition"))
                    {
                        try { ExecuteInEnvironment(loop.Body, loopEnvironment); }
                        catch (ContinueSignal) { }
                        catch (BreakSignal) { break; }
                    }
                }
                finally { _loopDepth--; }
                break;
            case ForStatement loop:
                ExecuteFor(loop);
                break;
            case BreakStatement:
                if (_loopDepth == 0) throw new NoktException("break can only be used inside a loop");
                throw new BreakSignal();
            case ContinueStatement:
                if (_loopDepth == 0) throw new NoktException("continue can only be used inside a loop");
                throw new ContinueSignal();
            case TryCatchStatement tryCatch:
                ExecuteTryCatch(tryCatch);
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
            foreach (KeyValuePair<string, Variable> variable in moduleEnvironment.ExportedVariables)
                _currentEnvironment.Define(variable.Key, variable.Value);
        }
    }

    private void ExecuteLocalBlock(List<Statement> statements)
    {
        ExecuteInEnvironment(statements, _currentEnvironment.CreateChild(ScopeKind.Local));
    }

    private void ExecuteTryCatch(TryCatchStatement tryCatch)
    {
        try
        {
            ExecuteLocalBlock(tryCatch.TryBranch);
        }
        catch (NoktException error)
        {
            Environment catchEnvironment = _currentEnvironment.CreateChild(ScopeKind.Local);
            if (tryCatch.ErrorName is not null)
                catchEnvironment.Define(tryCatch.ErrorName, new Variable(ValueType.String, error.Message));
            ExecuteInEnvironment(tryCatch.CatchBranch, catchEnvironment);
        }
    }

    private void ExecuteFor(ForStatement loop)
    {
        object iterable = Evaluate(loop.Iterable);
        if (iterable is not ListValue values)
            throw new NoktException("for loop requires a list");

        Environment loopEnvironment = _currentEnvironment.CreateChild(ScopeKind.Local);
        _loopDepth++;
        try
        {
            foreach (object value in values)
            {
                loopEnvironment.Define(loop.VariableName, new Variable(GetValueType(value), value));
                try { ExecuteInEnvironment(loop.Body, loopEnvironment); }
                catch (ContinueSignal) { }
                catch (BreakSignal) { break; }
            }
        }
        finally { _loopDepth--; }
    }

    private object Evaluate(Expression expression)
    {
        switch (expression)
        {
            case StringLiteral str:
                return InterpolateString(str.Value);
            case NumberLiteral number:
                return number.Value;
            case BooleanLiteral boolean:
                return boolean.Value;
            case ListExpression list:
                return EvaluateList(list);
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
        if (collection is not ListValue values)
            throw new NoktException($"cannot index value of type '{TypeName(GetValueType(collection))}'");
        if (index < 0 || index >= values.Count)
            throw new NoktException($"collection index {index} is outside 0..{values.Count - 1}");
        return values[index];
    }

    private object EvaluateList(ListExpression expression)
    {
        var values = expression.Items.Select(Evaluate).ToList();
        ValueType? elementType = null;
        foreach (object value in values)
        {
            ValueType currentType = GetValueType(value);
            if (elementType is null)
            {
                elementType = currentType;
            }
            else if (elementType != currentType)
            {
                throw new NoktException(
                    $"list elements must have the same type, got '{TypeName(elementType.Value)}' and '{TypeName(currentType)}'");
            }
        }

        return new ListValue(values, elementType);
    }

    private string InterpolateString(string value)
    {
        var result = new System.Text.StringBuilder();
        int position = 0;
        while (position < value.Length)
        {
            int start = value.IndexOf("${", position, StringComparison.Ordinal);
            if (start < 0)
            {
                result.Append(value[position..]);
                break;
            }

            result.Append(value[position..start]);
            int end = value.IndexOf('}', start + 2);
            if (end < 0)
                throw new NoktException("unterminated string interpolation; expected '}'");

            string source = value[(start + 2)..end].Trim();
            if (source.Length == 0)
                throw new NoktException("empty string interpolation expression");
            Expression expression = new Parser(new Lexer(source).Tokenize()).ParseExpressionForInterpolation();
            result.Append(Evaluate(expression));
            position = end + 1;
        }
        return result.ToString();
    }

    private object EvaluateCall(CallExpression expression)
    {
        object callee = Evaluate(expression.Callee);
        if (callee is BuiltinFunctionValue builtin)
            return builtin.Invoke(expression.Arguments.Select(Evaluate).ToList());
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
            EnsureListElementType(parameter.DeclaredType, argument, parameter.Name);
            callEnvironment.Define(parameter.Name, new Variable(type, argument));
        }

        int previousLoopDepth = _loopDepth;
        _loopDepth = 0;
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
            _loopDepth = previousLoopDepth;
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
            EnsureListElementType(declaration.ReturnType, value, $"function '{declaration.Name}' return");
    }

    private object EvaluateMember(MemberExpression expression)
    {
        object value = Evaluate(expression.Object);
        if (value is ModuleValue module && module.TryGet(expression.Name, out Variable? member) && member is not null)
            return member.Value;
        if (value is string text)
            return CreateStringMethod(text, expression.Name);
        if (value is ListValue list)
            return CreateListMethod(list, expression.Name);
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
        if (left is string text && right is int number) return text + number;
        if (left is int value && right is string suffix) return value + suffix;
        throw OperatorError("operator '+' requires two integers or two strings", token);
    }

    private static ValueType GetValueType(object value) => value switch
    {
        int => ValueType.Int,
        string => ValueType.String,
        bool => ValueType.Bool,
        ListValue => ValueType.List,
        FunctionValue => ValueType.Function,
        BuiltinFunctionValue => ValueType.Function,
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
        _ when type.StartsWith("list[", StringComparison.Ordinal) && type.EndsWith("]", StringComparison.Ordinal)
            => ParseType("list", variableName),
        _ => throw new NoktException($"unknown type '{type}' for variable '{variableName}'")
    };

    private static void EnsureListElementType(string? declaredType, object value, string name)
    {
        if (declaredType is null || !declaredType.StartsWith("list[", StringComparison.Ordinal)) return;
        if (value is not ListValue list) return;

        string elementName = declaredType[5..^1];
        ValueType expected = ParseType(elementName, name);
        if (list.ElementType is not null && list.ElementType != expected)
        {
            throw new NoktException(
                $"list '{name}' requires elements of type '{TypeName(expected)}', got '{TypeName(list.ElementType.Value)}'");
        }
    }

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

    private static BuiltinFunctionValue CreateStringMethod(string text, string name) => name switch
    {
        "upper" => new BuiltinFunctionValue("string.upper", arguments =>
        {
            RequireArgumentCount(name, arguments, 0);
            return text.ToUpperInvariant();
        }),
        "lower" => new BuiltinFunctionValue("string.lower", arguments =>
        {
            RequireArgumentCount(name, arguments, 0);
            return text.ToLowerInvariant();
        }),
        "trim" => new BuiltinFunctionValue("string.trim", arguments =>
        {
            RequireArgumentCount(name, arguments, 0);
            return text.Trim();
        }),
        "contains" => new BuiltinFunctionValue("string.contains", arguments =>
        {
            RequireArgumentCount(name, arguments, 1);
            return text.Contains(RequireString(arguments[0], "string.contains"), StringComparison.Ordinal);
        }),
        "startsWith" => new BuiltinFunctionValue("string.startsWith", arguments =>
        {
            RequireArgumentCount(name, arguments, 1);
            return text.StartsWith(RequireString(arguments[0], "string.startsWith"), StringComparison.Ordinal);
        }),
        "endsWith" => new BuiltinFunctionValue("string.endsWith", arguments =>
        {
            RequireArgumentCount(name, arguments, 1);
            return text.EndsWith(RequireString(arguments[0], "string.endsWith"), StringComparison.Ordinal);
        }),
        "substring" => new BuiltinFunctionValue("string.substring", arguments =>
        {
            RequireArgumentCount(name, arguments, 2);
            int start = RequireInt(arguments[0], "string.substring");
            int length = RequireInt(arguments[1], "string.substring");
            if (start < 0 || length < 0 || start + length > text.Length)
                throw new NoktException("string.substring range is outside the string");
            return text.Substring(start, length);
        }),
        "replace" => new BuiltinFunctionValue("string.replace", arguments =>
        {
            RequireArgumentCount(name, arguments, 2);
            return text.Replace(RequireString(arguments[0], "string.replace"), RequireString(arguments[1], "string.replace"), StringComparison.Ordinal);
        }),
        "split" => new BuiltinFunctionValue("string.split", arguments =>
        {
            RequireArgumentCount(name, arguments, 1);
            string separator = RequireString(arguments[0], "string.split");
            return new ListValue(text.Split(separator, StringSplitOptions.None).Cast<object>(), ValueType.String);
        }),
        _ => throw new NoktException($"string has no method '{name}'")
    };

    private static BuiltinFunctionValue CreateListMethod(ListValue list, string name) => name switch
    {
        "push" => new BuiltinFunctionValue("list.push", arguments => Push(list, arguments, name)),
        "pop" => new BuiltinFunctionValue("list.pop", arguments => Pop(list, arguments, name)),
        "contains" => new BuiltinFunctionValue("list.contains", arguments => Contains(list, arguments, name)),
        "join" => new BuiltinFunctionValue("list.join", arguments => Join(list, arguments, name)),
        _ => throw new NoktException($"list has no method '{name}'")
    };

    private static object BuiltinReadFile(List<object> arguments)
    {
        RequireArgumentCount("readFile", arguments, 1);
        try { return File.ReadAllText(RequireString(arguments[0], "readFile")); }
        catch (Exception error) { throw new NoktException($"readFile failed: {error.Message}"); }
    }

    private static object BuiltinWriteFile(List<object> arguments)
    {
        RequireArgumentCount("writeFile", arguments, 2);
        try
        {
            File.WriteAllText(RequireString(arguments[0], "writeFile"), RequireString(arguments[1], "writeFile"));
            return VoidValue.Instance;
        }
        catch (Exception error) { throw new NoktException($"writeFile failed: {error.Message}"); }
    }

    private static object BuiltinAppendFile(List<object> arguments)
    {
        RequireArgumentCount("appendFile", arguments, 2);
        try
        {
            File.AppendAllText(RequireString(arguments[0], "appendFile"), RequireString(arguments[1], "appendFile"));
            return VoidValue.Instance;
        }
        catch (Exception error) { throw new NoktException($"appendFile failed: {error.Message}"); }
    }

    private static object BuiltinFileExists(List<object> arguments)
    {
        RequireArgumentCount("fileExists", arguments, 1);
        return File.Exists(RequireString(arguments[0], "fileExists"));
    }

    private static object BuiltinPush(List<object> arguments)
    {
        RequireArgumentCount("push", arguments, 2);
        return Push(RequireList(arguments[0], "push"), arguments.Skip(1).ToList(), "push");
    }

    private static object BuiltinPop(List<object> arguments)
    {
        RequireArgumentCount("pop", arguments, 1);
        return Pop(RequireList(arguments[0], "pop"), new List<object>(), "pop");
    }

    private static object BuiltinContains(List<object> arguments)
    {
        RequireArgumentCount("contains", arguments, 2);
        return Contains(RequireList(arguments[0], "contains"), arguments.Skip(1).ToList(), "contains");
    }

    private static object BuiltinJoin(List<object> arguments)
    {
        RequireArgumentCount("join", arguments, 2);
        return Join(RequireList(arguments[0], "join"), arguments.Skip(1).ToList(), "join");
    }

    private static object Push(ListValue list, List<object> arguments, string name)
    {
        RequireArgumentCount(name, arguments, 1);
        object value = arguments[0];
        if (list.ElementType is not null && GetValueType(value) != list.ElementType)
            throw new NoktException($"{name} value must be '{TypeName(list.ElementType.Value)}'");
        list.Add(value);
        return VoidValue.Instance;
    }

    private static object Pop(ListValue list, List<object> arguments, string name)
    {
        RequireArgumentCount(name, arguments, 0);
        if (list.Count == 0) throw new NoktException("cannot pop from an empty list");
        object value = list[^1];
        list.RemoveAt(list.Count - 1);
        return value;
    }

    private static object Contains(ListValue list, List<object> arguments, string name)
    {
        RequireArgumentCount(name, arguments, 1);
        return list.Contains(arguments[0]);
    }

    private static object Join(ListValue list, List<object> arguments, string name)
    {
        RequireArgumentCount(name, arguments, 1);
        string separator = RequireString(arguments[0], name);
        return string.Join(separator, list.Select(ValueToString));
    }

    private static void RequireArgumentCount(string name, List<object> arguments, int expected)
    {
        if (arguments.Count != expected)
            throw new NoktException($"function '{name}' expected {expected} argument(s), got {arguments.Count}");
    }

    private static string RequireString(object value, string name) => value is string text
        ? text
        : throw new NoktException("function '" + name + "' expects a string");

    private static int RequireInt(object value, string name) => value is int number
        ? number
        : throw new NoktException("function '" + name + "' expects an integer");

    private static ListValue RequireList(object value, string name) => value as ListValue
        ?? throw new NoktException("function '" + name + "' expects a list");

    private static string ValueToString(object value) => value switch
    {
        ListValue list => "[" + string.Join(", ", list.Select(ValueToString)) + "]",
        VoidValue => string.Empty,
        _ => value.ToString() ?? string.Empty
    };

    private sealed class ReturnSignal : Exception
    {
        public object? Value { get; }
        public ReturnSignal(object? value) => Value = value;
    }

    private static object BuiltinLen(List<object> arguments)
    {
        if (arguments.Count != 1) throw new NoktException($"function 'len' expected 1 argument(s), got {arguments.Count}");
        if (arguments[0] is ListValue list) return list.Count;
        if (arguments[0] is string text) return text.Length;
        throw new NoktException("function 'len' expects a list or string");
    }

    private static object BuiltinRange(List<object> arguments)
    {
        if (arguments.Count is < 1 or > 2) throw new NoktException($"function 'range' expected 1 or 2 argument(s), got {arguments.Count}");
        if (arguments.Any(argument => argument is not int)) throw new NoktException("function 'range' expects integer arguments");
        int start = arguments.Count == 1 ? 0 : (int)arguments[0];
        int end = arguments.Count == 1 ? (int)arguments[0] : (int)arguments[1];
        return new ListValue(Enumerable.Range(start, Math.Max(0, end - start)).Cast<object>(), ValueType.Int);
    }

    private static object BuiltinReadInt(List<object> arguments)
    {
        if (arguments.Count != 0)
            throw new NoktException($"function 'readInt' expected 0 argument(s), got {arguments.Count}");

        string? input = Console.ReadLine();
        if (int.TryParse(input, out int value)) return value;
        throw new NoktException($"readInt expected an integer, got '{input ?? "end of input"}'");
    }

    private sealed class BreakSignal : Exception { }
    private sealed class ContinueSignal : Exception { }
}

public sealed record ModuleSource(string Path, List<Statement> Statements);