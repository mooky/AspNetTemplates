namespace RazorRenderCli;

/// <summary>Renders a Razor <c>.cshtml</c> template against a model and returns the result.</summary>
public interface IRazorRenderer
{
    /// <summary>
    /// Renders the template at <paramref name="templatePath"/> using <paramref name="model"/>
    /// as the Razor <c>@Model</c>.
    /// </summary>
    /// <exception cref="RenderException">
    /// Thrown when the template cannot be found, fails to compile, or throws while rendering
    /// (for example, a missing member on the dynamic model).
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when <paramref name="cancellationToken"/> is signalled before rendering begins.
    /// </exception>
    Task<string> RenderAsync(
        string templatePath, object? model, CancellationToken cancellationToken = default);
}
