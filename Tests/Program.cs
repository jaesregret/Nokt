using Nokt;

var tests = new (string Name, Action Run)[]
{
    ("returns values and accepts typed parameters", TestReturnValue),
    ("captures closure variables", TestClosure),
    ("supports recursion", TestRecursion),
    ("preserves returned value types", TestReturnTypes),
    ("supports parameter shadowing", TestParameterShadowing),
    ("keeps function locals private", TestFunctionScope),
    ("rejects invalid argument counts", TestArgumentCount),
    ("rejects invalid argument types", TestArgumentType)
};

foreach ((string name, Action run) in tests)
{
    run();
    Console.WriteLine($"PASS: {name}");
}

static void TestReturnValue()
{
    string output = Run("fn add(a: int, b: int)\n    return a + b\n\nsay add(10, 20)\n");
    AssertEqual("30", output.Trim());
}

static void TestParameterShadowing()
{
    string output = Run("let value = 1\nfn show(value: int)\n    return value\nsay show(2)\n");
    AssertEqual("2", output.Trim());
}

static void TestClosure()
{
    string output = Run("fn makeAdder(base: int)\n    fn add(value: int)\n        return base + value\n    return add(5)\n\nsay makeAdder(10)\n");
    AssertEqual("15", output.Trim());
}

static void TestRecursion()
{
    string output = Run("fn factorial(value: int)\n    if value == 0\n        return 1\n    return value * factorial(value - 1)\n\nsay factorial(5)\n");
    AssertEqual("120", output.Trim());
}

static void TestReturnTypes()
{
    string output = Run("fn number()\n    return 42\nfn getText()\n    return \"ok\"\nsay number() + 1\nsay getText() + \"!\"\n");
    AssertEqual("43\r\nok!", output.Trim());
}

static void TestFunctionScope()
{
    AssertThrows("fn make()\n    let hidden = 10\n\nmake()\nsay hidden\n", "undefined variable 'hidden'");
}

static void TestArgumentCount()
{
    AssertThrows("fn add(a: int, b: int)\n    return a + b\n\nsay add(1)\n", "expected 2 argument(s), got 1");
}

static void TestArgumentType()
{
    AssertThrows("fn add(a: int)\n    return a\n\nsay add(\"wrong\")\n", "variable 'a' is 'int' but received 'string'");
}

static string Run(string source)
{
    var writer = new StringWriter();
    TextWriter previous = Console.Out;
    Console.SetOut(writer);
    try
    {
        var statements = new Parser(new Lexer(source).Tokenize()).Parse();
        new Interpreter().Execute(statements);
        return writer.ToString();
    }
    finally
    {
        Console.SetOut(previous);
    }
}

static void AssertThrows(string source, string expectedMessage)
{
    try
    {
        Run(source);
        throw new InvalidOperationException($"Expected error containing '{expectedMessage}'");
    }
    catch (NoktException exception) when (exception.Message.Contains(expectedMessage, StringComparison.Ordinal))
    {
    }
}

static void AssertEqual(string expected, string actual)
{
    if (!string.Equals(expected, actual, StringComparison.Ordinal))
        throw new InvalidOperationException($"Expected '{expected}', got '{actual}'");
}
