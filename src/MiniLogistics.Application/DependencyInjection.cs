using FluentValidation;
using MiniLogistics.Application.AdminUsers.CreateInternalUser;
using MiniLogistics.Application.AdminUsers.GetAdminUsers;
using MiniLogistics.Application.AdminUsers.SetUserActiveStatus;
using MiniLogistics.Application.CashOnDelivery.GetCodSettlementCandidates;
using Microsoft.Extensions.DependencyInjection;
using MiniLogistics.Application.CashOnDelivery.MarkCodCollected;
using MiniLogistics.Application.CashOnDelivery.MarkCodSettled;
using MiniLogistics.Application.Fees;
using MiniLogistics.Application.PartnerApi;
using MiniLogistics.Application.Routing;
using MiniLogistics.Application.Shippers.GetActiveShippers;
using MiniLogistics.Application.Shippers.GetLogisticsHubs;
using MiniLogistics.Application.Shippers.GetShipperWorkingAreas;
using MiniLogistics.Application.Shippers.SetShipperWorkingAreas;
using MiniLogistics.Application.Shipments.AssignmentSelection;
using MiniLogistics.Application.Shops.GetCurrentShop;
using MiniLogistics.Application.Shops.RegisterShop;
using MiniLogistics.Application.Shipments.AssignShipperToShipment;
using MiniLogistics.Application.Shipments.AutoAssignShipment;
using MiniLogistics.Application.Shipments.CancelShipmentForCurrentShop;
using MiniLogistics.Application.Shipments.CreateShipment;
using MiniLogistics.Application.Shipments.GetAssignedShipmentsForShipper;
using MiniLogistics.Application.Shipments.GetOperationsShipments;
using MiniLogistics.Application.Shipments.GetPendingPickupShipments;
using MiniLogistics.Application.Shipments.GetPublicTracking;
using MiniLogistics.Application.Shipments.GetShipmentDetailForCurrentShop;
using MiniLogistics.Application.Shipments.GetShipmentsForCurrentShop;
using MiniLogistics.Application.Shipments.UpdateShipmentStatus;

namespace MiniLogistics.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddScoped<ICreateInternalUserService, CreateInternalUserService>();
        services.AddScoped<IGetAdminUsersService, GetAdminUsersService>();
        services.AddScoped<ISetUserActiveStatusService, SetUserActiveStatusService>();
        services.AddScoped<IMarkCodCollectedService, MarkCodCollectedService>();
        services.AddScoped<IMarkCodSettledService, MarkCodSettledService>();
        services.AddScoped<IGetCodSettlementCandidatesService, GetCodSettlementCandidatesService>();
        services.AddScoped<IPartnerApiAuthenticationService, PartnerApiAuthenticationService>();
        services.AddScoped<IPartnerQuoteService, PartnerQuoteService>();
        services.AddScoped<IPartnerCreateShipmentService, PartnerCreateShipmentService>();
        services.AddScoped<IPartnerShipmentQueryService, PartnerShipmentQueryService>();
        services.AddScoped<IPartnerCancelShipmentService, PartnerCancelShipmentService>();
        services.AddScoped<IPartnerIntegrationManagementService, PartnerIntegrationManagementService>();
        services.AddScoped<IWebhookEventPublisher, WebhookEventPublisher>();
        services.AddScoped<IShippingFeeService, ShippingFeeService>();
        services.AddScoped<IRouteClassificationService, RouteClassificationService>();
        services.AddScoped<IGetActiveShippersService, GetActiveShippersService>();
        services.AddScoped<IGetLogisticsHubsService, GetLogisticsHubsService>();
        services.AddScoped<IGetShipperWorkingAreasService, GetShipperWorkingAreasService>();
        services.AddScoped<ISetShipperWorkingAreasService, SetShipperWorkingAreasService>();
        services.AddScoped<IShipmentAssignmentSelector, ShipmentAssignmentSelector>();
        services.AddScoped<IAutoAssignShipmentService, AutoAssignShipmentService>();
        services.AddScoped<IAssignShipperToShipmentService, AssignShipperToShipmentService>();
        services.AddScoped<ICancelShipmentForCurrentShopService, CancelShipmentForCurrentShopService>();
        services.AddScoped<ICreateShipmentService, CreateShipmentService>();
        services.AddScoped<IGetAssignedShipmentsForShipperService, GetAssignedShipmentsForShipperService>();
        services.AddScoped<IGetOperationsShipmentsService, GetOperationsShipmentsService>();
        services.AddScoped<IGetPendingPickupShipmentsService, GetPendingPickupShipmentsService>();
        services.AddScoped<IGetShipmentsForCurrentShopService, GetShipmentsForCurrentShopService>();
        services.AddScoped<IGetShipmentDetailForCurrentShopService, GetShipmentDetailForCurrentShopService>();
        services.AddScoped<IGetPublicTrackingService, GetPublicTrackingService>();
        services.AddScoped<IUpdateShipmentStatusService, UpdateShipmentStatusService>();
        services.AddScoped<IGetCurrentShopService, GetCurrentShopService>();
        services.AddScoped<IRegisterShopService, RegisterShopService>();

        return services;
    }
}
