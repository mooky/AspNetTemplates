namespace RazorRenderCli;

/// <summary>
/// Raised for user-facing failures (bad input file, invalid JSON, template not found,
/// render error). The CLI catches it, writes <see cref="Exception.Message"/> to stderr,
/// and exits with code 1.
/// </summary>
public sealed class RenderException : Exception
{
    public RenderException(string message) : base(message)
    {
    }

    public RenderException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
