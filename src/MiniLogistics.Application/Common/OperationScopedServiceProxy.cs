using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.DependencyInjection;
using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Common;

/// <summary>
/// Resolves an application service and its scoped dependencies for the duration of each call.
/// This prevents Interactive Server circuits from retaining EF Core contexts between operations.
/// </summary>
public class OperationScopedServiceProxy : DispatchProxy
{
    private static readonly AsyncLocal<IServiceProvider?> CurrentOperationServices = new();
    private static readonly MethodInfo CompleteTaskMethod = typeof(OperationScopedServiceProxy)
        .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
        .Single(method => method.Name == nameof(CompleteTaskAsync) && method.IsGenericMethodDefinition);

    private IServiceScopeFactory _scopeFactory = null!;
    private Type _implementationType = null!;

    public static object Create(
        Type serviceType,
        Type implementationType,
        IServiceScopeFactory scopeFactory)
    {
        var proxy = (OperationScopedServiceProxy)DispatchProxy.Create(
            serviceType,
            typeof(OperationScopedServiceProxy));
        proxy._scopeFactory = scopeFactory;
        proxy._implementationType = implementationType;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod is null)
        {
            throw new ArgumentNullException(nameof(targetMethod));
        }

        var currentServices = CurrentOperationServices.Value;
        if (currentServices is not null)
        {
            return InvokeTarget(currentServices, targetMethod, args);
        }

        var operationScope = _scopeFactory.CreateAsyncScope();
        var previousServices = CurrentOperationServices.Value;
        CurrentOperationServices.Value = operationScope.ServiceProvider;

        object? result;
        try
        {
            result = InvokeTarget(operationScope.ServiceProvider, targetMethod, args);
        }
        catch
        {
            CurrentOperationServices.Value = previousServices;
            operationScope.DisposeAsync().AsTask().GetAwaiter().GetResult();
            throw;
        }

        CurrentOperationServices.Value = previousServices;
        if (result is null)
        {
            operationScope.DisposeAsync().AsTask().GetAwaiter().GetResult();
            return null;
        }

        if (result is Task task)
        {
            var returnType = targetMethod.ReturnType;
            if (returnType.IsGenericType)
            {
                return CompleteTaskMethod
                    .MakeGenericMethod(returnType.GenericTypeArguments[0])
                    .Invoke(null, [task, operationScope]);
            }

            return CompleteTaskAsync(task, operationScope);
        }

        operationScope.DisposeAsync().AsTask().GetAwaiter().GetResult();
        throw new InvalidOperationException(
            $"Application service method '{targetMethod.DeclaringType?.Name}.{targetMethod.Name}' " +
            "must return Task or Task<T> so its operation scope remains alive until completion.");
    }

    private object? InvokeTarget(IServiceProvider services, MethodInfo method, object?[]? args)
    {
        var implementation = services.GetRequiredService(_implementationType);
        try
        {
            return method.Invoke(implementation, args);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    private static async Task CompleteTaskAsync(Task task, AsyncServiceScope operationScope)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        finally
        {
            await operationScope.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static async Task<T> CompleteTaskAsync<T>(Task task, AsyncServiceScope operationScope)
    {
        try
        {
            return await ((Task<T>)task).ConfigureAwait(false);
        }
        catch (ConcurrencyConflictException)
        {
            var error = ApplicationErrors.ConcurrencyConflict(
                "The data changed while this operation was running. Reload and try again.");
            if (typeof(T) == typeof(Result))
            {
                return (T)(object)Result.Failure(error);
            }

            if (typeof(T).IsGenericType
                && typeof(T).GetGenericTypeDefinition() == typeof(Result<>))
            {
                var failureFactory = typeof(T).GetMethod(
                    nameof(Result<object>.Failure),
                    BindingFlags.Public | BindingFlags.Static)!;
                return (T)failureFactory.Invoke(null, [error])!;
            }

            throw;
        }
        finally
        {
            await operationScope.DisposeAsync().ConfigureAwait(false);
        }
    }
}
