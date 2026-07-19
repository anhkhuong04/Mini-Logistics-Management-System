using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MiniLogistics.Application.PartnerApi;
using MiniLogistics.Domain.Operations;
using MiniLogistics.Domain.PartnerApi;
using MiniLogistics.Domain.Shops;
using MiniLogistics.Domain.ValueObjects;
using MiniLogistics.Infrastructure.Identity;

namespace MiniLogistics.Infrastructure.Persistence;

public sealed class DatabaseSeeder
{
    private static readonly string[] Roles = ["Admin", "Operator", "Shop", "Shipper", "IntegrationAdmin"];

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

    private static readonly HubSeedDefinition[] HubSeeds =
    [
        new("SPX-HY-SORT", "SPX Hung Yen Sorting Hub", "Hung Yen", true),
        new("SPX-BD-SORT", "SPX Binh Duong Sorting Hub", "Binh Duong", true),
        new("SPX-HAN-HUB", "SPX Ha Noi Province Hub", "Ha Noi", false),
        new("SPX-CBG-HUB", "SPX Cao Bang Province Hub", "Cao Bang", false),
        new("SPX-TQG-HUB", "SPX Tuyen Quang Province Hub", "Tuyen Quang", false),
        new("SPX-DBN-HUB", "SPX Dien Bien Province Hub", "Dien Bien", false),
        new("SPX-LCU-HUB", "SPX Lai Chau Province Hub", "Lai Chau", false),
        new("SPX-SLA-HUB", "SPX Son La Province Hub", "Son La", false),
        new("SPX-LCI-HUB", "SPX Lao Cai Province Hub", "Lao Cai", false),
        new("SPX-TNN-HUB", "SPX Thai Nguyen Province Hub", "Thai Nguyen", false),
        new("SPX-LSN-HUB", "SPX Lang Son Province Hub", "Lang Son", false),
        new("SPX-QNH-HUB", "SPX Quang Ninh Province Hub", "Quang Ninh", false),
        new("SPX-BNH-HUB", "SPX Bac Ninh Province Hub", "Bac Ninh", false),
        new("SPX-PTO-HUB", "SPX Phu Tho Province Hub", "Phu Tho", false),
        new("SPX-HPG-HUB", "SPX Hai Phong Province Hub", "Hai Phong", false),
        new("SPX-HYN-HUB", "SPX Hung Yen Province Hub", "Hung Yen", false),
        new("SPX-NBH-HUB", "SPX Ninh Binh Province Hub", "Ninh Binh", false),
        new("SPX-THA-HUB", "SPX Thanh Hoa Province Hub", "Thanh Hoa", false),
        new("SPX-NAN-HUB", "SPX Nghe An Province Hub", "Nghe An", false),
        new("SPX-HTH-HUB", "SPX Ha Tinh Province Hub", "Ha Tinh", false),
        new("SPX-QTI-HUB", "SPX Quang Tri Province Hub", "Quang Tri", false),
        new("SPX-HUE-HUB", "SPX Hue Province Hub", "Hue", false),
        new("SPX-DNG-HUB", "SPX Da Nang Province Hub", "Da Nang", false),
        new("SPX-QNI-HUB", "SPX Quang Ngai Province Hub", "Quang Ngai", false),
        new("SPX-GLI-HUB", "SPX Gia Lai Province Hub", "Gia Lai", false),
        new("SPX-KHA-HUB", "SPX Khanh Hoa Province Hub", "Khanh Hoa", false),
        new("SPX-DLK-HUB", "SPX Dak Lak Province Hub", "Dak Lak", false),
        new("SPX-LDG-HUB", "SPX Lam Dong Province Hub", "Lam Dong", false),
        new("SPX-DNI-HUB", "SPX Dong Nai Province Hub", "Dong Nai", false),
        new("SPX-HCM-HUB", "SPX Ho Chi Minh Province Hub", "Ho Chi Minh", false),
        new("SPX-TNH-HUB", "SPX Tay Ninh Province Hub", "Tay Ninh", false),
        new("SPX-DTP-HUB", "SPX Dong Thap Province Hub", "Dong Thap", false),
        new("SPX-VLG-HUB", "SPX Vinh Long Province Hub", "Vinh Long", false),
        new("SPX-AGG-HUB", "SPX An Giang Province Hub", "An Giang", false),
        new("SPX-CTO-HUB", "SPX Can Tho Province Hub", "Can Tho", false),
        new("SPX-CMU-HUB", "SPX Ca Mau Province Hub", "Ca Mau", false)
    ];

    private readonly MiniLogisticsDbContext _dbContext;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SeedingOptions _options;
    private readonly TimeProvider _timeProvider;

    public DatabaseSeeder(
        MiniLogisticsDbContext dbContext,
        RoleManager<IdentityRole<Guid>> roleManager,
        UserManager<ApplicationUser> userManager,
        IOptions<SeedingOptions> options,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _roleManager = roleManager;
        _userManager = userManager;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.HasRequiredDemoCredentials())
        {
            return;
        }

