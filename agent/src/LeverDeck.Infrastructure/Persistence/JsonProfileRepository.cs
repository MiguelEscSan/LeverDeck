using LeverDeck.Application.Profiles.Ports;
using LeverDeck.Domain.Profiles;
using LeverDeck.Infrastructure.Configuration;
using LeverDeck.Infrastructure.Persistence.Records;

namespace LeverDeck.Infrastructure.Persistence;

/// <summary>
/// Profiles in <c>profiles.json</c>, in insertion order. When the file is missing or holds no profile, the
/// <see cref="DefaultProfile"/> is seeded and written at once, so its id stays stable across reads and restarts.
/// Every call reads the file, so callers always get fresh aggregates they may mutate.
/// </summary>
public sealed class JsonProfileRepository : IProfileRepository, IDisposable
{
    public const string FileName = "profiles.json";

    private readonly JsonDocumentFile<ProfilesDocument> _file;

    public JsonProfileRepository(LeverDeckOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _file = new JsonDocumentFile<ProfilesDocument>(
            Path.Combine(options.DataDirectory, FileName),
            PersistenceJsonContext.Default.ProfilesDocument,
            document => document.SchemaVersion,
            ProfilesDocument.CurrentVersion);
    }

    public Task<Profile?> GetByIdAsync(ProfileId id, CancellationToken cancellationToken) =>
        WithProfilesAsync(
            profiles => (Changed: false, Result: profiles.Find(record => record.Id == id.Value) is { } found
                ? ProfileRecordMapper.ToDomain(found)
                : null),
            cancellationToken);

    public Task<IReadOnlyList<Profile>> ListAsync(CancellationToken cancellationToken) =>
        WithProfilesAsync(
            profiles => (Changed: false, Result: (IReadOnlyList<Profile>)[.. profiles.Select(ProfileRecordMapper.ToDomain)]),
            cancellationToken);

    public Task SaveAsync(Profile profile, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var record = ProfileRecordMapper.ToRecord(profile);
        return WithProfilesAsync(
            profiles =>
            {
                var index = profiles.FindIndex(existing => existing.Id == record.Id);
                if (index >= 0)
                {
                    profiles[index] = record;
                }
                else
                {
                    profiles.Add(record);
                }

                return (Changed: true, Result: true);
            },
            cancellationToken);
    }

    public Task DeleteAsync(ProfileId id, CancellationToken cancellationToken) =>
        WithProfilesAsync(
            profiles => (Changed: profiles.RemoveAll(record => record.Id == id.Value) > 0, Result: true),
            cancellationToken);

    private Task<TResult> WithProfilesAsync<TResult>(
        Func<List<ProfileRecord>, (bool Changed, TResult Result)> body,
        CancellationToken cancellationToken) =>
        _file.TransactAsync(
            current =>
            {
                var seeded = current is null || current.Profiles.Count == 0;
                List<ProfileRecord> profiles = seeded
                    ? [ProfileRecordMapper.ToRecord(DefaultProfile.Create())]
                    : [.. current!.Profiles];

                var (changed, result) = body(profiles);
                var write = changed || seeded ? new ProfilesDocument(ProfilesDocument.CurrentVersion, profiles) : null;
                return (write, result);
            },
            cancellationToken);

    public void Dispose() => _file.Dispose();
}
