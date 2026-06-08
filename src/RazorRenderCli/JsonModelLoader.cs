using System.Dynamic;
using System.Text.Json;

namespace RazorRenderCli;

/// <summary>
/// Loads a JSON document into a <c>dynamic</c> object graph suitable for use as a Razor
/// <c>@Model</c>:
/// <list type="bullet">
///   <item>JSON objects become <see cref="ExpandoObject"/> (so <c>@Model.user.city</c> works),</item>
///   <item>JSON arrays become <see cref="List{Object}"/> (so <c>@foreach</c> works),</item>
///   <item>scalars become <see cref="string"/>, <see cref="long"/>/<see cref="double"/>, <see cref="bool"/>, or null.</item>
/// </list>
/// </summary>
public static class JsonModelLoader
{
    private static readonly JsonDocumentOptions Options = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Loads and converts the JSON contents of <paramref name="path"/>.</summary>
    public static object? LoadFile(string path)
    {
        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new RenderException($"Could not read data file '{path}': {ex.Message}", ex);
        }

        try
        {
            return Load(json);
        }
        catch (RenderException ex)
        {
            // Re-wrap so the message names the offending file.
            throw new RenderException($"Invalid JSON in data file '{path}': {ex.Message}", ex);
        }
    }

    /// <summary>Parses and converts a JSON string.</summary>
    public static object? Load(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json, Options);
            return Convert(document.RootElement);
        }
        catch (JsonException ex)
        {
            throw new RenderException(ex.Message, ex);
        }
    }

    private static object? Convert(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var expando = new ExpandoObject();
                var dict = (IDictionary<string, object?>)expando;
                foreach (var property in element.EnumerateObject())
                {
                    dict[property.Name] = Convert(property.Value);
                }

                return expando;

            case JsonValueKind.Array:
                var list = new List<object?>();
                foreach (var item in element.EnumerateArray())
                {
                    list.Add(Convert(item));
                }

                return list;

            case JsonValueKind.String:
                return element.GetString();

            case JsonValueKind.Number:
                // Prefer an integral type when the value has no fractional part so that
                // templates render "3" instead of "3" vs "3.0" surprises.
                return element.TryGetInt64(out var l) ? l : element.GetDouble();

            case JsonValueKind.True:
            case JsonValueKind.False:
                return element.GetBoolean();

            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
            default:
                return null;
        }
    }
}
