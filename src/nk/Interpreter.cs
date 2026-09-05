using System;
using System.Collections.Generic;

namespace Nokt;

public class Interpreter
{
    private readonly Dictionary<string, object> _variables = new();

    public void Execute(List<Statement> statements)
    {
        foreach (var statement in statements)
        {
            ExecuteStatement(statement);
        }
    }

    private void ExecuteStatement(Statement statement)
    {
        switch (statement)
        {
            case SayStatement say:
                object sayValue = Evaluate(say.Value);
                Console.WriteLine(sayValue);
                break;

            case LetStatement let:
                object letValue = Evaluate(let.Value);
                _variables[let.Name] = letValue;
                break;

            default:
                throw new NoktException("Unknown statement type");
        }
    }

    private object Evaluate(Expression expression)
    {
        switch (expression)
        {
            case StringLiteral str:
                return str.Value;

            case NumberLiteral num:
                return num.Value;

            case VariableExpression variable:
                if (_variables.TryGetValue(variable.Name, out var value))
                {
                    return value;
                }
                throw new NoktException($"Undefined variable '{variable.Name}'");

            default:
                throw new NoktException("Unknown expression type");
        }
    }
}