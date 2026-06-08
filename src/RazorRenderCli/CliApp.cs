using System.CommandLine;
using System.Diagnostics;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Extensions.Logging;

namespace RazorRenderCli;

/// <summary>
/// Wires up the command-line interface. <see cref="RunAsync"/> writes only to the supplied
/// <see cref="TextWriter"/>s and returns an exit code, so it can be driven directly from tests
/// without spawning a process or touching <see cref="Console"/>.
/// </summary>
public static class CliApp
{
    /// <summary>Successful render.</summary>
    public const int ExitSuccess = 0;

    /// <summary>Render, I/O, or JSON error.</summary>
    public const int ExitRenderError = 1;

    /// <summary>Argument / usage error.</summary>
    public const int ExitUsageError = 2;

    public static async Task<int> RunAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        LoggingLevelSwitch? levelSwitch = null)
    {
        var templateArgument = new Argument<FileInfo>("template")
        {
            Description = "Path to the Razor (.cshtml) template file.",
        };
        var dataArgument = new Argument<FileInfo>("data")
        {
            Description = "Path to the JSON data file bound as the Razor @Model.",
        };
        var outputOption = new Option<FileInfo?>("--output", "-o")
        {
            Description = "Write the rendered result to this file instead of stdout.",
        };
        var verboseOption = new Option<bool>("--verbose", "-v")
        {
            Description = "Enable informational logging (to stderr).",
        };

        var rootCommand = new RootCommand(
            "Render a Razor (.cshtml) template against a JSON data file and write the result " +
            "to stdout or a file.")
        {
            templateArgument,
            dataArgument,
            outputOption,
            verboseOption,
        };

        rootCommand.SetAction((parseResult, _) =>
        {
            if (parseResult.GetValue(verboseOption) && levelSwitch is not null)
            {
                levelSwitch.MinimumLevel = LogEventLevel.Information;
            }

            var template = parseResult.GetValue(templateArgument)!;
            var data = parseResult.GetValue(dataArgument)!;
            var outputFile = parseResult.GetValue(outputOption);
            return ExecuteAsync(template, data, outputFile, output, error);
        });

        var parseResult = rootCommand.Parse(args);
        if (parseResult.Errors.Count > 0)
        {
            foreach (var parseError in parseResult.Errors)
            {
                error.WriteLine($"error: {parseError.Message}");
            }

            error.WriteLine("Run with --help for usage.");
            return ExitUsageError;
        }

        var invocationConfiguration = new InvocationConfiguration
        {
            Output = output,
            Error = error,
        };

        return await parseResult.InvokeAsync(invocationConfiguration);
    }

    private static async Task<int> ExecuteAsync(
        FileInfo template,
        FileInfo data,
        FileInfo? outputFile,
        TextWriter output,
        TextWriter error)
    {
        try
        {
            Log.Information("Loading data file {DataPath}", data.FullName);
            var model = JsonModelLoader.LoadFile(data.FullName);

            var modelKeyCount = model is IDictionary<string, object?> dict ? dict.Count : 0;
            Log.Debug("Model loaded with {KeyCount} top-level key(s)", modelKeyCount);

            Log.Information("Rendering template {TemplatePath}", template.FullName);
            var stopwatch = Stopwatch.StartNew();

            using var renderer = new RazorRenderer(new SerilogLoggerFactory());
            var rendered = await renderer.RenderAsync(template.FullName, model);

            stopwatch.Stop();
            Log.Information(
                "Rendered {Length} characters in {ElapsedMs} ms",
                rendered.Length,
                stopwatch.ElapsedMilliseconds);

            if (outputFile is not null)
            {
                await File.WriteAllTextAsync(outputFile.FullName, rendered);
                Log.Information("Wrote output to {OutputPath}", outputFile.FullName);
            }
            else
            {
                await output.WriteAsync(rendered);
            }

            return ExitSuccess;
        }
        catch (RenderException ex)
        {
            Log.Error(ex, "Render failed");
            error.WriteLine($"error: {ex.Message}");
            return ExitRenderError;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unexpected failure");
            error.WriteLine($"error: {ex.Message}");
            return ExitRenderError;
        }
    }
}
