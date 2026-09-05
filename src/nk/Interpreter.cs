using System;
using System.Collections.Generic;

namespace Nokt;

public class Interpreter
{
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
                Console.WriteLine(say.Message);
                break;

            default:
                throw new NoktException("Unknown statement type");
        }
    }
}
