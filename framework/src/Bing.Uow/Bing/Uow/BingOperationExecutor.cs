using System.Runtime.ExceptionServices;
using Bing.Tracing;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Uow;

/// <summary>
/// 在工作单元作用域内执行应用服务操作的执行器。
/// </summary>
internal sealed class BingOperationExecutor : IBingOperationExecutor
{
    /// <summary>
    /// 保存创建操作作用域的服务作用域工厂。
    /// </summary>
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>
    /// 初始化一个 <see cref="BingOperationExecutor" /> 类型的实例。
    /// </summary>
    /// <param name="scopeFactory">用于创建工作单元执行作用域的工厂。</param>
    public BingOperationExecutor(IServiceScopeFactory scopeFactory) =>
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));

    /// <inheritdoc />
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

    /// <inheritdoc />
    public Task<TResult> ExecuteAsync<TService, TResult>(
        Func<TService, CancellationToken, Task<TResult>> operation,
        UnitOfWorkScopeOptions options = null, CancellationToken cancellationToken = default)
        where TService : class
    {
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));

        return ExecuteCoreAsync(operation, options, cancellationToken);
    }

    /// <summary>
    /// 在新的工作单元操作范围内执行业务回调并完成提交。
    /// </summary>
    /// <typeparam name="TService">业务服务类型。</typeparam>
    /// <typeparam name="TResult">业务操作结果类型。</typeparam>
    /// <param name="operation">接收已解析服务和操作取消令牌的异步业务回调。</param>
    /// <param name="options">操作范围选项；为 null 时使用默认选项。</param>
    /// <param name="cancellationToken">用于启动操作并创建工作单元范围的取消令牌。</param>
    /// <returns>业务回调返回的结果。</returns>
    /// <remarks>回调成功后提交操作范围，并在退出前释放操作范围和依赖注入服务范围。主要异常保持原样，清理异常会附加到其 Data；没有主要异常时传播单个清理异常或聚合多个清理异常。</remarks>
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

    /// <summary>
    /// 将资源清理异常附加到业务主异常。
    /// </summary>
    /// <param name="primaryException">业务执行期间发生的主异常。</param>
    /// <param name="cleanupExceptions">释放操作范围和服务范围时收集的异常。</param>
    /// <remarks>清理异常存入主异常的 Data 字典；写入失败时忽略该诊断，避免替换主异常。</remarks>
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

    /// <summary>
    /// 传播资源清理过程中收集的异常。
    /// </summary>
    /// <param name="cleanupExceptions">释放资源时收集的异常。</param>
    /// <remarks>只有一个异常时重新抛出该异常；存在多个异常时抛出聚合异常。</remarks>
    private static void ThrowCleanupExceptions(IReadOnlyCollection<Exception> cleanupExceptions)
    {
        if (cleanupExceptions.Count == 1)
            ExceptionDispatchInfo.Capture(cleanupExceptions.First()).Throw();
        if (cleanupExceptions.Count > 1)
            throw new AggregateException(cleanupExceptions);
    }
}
