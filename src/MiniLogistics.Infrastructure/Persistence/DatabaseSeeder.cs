using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MiniLogistics.Domain.Shops;
using MiniLogistics.Domain.ValueObjects;
using MiniLogistics.Infrastructure.Identity;

namespace MiniLogistics.Infrastructure.Persistence;

public sealed class DatabaseSeeder
{
    private static readonly string[] Roles = ["Admin", "Operator", "Shop", "Shipper"];

    private static readonly Guid DemoShopUserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

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

    private async Task SeedDemoShopAsync(CancellationToken cancellationToken)
    {
        const string demoEmail = "shop@minilogistics.local";
        const string demoPassword = "Shop@123456";
        const string demoPhone = "0900000001";

        var user = await _userManager.FindByEmailAsync(demoEmail);
        if (user is null)
        {
            user = new ApplicationUser
            {
                Id = DemoShopUserId,
                UserName = demoEmail,
                Email = demoEmail,
                PhoneNumber = demoPhone,
                FullName = "Demo Shop Owner",
                IsActive = true,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                EmailConfirmed = true,
                PhoneNumberConfirmed = true
            };

            var createResult = await _userManager.CreateAsync(user, demoPassword);
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not seed demo shop user: {FormatErrors(createResult.Errors)}");
            }
        }

        if (!await _userManager.IsInRoleAsync(user, "Shop"))
        {
            var roleResult = await _userManager.AddToRoleAsync(user, "Shop");
            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not assign demo shop role: {FormatErrors(roleResult.Errors)}");
            }
        }

        var shopExists = await _dbContext.Shops
            .AnyAsync(shop => shop.OwnerUserId == user.Id, cancellationToken);

        if (shopExists)
        {
            return;
        }

        var shop = new Shop(
            user.Id,
            "Demo Mini Shop",
            new PhoneNumber(demoPhone),
            new Address(
                "123 Nguyen Trai",
                "Ben Thanh",
                "Ho Chi Minh City"));

        await _dbContext.Shops.AddAsync(shop, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string FormatErrors(IEnumerable<IdentityError> errors)
    {
        return string.Join("; ", errors.Select(error => error.Description));
    }
}
