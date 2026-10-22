using LeverDeck.Infrastructure.Games;

namespace LeverDeck.Infrastructure.Tests.Games;

public sealed class VdfReaderTests
{
    [Fact]
    public void Parse_ReadsNestedSectionsAndEscapes()
    {
        const string text = """
            "libraryfolders"
            {
                // A comment
                "0"
                {
                    "path"      "C:\\Program Files (x86)\\Steam"
                    "label"     "say \"hi\""
                }
                "1" { "path" "D:\\Games" }
            }
            """;

        var root = VdfReader.Parse(text);

        var folders = root?.Section("LIBRARYFOLDERS");
        Assert.NotNull(folders);
        Assert.Equal([@"C:\Program Files (x86)\Steam", @"D:\Games"], folders.Sections.Select(section => section.Value("path")));
        Assert.Equal("say \"hi\"", folders.Section("0")?.Value("label"));
    }

    [Fact]
    public void Parse_ReadsBareTokens()
    {
        var root = VdfReader.Parse("AppState { appid 570 name \"Dota 2\" }");

        Assert.Equal("570", root?.Section("AppState")?.Value("appid"));
    }

    [Theory]
    [InlineData("\"a\" { \"b\" \"c\"")]
    [InlineData("\"a\" \"unterminated")]
    [InlineData("}")]
    [InlineData("\"key\"")]
    public void Parse_MalformedText_GivesNull(string text)
    {
        Assert.Null(VdfReader.Parse(text));
    }
}
