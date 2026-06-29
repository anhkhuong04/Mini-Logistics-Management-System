using Microsoft.EntityFrameworkCore;
using MiniLogistics.Application;
using MiniLogistics.Infrastructure;
using MiniLogistics.Infrastructure.Persistence;
using MiniLogistics.Web.Components;
using MiniLogistics.Web.Endpoints;
using MiniLogistics.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddSingleton<VietnamAdministrativeDivisionService>();
builder.Services.AddSingleton<IPartnerApiRateLimiter, InMemoryPartnerApiRateLimiter>();

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
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapAuthenticationEndpoints();
app.MapPartnerApiEndpoints();
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
