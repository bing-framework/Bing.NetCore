using System;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Bing.Data.Inventory;
using Bing.Data.Transaction;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Admin.Data.Inventory;

/// <summary>
/// 在独立服务作用域中执行整单库存请求并处理可安全重试的死锁。
/// </summary>
/// <remarks>
/// 每次尝试都会创建并释放新的作用域。只有原生 MySQL 1213、事务明确回滚且清理没有失败时才会重试；提交结果未知时保留原异常并由调用方查询原请求。
/// </remarks>
public sealed class AdminInventoryOrderExecutor
{
    /// <summary>
    /// 单次请求允许的最大尝试次数。
    /// </summary>
    private const int MaxAttempts = 4;

    /// <summary>
    /// 死锁重试的初始等待时间。
    /// </summary>
    private const int InitialRetryDelayMilliseconds = 20;

    /// <summary>
    /// 作用域清理错误的诊断键。
    /// </summary>
    private const string CleanupErrorKey = "Bing.InventoryScopeCleanupException";

    /// <summary>
    /// 创建业务作用域的工厂。
    /// </summary>
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>
    /// 初始化一个 <see cref="AdminInventoryOrderExecutor" /> 类型的实例。
    /// </summary>
    /// <param name="scopeFactory">创建独立业务作用域的工厂。</param>
    public AdminInventoryOrderExecutor(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    }

    /// <summary>
    /// 执行整单库存请求。
    /// </summary>
    /// <param name="request">整单库存请求。</param>
    /// <param name="userId">可信调用上下文的用户标识。</param>
    /// <param name="traceId">调用关联标识。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>已提交的首次整单结果。</returns>
    /// <remarks>重试前先完成作用域异步释放，不复用上一轮工作单元、跟踪状态或事务回调。</remarks>
    public async Task<InventoryOrderResult> ExecuteAsync(InventoryOrderRequest request, string userId = null,
        string traceId = null, CancellationToken cancellationToken = default)
        => await ExecuteCoreAsync(request, userId, traceId, null, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// 执行整单库存请求，并在提交结果不确定时按原业务身份查询恢复。
    /// </summary>
    /// <param name="request">整单库存请求。</param>
    /// <param name="userId">可信调用上下文的用户标识。</param>
    /// <param name="traceId">调用关联标识。</param>
    /// <param name="recoverAsync">使用原请求身份查询已提交结果的回调。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>已提交或恢复查询得到的整单结果。</returns>
    /// <remarks>
    /// 回调返回空值时保留原始异常；此入口不会以新订单标识重做未知结果的业务。
    /// 恢复确认提交后仍有清理错误时，结果附于原异常的 Bing.InventoryRecoveredResult 数据项并继续抛出异常。
    /// 业务成功但作用域释放失败时不查询恢复，结果附于释放异常的 Bing.InventoryCommittedResult 数据项。
    /// </remarks>
    public async Task<InventoryOrderResult> ExecuteWithRecoveryAsync(InventoryOrderRequest request, string userId,
        string traceId, Func<InventoryOrderRequest, CancellationToken, Task<InventoryOrderResult>> recoverAsync,
        CancellationToken cancellationToken = default)
    {
        if (recoverAsync == null)
            throw new ArgumentNullException(nameof(recoverAsync));
        return await ExecuteCoreAsync(request, userId, traceId, recoverAsync, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// 执行整单库存请求的内部流程。
    /// </summary>
    /// <param name="request">整单库存请求。</param>
    /// <param name="userId">可信调用上下文的用户标识。</param>
    /// <param name="traceId">调用关联标识。</param>
    /// <param name="recoverAsync">按原请求查询结果的回调；未启用恢复时为空。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>已提交或恢复得到的整单结果。</returns>
    private async Task<InventoryOrderResult> ExecuteCoreAsync(InventoryOrderRequest request, string userId,
        string traceId, Func<InventoryOrderRequest, CancellationToken, Task<InventoryOrderResult>> recoverAsync,
        CancellationToken cancellationToken)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        for (var attempt = 0; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var scope = _scopeFactory.CreateAsyncScope();
            Exception failure = null;
            InventoryOrderResult result = null;
            try
            {
                var service = scope.ServiceProvider.GetRequiredService<AdminInventoryService>();
                result = await service.ExecuteAsync(request, userId, traceId, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failure = exception;
            }

            try
            {
                await scope.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception cleanupException)
            {
                if (failure == null)
                {
                    failure = cleanupException;
                    if (result != null)
                    {
                        failure.SetTransactionOutcome(TransactionOutcome.Committed);
                        failure.Data["Bing.InventoryCommittedResult"] = result;
                    }
                }
                else
                    failure.Data[CleanupErrorKey] = cleanupException;
            }

            if (failure == null)
                return result ?? throw new InvalidOperationException("库存作用域未返回整单结果。");

            var outcome = failure.GetTransactionOutcome();
            // 已有正常业务结果时只有清理失败，不能再查询并掩盖释放错误。
            if (result == null && recoverAsync != null && outcome != TransactionOutcome.RolledBack &&
                !cancellationToken.IsCancellationRequested)
            {
                InventoryOrderResult recovered = null;
                try
                {
                    recovered = await recoverAsync(request, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception recoveryException)
                {
                    failure.Data["Bing.InventoryRecoveryException"] = recoveryException;
                }
                if (recovered != null)
                {
                    if (!HasCleanupFailure(failure))
                        return recovered;
                    // 查询可确认业务提交，但不能改变清理失败的事实。
                    failure.SetTransactionOutcome(TransactionOutcome.Committed);
                    failure.Data["Bing.InventoryRecoveredResult"] = recovered;
                }
            }

            if (attempt >= MaxAttempts - 1 || !IsRetryableDeadlock(failure))
                ExceptionDispatchInfo.Capture(failure).Throw();

            await Task.Delay(InitialRetryDelayMilliseconds << attempt, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 判断异常是否表示已安全回滚的原生 MySQL 死锁。
    /// </summary>
    /// <param name="exception">待判断的异常。</param>
    /// <returns>满足安全重试条件时返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    internal static bool IsRetryableDeadlock(Exception exception)
    {
        if (exception == null || exception.GetTransactionOutcome() != TransactionOutcome.RolledBack ||
            HasCleanupFailure(exception))
            return false;
        var type = exception.GetType();
        if (type.FullName != "MySqlConnector.MySqlException" &&
            type.FullName != "MySql.Data.MySqlClient.MySqlException")
            return false;
        return type.GetProperty("Number", BindingFlags.Instance | BindingFlags.Public)?.GetValue(exception) is int number &&
               number == 1213;
    }

    /// <summary>
    /// 判断失败是否包含尚未消除的事务或作用域清理错误。
    /// </summary>
    /// <param name="failure">业务或清理期间捕获的异常。</param>
    /// <returns>存在清理错误或聚合错误时返回 true，否则返回 false。</returns>
    /// <remarks>旧工作单元使用聚合异常保存事务释放错误，恢复入口保守保留该异常。</remarks>
    private static bool HasCleanupFailure(Exception failure) =>
        failure is AggregateException || failure.Data.Contains(CleanupErrorKey) ||
        failure.Data.Contains("Bing.TransactionCleanupException") ||
        failure.Data.Contains("Bing.TransactionRollbackException");
}
