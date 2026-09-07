# Nokt
**Nokt** is a small programming(interpreter) language made from scratch with .NET 8. It is still growing, but you can already write useful little programs with it.

## What you can do
- Print things with `say`.
- Create and update variables with `let` and `=`.
- Use strings, integers, booleans, lists, and variables.
- Do math, string concatenation, comparisons, and logic with `and`, `or`, and `not`.
- Control the flow with `if`, `else`, and `while`.
- Add comments with `#`.
- Define functions with `fn`, parameters, calls, and `return`.
- Use closures and nested scopes, including variable shadowing.
- Split code into modules with `export` and `import ... as ...`.
- Build simple Windows Forms interfaces through the optional UI project.
- Get useful lexer, parser, and runtime error messages with line and column information.

The language core targets `net8.0` and does not depend on Windows. GUI support is optional and lives in `src/nk.Ui`, targeting `net8.0-windows`.

## Running it
To try the main example, run:

```text
dotnet run --project src/nk/Nokt.csproj -- Examples/features.nk

```
The GUI example runs on Windows through the optional UI project:

```text
dotnet run --project src/nk.Ui/Nokt.Ui.csproj -- Examples/ui.nk

```

There are more examples in `Examples/`, including scopes, functions, modules, collections, and the older `hello.nk` and `vars.nk` files.

To run the tests:

```text
dotnet run --project Tests/Nokt.Tests.csproj
```
