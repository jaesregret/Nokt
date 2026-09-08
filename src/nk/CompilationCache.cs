using System.Collections.Concurrent;

namespace Nokt;

public sealed class CompiledProgram
{
    public string? SourcePath { get; }
    public List<Statement> Statements { get; }

    public CompiledProgram(string? sourcePath, List<Statement> statements)
    {
        SourcePath = sourcePath;
        Statements = statements;
    }
}

public sealed class CompilationCache
{
    private readonly ConcurrentDictionary<string, CacheEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

    public CompiledProgram GetOrCompileFile(string filePath)
    {
        string fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath))
            throw new NoktException($"module file '{fullPath}' not found");

        DateTime lastWrite = File.GetLastWriteTimeUtc(fullPath);
        long length = new FileInfo(fullPath).Length;
        if (_entries.TryGetValue(fullPath, out CacheEntry? cached) && cached.LastWriteUtc == lastWrite && cached.Length == length)
            return cached.Program;

        CompiledProgram program = Compile(fullPath, File.ReadAllText(fullPath));
        _entries[fullPath] = new CacheEntry(lastWrite, length, program);
        return program;
    }

    public CompiledProgram CompileSource(string source, string? sourcePath = null)
    {
        var lexer = new Lexer(source);
        var parser = new Parser(lexer.Tokenize());
        List<Statement> statements = parser.Parse();
        new TypeChecker().Check(statements);
        return new CompiledProgram(sourcePath, statements);
    }

    public void Clear() => _entries.Clear();

    private CompiledProgram Compile(string sourcePath, string source) => CompileSource(source, sourcePath);

    private sealed record CacheEntry(DateTime LastWriteUtc, long Length, CompiledProgram Program);
}
