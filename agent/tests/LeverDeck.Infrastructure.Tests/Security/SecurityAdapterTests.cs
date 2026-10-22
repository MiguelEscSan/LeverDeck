using LeverDeck.Infrastructure.Configuration;
using LeverDeck.Infrastructure.Security;

namespace LeverDeck.Infrastructure.Tests.Security;

public sealed class SecureTokenGeneratorTests
{
    private readonly SecureTokenGenerator _generator = new();

    [Fact]
    public void NewDeviceToken_Is32RandomBytesInBase64Url()
    {
        var token = _generator.NewDeviceToken();

        Assert.Equal(43, token.Length);
        Assert.Matches("^[A-Za-z0-9_-]{43}$", token);
    }

    [Fact]
    public void NewDeviceToken_IsDifferentEveryTime()
    {
        var tokens = Enumerable.Range(0, 100).Select(_ => _generator.NewDeviceToken()).ToHashSet();

        Assert.Equal(100, tokens.Count);
    }

    [Fact]
    public void NewPairingCode_IsSixDigits()
    {
        Assert.All(
            Enumerable.Range(0, 200).Select(_ => _generator.NewPairingCode().Value),
            code => Assert.Matches("^[0-9]{6}$", code));
    }
}

public sealed class Sha256TokenHasherTests
{
    private readonly Sha256TokenHasher _hasher = new();

    [Fact]
    public void Hash_IsLowercaseHexSha256()
    {
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", _hasher.Hash("abc").Value);
    }

    [Fact]
    public void Hash_IsDeterministic() => Assert.Equal(_hasher.Hash("token"), _hasher.Hash("token"));

    [Fact]
    public void Hash_DiffersForDifferentTokens() => Assert.NotEqual(_hasher.Hash("a"), _hasher.Hash("b"));
}

public sealed class AdminTokenStoreTests : IDisposable
{
    private readonly TempDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    private AdminTokenStore NewStore() => new(new LeverDeckOptions { DataDirectory = _directory.Path });

    [Fact]
    public void GetOrCreate_GeneratesAHighEntropyTokenOnce()
    {
        var token = NewStore().GetOrCreate();

        Assert.Matches("^[A-Za-z0-9_-]{43}$", token);
        Assert.True(File.Exists(_directory.File("admin-token")));
    }

    [Fact]
    public void GetOrCreate_ReusesTheStoredTokenAcrossInstances()
    {
        var first = NewStore().GetOrCreate();

        Assert.Equal(first, NewStore().GetOrCreate());
    }

    [Fact]
    public void StoredFile_DoesNotContainTheTokenInPlainText()
    {
        var token = NewStore().GetOrCreate();

        Assert.DoesNotContain(token, File.ReadAllText(_directory.File("admin-token")), StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_AcceptsOnlyTheStoredToken()
    {
        var store = NewStore();
        var token = store.GetOrCreate();

        Assert.True(store.Verify(token));
        Assert.False(store.Verify(token + "x"));
        Assert.False(store.Verify(string.Empty));
        Assert.False(store.Verify(null));
    }

    [Fact]
    public void CorruptFile_FailsLoudlyAndIsLeftUntouched()
    {
        File.WriteAllText(_directory.File("admin-token"), "garbage");

        Assert.Throws<InvalidDataException>(() => NewStore().GetOrCreate());
        Assert.Equal("garbage", File.ReadAllText(_directory.File("admin-token")));
    }
}
