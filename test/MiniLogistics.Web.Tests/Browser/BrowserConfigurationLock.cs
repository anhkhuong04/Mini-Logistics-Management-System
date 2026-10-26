using MiniLogistics.Application.AdminSystemConfiguration;
namespace MiniLogistics.Web.Tests.Browser;
// InMemory fixture adapter only. Real SQL locking is exercised by SystemConfigurationConcurrencyTests.
internal sealed class BrowserConfigurationLock : IConfigurationUpdateLock
{
    public Task<bool> TryAcquireAsync(string resource,CancellationToken cancellationToken=default)=>Task.FromResult(true);
}
