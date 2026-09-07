using System;
using System.IO;
namespace Nokt;

class Program
{
    static int Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--lsp")
            return NoktLanguageServer.Run();

        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: Nokt <file>");
            return 1;
        }
        string filePath = args[0];
        if (!File.Exists(filePath))
        {
            Console.Error.WriteLine($"Error: file '{filePath}' not found.");
            return 1;
        }

        try
        {
            string fullPath = Path.GetFullPath(filePath);
            List<Statement> statements = ParseFile(fullPath);
            var interpreter = new Interpreter(LoadModule);
            interpreter.Execute(statements, fullPath);
        }
        catch (NoktException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Internal error: {ex.Message}");
            return 1;
        }
        return 0;
    }

    private static ModuleSource LoadModule(string requestedPath, string importerPath)
    {
        string baseDirectory = Path.GetDirectoryName(importerPath) ?? Directory.GetCurrentDirectory();
        string modulePath = Path.GetFullPath(Path.Combine(baseDirectory, requestedPath));
        if (Path.GetExtension(modulePath).Length == 0) modulePath += ".nk";
        return new ModuleSource(modulePath, ParseFile(modulePath));
    }

    private static List<Statement> ParseFile(string filePath)
    {
        if (!File.Exists(filePath))
            throw new NoktException($"module file '{filePath}' not found");

        string source = File.ReadAllText(filePath);
        var lexer = new Lexer(source);
        var parser = new Parser(lexer.Tokenize());
        List<Statement> statements = parser.Parse();
        new TypeChecker().Check(statements);
        return statements;
    }
}