        await SeedRolesAsync();
        await SeedDemoInternalUsersAsync();
        await SeedHubsAsync(cancellationToken);
        await SeedDemoShipperWorkingAreasAsync(cancellationToken);
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
            _options.DemoAdminPassword,
            "0900000002",
            "Demo Admin",
            "Admin");

        await SeedDemoUserAsync(
            DemoOperatorUserId,
            "operator@minilogistics.local",
            _options.DemoOperatorPassword,
            "0900000003",
            "Demo Operator",
            "Operator");

        await SeedDemoUserAsync(
            DemoShipperUserId,
            "shipper@minilogistics.local",
            _options.DemoShipperPassword,
            "0900000004",
            "Demo Shipper",
            "Shipper");
    }

    private async Task SeedDemoShopAsync(CancellationToken cancellationToken)
    {
        const string demoEmail = "shop@minilogistics.local";

        var user = await SeedDemoUserAsync(
            DemoShopUserId,
            demoEmail,
            _options.DemoShopPassword,
            DemoShopPhone,
            "Demo Shop Owner",
            "Shop");

        var existingShop = await _dbContext.Shops
            .FirstOrDefaultAsync(shop => shop.OwnerUserId == user.Id, cancellationToken);

        if (existingShop is not null)
        {
            var now = _timeProvider.GetUtcNow();
            if (existingShop.Name != DemoShopName)
            {
                existingShop.Rename(DemoShopName, now);
            }

            if (existingShop.PhoneNumber.Value != DemoShopPhone
                || existingShop.Address.Street != DemoShopAddressLine
                || existingShop.Address.Ward != DemoShopWard
                || existingShop.Address.Province != DemoShopProvince)
            {
                existingShop.UpdateContact(
                    new PhoneNumber(DemoShopPhone),
                    CreateDemoShopAddress(),
                    now);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await SeedDemoApiClientAsync(existingShop, cancellationToken);
            return;
        }

        var shop = new Shop(
            user.Id,
            DemoShopName,
            new PhoneNumber(DemoShopPhone),
            CreateDemoShopAddress(),
            _timeProvider.GetUtcNow());

        await _dbContext.Shops.AddAsync(shop, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await SeedDemoApiClientAsync(shop, cancellationToken);
    }

    private async Task SeedDemoApiClientAsync(Shop shop, CancellationToken cancellationToken)
    {
        var demoPartnerApiKey = _options.DemoPartnerApiKey.Trim();
        var apiKeyHash = ApiKeyHasher.Hash(demoPartnerApiKey);
        var existingClient = await _dbContext.ApiClients
            .FirstOrDefaultAsync(apiClient => apiClient.ApiKeyHash == apiKeyHash, cancellationToken);

        if (existingClient is not null)
        {
            var now = _timeProvider.GetUtcNow();
            if (!existingClient.IsActive)
            {
                existingClient.Activate(now);
            }

            if (existingClient.Name != DemoApiClientName)
            {
                existingClient.Rename(DemoApiClientName, now);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        var apiClient = new ApiClient(
            shop.Id,
            DemoApiClientName,
            ApiKeyHasher.GetPrefix(demoPartnerApiKey),
            apiKeyHash,
            _timeProvider.GetUtcNow());

        await _dbContext.ApiClients.AddAsync(apiClient, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedHubsAsync(CancellationToken cancellationToken)
    {
        foreach (var seed in HubSeeds)
        {
            var existingHub = await _dbContext.Hubs
                .FirstOrDefaultAsync(hub => hub.Code == seed.Code, cancellationToken);

            if (existingHub is null)
            {
                var now = _timeProvider.GetUtcNow();
                await _dbContext.Hubs.AddAsync(
                    new Hub(
                        seed.Code,
                        seed.Name,
                        seed.Province,
                        now,
                        isRegionalSortingHub: seed.IsRegionalSortingHub),
                    cancellationToken);
                continue;
            }

            var updatedAtUtc = _timeProvider.GetUtcNow();
            if (!existingHub.IsActive)
            {
                existingHub.Activate(updatedAtUtc);
            }

            if (existingHub.Name != seed.Name)
            {
                existingHub.Rename(seed.Name, updatedAtUtc);
            }

            if (existingHub.Province != seed.Province)
            {
                existingHub.UpdateLocation(seed.Province, updatedAtUtc);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedDemoShipperWorkingAreasAsync(CancellationToken cancellationToken)
    {
        var hoChiMinhHub = await _dbContext.Hubs
            .FirstOrDefaultAsync(hub => hub.Code == "SPX-HCM-HUB", cancellationToken);
        if (hoChiMinhHub is null)
        {
            return;
        }

        var hasDemoWorkingArea = await _dbContext.ShipperWorkingAreas
            .AnyAsync(area =>
                area.ShipperId == DemoShipperUserId
                && area.HubId == hoChiMinhHub.Id
                && area.IsActive,
                cancellationToken);
        if (hasDemoWorkingArea)
        {
            return;
        }

        await _dbContext.ShipperWorkingAreas.AddAsync(
            new ShipperWorkingArea(
                DemoShipperUserId,
                hoChiMinhHub.Id,
                hoChiMinhHub.Province,
                _timeProvider.GetUtcNow()),
            cancellationToken);
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
                CreatedAtUtc = _timeProvider.GetUtcNow(),
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

    private sealed record HubSeedDefinition(
        string Code,
        string Name,
        string Province,
        bool IsRegionalSortingHub);
}
