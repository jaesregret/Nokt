using Nokt;
using Nokt.Ui;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: Nokt.Ui <file>");
    return 1;
}

try
{
    string fullPath = Path.GetFullPath(args[0]);
    var interpreter = new Interpreter(LoadModule, new XUi());
    interpreter.Execute(ParseFile(fullPath), fullPath);
    return 0;
}
catch (NoktException exception)
{
    Console.Error.WriteLine($"Error: {exception.Message}");
    return 1;
}

static ModuleSource LoadModule(string requestedPath, string importerPath)
{
    string directory = Path.GetDirectoryName(importerPath) ?? Directory.GetCurrentDirectory();
    string modulePath = Path.GetFullPath(Path.Combine(directory, requestedPath));
    if (Path.GetExtension(modulePath).Length == 0) modulePath += ".nk";
    return new ModuleSource(modulePath, ParseFile(modulePath));
}

static List<Statement> ParseFile(string filePath)
{
    if (!File.Exists(filePath))
        throw new NoktException($"module file '{filePath}' not found");

    string source = File.ReadAllText(filePath);
    return new Parser(new Lexer(source).Tokenize()).Parse();
}
