using RazorRenderCli;
using Serilog;
using Serilog.Core;
using Serilog.Events;

// Quiet by default; --verbose raises this switch to Information at runtime.
var levelSwitch = new LoggingLevelSwitch(LogEventLevel.Warning);

// All log output goes to stderr so stdout carries only the rendered template.
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.ControlledBy(levelSwitch)
    .WriteTo.Console(
        standardErrorFromLevel: LogEventLevel.Verbose,
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    return await CliApp.RunAsync(args, Console.Out, Console.Error, levelSwitch);
}
finally
{
    Log.CloseAndFlush();
}
