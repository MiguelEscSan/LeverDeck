var builder = WebApplication.CreateSlimBuilder(args);

builder.WebHost.UseUrls("http://0.0.0.0:8765");

var app = builder.Build();

app.MapGet("/health", () => TypedResults.Text("ok"));

app.Run();

public partial class Program;
