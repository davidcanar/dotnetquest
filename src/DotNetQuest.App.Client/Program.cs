using DotNetQuest.App.Client.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});

builder.Services.AddScoped<ApiClient>();
builder.Services.AddScoped<I18nService>();
builder.Services.AddScoped<ContentLoader>();
builder.Services.AddScoped<GameState>();
builder.Services.AddScoped<BitVoice>();
builder.Services.AddScoped<Confetti>();

await builder.Build().RunAsync();
