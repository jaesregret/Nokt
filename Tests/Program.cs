using Nokt;

var tests = new (string Name, Action Run)[]
{
    ("returns values and accepts typed parameters", TestReturnValue),
    ("captures closure variables", TestClosure),
    ("returns closures that keep captured environments", TestReturnedClosure),
    ("supports recursion", TestRecursion),
    ("preserves returned value types", TestReturnTypes),
    ("validates explicit return types", TestExplicitReturnTypes),
    ("rejects invalid return types", TestInvalidReturnTypes),
    ("supports parameter shadowing", TestParameterShadowing),
    ("keeps function locals private", TestFunctionScope),
    ("rejects invalid argument counts", TestArgumentCount),
    ("rejects invalid argument types", TestArgumentType)
    ,("supports exported module namespaces", TestModuleNamespace)
    ,("hides non-exported module members", TestPrivateModuleMember)
    ,("hides non-exported members without aliases", TestPrivateModuleMemberWithoutAlias)
    ,("rejects heterogeneous lists", TestHeterogeneousLists)
    ,("catches obvious type errors statically", TestStaticTypeErrors)
    ,("supports typed homogeneous lists", TestTypedLists)
    ,("supports for loops with break and continue", TestForLoops)
    ,("supports len and range", TestStdlib)
    ,("keeps break scoped to its function", TestFunctionLoopScope)
    ,("supports input and display concatenation", TestInputAndDisplay)
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

static void TestReturnedClosure()
{
    string output = Run("fn makeAdder(base: int)\n    fn add(value: int)\n        return base + value\n    return add\n\nlet add10 = makeAdder(10)\nsay add10(5)\n");
    AssertEqual("15", output.Trim());
}

static void TestReturnTypes()
{
    string output = Run("fn number()\n    return 42\nfn getText()\n    return \"ok\"\nsay number() + 1\nsay getText() + \"!\"\n");
    AssertEqual("43\nok!", output.Trim());
}

static void TestExplicitReturnTypes()
{
    string output = Run("fn add(a: int, b: int) -> int\n    return a + b\n\nsay add(10, 20)\n");
    AssertEqual("30", output.Trim());
}

static void TestInvalidReturnTypes()
{
    AssertThrows("fn wrong() -> int\n    return \"nope\"\n\nsay wrong()\n", "function 'wrong' must return 'int', got 'string'");
    AssertThrows("fn missing() -> int\n    say 1\n\nsay missing()\n", "function 'missing' must return 'int', got 'void'");
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

static void TestModuleNamespace()
{
    string output = RunWithModules(
        "import \"math.nk\" as math\nsay math.add(1, 2)\n",
        new Dictionary<string, string>
        {
            ["math.nk"] = "export fn add(a: int, b: int)\n    return a + b\n"
        });
    AssertEqual("3", output.Trim());
}

static void TestPrivateModuleMember()
{
    AssertThrowsWithModules(
        "import \"math.nk\" as math\nsay math.hidden()\n",
        new Dictionary<string, string>
        {
            ["math.nk"] = "fn hidden()\n    return 1\n"
        },
        "module has no exported member 'hidden'");
}

static void TestPrivateModuleMemberWithoutAlias()
{
    AssertThrowsWithModules(
        "import \"math.nk\"\nsay hidden()\n",
        new Dictionary<string, string>
        {
            ["math.nk"] = "export fn add()\n    return 1\nfn hidden()\n    return 2\n"
        },
        "undefined variable 'hidden'");
}

static void TestHeterogeneousLists()
{
    AssertThrows("let values = [1, \"two\"]\n", "list elements must have the same type");
}

static void TestStaticTypeErrors()
{
    AssertStaticThrows("let values = [1, \"two\"]\n", "static type error: list elements must have the same type");
    AssertStaticThrows("let value: int = \"wrong\"\n", "static type error: variable 'value' is 'int'");
    AssertStaticThrows("if 1\n    say 1\n", "static type error: if condition must be 'bool'");
}

static void TestTypedLists()
{
    string output = Run("let values: list[int] = [1, 2, 3]\nsay values[1]\n");
    AssertEqual("2", output.Trim());
    AssertStaticThrows("let values: list[int] = [1, \"two\"]\n", "list elements must have the same type");
}

static void TestForLoops()
{
    string output = Run("let total = 0\nfor value in range(6)\n    if value == 2\n        continue\n    if value == 5\n        break\n    total = total + value\nsay total\n");
    AssertEqual("8", output.Trim());
}

static void TestStdlib()
{
    string output = Run("let values = range(3)\nsay len(values)\nsay len(\"nokt\")\n");
    AssertEqual("3\n4", output.Trim());
}

static void TestFunctionLoopScope()
{
    AssertThrows("fn stop()\n    break\n\nfor value in range(1)\n    stop()\n", "break can only be used inside a loop");
}

static void TestInputAndDisplay()
{
    string output = RunWithInput("say \"HP: \" + 10\n", "");
    AssertEqual("HP: 10", output.Trim());
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
        return writer.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
    }
    finally
    {
        Console.SetOut(previous);
    }
}

static string RunWithInput(string source, string input)
{
    TextReader previousInput = Console.In;
    Console.SetIn(new StringReader(input));
    try
    {
        return Run(source);
    }
    finally
    {
        Console.SetIn(previousInput);
    }
}

static void AssertStaticThrows(string source, string expectedMessage)
{
    try
    {
        var statements = new Parser(new Lexer(source).Tokenize()).Parse();
        new TypeChecker().Check(statements);
        throw new InvalidOperationException($"Expected static error containing '{expectedMessage}'");
    }
    catch (NoktException exception) when (exception.Message.Contains(expectedMessage, StringComparison.Ordinal))
    {
    }
}

static string RunWithModules(string source, Dictionary<string, string> modules)
{
    var writer = new StringWriter();
    TextWriter previous = Console.Out;
    Console.SetOut(writer);
    try
    {
        var statements = new Parser(new Lexer(source).Tokenize()).Parse();
        var interpreter = new Interpreter((path, _) =>
        {
            if (!modules.TryGetValue(path, out string? moduleSource))
                throw new NoktException($"module file '{path}' not found");
            return new ModuleSource(path, new Parser(new Lexer(moduleSource).Tokenize()).Parse());
        });
        interpreter.Execute(statements);
        return writer.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
    }
    finally
    {
        Console.SetOut(previous);
    }
}

static void AssertThrowsWithModules(string source, Dictionary<string, string> modules, string expectedMessage)
{
    try
    {
        RunWithModules(source, modules);
        throw new InvalidOperationException($"Expected error containing '{expectedMessage}'");
    }
    catch (NoktException exception) when (exception.Message.Contains(expectedMessage, StringComparison.Ordinal))
    {
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
