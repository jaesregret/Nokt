using System;
using System.Collections.Generic;

namespace Nokt;

public class Interpreter
{
    private readonly Dictionary<string, object> _variables = new();
    private readonly XUi _xui = new();

    public void Execute(List<Statement> statements)
    {
        foreach (Statement statement in statements)
            ExecuteStatement(statement);
    }

    private void ExecuteStatement(Statement statement)
    {
        switch (statement)
        {
            case SayStatement say:
                Console.WriteLine(Evaluate(say.Value));
                break;
            case LetStatement let:
                _variables[let.Name] = Evaluate(let.Value);
                break;
            case AssignmentStatement assignment:
                if (!_variables.ContainsKey(assignment.Name))
                    throw new NoktException($"cannot assign undefined variable '{assignment.Name}'");
                _variables[assignment.Name] = Evaluate(assignment.Value);
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
                _xui.Show(window.Window, Execute);
                break;
            default:
                throw new NoktException("unknown statement type");
        }
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
            case VariableExpression variable:
                if (_variables.TryGetValue(variable.Name, out object? value)) return value;
                throw new NoktException($"undefined variable '{variable.Name}'");
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