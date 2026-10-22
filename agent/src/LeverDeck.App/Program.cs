using LeverDeck.App;
using LeverDeck.Infrastructure.Configuration;

var builder = WebApplication.CreateSlimBuilder(args);

builder.WebHost.UseUrls($"http://0.0.0.0:{LeverDeckOptions.Port}");
builder.Services.AddLeverDeck();

var app = builder.Build();

app.MapLeverDeckEndpoints();

app.Run();

public partial class Program;
