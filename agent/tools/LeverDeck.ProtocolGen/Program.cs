using System.Text;
using LeverDeck.ProtocolGen;

// Usage (from agent/): dotnet run --project tools/LeverDeck.ProtocolGen -- ../web/src/app/core/protocol
if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: LeverDeck.ProtocolGen <output-directory>");
    return 1;
}

var outputDirectory = Path.GetFullPath(args[0]);
Directory.CreateDirectory(outputDirectory);
var files = TypeScriptGenerator.Generate();
var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

// Remove generated files that no longer exist, never hand-written ones.
foreach (var stale in Directory.EnumerateFiles(outputDirectory, "*.ts"))
{
    if (!files.ContainsKey(Path.GetFileName(stale))
        && File.ReadLines(stale).FirstOrDefault() == TypeScriptGenerator.Header)
    {
        File.Delete(stale);
        Console.WriteLine($"Deleted {stale}");
    }
}

foreach (var (fileName, content) in files)
{
    var path = Path.Combine(outputDirectory, fileName);
    File.WriteAllText(path, content, utf8);
    Console.WriteLine($"Wrote {path}");
}

return 0;
