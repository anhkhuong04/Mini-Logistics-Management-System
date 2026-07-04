using Microsoft.AspNetCore.Identity;

namespace MiniLogistics.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public bool IsAvailableForAssignment { get; set; } = true;

    public int MaxActiveShipments { get; set; } = 30;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
