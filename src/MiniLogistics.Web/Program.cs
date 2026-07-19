using Microsoft.EntityFrameworkCore;
using MiniLogistics.Application;
using MiniLogistics.Infrastructure;
using MiniLogistics.Infrastructure.Persistence;
using MiniLogistics.Web.Components;
using MiniLogistics.Web.Endpoints;
using MiniLogistics.Web.Services;

var builder = WebApplication.CreateBuilder(args);
const string PartnerApiCorsPolicy = "PartnerApiPolicy";

// Add services to the container.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
var partnerApiAllowedOrigins = builder.Configuration
    .GetSection("Cors:PartnerApi:AllowedOrigins")
    .Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy(PartnerApiCorsPolicy, policy =>
    {
        policy
            .WithOrigins(partnerApiAllowedOrigins)
            .WithMethods("GET", "POST")
            .WithHeaders("Authorization", "Content-Type", "Idempotency-Key")
            .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
    });
});
builder.Services.AddExceptionHandler<PartnerApiExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddSingleton<VietnamAdministrativeDivisionService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.Configure<PublicTrackingRateLimitOptions>(
    builder.Configuration.GetSection(PublicTrackingRateLimitOptions.SectionName));
builder.Services.AddSingleton<IPublicTrackingRateLimiter, DistributedCachePublicTrackingRateLimiter>();
builder.Services.Configure<PartnerApiRateLimitOptions>(
    builder.Configuration.GetSection(PartnerApiRateLimitOptions.SectionName));
if (string.Equals(
    builder.Configuration.GetValue<string>($"{PartnerApiRateLimitOptions.SectionName}:Mode"),
    "Distributed",
    StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IPartnerApiRateLimiter, DistributedCachePartnerApiRateLimiter>();
}
else
{
    builder.Services.AddSingleton<IPartnerApiRateLimiter, InMemoryPartnerApiRateLimiter>();
}

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

var shouldMigrate = args.Contains("--migrate", StringComparer.OrdinalIgnoreCase);
var shouldSeed = args.Contains("--seed", StringComparer.OrdinalIgnoreCase);
if (shouldMigrate || shouldSeed)
{
    await RunDatabaseCommandsAsync(app, shouldMigrate, shouldSeed);
    return;
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/api"),
    apiApp => apiApp.UseExceptionHandler());
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseRouting();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapAuthenticationEndpoints();
app.MapPartnerApiEndpoints(PartnerApiCorsPolicy);
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

static async Task RunDatabaseCommandsAsync(
    WebApplication app,
    bool shouldMigrate,
    bool shouldSeed)
{
    await using var scope = app.Services.CreateAsyncScope();

    if (shouldMigrate)
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    if (shouldSeed)
    {
        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedAsync();
    }
}

public partial class Program;
