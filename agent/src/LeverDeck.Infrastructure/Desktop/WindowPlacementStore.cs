using System.Text.Json;
using System.Text.Json.Serialization;
using LeverDeck.Infrastructure.Configuration;
using LeverDeck.Infrastructure.Persistence;

namespace LeverDeck.Infrastructure.Desktop;

/// <summary>
/// Where the editor window was, in the window placement's own coordinates (<c>GetWindowPlacement</c>'s normal
/// position), and whether it was maximized.
/// </summary>
public sealed record WindowPlacement(int X, int Y, int Width, int Height, bool Maximized)
{
    public const int MinimumWidth = 480;

    public const int MinimumHeight = 360;

    /// <summary>Larger than any desktop; a bigger value means the file is not a placement this agent wrote.</summary>
    internal const int MaximumExtent = 100_000;
}

/// <summary>
/// Remembers the editor window's size and position in <c>window.json</c> in the data directory. Unlike profiles, a
/// file that cannot be used is not worth failing for: the window then opens at its default place and the next close
/// writes a good file.
/// </summary>
public sealed class WindowPlacementStore(LeverDeckOptions options)
{
    public const string FileName = "window.json";

    private const int SchemaVersion = 1;

    private readonly string _path = Path.Combine(options.DataDirectory, FileName);

    public WindowPlacement? Load()
    {
        WindowPlacementDocument? document;
        try
        {
            if (!File.Exists(_path))
            {
                return null;
            }

            document = JsonSerializer.Deserialize(File.ReadAllBytes(_path), WindowPlacementJsonContext.Default.WindowPlacementDocument);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }

        if (document is null
            || document.SchemaVersion != SchemaVersion
            || Math.Abs((long)document.X) > WindowPlacement.MaximumExtent
            || Math.Abs((long)document.Y) > WindowPlacement.MaximumExtent
            || document.Width > WindowPlacement.MaximumExtent
            || document.Height > WindowPlacement.MaximumExtent)
        {
            return null;
        }

        return new WindowPlacement(
            document.X,
            document.Y,
            Math.Max(document.Width, WindowPlacement.MinimumWidth),
            Math.Max(document.Height, WindowPlacement.MinimumHeight),
            document.Maximized);
    }

    public void Save(WindowPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);
        var document = new WindowPlacementDocument(
            SchemaVersion, placement.X, placement.Y, placement.Width, placement.Height, placement.Maximized);
        AtomicFile.WriteAllBytes(_path, JsonSerializer.SerializeToUtf8Bytes(document, WindowPlacementJsonContext.Default.WindowPlacementDocument));
    }
}

internal sealed record WindowPlacementDocument(int SchemaVersion, int X, int Y, int Width, int Height, bool Maximized);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true)]
[JsonSerializable(typeof(WindowPlacementDocument))]
internal sealed partial class WindowPlacementJsonContext : JsonSerializerContext;
