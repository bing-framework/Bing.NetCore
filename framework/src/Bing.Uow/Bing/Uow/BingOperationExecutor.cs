using System.Runtime.ExceptionServices;
using Bing.Tracing;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Uow;

internal sealed class BingOperationExecutor : IBingOperationExecutor
{
    private readonly IServiceScopeFactory _scopeFactory;

    public BingOperationExecutor(IServiceScopeFactory scopeFactory) =>
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));

    public Task ExecuteAsync<TService>(Func<TService, CancellationToken, Task> operation,
        UnitOfWorkScopeOptions options = null, CancellationToken cancellationToken = default)
        where TService : class
    {
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));

        return ExecuteCoreAsync<TService, object>(async (service, token) =>
        {
            var task = operation(service, token);
            if (task == null)
                throw new InvalidOperationException("业务操作不能返回 null Task。");
            await task.ConfigureAwait(false);
            return null;
        }, options, cancellationToken);
    }

    public Task<TResult> ExecuteAsync<TService, TResult>(
        Func<TService, CancellationToken, Task<TResult>> operation,
        UnitOfWorkScopeOptions options = null, CancellationToken cancellationToken = default)
        where TService : class
    {
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));

        return ExecuteCoreAsync(operation, options, cancellationToken);
    }

    private async Task<TResult> ExecuteCoreAsync<TService, TResult>(
        Func<TService, CancellationToken, Task<TResult>> operation,
        UnitOfWorkScopeOptions options, CancellationToken cancellationToken)
        where TService : class
    {
        var optionSnapshot = UnitOfWorkScopeManager.CloneAndValidateOptions(
            options ?? new UnitOfWorkScopeOptions());
        cancellationToken.ThrowIfCancellationRequested();

        var parentTrace = TraceIdContext.Current;
        TraceIdContext.Current = parentTrace == null
            ? null
            : new TraceIdContext(parentTrace.TraceId, parentTrace.RootId, parentTrace.ParentId, parentTrace.ChildId);

        try
        {
            var serviceScope = _scopeFactory.CreateScope();
            IUnitOfWorkExecutionScope operationScope = null;
            TResult result = default;
            Exception primaryException = null;
            var cleanupExceptions = new List<Exception>();

            try
            {
                var services = serviceScope.ServiceProvider;
                var scopeManager = services.GetRequiredService<IUnitOfWorkScopeManager>();
                if (scopeManager is not IUnitOfWorkOperationManager operationManager)
                    throw new InvalidOperationException(
                        $"工作单元管理器 {scopeManager.GetType().FullName} 未实现 {nameof(IUnitOfWorkOperationManager)}，不能启动业务操作。");

                operationScope = operationManager.BeginOperation(optionSnapshot, cancellationToken);
                var service = services.GetRequiredService<TService>();
                var task = operation(service, operationScope.CancellationToken);
                if (task == null)
                    throw new InvalidOperationException("业务操作不能返回 null Task。");

                result = await task.ConfigureAwait(false);
                await operationScope.CompleteAsync(operationScope.CancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                primaryException = exception;
            }

            if (operationScope != null)
            {
                try
                {
                    await operationScope.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    cleanupExceptions.Add(exception);
                }
            }

            try
            {
                if (serviceScope is IAsyncDisposable asyncScope)
                    await asyncScope.DisposeAsync().ConfigureAwait(false);
                else
                    serviceScope.Dispose();
            }
            catch (Exception exception)
            {
                cleanupExceptions.Add(exception);
            }

            if (primaryException != null)
            {
                AttachCleanupExceptions(primaryException, cleanupExceptions);
                ExceptionDispatchInfo.Capture(primaryException).Throw();
            }

            ThrowCleanupExceptions(cleanupExceptions);
            return result;
        }
        finally
        {
            TraceIdContext.Current = parentTrace;
        }
    }

    private static void AttachCleanupExceptions(Exception primaryException,
        IReadOnlyCollection<Exception> cleanupExceptions)
    {
        if (cleanupExceptions.Count == 0)
            return;
        try
        {
            primaryException.Data["Bing.Operation.CleanupExceptions"] = cleanupExceptions.ToArray();
        }
        catch
        {
            // 清理诊断不能替换业务异常。
        }
    }

    private static void ThrowCleanupExceptions(IReadOnlyCollection<Exception> cleanupExceptions)
    {
        if (cleanupExceptions.Count == 1)
            ExceptionDispatchInfo.Capture(cleanupExceptions.First()).Throw();
        if (cleanupExceptions.Count > 1)
            throw new AggregateException(cleanupExceptions);
    }
}
