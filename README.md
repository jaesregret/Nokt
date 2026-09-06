# Nokt
**Nokt** is a programming language built entirely from scratch using .NET 8.

## What it can do
* **Display messages:** Uses the `say` command to print output.
* **Store values:** Uses `let` to declare variables and `=` to update them.
* **Use nested scopes:** Supports global, local, and module environments with parent lookup and variable shadowing.
* **Support multiple data types:** Handles text (strings), numbers (integers), booleans (`true`/`false`), and variable references.
* **Perform math and combine text:** Standard arithmetic (`+`, `-`, `*`, `/`) with parentheses support, plus string concatenation.
* **Compare and evaluate logic:** Checks equalities and inequalities (`==`, `>`, `<`, etc.) and uses logical operators (`and`, `or`, `not`).
* **Control code flow:** Makes decisions using `if`/`else` statements and repeats actions with `while` loops.
* **Add comments:** Allows code annotations starting with `#`.
* **Report detailed errors:** Points out exact lines, columns, and values when lexical, syntax, or runtime errors occur.
* **Create desktop windows (GUI):** Features basic Windows Forms support to build windows containing text inputs, buttons, and click event blocks.

## How to use
To run a feature showcase example:

```text
dotnet run --project src/nk/Nokt.csproj -- Examples/features.nk

```
To run the graphical interface example (Windows only):

```text
dotnet run --project src/nk/Nokt.csproj -- Examples/ui.nk

```

Legacy examples are also available at `Examples/hello.nk` and `Examples/vars.nk`.
The scope example is available at `Examples/scopes.nk`.
