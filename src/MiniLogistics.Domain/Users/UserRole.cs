namespace MiniLogistics.Domain.Users;

/// <summary>
/// Defines the supported User Role values in the domain model.
/// </summary>
public enum UserRole
{
    Admin = 1,
    Operator = 2,
    Shop = 3,
    Shipper = 4,
    IntegrationAdmin = 5
}
