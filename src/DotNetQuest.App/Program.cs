using DotNetQuest.App.Components;
using DotNetQuest.App.Data;
using DotNetQuest.App.Endpoints;
using DotNetQuest.App.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---------- Data protection (required for auth cookies on IIS) ----------
// Persist keys next to the app so cookies survive app-pool recycles. Without
// this, IIS app pools without "Load User Profile" can't store keys and every
// cookie becomes undecryptable — login appears to silently fail.
var dataProtectionKeysPath = Path.Combine(builder.Environment.ContentRootPath, "keys");
Directory.CreateDirectory(dataProtectionKeysPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
    .SetApplicationName("DevQuest");

// ---------- Data ----------
builder.Services.AddDbContext<GameDb>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=learnai.db"));

// ---------- Auth: Google OAuth + cookie sessions ----------
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
        options.DefaultSignOutScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.Cookie.Name = "devquest.auth";
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.HttpOnly = true;
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
    })
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"] ?? string.Empty;
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? string.Empty;
        options.CallbackPath = "/signin-google";
        // Surface remote auth failures instead of silently bouncing to "/".
        options.Events = new OAuthEvents
        {
            OnRemoteFailure = context =>
            {
                var message = context.Failure?.Message ?? "authentication failed";
                context.Response.Redirect("/?authError=" + Uri.EscapeDataString(message));
                context.HandleResponse();
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ---------- Game services ----------
builder.Services.Configure<AiTutorOptions>(builder.Configuration.GetSection(AiTutorOptions.SectionName));
builder.Services.AddHttpClient<AiTutorService>();
builder.Services.AddSingleton<TutorRateLimiter>();
builder.Services.AddSingleton<CodeRunnerService>();

// ---------- Blazor ----------
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

var app = builder.Build();

// Apply database migrations automatically (local-first game).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GameDb>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

// Behind a reverse proxy (nginx/Caddy/Cloudflare) TLS terminates there — honor
// X-Forwarded-Proto/Host so OAuth redirect URIs and cookies use the public
// https:// scheme+host instead of http://localhost:8080. Harmless when the
// headers are absent (direct exposure). Proxies on non-loopback addresses are
// trusted too, which covers Docker deployments; don't expose the raw app port
// to the internet if that matters for your setup.
var forwardedHeaders = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost
};
forwardedHeaders.KnownNetworks.Clear();
forwardedHeaders.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeaders);

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

// Direct-IIS or plain-HTTP deployments: don't force https (the reverse proxy
// or IIS itself handles TLS). Enable with RedirectHttps=true if wanted.
if (builder.Configuration.GetValue("RedirectHttps", false))
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapGameEndpoints();
app.MapGamificationEndpoints();
app.MapCertificateEndpoints();
app.MapExportEndpoints();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(DotNetQuest.App.Client._Imports).Assembly);

app.Run();
