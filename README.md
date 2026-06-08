# razorrender

A small .NET 9 command-line tool that renders a **Razor (`.cshtml`) template** against a **JSON
data file** and writes the result to standard output or a file.

It uses Microsoft-native technology for the core — the ASP.NET Core Razor view engine with runtime
compilation, `System.CommandLine` for argument parsing, and `System.Text.Json` for data — so there
is no third-party templating engine. [Serilog](https://serilog.net) is used for structured logging.

## Features

- **Razor templating** — full `.cshtml` syntax: `@Model.x`, `@foreach`, `@if`, layouts, helpers.
- **JSON model** — the data file is parsed into a dynamic model, so nested objects
  (`@Model.user.address.city`) and arrays (`@foreach (var i in Model.items)`) work naturally.
- **Output to stdout or a file** via `--output`.
- **HTML-safe by default** — `@Model.x` is HTML-encoded; use `@Html.Raw(...)` for literal output.
- **Structured logging** to stderr (quiet by default, `--verbose` for detail) — stdout always
  carries only the rendered template, so it stays pipe-friendly.

## Requirements

- [.NET SDK 9.0](https://dotnet.microsoft.com/download) or later.

## Build & test

```sh
dotnet build
dotnet test
```

## Usage

```
razorrender <template> <data> [options]

Arguments:
  <template>   Path to the Razor (.cshtml) template file.
  <data>       Path to the JSON data file bound as the Razor @Model.

Options:
  -o, --output <file>   Write the rendered result to this file instead of stdout.
  -v, --verbose         Enable informational logging (to stderr).
  -?, -h, --help        Show help and usage information.
```

Run during development with `dotnet run`:

```sh
dotnet run --project src/RazorRenderCli -- <template> <data> [options]
```

## Examples

### Basic substitution

`hello.cshtml`:

```cshtml
Hello @Model.name, you have @Model.items.Count items!
```

`data.json`:

```json
{ "name": "Steve", "items": [1, 2, 3] }
```

```sh
dotnet run --project src/RazorRenderCli -- hello.cshtml data.json
# Hello Steve, you have 3 items!
```

### Loops and conditionals

`report.cshtml`:

```cshtml
@foreach (var user in Model.users)
{
    <text>- @user.name (@(user.admin ? "admin" : "member"))
</text>
}
```

`users.json`:

```json
{ "users": [ { "name": "Ann", "admin": true }, { "name": "Bob", "admin": false } ] }
```

```sh
dotnet run --project src/RazorRenderCli -- report.cshtml users.json
# - Ann (admin)
# - Bob (member)
```

### Write to a file

```sh
dotnet run --project src/RazorRenderCli -- hello.cshtml data.json --output greeting.txt
```

## Template syntax

Templates are standard Razor. The JSON data file is bound as `@Model`:

- `@Model.title` — inserts a value, **HTML-encoded**.
- `@Html.Raw(Model.html)` — inserts a value **without** encoding.
- `@Model.user.address.city` — nested objects via member access.
- `@foreach (var item in Model.items) { ... }` — iterate arrays.
- `@if (Model.flag) { ... } else { ... }` — conditionals.

Referencing a key that is absent from the data throws at render time (standard ASP.NET dynamic-model
behavior); the tool reports the error on stderr and exits with code `1`.

## Exit codes

| Code | Meaning                                            |
|------|----------------------------------------------------|
| `0`  | Success.                                            |
| `1`  | Render, I/O, or JSON error (message on stderr).     |
| `2`  | Argument / usage error (message on stderr).         |

## Logging

All log output is written to **stderr**, leaving stdout clean for the rendered result. Logging is
quiet by default (warnings and errors only); pass `--verbose` to include informational messages such
as the resolved file paths and render duration.

## Project layout

```
src/RazorRenderCli/        # the console application
  JsonModelLoader.cs       # JSON -> dynamic model
  RazorRenderer.cs         # hosts the ASP.NET Core Razor view engine
  CliApp.cs / Program.cs   # CLI wiring and Serilog setup
tests/RazorRenderCli.Tests # NUnit tests (logic + CLI end-to-end)
```
