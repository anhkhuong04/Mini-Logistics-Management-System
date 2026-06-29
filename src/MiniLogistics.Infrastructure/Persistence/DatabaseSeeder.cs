using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MiniLogistics.Application.PartnerApi;
using MiniLogistics.Domain.PartnerApi;
using MiniLogistics.Domain.Shops;
using MiniLogistics.Domain.ValueObjects;
using MiniLogistics.Infrastructure.Identity;

namespace MiniLogistics.Infrastructure.Persistence;

public sealed class DatabaseSeeder
{
    private static readonly string[] Roles = ["Admin", "Operator", "Shop", "Shipper"];

    private static readonly Guid DemoAdminUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid DemoOperatorUserId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid DemoShopUserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid DemoShipperUserId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private const string DemoShopName = "Demo Mini Shop";
    private const string DemoShopPhone = "0900000001";
    private const string DemoShopAddressLine = "123 Nguyen Trai";
    private const string DemoShopWard = "Phuong Ben Thanh";
    private const string DemoShopProvince = "Ho Chi Minh";
    private const string DemoApiClientName = "Demo E-commerce Integration";
    public const string DemoPartnerApiKey = "ml_demo_partner_key_123456";

    private readonly MiniLogisticsDbContext _dbContext;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;

    public DatabaseSeeder(
        MiniLogisticsDbContext dbContext,
        RoleManager<IdentityRole<Guid>> roleManager,
        UserManager<ApplicationUser> userManager)
    {
        _dbContext = dbContext;
        _roleManager = roleManager;
        _userManager = userManager;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedRolesAsync();
        await SeedDemoInternalUsersAsync();
        await SeedDemoShopAsync(cancellationToken);
    }

    private async Task SeedRolesAsync()
    {
        foreach (var role in Roles)
        {
            if (await _roleManager.RoleExistsAsync(role))
            {
                continue;
            }

            await _roleManager.CreateAsync(new IdentityRole<Guid>(role));
        }
    }

    private async Task SeedDemoInternalUsersAsync()
    {
        await SeedDemoUserAsync(
            DemoAdminUserId,
            "admin@minilogistics.local",
            "Admin@123456",
            "0900000002",
            "Demo Admin",
            "Admin");

        await SeedDemoUserAsync(
            DemoOperatorUserId,
            "operator@minilogistics.local",
            "Operator@123456",
            "0900000003",
            "Demo Operator",
            "Operator");

        await SeedDemoUserAsync(
            DemoShipperUserId,
            "shipper@minilogistics.local",
            "Shipper@123456",
            "0900000004",
            "Demo Shipper",
            "Shipper");
    }

    private async Task SeedDemoShopAsync(CancellationToken cancellationToken)
    {
        const string demoEmail = "shop@minilogistics.local";
        const string demoPassword = "Shop@123456";

        var user = await SeedDemoUserAsync(
            DemoShopUserId,
            demoEmail,
            demoPassword,
            DemoShopPhone,
            "Demo Shop Owner",
            "Shop");

        var existingShop = await _dbContext.Shops
            .FirstOrDefaultAsync(shop => shop.OwnerUserId == user.Id, cancellationToken);

        if (existingShop is not null)
        {
            if (existingShop.Name != DemoShopName)
            {
                existingShop.Rename(DemoShopName);
            }

            if (existingShop.PhoneNumber.Value != DemoShopPhone
                || existingShop.Address.Street != DemoShopAddressLine
                || existingShop.Address.Ward != DemoShopWard
                || existingShop.Address.Province != DemoShopProvince)
            {
                existingShop.UpdateContact(
                    new PhoneNumber(DemoShopPhone),
                    CreateDemoShopAddress());
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await SeedDemoApiClientAsync(existingShop, cancellationToken);
            return;
        }

        var shop = new Shop(
            user.Id,
            DemoShopName,
            new PhoneNumber(DemoShopPhone),
            CreateDemoShopAddress());

        await _dbContext.Shops.AddAsync(shop, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await SeedDemoApiClientAsync(shop, cancellationToken);
    }

    private async Task SeedDemoApiClientAsync(Shop shop, CancellationToken cancellationToken)
    {
        var apiKeyHash = ApiKeyHasher.Hash(DemoPartnerApiKey);
        var existingClient = await _dbContext.ApiClients
            .FirstOrDefaultAsync(apiClient => apiClient.ApiKeyHash == apiKeyHash, cancellationToken);

        if (existingClient is not null)
        {
            if (!existingClient.IsActive)
            {
                existingClient.Activate();
            }

            if (existingClient.Name != DemoApiClientName)
            {
                existingClient.Rename(DemoApiClientName);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        var apiClient = new ApiClient(
            shop.Id,
            DemoApiClientName,
            ApiKeyHasher.GetPrefix(DemoPartnerApiKey),
            apiKeyHash);

        await _dbContext.ApiClients.AddAsync(apiClient, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static Address CreateDemoShopAddress()
    {
        return new Address(
            DemoShopAddressLine,
            DemoShopWard,
            DemoShopProvince);
    }

    private async Task<ApplicationUser> SeedDemoUserAsync(
        Guid userId,
        string email,
        string password,
        string phoneNumber,
        string fullName,
        string role)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                Id = userId,
                UserName = email,
                Email = email,
                PhoneNumber = phoneNumber,
                FullName = fullName,
                IsActive = true,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                EmailConfirmed = true,
                PhoneNumberConfirmed = true
            };

            var createResult = await _userManager.CreateAsync(user, password);
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not seed demo {role.ToLowerInvariant()} user: {FormatErrors(createResult.Errors)}");
            }
        }
        else
        {
            var shouldUpdate = false;

            if (!user.IsActive)
            {
                user.IsActive = true;
                shouldUpdate = true;
            }

            if (!user.EmailConfirmed)
            {
                user.EmailConfirmed = true;
                shouldUpdate = true;
            }

            if (!user.PhoneNumberConfirmed)
            {
                user.PhoneNumberConfirmed = true;
                shouldUpdate = true;
            }

            if (string.IsNullOrWhiteSpace(user.FullName))
            {
                user.FullName = fullName;
                shouldUpdate = true;
            }

            if (shouldUpdate)
            {
                var updateResult = await _userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Could not update demo {role.ToLowerInvariant()} user: {FormatErrors(updateResult.Errors)}");
                }
            }
        }

        if (!await _userManager.IsInRoleAsync(user, role))
        {
            var roleResult = await _userManager.AddToRoleAsync(user, role);
            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not assign demo {role.ToLowerInvariant()} role: {FormatErrors(roleResult.Errors)}");
            }
        }

        return user;
    }

    private static string FormatErrors(IEnumerable<IdentityError> errors)
    {
        return string.Join("; ", errors.Select(error => error.Description));
    }
}
