using LeverDeck.Application.Settings.Ports;
using LeverDeck.Domain.Settings;
using LeverDeck.Infrastructure.Configuration;
using LeverDeck.Infrastructure.Persistence.Records;
using LeverDeck.Infrastructure.Security;

namespace LeverDeck.Infrastructure.Persistence;

/// <summary>
/// Agent settings in <c>settings.json</c>. A missing file reads as <see cref="AgentSettings.CreateDefault"/> and is
/// written on the first save only. The OBS password is DPAPI-protected on disk.
/// </summary>
public sealed class JsonSettingsRepository : ISettingsRepository, IDisposable
{
    public const string FileName = "settings.json";

    private readonly JsonDocumentFile<SettingsDocument> _file;
    private readonly ISecretProtector _protector;

    public JsonSettingsRepository(LeverDeckOptions options)
        : this(options, new DpapiSecretProtector("obs-password"))
    {
    }

    internal JsonSettingsRepository(LeverDeckOptions options, ISecretProtector protector)
    {
        ArgumentNullException.ThrowIfNull(options);
        _protector = protector;
        _file = new JsonDocumentFile<SettingsDocument>(
            Path.Combine(options.DataDirectory, FileName),
            PersistenceJsonContext.Default.SettingsDocument,
            document => document.SchemaVersion,
            SettingsDocument.CurrentVersion);
    }

    public Task<AgentSettings> GetAsync(CancellationToken cancellationToken) =>
        _file.ReadAsync(
            current => current is null ? AgentSettings.CreateDefault() : SettingsRecordMapper.ToDomain(current, _protector),
            cancellationToken);

    public Task SaveAsync(AgentSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var document = SettingsRecordMapper.ToDocument(settings, _protector);
        return _file.UpdateAsync(_ => document, cancellationToken);
    }

    public void Dispose() => _file.Dispose();
}
