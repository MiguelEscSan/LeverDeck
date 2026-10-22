using LeverDeck.Application.Icons.Ports;
using LeverDeck.Domain.Icons;
using LeverDeck.Domain.Profiles;
using LeverDeck.Infrastructure.Configuration;
using LeverDeck.Infrastructure.Persistence;

namespace LeverDeck.Infrastructure.Icons;

/// <summary>
/// Uploaded icons as <c>icons/&lt;id&gt;.png</c> in the data directory, written atomically. Only files named like a
/// <see cref="CustomIcon"/> id count; anything else in the folder is ignored and never served.
/// </summary>
public sealed class FileIconStore : IIconStore
{
    public const string FolderName = "icons";
    public const string Extension = ".png";

    public FileIconStore(LeverDeckOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Folder = Path.Combine(options.DataDirectory, FolderName);
    }

    public string Folder { get; }

    public Task<IReadOnlyList<IconId>> ListAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(Folder))
        {
            return Task.FromResult<IReadOnlyList<IconId>>([]);
        }

        IReadOnlyList<IconId> icons =
        [
            .. Directory.EnumerateFiles(Folder, "*" + Extension)
                .Select(Path.GetFileNameWithoutExtension)
                .Select(TryParse)
                .OfType<IconId>(),
        ];
        return Task.FromResult(icons);
    }

    public Task<bool> ExistsAsync(IconId icon, CancellationToken cancellationToken) =>
        Task.FromResult(PathOf(icon) is { } path && File.Exists(path));

    public Task SaveAsync(IconId icon, IconImage image, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(image);
        var path = PathOf(icon) ?? throw new ArgumentException($"'{icon}' is not an uploaded icon id.", nameof(icon));
        return AtomicFile.WriteAllBytesAsync(path, image.Bytes.ToArray(), cancellationToken);
    }

    public Task DeleteAsync(IconId icon, CancellationToken cancellationToken)
    {
        if (PathOf(icon) is { } path)
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    /// <summary>The file of an uploaded icon, or <c>null</c> for an id that is not a <see cref="CustomIcon"/> id.</summary>
    public string? PathOf(IconId icon)
    {
        ArgumentNullException.ThrowIfNull(icon);
        return CustomIcon.IsCustom(icon) ? Path.Combine(Folder, icon.Value + Extension) : null;
    }

    /// <summary>Reads a file or URL name (without extension) as an uploaded icon id, or <c>null</c>.</summary>
    public static IconId? TryParse(string? name)
    {
        try
        {
            return name is not null && new IconId(name) is var icon && CustomIcon.IsCustom(icon) ? icon : null;
        }
        catch (LeverDeck.Domain.Errors.DomainException)
        {
            return null;
        }
    }
}
