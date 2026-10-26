using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using MiniLogistics.Domain.Shops;
using MiniLogistics.Domain.ValueObjects;
using MiniLogistics.Domain.Fees;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Web.Services;
using MiniLogistics.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MiniLogistics.Application.Shipments.AssignmentSelection;
using MiniLogistics.Domain.CashOnDelivery;
using MiniLogistics.Domain.Operations;
using Xunit;

namespace MiniLogistics.Web.Tests.Browser;

public sealed class BrowserE2EFactAttribute : FactAttribute
{
    public BrowserE2EFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("MINILOGISTICS_BROWSER_E2E") != "1")
            Skip = "Opt-in: build frontend, install Chromium, prepare dev cert, then set MINILOGISTICS_BROWSER_E2E=1.";
    }
}

public sealed class BrowserRealBrowserTests
{
    [BrowserE2EFact]
    [Trait("Category", "BrowserE2E")]
    public async Task RealBrowser_UsesIdentityCookiesAndIsolatedApplicationData()
    {
        var root = BrowserIdentityFactory.RepositoryRoot();
        var certDirectory = Path.Combine(root, ".agent", "https");
        using var pemCertificate = X509Certificate2.CreateFromPemFile(Path.Combine(certDirectory, "localhost.pem"), Path.Combine(certDirectory, "localhost.key"));
        // Windows Schannel needs a persisted key handle; do not write the PFX or its key to disk.
        using var certificate = X509CertificateLoader.LoadPkcs12(pemCertificate.Export(X509ContentType.Pfx), null);
        await using var factory = new BrowserIdentityFactory(enableFrontend: true,
            configureServices: services => {
                services.AddHostedService<ShipmentImportWorker>();
                services.RemoveAll<IShipperAssignmentCapacityGuard>();
                services.AddScoped<IShipperAssignmentCapacityGuard, BrowserOperationsCapacityGuard>();
                services.RemoveAll<MiniLogistics.Application.AdminSystemConfiguration.IConfigurationUpdateLock>();
                services.AddScoped<MiniLogistics.Application.AdminSystemConfiguration.IConfigurationUpdateLock, BrowserConfigurationLock>();
                services.RemoveAll<MiniLogistics.Infrastructure.PartnerApi.IWebhookDnsResolver>();
                services.AddSingleton<MiniLogistics.Infrastructure.PartnerApi.IWebhookDnsResolver, BrowserIntegrationDns>();
            });
        factory.UseKestrel(options => options.Listen(IPAddress.Loopback, 0, listen => listen.UseHttps(certificate)));
        using var client = factory.Client();
        var owner = await factory.AddUser("browser-owner@example.test", "Shop");
        var staff = await factory.AddUser("browser-staff@example.test", "Shop");
        var mutationOwner = await factory.AddUser("browser-mutation-owner@example.test", "Shop");
        await factory.AddUser("browser-other@example.test", "Shop");
        var operationsActor = await factory.AddUser("browser-operator@example.test", "Operator");
        var adminActor = await factory.AddUser("browser-admin@example.test", "Admin");
        await factory.AddUser("browser-admin-target@example.test", "Shipper");
        var adminShopOwner = await factory.AddUser("browser-admin-shop@example.test", "Shop");
        var driverA = await factory.AddUser("browser-driver-a@example.test", "Shipper");
        var driverB = await factory.AddUser("browser-driver-b@example.test", "Shipper");
        var operationsOwner = await factory.AddUser("browser-operations-shop@example.test", "Shop");
        var mobileShipper = await factory.AddUser("browser-shipper@example.test", "Shipper");
        var otherShipper = await factory.AddUser("browser-shipper-other@example.test", "Shipper");
        var crossOwner = await factory.AddUser("browser-cross-shop@example.test", "Shop");
        var crossShipper = await factory.AddUser("browser-cross-shipper@example.test", "Shipper");
        var profileOwner = await factory.AddUser("browser-profile-owner@example.test", "Shop");
        var staffOwner = await factory.AddUser("browser-staff-owner@example.test", "Shop");
        var settingsStaff = await factory.AddUser("browser-settings-staff@example.test", "Shop");
        var reportOwner = await factory.AddUser("browser-report-owner@example.test", "Shop");
        var reportReader = await factory.AddUser("browser-report-reader@example.test", "Shop");
        await factory.EnsureRole("Shop");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>();
            // A nonempty configured map replaces the built-in fallback. Keep a complete map while Admin edits a distinct province.
            db.RouteRegionConfigs.AddRange(MiniLogistics.Application.Routing.DefaultRouteRegionConfigSource.Instance.GetProvinceRegionSnapshot()
                .Select(item=>new RouteRegionConfig(item.Province,item.Region,DateTimeOffset.UtcNow)));
            var province = (await scope.ServiceProvider.GetRequiredService<VietnamAdministrativeDivisionService>().GetProvincesAsync()).First(item => item.Name.Contains("Hà Nội"));
            Shop New(string name, Guid? ownerId = null) => new(ownerId ?? owner, name, new PhoneNumber("0901234567"),
                new Address("Private test address", province.Wards[0].Name, province.Name, "Vietnam"), DateTimeOffset.UtcNow);
            var first = New("Browser Shop A"); var second = New("Browser Shop B");
            db.Shops.Add(New("Phase9 Admin Shop", adminShopOwner));
            db.Shops.AddRange(first, second);
            db.Shops.Add(New("Browser Mutation Shop", mutationOwner));
            db.FeeRules.Add(new FeeRule(RouteType.IntraProvince, 1, new Money(25000), 0.5m, new Money(5000), DateTimeOffset.UtcNow));
            db.Shipments.Add(BrowserShopShipmentReadTests.NewShipment(first.Id, owner, "ML-BROWSER-001"));
            (await db.Users.FindAsync(driverA))!.FullName = "Driver A";
            (await db.Users.FindAsync(driverB))!.FullName = "Driver B";
            (await db.Users.FindAsync(mobileShipper))!.FullName = "Mobile Driver";
            (await db.Users.FindAsync(crossShipper))!.FullName = "Cross Driver";
            db.Shops.Add(New("Browser Cross Shop", crossOwner));
            var profileShop = New("Browser Profile Shop", profileOwner); db.Shops.Add(profileShop);
            db.Shipments.Add(BrowserShopShipmentReadTests.NewShipment(profileShop.Id, profileOwner, "ML-PROFILE-E2E-SNAPSHOT", 0));
            var staffShop = New("Browser Staff Shop", staffOwner); db.Shops.Add(staffShop);
            (await db.Users.FindAsync(settingsStaff))!.FullName = "Settings Staff";
            db.ShopStaffMemberships.Add(new ShopStaffMembership(staffShop.Id, settingsStaff, staffOwner, ShopStaffRole.Viewer, ShopPermission.ViewShipments, DateTimeOffset.UtcNow));
            var reportShop = New("Browser Report Shop", reportOwner); db.Shops.Add(reportShop);
            db.ShopStaffMemberships.Add(new ShopStaffMembership(reportShop.Id, reportReader, reportOwner, ShopStaffRole.Viewer, ShopPermission.ViewShipments, DateTimeOffset.UtcNow));
            var reportShipment = BrowserShopShipmentReadTests.NewShipment(reportShop.Id, reportOwner, "ML-SETTINGS-COD", 100000); db.Shipments.Add(reportShipment);
            db.CodTransactions.Add(CodTransaction.Create(reportShipment.Id, reportShipment.CodAmount, DateTimeOffset.UtcNow));
            db.ShopNotifications.Add(new ShopNotification(Guid.NewGuid(), reportShop.Id, reportOwner, "shipment.delivered", "Settings delivery notice", "Synthetic delivery notification", DateTimeOffset.UtcNow, reportShipment.Id));
            db.AdminAuditLogs.Add(new MiniLogistics.Domain.AdminAuditing.AdminAuditLog(reportOwner, "Shop", "FixtureSettingsAudit", "Shop", reportShop.Id, DateTimeOffset.UtcNow, newValueJson: "{\"password\":\"settings-secret-fixture\"}"));
            var mobileShop = New("Browser Mobile Shop", operationsOwner); db.Shops.Add(mobileShop);
            var mobile = BrowserShopShipmentReadTests.NewShipment(mobileShop.Id, operationsOwner, "ML-SHIPPER-MOBILE-1", 100000);
            var noCod = BrowserShopShipmentReadTests.NewShipment(mobileShop.Id, operationsOwner, "ML-SHIPPER-NOCOD", 0);
            var pendingCod = BrowserShopShipmentReadTests.NewShipment(mobileShop.Id, operationsOwner, "ML-SHIPPER-COD", 25000);
            var otherTask = BrowserShopShipmentReadTests.NewShipment(mobileShop.Id, operationsOwner, "ML-SHIPPER-OTHER", 5000);
            foreach (var task in new[] { mobile, noCod, pendingCod, otherTask })
            {
                var assignedShipper = task == otherTask ? otherShipper : mobileShipper;
                Assert.True(task.AssignShipper(assignedShipper, operationsActor, DateTimeOffset.UtcNow, "seed").IsSuccess);
                if (task == pendingCod) foreach (var status in new[] { ShipmentStatus.PickingUp, ShipmentStatus.PickedUp, ShipmentStatus.InTransit, ShipmentStatus.Delivering, ShipmentStatus.Delivered })
                    Assert.True(task.UpdateStatus(status, mobileShipper, DateTimeOffset.UtcNow, "seed").IsSuccess);
                db.Shipments.Add(task); db.CodTransactions.Add(CodTransaction.Create(task.Id, task.CodAmount, DateTimeOffset.UtcNow));
            }
            // Distinct synthetic area preserves the existing no-eligible-shipper and cross-role manual-assignment fixtures.
            var mobileHub = new Hub("MOBILE-TEST", "Mobile Hub", "Mobile Fixture Province", DateTimeOffset.UtcNow);
            db.Hubs.Add(mobileHub); db.ShipperWorkingAreas.Add(new ShipperWorkingArea(mobileShipper, mobileHub.Id, mobileHub.Province, DateTimeOffset.UtcNow));
            var operationsShop = New("Browser Operations Shop", operationsOwner); db.Shops.Add(operationsShop);
            var opsPending = BrowserShopShipmentReadTests.NewShipment(operationsShop.Id, operationsOwner, "ML-OPS-PENDING");
            db.Shipments.Add(opsPending);
            db.CodTransactions.Add(CodTransaction.Create(opsPending.Id, opsPending.CodAmount, DateTimeOffset.UtcNow));
            var settlement = BrowserShopShipmentReadTests.NewShipment(operationsShop.Id, operationsOwner, "ML-OPS-SETTLE");
            Assert.True(settlement.AssignShipper(driverA, operationsActor, DateTimeOffset.UtcNow, "seed").IsSuccess);
            foreach (var status in new[] { ShipmentStatus.PickingUp, ShipmentStatus.PickedUp, ShipmentStatus.InTransit, ShipmentStatus.Delivering, ShipmentStatus.Delivered })
                Assert.True(settlement.UpdateStatus(status, operationsActor, DateTimeOffset.UtcNow, "seed").IsSuccess);
            var settledCod = CodTransaction.Create(settlement.Id, settlement.CodAmount, DateTimeOffset.UtcNow);
            Assert.True(settledCod.MarkCollected(ShipmentStatus.Delivered, adminActor, DateTimeOffset.UtcNow).IsSuccess);
            Assert.True(settlement.CompleteCodCollection(DateTimeOffset.UtcNow).IsSuccess);
            db.Shipments.Add(settlement); db.CodTransactions.Add(settledCod);
            var adminSettlement = BrowserShopShipmentReadTests.NewShipment(operationsShop.Id, operationsOwner, "ML-ADMIN-SETTLE");
            Assert.True(adminSettlement.AssignShipper(driverA, operationsActor, DateTimeOffset.UtcNow, "seed").IsSuccess);
            foreach (var status in new[] { ShipmentStatus.PickingUp, ShipmentStatus.PickedUp, ShipmentStatus.InTransit, ShipmentStatus.Delivering, ShipmentStatus.Delivered })
                Assert.True(adminSettlement.UpdateStatus(status, operationsActor, DateTimeOffset.UtcNow, "seed").IsSuccess);
            var adminCod = CodTransaction.Create(adminSettlement.Id, adminSettlement.CodAmount, DateTimeOffset.UtcNow);
            Assert.True(adminCod.MarkCollected(ShipmentStatus.Delivered, adminActor, DateTimeOffset.UtcNow).IsSuccess);
            Assert.True(adminSettlement.CompleteCodCollection(DateTimeOffset.UtcNow).IsSuccess);
            db.Shipments.Add(adminSettlement); db.CodTransactions.Add(adminCod);
            db.ShopStaffMemberships.Add(new ShopStaffMembership(first.Id, staff, owner, ShopStaffRole.Viewer,
                ShopPermission.ViewShipments, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }
        var portalOwner=await factory.AddUser("browser-integration-owner@example.test","Shop");
        var portalOther=await factory.AddUser("browser-integration-other@example.test","Shop");
        var webhookOwner=await factory.AddUser("browser-webhook-owner@example.test","Shop");
        var portalManager=await factory.AddUser("browser-integration-manager@example.test","IntegrationAdmin");
        var portal=await BrowserIntegrationTests.Seed(factory,portalOwner,"Portal Shop","Portal Province");
        var excludedPortal=await BrowserIntegrationTests.Seed(factory,portalOther,"Excluded Portal Shop","Elsewhere");
        var webhookPortal=await BrowserIntegrationTests.Seed(factory,webhookOwner,"Webhook Shop","Webhook Province");
        var recoveries=await BrowserIntegrationTests.SeedFailures(factory,webhookPortal.Client.Id);
        using(var scope=factory.Services.CreateScope()) {
            var db=scope.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>();
            db.IntegrationManagementScopes.AddRange(new MiniLogistics.Domain.PartnerApi.IntegrationManagementScope(adminActor,DateTimeOffset.UtcNow,isGlobal:true),new MiniLogistics.Domain.PartnerApi.IntegrationManagementScope(portalManager,DateTimeOffset.UtcNow,province:"Portal Province"));
            await db.SaveChangesAsync();
        }
        var address = factory.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var healthClient = new HttpClient(new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, _, _, _) => true });
        Assert.Equal(HttpStatusCode.OK, (await healthClient.GetAsync(address + "/health/live")).StatusCode);
        var node = Environment.GetEnvironmentVariable("MINILOGISTICS_NODE") ?? "node";
        var start = new ProcessStartInfo(node) { WorkingDirectory = Path.Combine(root, "src", "MiniLogistics.Frontend"),
            RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8, UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add("node_modules/@playwright/test/cli.js"); start.ArgumentList.Add("test");
        start.ArgumentList.Add("e2e/auth.spec.ts"); start.ArgumentList.Add("e2e/shop-context.spec.ts");
        start.ArgumentList.Add("e2e/admin-reporting.spec.ts");
        start.ArgumentList.Add("e2e/admin-users-shops.spec.ts");
        start.ArgumentList.Add("e2e/admin-config.spec.ts");
        start.ArgumentList.Add("e2e/admin-banners.spec.ts");
        start.ArgumentList.Add("e2e/integration-clients.spec.ts");
        start.ArgumentList.Add("e2e/integration-webhooks.spec.ts");
        start.ArgumentList.Add("e2e/shop-shipments.spec.ts");
        start.ArgumentList.Add("e2e/dashboard.spec.ts");
        start.ArgumentList.Add("e2e/shipment-lifecycle-shop.spec.ts");
        start.ArgumentList.Add("e2e/shipment-import.spec.ts");
        start.ArgumentList.Add("e2e/downloads.spec.ts");
        start.ArgumentList.Add("e2e/operations.spec.ts");
        start.ArgumentList.Add("e2e/shipper.spec.ts");
        start.ArgumentList.Add("e2e/cross-role-lifecycle.spec.ts");
        start.ArgumentList.Add("e2e/shop-profile.spec.ts");
        start.ArgumentList.Add("e2e/shop-staff.spec.ts");
        start.ArgumentList.Add("e2e/shop-settings.spec.ts");
        if (Environment.GetEnvironmentVariable("MINILOGISTICS_E2E_GREP") is { Length: > 0 } grep)
        {
            start.ArgumentList.Add("--grep"); start.ArgumentList.Add(grep);
        }
        start.Environment["MINILOGISTICS_E2E_BASE_URL"] = address + "/app/";
        start.Environment["MINILOGISTICS_E2E_IDENTITY_FIXTURE"] = "1";
        start.Environment["MINILOGISTICS_E2E_PASSWORD"] = BrowserIdentityFactory.Password;
        start.Environment["MINILOGISTICS_E2E_FOREIGN_INTEGRATION_SHOP"] = excludedPortal.Shop.Id.ToString();
        // Prevent inherited diagnostics from placing cookie-bearing traces in artifacts.
        start.Environment["MINILOGISTICS_E2E_SENSITIVE"] = "1";
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        // Phase 9 adds five complete administration workflows to the existing 22 scenarios.
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch { process.Kill(entireProcessTree: true); throw; }
        var output = await stdout + await stderr;
        Assert.True(process.ExitCode == 0, output);
        var selected = Environment.GetEnvironmentVariable("MINILOGISTICS_E2E_GREP");
        if(string.IsNullOrEmpty(selected)||selected.Contains("Integration client lifecycle",StringComparison.Ordinal)) {
            using var scope=factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>();
            var created=await db.ApiClients.SingleAsync(item=>item.Name=="Browser Portal Client");
            Assert.True(created.IsActive);Assert.Equal(MiniLogistics.Domain.PartnerApi.PartnerApiScope.Quote|MiniLogistics.Domain.PartnerApi.PartnerApiScope.TrackShipment,created.Scopes);
            Assert.True(await db.PartnerApiCredentialAudits.AnyAsync(item=>item.ApiClientId==created.Id&&item.ActorUserId==portalOwner));
        }
        if(string.IsNullOrEmpty(selected)||selected.Contains("Integration webhook lifecycle",StringComparison.Ordinal)) {
            using var scope=factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>();
            Assert.Equal(MiniLogistics.Domain.PartnerApi.WebhookDeliveryStatus.Pending,(await db.WebhookDeliveries.FindAsync(recoveries.Delivery))!.Status);
            Assert.Equal(MiniLogistics.Domain.Outbox.OutboxMessageStatus.Pending,(await db.OutboxMessages.FindAsync(recoveries.Outbox))!.Status);
            Assert.True(await db.PartnerApiCredentialAudits.AnyAsync(item=>item.ApiClientId==webhookPortal.Client.Id&&item.ActorUserId==webhookOwner));
        }
        if (string.IsNullOrEmpty(selected) || selected.Contains("Admin reporting scopes", StringComparison.Ordinal))
        {
            using var scope=factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>();
            var shipment=await db.Shipments.SingleAsync(item=>item.TrackingCode==new TrackingCode("ML-ADMIN-SETTLE"));
            var cod=await db.CodTransactions.SingleAsync(item=>item.ShipmentId==shipment.Id);
            Assert.Equal(CodStatus.Settled,cod.Status);Assert.Equal(adminActor,cod.SettledByUserId);
            Assert.True(await db.AdminAuditLogs.AnyAsync(item=>item.TargetId==cod.Id&&item.ActorUserId==adminActor));
        }
        if (string.IsNullOrEmpty(selected) || selected.Contains("Admin creates internal user", StringComparison.Ordinal))
        {
            using var scope=factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>();
            var user=await db.Users.SingleAsync(item=>item.Email=="phase9-created@example.test");
            Assert.False(user.IsActive);Assert.Equal(9,user.MaxActiveShipments);
            Assert.True(await db.ShipperWorkingAreas.AnyAsync(item=>item.ShipperId==user.Id&&item.IsActive));
            Assert.False((await db.Shops.SingleAsync(item=>item.OwnerUserId==adminShopOwner)).IsActive);
            Assert.True(await db.AdminAuditLogs.AnyAsync(item=>item.TargetId==user.Id&&item.ActorUserId==adminActor));
        }
        if (string.IsNullOrEmpty(selected) || selected.Contains("Shop profile edit", StringComparison.Ordinal))
        {
            using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>();
            var profile = await db.Shops.SingleAsync(item => item.OwnerUserId == profileOwner && item.Name == "Updated Profile Shop");
            Assert.Equal("Updated profile street", profile.Address.Street);
            Assert.Equal(profileOwner, (await db.Shops.SingleAsync(item => item.Name == "Additional Profile Shop")).OwnerUserId);
            var shipment = await db.Shipments.SingleAsync(item => item.TrackingCode == new TrackingCode("ML-PROFILE-E2E-SNAPSHOT"));
            Assert.Equal("Pickup private", shipment.PickupAddress.Street);
            Assert.True(await db.AdminAuditLogs.AnyAsync(item => item.TargetId == profile.Id && item.ActorUserId == profileOwner));
        }
        if (string.IsNullOrEmpty(selected) || selected.Contains("Shop staff owner", StringComparison.Ordinal))
        {
            using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>();
            var membership = await db.ShopStaffMemberships.SingleAsync(item => item.UserId == settingsStaff);
            Assert.False(membership.IsActive); Assert.Equal(ShopStaffRole.Viewer, membership.Role); Assert.Equal(ShopPermission.ViewShipments, membership.Permissions);
            Assert.True(await db.AdminAuditLogs.CountAsync(item => item.TargetId == membership.Id && item.ActorUserId == staffOwner) >= 3);
            var createdStaff = await db.Users.SingleAsync(item => item.Email == "browser-created-settings-staff@example.test");
            Assert.True(await db.ShopStaffMemberships.AnyAsync(item => item.UserId == createdStaff.Id && item.ShopId == membership.ShopId && item.IsActive));
        }
        if (string.IsNullOrEmpty(selected) || selected.Contains("Shop reporting notifications", StringComparison.Ordinal))
        {
            using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>();
            Assert.True((await db.ShopNotifications.SingleAsync(item => item.UserId == reportOwner)).IsRead);
            Assert.DoesNotContain("cod.collected", (await db.ShopNotificationPreferences.SingleAsync(item => item.UserId == reportOwner)).EnabledEventTypes);
        }
        if (string.IsNullOrEmpty(selected) || selected.Contains("cross-role", StringComparison.Ordinal))
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>();
            var shipment = await db.Shipments.Include(item => item.Assignments).Include(item => item.StatusHistory)
                .SingleAsync(item => item.ReceiverName == "Cross-role Receiver");
            Assert.Equal(ShipmentStatus.Delivered, shipment.Status);
            Assert.DoesNotContain(shipment.Assignments, item => item.IsActive);
            Assert.Equal(operationsActor, shipment.StatusHistory.Single(item => item.Status == ShipmentStatus.Assigned).ChangedByUserId);
            Assert.All(shipment.StatusHistory.Where(item => item.Status is not (ShipmentStatus.PendingPickup or ShipmentStatus.Assigned)),
                item => Assert.Equal(crossShipper, item.ChangedByUserId));
            var failure = Assert.Single(shipment.StatusHistory, item => item.Status == ShipmentStatus.DeliveryFailed);
            Assert.Equal(FailureReasonCode.ReceiverUnavailable, failure.FailureReasonCode);
            Assert.Equal("Cross-role receiver unavailable", failure.Note);
            Assert.Equal(2, shipment.StatusHistory.Count(item => item.Status == ShipmentStatus.Delivering));
            var cod = await db.CodTransactions.SingleAsync(item => item.ShipmentId == shipment.Id);
            Assert.Equal(CodStatus.Settled, cod.Status); Assert.Equal(crossShipper, cod.CollectedByUserId); Assert.Equal(adminActor, cod.SettledByUserId);
            Assert.Equal(99000m, cod.CollectedAmount!.Amount); Assert.Equal(1000m, cod.DiscrepancyAmount!.Amount);
            Assert.Equal("Cross-role COD shortfall", cod.CollectionNote);
            Assert.True(await db.AdminAuditLogs.AnyAsync(item => item.TargetId == shipment.Id && item.ActorUserId == crossShipper));
            Assert.True(await db.AdminAuditLogs.AnyAsync(item => item.TargetId == cod.Id && item.ActorUserId == crossShipper));
            Assert.True(await db.AdminAuditLogs.AnyAsync(item => item.TargetId == cod.Id && item.ActorUserId == adminActor));
            var notificationEvents = await db.OutboxMessages.Where(item => item.AggregateId == shipment.Id && item.Type == "ShopNotification").ToListAsync();
            Assert.Contains(notificationEvents, item => item.PayloadJson.Contains("shipment.delivery_failed", StringComparison.Ordinal));
            Assert.Contains(notificationEvents, item => item.PayloadJson.Contains("cod.collected", StringComparison.Ordinal));
            Assert.Contains(notificationEvents, item => item.PayloadJson.Contains("cod.settled", StringComparison.Ordinal));
        }
    }
}
