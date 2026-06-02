using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using MiniLogistics.Application.Fees;
using MiniLogistics.Application.Routing;
using MiniLogistics.Application.Shops.GetCurrentShop;
using MiniLogistics.Application.Shops.RegisterShop;
using MiniLogistics.Application.Shipments.AssignShipperToShipment;
using MiniLogistics.Application.Shipments.CancelShipmentForCurrentShop;
using MiniLogistics.Application.Shipments.CreateShipment;
using MiniLogistics.Application.Shipments.GetPublicTracking;
using MiniLogistics.Application.Shipments.GetShipmentDetailForCurrentShop;
using MiniLogistics.Application.Shipments.GetShipmentsForCurrentShop;

namespace MiniLogistics.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddScoped<IShippingFeeService, ShippingFeeService>();
        services.AddScoped<IRouteClassificationService, RouteClassificationService>();
        services.AddScoped<IAssignShipperToShipmentService, AssignShipperToShipmentService>();
        services.AddScoped<ICancelShipmentForCurrentShopService, CancelShipmentForCurrentShopService>();
        services.AddScoped<ICreateShipmentService, CreateShipmentService>();
        services.AddScoped<IGetShipmentsForCurrentShopService, GetShipmentsForCurrentShopService>();
        services.AddScoped<IGetShipmentDetailForCurrentShopService, GetShipmentDetailForCurrentShopService>();
        services.AddScoped<IGetPublicTrackingService, GetPublicTrackingService>();
        services.AddScoped<IGetCurrentShopService, GetCurrentShopService>();
        services.AddScoped<IRegisterShopService, RegisterShopService>();

        return services;
    }
}
