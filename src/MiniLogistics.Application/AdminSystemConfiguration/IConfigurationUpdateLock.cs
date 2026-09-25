namespace MiniLogistics.Application.AdminSystemConfiguration;

/// <summary>
/// Serializes updates for one configuration scope while the caller's database transaction is active.
/// </summary>
public interface IConfigurationUpdateLock
{
    Task<bool> TryAcquireAsync(string resource, CancellationToken cancellationToken = default);
}
