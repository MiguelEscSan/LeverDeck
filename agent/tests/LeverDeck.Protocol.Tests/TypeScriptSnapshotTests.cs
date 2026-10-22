using LeverDeck.ProtocolGen;

namespace LeverDeck.Protocol.Tests;

/// <summary>
/// Fails when the committed TypeScript in web/src/app/core/protocol is out of date. Regenerate from agent/ with
/// <c>dotnet run --project tools/LeverDeck.ProtocolGen -- ../web/src/app/core/protocol</c>.
/// </summary>
public sealed class TypeScriptSnapshotTests
{
    private static readonly string _outputDirectory =
        Path.Combine(RepositoryRoot(), "web", "src", "app", "core", "protocol");

    [Fact]
    public void GeneratedFiles_MatchTheCommittedOnes()
    {
        foreach (var (fileName, expected) in TypeScriptGenerator.Generate())
        {
            var path = Path.Combine(_outputDirectory, fileName);
            Assert.True(File.Exists(path), $"{path} is missing; regenerate the protocol types.");

            var committed = File.ReadAllText(path).ReplaceLineEndings("\n");

            Assert.True(committed == expected, $"{fileName} is out of date; regenerate the protocol types.");
        }
    }

    [Fact]
    public void Generation_IsDeterministic()
    {
        Assert.Equal(TypeScriptGenerator.Generate(), TypeScriptGenerator.Generate());
    }

    [Fact]
    public void GeneratedFiles_StartWithTheHeaderAndUseLf()
    {
        foreach (var (_, content) in TypeScriptGenerator.Generate())
        {
            Assert.StartsWith(TypeScriptGenerator.Header, content, StringComparison.Ordinal);
            Assert.DoesNotContain('\r', content);
            Assert.EndsWith("\n", content, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Unions_UseLiteralDiscriminators()
    {
        var protocol = TypeScriptGenerator.Generate()["protocol.ts"];

        Assert.Contains("export type ActionDto =", protocol, StringComparison.Ordinal);
        Assert.Contains("  type: 'hotkey';", protocol, StringComparison.Ordinal);
        Assert.Contains("  kind: 'command';\n  type: 'executeAction';", protocol, StringComparison.Ordinal);
        Assert.Contains("export type KeyColorDto = 'red' | 'blue'", protocol, StringComparison.Ordinal);
        Assert.Contains("  session: PairingSessionDto | null;", protocol, StringComparison.Ordinal);
        Assert.Contains("  payload?: AppRegisteredPayload;", protocol, StringComparison.Ordinal);
        Assert.Contains("export const PROTOCOL_VERSION = 1;", protocol, StringComparison.Ordinal);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "agent", "LeverDeck.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found above the test output.");
    }
}
