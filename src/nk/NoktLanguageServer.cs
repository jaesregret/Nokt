using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Nokt;

public static class NoktLanguageServer
{
    private static readonly string[] Keywords =
    {
        "say", "import", "let", "if", "else", "while", "true", "false",
        "and", "or", "not", "window", "text", "button", "input", "size"
    };

    public static int Run()
    {
        using Stream input = Console.OpenStandardInput();
        using Stream output = Console.OpenStandardOutput();

        while (ReadMessage(input, out string message))
        {
            using JsonDocument document = JsonDocument.Parse(message);
            JsonElement root = document.RootElement;
            string method = root.GetProperty("method").GetString() ?? string.Empty;
            JsonElement id = root.TryGetProperty("id", out JsonElement requestId)
                ? requestId
                : default;
            JsonElement parameters = root.TryGetProperty("params", out JsonElement requestParams)
                ? requestParams
                : default;

            switch (method)
            {
                case "initialize":
                    SendResponse(output, id, new
                    {
                        capabilities = new
                        {
                            textDocumentSync = 1,
                            completionProvider = new { resolveProvider = false }
                        },
                        serverInfo = new { name = "Nokt", version = "0.1" }
                    });
                    break;
                case "shutdown":
                    SendResponse(output, id, null);
                    break;
                case "exit":
                    return 0;
                case "textDocument/didOpen":
                    PublishDiagnostics(output, parameters);
                    break;
                case "textDocument/didChange":
                    PublishDiagnostics(output, parameters);
                    break;
                case "textDocument/completion":
                    SendResponse(output, id, new { isIncomplete = false, items = Keywords.Select(label => new { label, kind = 14 }) });
                    break;
            }
        }

        return 0;
    }

    private static void PublishDiagnostics(Stream output, JsonElement parameters)
    {
        string uri;
        string source;
        if (parameters.TryGetProperty("contentChanges", out JsonElement changes))
        {
            JsonElement textDocument = parameters.GetProperty("textDocument");
            uri = textDocument.GetProperty("uri").GetString() ?? string.Empty;
            source = changes[0].GetProperty("text").GetString() ?? string.Empty;
        }
        else
        {
            JsonElement textDocument = parameters.GetProperty("textDocument");
            uri = textDocument.GetProperty("uri").GetString() ?? string.Empty;
            source = textDocument.GetProperty("text").GetString() ?? string.Empty;
        }

        var diagnostics = new List<object>();
        try
        {
            var lexer = new Lexer(source);
            var parser = new Parser(lexer.Tokenize());
            parser.Parse();
        }
        catch (NoktException exception)
        {
            Match location = Regex.Match(exception.Message, @"line (\d+), column (\d+)");
            int line = location.Success ? Math.Max(0, int.Parse(location.Groups[1].Value) - 1) : 0;
            int column = location.Success ? Math.Max(0, int.Parse(location.Groups[2].Value) - 1) : 0;
            diagnostics.Add(new
            {
                range = new
                {
                    start = new { line, character = column },
                    end = new { line, character = column + 1 }
                },
                severity = 1,
                source = "nokt",
                message = exception.Message
            });
        }

        SendNotification(output, "textDocument/publishDiagnostics", new { uri, diagnostics });
    }

    private static bool ReadMessage(Stream input, out string message)
    {
        var header = new StringBuilder();
        int previous = -1;
        int current;
        while ((current = input.ReadByte()) != -1)
        {
            header.Append((char)current);
            if (previous == '\r' && current == '\n' && header.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
                break;
            previous = current;
        }

        if (current == -1)
        {
            message = string.Empty;
            return false;
        }

        Match lengthMatch = Regex.Match(header.ToString(), @"Content-Length:\s*(\d+)", RegexOptions.IgnoreCase);
        if (!lengthMatch.Success)
            throw new InvalidDataException("LSP message is missing Content-Length");

        int length = int.Parse(lengthMatch.Groups[1].Value);
        byte[] body = new byte[length];
        int offset = 0;
        while (offset < length)
        {
            int read = input.Read(body, offset, length - offset);
            if (read == 0) throw new EndOfStreamException();
            offset += read;
        }

        message = Encoding.UTF8.GetString(body);
        return true;
    }

    private static void SendResponse(Stream output, JsonElement id, object? result)
    {
        Send(output, new { jsonrpc = "2.0", id, result });
    }

    private static void SendNotification(Stream output, string method, object parameters)
    {
        Send(output, new { jsonrpc = "2.0", method, @params = parameters });
    }

    private static void Send(Stream output, object message)
    {
        byte[] body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
        byte[] header = Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\n\r\n");
        output.Write(header, 0, header.Length);
        output.Write(body, 0, body.Length);
        output.Flush();
    }
}
