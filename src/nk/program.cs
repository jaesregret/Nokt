using System;
using System.IO;

namespace Nokt;

class Program
{
    static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Uso: Nokt <arquivo.nk>");
            return 1;
        }

        string filePath = args[0];

        if (!File.Exists(filePath))
        {
            Console.Error.WriteLine($"Erro: arquivo '{filePath}' não encontrado.");
            return 1;
        }

        try
        {
            string source = File.ReadAllText(filePath);
            var lexer = new Lexer(source);
            var tokens = lexer.Tokenize();

            var parser = new Parser(tokens);
            var statements = parser.Parse();

            var interpreter = new Interpreter();
            interpreter.Execute(statements);
        }
        catch (NoktException ex)
        {
            Console.Error.WriteLine($"Erro: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Erro interno: {ex.Message}");
            return 1;
        }

        return 0;
    }
}
