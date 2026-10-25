using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using MiniLogistics.Infrastructure.Identity;
using MiniLogistics.Infrastructure.Persistence;
using Xunit;

namespace MiniLogistics.Web.Tests.Browser;

public sealed class BrowserAuthenticationTests
{
    [Fact]
    public async Task ExistingCookie_RefreshesRolesAndRejectsDeactivatedUser()
    {
        await using var factory = new BrowserIdentityFactory();
        using var client = factory.Client();
        var id = await factory.AddUser("changing@example.test", "Operator");
        await BrowserIdentityFactory.Csrf(client);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/app/auth/login", new {
            email = "changing@example.test", password = BrowserIdentityFactory.Password })).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await users.FindByIdAsync(id.ToString()))!;
            Assert.True((await users.RemoveFromRoleAsync(user, "Operator")).Succeeded);
        }
        var session = await client.GetFromJsonAsync<JsonElement>("/api/v1/app/auth/me");
        Assert.Empty(session.GetProperty("roles").EnumerateArray());
        Assert.Empty(session.GetProperty("operationPermissions").EnumerateArray());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/app/shop-context")).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await users.FindByIdAsync(id.ToString()))!; user.IsActive = false;
            Assert.True((await users.UpdateAsync(user)).Succeeded);
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/app/auth/me")).StatusCode);
    }
    [Fact]
    public async Task AnonymousAndCsrfFailures_AreSafeJsonWithoutRedirects()
    {
        await using var factory = new BrowserIdentityFactory();
        using var client = factory.Client();
        var me = await client.GetAsync("/api/v1/app/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        Assert.Null(me.Headers.Location);
        Assert.Equal("application/problem+json", me.Content.Headers.ContentType?.MediaType);
        foreach (var path in new[] { "login", "register-shop" })
        {
            client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
            var response = await client.PostAsJsonAsync($"/api/v1/app/auth/{path}", new { email = "secret@example.test", password = "secret" });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("Browser.CsrfValidationFailed", await response.Content.ReadAsStringAsync());
            Assert.DoesNotContain("secret", await response.Content.ReadAsStringAsync());
            client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", "invalid-token");
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/v1/app/auth/{path}", new { })).StatusCode);
        }
        await BrowserIdentityFactory.Csrf(client);
        var invalid = await client.PostAsJsonAsync("/api/v1/app/auth/login", new { email = "missing@example.test", password = "wrong" });
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
    }

    [Fact]
    public async Task RealIdentityCookie_LoginMeLockoutInactiveLogoutAndTokenIdentityRotation()
    {
        await using var factory = new BrowserIdentityFactory();
        using var client = factory.Client();
        await factory.AddUser("owner@example.test", "Shop");
        await factory.AddUser("blocked@example.test", "Shop", active: false);
        await BrowserIdentityFactory.Csrf(client);
        var blocked = await client.PostAsJsonAsync("/api/v1/app/auth/login", new { email = "blocked@example.test", password = BrowserIdentityFactory.Password });
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        var login = await client.PostAsJsonAsync("/api/v1/app/auth/login", new { email = "owner@example.test", password = BrowserIdentityFactory.Password, rememberMe = true });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Contains(login.Headers.GetValues("Set-Cookie"), value => value.Contains(".AspNetCore.Identity.Application") && value.Contains("expires=", StringComparison.OrdinalIgnoreCase));
        Assert.True(login.Headers.CacheControl?.NoStore);
        var session = await client.GetFromJsonAsync<JsonElement>("/api/v1/app/auth/me");
        Assert.Equal("Shop", session.GetProperty("roles")[0].GetString());
        var staleCsrf = await client.PostAsJsonAsync("/api/v1/app/auth/logout", new { });
        Assert.Equal(HttpStatusCode.BadRequest, staleCsrf.StatusCode);
        await BrowserIdentityFactory.Csrf(client);
        var logout = await client.PostAsJsonAsync("/api/v1/app/auth/logout", new { });
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/app/auth/me")).StatusCode);
        await BrowserIdentityFactory.Csrf(client);
        HttpResponseMessage? failure = null;
        for (var attempt = 0; attempt < 5; attempt++)
            failure = await client.PostAsJsonAsync("/api/v1/app/auth/login", new { email = "owner@example.test", password = "WrongPassword!1" });
        Assert.Equal((HttpStatusCode)423, failure!.StatusCode);
        Assert.Contains("Auth.LockedOut", await failure.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ReferenceDataRegistrationValidationAndAutoSignIn_UseRealServices()
    {
        await using var factory = new BrowserIdentityFactory();
        using var client = factory.Client();
        var reference = await client.GetFromJsonAsync<JsonElement>("/api/v1/public/reference-data/provinces");
        await factory.EnsureRole("Shop");
        var province = reference.GetProperty("provinces")[0];
        Assert.Equal(2, province.EnumerateObject().Count());
        await BrowserIdentityFactory.Csrf(client);
        var invalid = await client.PostAsJsonAsync("/api/v1/app/auth/register-shop", new { });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Contains("fullName", await invalid.Content.ReadAsStringAsync());
        var response = await client.PostAsJsonAsync("/api/v1/app/auth/register-shop", new {
            fullName = "Browser Test", email = "new@example.test", password = BrowserIdentityFactory.Password,
            shopName = "Test Shop", phoneNumber = "0901234567", addressLine = "Test address",
            province = province.GetProperty("name").GetString(), ward = province.GetProperty("wards")[0].GetProperty("name").GetString(), country = "Vietnam" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/app/auth/me")).StatusCode);
        await BrowserIdentityFactory.Csrf(client);
        var malformed = await client.PostAsync("/api/v1/app/auth/login", new StringContent("{password-secret", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        Assert.DoesNotContain("password-secret", await malformed.Content.ReadAsStringAsync());
    }
}

public sealed class BrowserIdentityFactory(bool enableFrontend = false) : WebApplicationFactory<Program>
{
    public const string Password = "BrowserTest!123";
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        if (enableFrontend)
        {
            builder.UseSetting("BrowserSpa:Enabled", "true");
            builder.UseSetting("BrowserSpa:DistPath", Path.Combine(RepositoryRoot(), "src", "MiniLogistics.Frontend", "dist"));
        }
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=(localdb)\\MSSQLLocalDB;Database=UnusedBrowserTests;Trusted_Connection=True");
        var database = "BrowserIdentity-" + Guid.NewGuid();
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<MiniLogisticsDbContext>>();
            services.RemoveAll<IHostedService>();
            var provider = new ServiceCollection().AddEntityFrameworkInMemoryDatabase().BuildServiceProvider();
            services.AddDbContext<MiniLogisticsDbContext>(options => options.UseInMemoryDatabase(database)
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .AddInterceptors(new InMemoryRowVersionInterceptor()).UseInternalServiceProvider(provider));
        });
    }
    public HttpClient Client() => CreateClient(new WebApplicationFactoryClientOptions {
        BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
    public static async Task Csrf(HttpClient client)
    {
        var token = await client.GetFromJsonAsync<JsonElement>("/api/v1/app/auth/csrf");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("requestToken").GetString());
    }
    public async Task<Guid> AddUser(string email, string role, bool active = true)
    {
        using var scope = Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        if (!await roles.RoleExistsAsync(role)) Assert.True((await roles.CreateAsync(new IdentityRole<Guid>(role))).Succeeded);
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = email, Email = email, FullName = "Test User", IsActive = active };
        Assert.True((await users.CreateAsync(user, Password)).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
        return user.Id;
    }
    public async Task EnsureRole(string role)
    {
        using var scope = Services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        if (!await roles.RoleExistsAsync(role)) Assert.True((await roles.CreateAsync(new IdentityRole<Guid>(role))).Succeeded);
    }
    public static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Mini-logistics-manegemant-system.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
