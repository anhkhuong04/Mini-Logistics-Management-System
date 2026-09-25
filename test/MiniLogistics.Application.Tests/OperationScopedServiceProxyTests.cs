using Microsoft.Extensions.DependencyInjection;
using MiniLogistics.Application.Common;
using MiniLogistics.Domain.Common;
using Xunit;

namespace MiniLogistics.Application.Tests;

public sealed class OperationScopedServiceProxyTests
{
    [Fact]
    public async Task EachCallGetsFreshScopedDependenciesAndNestedServicesShareOperationScope()
    {
        var services = new ServiceCollection();
        services.AddScoped<OperationProbe>();
        services.AddScoped<InnerOperationService>();
        services.AddScoped<IInnerOperationService>(provider => (IInnerOperationService)OperationScopedServiceProxy.Create(
            typeof(IInnerOperationService),
            typeof(InnerOperationService),
            provider.GetRequiredService<IServiceScopeFactory>()));
        services.AddScoped<OuterOperationService>();
        services.AddScoped<IOuterOperationService>(provider => (IOuterOperationService)OperationScopedServiceProxy.Create(
            typeof(IOuterOperationService),
            typeof(OuterOperationService),
            provider.GetRequiredService<IServiceScopeFactory>()));

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IOuterOperationService>();

        var firstCall = await service.GetScopeIdsAsync();
        var secondCall = await service.GetScopeIdsAsync();

        Assert.Equal(firstCall.OuterScopeId, firstCall.InnerScopeId);
        Assert.Equal(secondCall.OuterScopeId, secondCall.InnerScopeId);
        Assert.NotEqual(firstCall.OuterScopeId, secondCall.OuterScopeId);

        var concurrentCalls = await Task.WhenAll(
            Enumerable.Range(0, 4).Select(_ => service.GetScopeIdsAsync()));

        Assert.All(concurrentCalls, call => Assert.Equal(call.OuterScopeId, call.InnerScopeId));
        Assert.Equal(4, concurrentCalls.Select(call => call.OuterScopeId).Distinct().Count());
    }

    [Fact]
    public async Task ConcurrencyConflictBecomesApplicationConflictResult()
    {
        var services = new ServiceCollection();
        services.AddScoped<ThrowingConflictService>();
        services.AddScoped<IConflictOperationService>(provider => (IConflictOperationService)OperationScopedServiceProxy.Create(
            typeof(IConflictOperationService),
            typeof(ThrowingConflictService),
            provider.GetRequiredService<IServiceScopeFactory>()));

        await using var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IConflictOperationService>();

        var result = await service.ExecuteAsync();

        Assert.True(result.IsFailure);
        Assert.Equal("Application.ConcurrencyConflict", result.Error.Code);
    }

    [Fact]
    public async Task ConcurrencyConflictBecomesApplicationConflictGenericResult()
    {
        var services = new ServiceCollection();
        services.AddScoped<ThrowingGenericConflictService>();
        services.AddScoped<IConflictValueOperationService>(provider => (IConflictValueOperationService)OperationScopedServiceProxy.Create(
            typeof(IConflictValueOperationService),
            typeof(ThrowingGenericConflictService),
            provider.GetRequiredService<IServiceScopeFactory>()));

        await using var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IConflictValueOperationService>();

        var result = await service.ExecuteAsync();

        Assert.True(result.IsFailure);
        Assert.Equal("Application.ConcurrencyConflict", result.Error.Code);
    }

    public interface IOuterOperationService
    {
        Task<ScopeIds> GetScopeIdsAsync();
    }

    public interface IInnerOperationService
    {
        Task<Guid> GetScopeIdAsync();
    }

    public interface IConflictOperationService
    {
        Task<Result> ExecuteAsync();
    }

    public interface IConflictValueOperationService
    {
        Task<Result<Guid>> ExecuteAsync();
    }

    public sealed class OuterOperationService(
        OperationProbe probe,
        IInnerOperationService inner) : IOuterOperationService
    {
        public async Task<ScopeIds> GetScopeIdsAsync()
        {
            var outerScopeId = probe.ScopeId;
            await Task.Delay(15);
            return new ScopeIds(outerScopeId, await inner.GetScopeIdAsync());
        }
    }

    public sealed class InnerOperationService(OperationProbe probe) : IInnerOperationService
    {
        public async Task<Guid> GetScopeIdAsync()
        {
            await Task.Delay(15);
            return probe.ScopeId;
        }
    }

    public sealed class ThrowingConflictService : IConflictOperationService
    {
        public Task<Result> ExecuteAsync() => Task.FromException<Result>(
            new ConcurrencyConflictException(
                "Concurrent update.",
                new InvalidOperationException("simulated database conflict")));
    }

    public sealed class ThrowingGenericConflictService : IConflictValueOperationService
    {
        public Task<Result<Guid>> ExecuteAsync() => Task.FromException<Result<Guid>>(
            new ConcurrencyConflictException(
                "Concurrent update.",
                new InvalidOperationException("simulated database conflict")));
    }

    public sealed class OperationProbe
    {
        public Guid ScopeId { get; } = Guid.NewGuid();
    }

    public sealed record ScopeIds(Guid OuterScopeId, Guid InnerScopeId);
}
