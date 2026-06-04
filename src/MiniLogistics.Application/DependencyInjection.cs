using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using MiniLogistics.Application.CashOnDelivery.MarkCodCollected;
using MiniLogistics.Application.Fees;
using MiniLogistics.Application.Routing;
using MiniLogistics.Application.Shippers.GetActiveShippers;
using MiniLogistics.Application.Shops.GetCurrentShop;
using MiniLogistics.Application.Shops.RegisterShop;
using MiniLogistics.Application.Shipments.AssignShipperToShipment;
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

        services.AddScoped<IMarkCodCollectedService, MarkCodCollectedService>();
        services.AddScoped<IShippingFeeService, ShippingFeeService>();
        services.AddScoped<IRouteClassificationService, RouteClassificationService>();
        services.AddScoped<IGetActiveShippersService, GetActiveShippersService>();
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
