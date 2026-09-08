using System;
using System.IO;

namespace Nokt;

class Program
{
    private static readonly CompilationCache Cache = new();

    static int Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--lsp")
            return NoktLanguageServer.Run();
        if (args.Length == 0 || (args.Length == 1 && args[0] == "--repl"))
            return RunRepl();

        string filePath = args[0];
        if (!File.Exists(filePath))
        {
            Console.Error.WriteLine($"Error: file '{filePath}' not found.");
            return 1;
        }

        try
        {
            string fullPath = Path.GetFullPath(filePath);
            Interpreter interpreter = new(LoadModule);
            interpreter.Execute(Cache.GetOrCompileFile(fullPath).Statements, fullPath);
            return 0;
        }
        catch (NoktException error)
        {
            Console.Error.WriteLine($"Error: {error.Message}");
            return 1;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine($"Internal error: {error.Message}");
            return 1;
        }
    }

    private static ModuleSource LoadModule(string requestedPath, string importerPath)
    {
        string baseDirectory = Path.GetDirectoryName(importerPath) ?? Directory.GetCurrentDirectory();
        string modulePath = Path.GetFullPath(Path.Combine(baseDirectory, requestedPath));
        if (Path.GetExtension(modulePath).Length == 0) modulePath += ".nk";
        return new ModuleSource(modulePath, Cache.GetOrCompileFile(modulePath).Statements);
    }

    private static int RunRepl()
    {
        Console.WriteLine("Nokt REPL. Use :help for commands and :quit to exit.");
        Interpreter interpreter = new(LoadModule);
        while (true)
        {
            Console.Write("nokt> ");
            string? firstLine = Console.ReadLine();
            if (firstLine is null) break;
            string command = firstLine.Trim();
            if (command is ":quit" or ":exit") break;
            if (command == ":help")
            {
                Console.WriteLine(":load <file>  execute a Nokt file");
                Console.WriteLine(":clear        clear the compilation cache");
                Console.WriteLine(":quit         leave the REPL");
                continue;
            }
            if (command == ":clear")
            {
                Cache.Clear();
                Console.WriteLine("compilation cache cleared");
                continue;
            }
            if (command.StartsWith(":load ", StringComparison.Ordinal))
            {
                try
                {
                    string fullPath = Path.GetFullPath(command[6..].Trim());
                    interpreter.Execute(Cache.GetOrCompileFile(fullPath).Statements, fullPath);
                }
                catch (Exception error) when (error is NoktException or IOException)
                {
                    Console.Error.WriteLine($"Error: {error.Message}");
                }
                continue;
            }
            if (string.IsNullOrWhiteSpace(firstLine)) continue;

            string source = ReadReplSubmission(firstLine);
            try
            {
                CompiledProgram program = Cache.CompileSource(source, "<repl>");
                interpreter.Execute(program.Statements, "<repl>");
            }
            catch (NoktException error)
            {
                Console.Error.WriteLine($"Error: {error.Message}");
            }
            catch (Exception error)
            {
                Console.Error.WriteLine($"Internal error: {error.Message}");
            }
        }
        return 0;
    }

    private static string ReadReplSubmission(string firstLine)
    {
        string trimmed = firstLine.TrimStart();
        bool expectsBlock = trimmed.StartsWith("if ", StringComparison.Ordinal) ||
            trimmed.StartsWith("while ", StringComparison.Ordinal) ||
            trimmed.StartsWith("for ", StringComparison.Ordinal) ||
            trimmed.StartsWith("fn ", StringComparison.Ordinal) ||
            trimmed.StartsWith("try", StringComparison.Ordinal) ||
            trimmed.StartsWith("window ", StringComparison.Ordinal);
        if (!expectsBlock) return firstLine + System.Environment.NewLine;

        var lines = new List<string> { firstLine };
        Console.Write("... ");
        while (true)
        {
            string? line = Console.ReadLine();
            if (line is null || string.IsNullOrWhiteSpace(line)) break;
            lines.Add(line);
            Console.Write("... ");
        }
        return string.Join(System.Environment.NewLine, lines) + System.Environment.NewLine;
    }
}
