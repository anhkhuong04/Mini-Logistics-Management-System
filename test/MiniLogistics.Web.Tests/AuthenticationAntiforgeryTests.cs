using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using MiniLogistics.Infrastructure.Persistence;
using MiniLogistics.Infrastructure.Identity;
using MiniLogistics.Application.Shops.RegisterShop;
using Xunit;

namespace MiniLogistics.Web.Tests;

public sealed class AuthenticationAntiforgeryTests
{
    [Fact]
    public async Task BrowserAuthenticationPostEndpointsRequireAntiforgeryMetadata()
    {
        await using var factory = new AuthenticationWebApplicationFactory();
        using var client = factory.CreateClient();
        _ = await client.GetAsync("/login");

        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText is "/auth/register-shop" or "/auth/login" or "/auth/logout")
            .ToArray();

        Assert.Equal(3, endpoints.Length);
        Assert.All(endpoints, endpoint =>
            Assert.True(endpoint.Metadata.GetMetadata<IAntiforgeryMetadata>()?.RequiresValidation));
    }

    [Fact]
    public async Task LoginPost_RejectsMissingAndInvalidTokens_AndAcceptsValidToken()
    {
        await using var factory = new AuthenticationWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        using var noTokenResponse = await client.PostAsync(
            "/auth/login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Email"] = "nobody@example.test",
                ["Password"] = "not-a-password"
            }));
        Assert.True(noTokenResponse.StatusCode == HttpStatusCode.BadRequest,
            $"Expected CSRF rejection, got {(int)noTokenResponse.StatusCode}: {await noTokenResponse.Content.ReadAsStringAsync()}");

        var token = await GetLoginAntiforgeryTokenAsync(client);
        using var invalidTokenResponse = await client.PostAsync(
            "/auth/login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Email"] = "nobody@example.test",
                ["Password"] = "not-a-password",
                ["__RequestVerificationToken"] = "invalid-token"
            }));
        Assert.Equal(HttpStatusCode.BadRequest, invalidTokenResponse.StatusCode);

        token = await GetLoginAntiforgeryTokenAsync(client);
        using var validTokenResponse = await client.PostAsync(
            "/auth/login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Email"] = "nobody@example.test",
                ["Password"] = "not-a-password",
                ["__RequestVerificationToken"] = token
            }));

        Assert.Equal(HttpStatusCode.Redirect, validTokenResponse.StatusCode);
        Assert.StartsWith("/login?error=", validTokenResponse.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task RegisterShopPost_WithoutTokenDoesNotCreateAnIdentityUser()
    {
        await using var factory = new AuthenticationWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var email = $"csrf-{Guid.NewGuid():N}@example.test";

        using var response = await client.PostAsync(
            "/auth/register-shop",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["FullName"] = "CSRF Test User",
                ["Email"] = email,
                ["Password"] = "P@ssword12345!",
                ["ShopName"] = "CSRF Test Shop",
                ["PhoneNumber"] = "09876543210",
                ["AddressLine"] = "1 Test Street",
                ["Ward"] = "Test Ward",
                ["Province"] = "Ho Chi Minh City",
                ["Country"] = "Vietnam"
            }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>();
        Assert.False(await dbContext.Users.AnyAsync(user => user.Email == email));
    }

    [Fact]
    public async Task RegisterShopPost_WithRenderedAntiforgeryTokenCompletesRegistration()
    {
        await using var factory = new AuthenticationWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        const string email = "valid-antiforgery-registration@example.test";
        var token = await GetLoginAntiforgeryTokenAsync(client, "/register-shop");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            _ = await roleManager.CreateAsync(new IdentityRole<Guid>(RegisterShopService.ShopRole));
        }

        using var response = await client.PostAsync(
            "/auth/register-shop",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["FullName"] = "Valid Antiforgery Registration",
                ["Email"] = email,
                ["Password"] = "P@ssword12345!",
                ["ShopName"] = "Antiforgery Test Shop",
                ["PhoneNumber"] = "09876543210",
                ["AddressLine"] = "1 Test Street",
                ["Ward"] = "Test Ward",
                ["Province"] = "Ho Chi Minh City",
                ["Country"] = "Vietnam",
                ["__RequestVerificationToken"] = token
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/dashboard", response.Headers.Location?.OriginalString);
        await using var verificationScope = factory.Services.CreateAsyncScope();
        var dbContext = verificationScope.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>();
        Assert.True(await dbContext.Users.AnyAsync(user => user.Email == email));
    }

    [Fact]
    public async Task LogoutPost_RequiresTokenAndAcceptsTokenForAuthenticatedUser()
    {
        await using var factory = new AuthenticationWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        const string email = "logout-antiforgery@example.test";
        const string password = "P@ssword12345!";
        var token = await GetLoginAntiforgeryTokenAsync(client);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            _ = await roleManager.CreateAsync(new IdentityRole<Guid>(RegisterShopService.ShopRole));

            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = "Antiforgery Logout Test",
                CreatedAtUtc = DateTimeOffset.UtcNow
            };
            var createResult = await userManager.CreateAsync(user, password);
            Assert.True(createResult.Succeeded, string.Join("; ", createResult.Errors.Select(error => error.Code)));
            var roleResult = await userManager.AddToRoleAsync(user, RegisterShopService.ShopRole);
            Assert.True(roleResult.Succeeded);
        }

        using var loginResponse = await client.PostAsync(
            "/auth/login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Email"] = email,
                ["Password"] = password,
                ["__RequestVerificationToken"] = token
            }));
        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);

        using var noTokenResponse = await client.PostAsync("/auth/logout", new FormUrlEncodedContent([]));
        Assert.Equal(HttpStatusCode.BadRequest, noTokenResponse.StatusCode);

        var logoutToken = await GetLoginAntiforgeryTokenAsync(client, "/");
        using var validTokenResponse = await client.PostAsync(
            "/auth/logout",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = logoutToken
            }));
        Assert.Equal(HttpStatusCode.Redirect, validTokenResponse.StatusCode);
        Assert.Equal("/login", validTokenResponse.Headers.Location?.OriginalString);
    }

    private static async Task<string> GetLoginAntiforgeryTokenAsync(HttpClient client, string path = "/login")
    {
        using var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        var match = Regex.Match(
            html,
            "<input[^>]*name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(match.Success, "The login form did not render an antiforgery token.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private sealed class AuthenticationWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = "AuthenticationAntiforgeryTests-" + Guid.NewGuid();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<MiniLogisticsDbContext>>();
                services.RemoveAll<IHostedService>();
                var inMemoryProvider = new ServiceCollection()
                    .AddEntityFrameworkInMemoryDatabase()
                    .BuildServiceProvider();
                services.AddDbContext<MiniLogisticsDbContext>(options =>
                    options
                        .UseInMemoryDatabase(_databaseName)
                        .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                        .AddInterceptors(new InMemoryRowVersionInterceptor())
                        .UseInternalServiceProvider(inMemoryProvider));

                using var serviceProvider = services.BuildServiceProvider();
                using var scope = serviceProvider.CreateScope();
                scope.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>()
                    .Database.EnsureCreated();
            });
        }
    }
}

internal sealed class InMemoryRowVersionInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        SetAddedRowVersions(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        SetAddedRowVersions(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private static void SetAddedRowVersions(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries()
                     .Where(entry => entry.State == EntityState.Added
                         && entry.Metadata.FindProperty("RowVersion") is not null))
        {
            entry.Property("RowVersion").CurrentValue = new byte[] { 1 };
        }
    }
}
