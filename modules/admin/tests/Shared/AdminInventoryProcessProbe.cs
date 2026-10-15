using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Text;
using AspectCore.Extensions.Hosting;
using Bing.Admin.Data;
using Bing.Admin.Data.Inventory;
using Bing.Admin.Data.UnitOfWorks.MySql;
using Bing.Auditing;
using Bing.Auditing.Persistence;
using Bing.Data.Inventory;
using Bing.Data.Transaction;
using Bing.DependencyInjection;
using Bing.Events.Cap;
using Bing.Test.Shared;
using Bing.Timing;
using Bing.Uow;
using Bing.Users;
using DotNetCore.CAP;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
#if BING_ADMIN_EFCORE_TESTS
using Microsoft.EntityFrameworkCore;
#else
using Bing.FreeSQL;
using FreeSqlBuilder = FreeSql.FreeSqlBuilder;
using DataType = FreeSql.DataType;
#endif

namespace Bing.Admin.InventoryProbe;

/// <summary>
/// 在独立进程中执行真实 Admin 库存、审计与 CAP 事务。
/// </summary>
internal static class Program
{
    /// <summary>
    /// 执行有界库存竞争或中断探针。
    /// </summary>
    /// <param name="args">模式、租户、订单和每项数量。</param>
    /// <returns>成功时返回零，失败时返回一。</returns>
    private static async Task<int> Main(string[] args)
    {
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = Encoding.UTF8;
        var phase = "Setup";
        try
        {
            if (args.Length != 4 || args[0] is not ("execute" or "before-commit" or "after-commit") ||
                !long.TryParse(args[3], NumberStyles.None, CultureInfo.InvariantCulture, out var quantity))
                throw new ArgumentException("库存探针参数无效。");
            var connection = Required("ConnectionStrings__MySqlConnection");
            IntegrationDatabaseSafetyValidator.EnsureDatabaseOperationAllowed(connection, "MySql");
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90));
#if !BING_ADMIN_EFCORE_TESTS
            using var orm = new FreeSqlBuilder().UseConnectionString(DataType.MySql, connection).Build();
#endif
            var host = new HostBuilder().UseServiceContext().ConfigureServices(services =>
            {
                services.EnableAop();
                services.AddLogging();
                services.AddScoped<ILazyServiceProvider, LazyServiceProvider>();
                services.AddScoped<ITransactionActionManager, TransactionActionManager>();
                services.AddScoped<IUnitOfWorkManager, UnitOfWorkManager>();
                services.AddSingleton(NullCurrentUser.Instance);
                services.AddSingleton<IClock, SystemClock>();
                services.AddTransient<IAuditPropertySetter, AuditPropertySetter>();
#if BING_ADMIN_EFCORE_TESTS
                services.AddSingleton(new DbContextOptionsBuilder<AdminUnitOfWork>()
                    .UseMySql(connection, ServerVersion.AutoDetect(connection)).Options);
#else
                services.AddSingleton(new FreeSqlWrapper { Orm = orm });
#endif
                services.AddScoped<AdminUnitOfWork>();
                services.AddAdminInventory();
                if (args[0] == "before-commit")
                    services.AddSingleton<ITransactionalAuditingStore, BlockingAuditingStore>();
                services.AddCapEventBus(options =>
                {
#if BING_ADMIN_EFCORE_TESTS
                    options.UseEntityFramework<AdminUnitOfWork>();
#endif
                    options.UseMySql(connection);
                    options.UseRabbitMQ(rabbit =>
                    {
                        rabbit.HostName = Required("CAP_RABBITMQ_HOST");
                        rabbit.Port = int.Parse(Required("CAP_RABBITMQ_PORT"), CultureInfo.InvariantCulture);
                        rabbit.UserName = Required("CAP_RABBITMQ_USERNAME");
                        rabbit.Password = Required("CAP_RABBITMQ_PASSWORD");
                    });
                    options.Version = "1.0";
                });
            }).Build();
            Exception original = null;
            try
            {
                phase = "Start";
                await host.StartAsync(deadline.Token);
                phase = "AwaitGo";
                Console.WriteLine("READY");
                var go = await Console.In.ReadLineAsync().WaitAsync(deadline.Token);
                if (go != "GO")
                    throw new InvalidOperationException("父进程未放行库存探针。");
                var request = new InventoryOrderRequest(args[1], "warehouse", args[2],
                    new[] { new InventoryOrderLine("a", quantity), new InventoryOrderLine("b", quantity) });
                phase = "Execute";
                var result = await host.Services.GetRequiredService<AdminInventoryOrderExecutor>()
                    .ExecuteAsync(request, "probe-user", "probe-trace", deadline.Token);
                Console.WriteLine($"RESULT|{Environment.ProcessId}|{result.Status}|{result.Duplicate}");
                if (args[0] == "after-commit")
                {
                    phase = "AfterCommit";
                    Console.WriteLine("COMMITTED");
                    await Task.Delay(Timeout.Infinite, deadline.Token);
                }
                return 0;
            }
            catch (Exception exception)
            {
                original = exception;
                throw;
            }
            finally
            {
                if (original == null)
                    phase = "Cleanup";
                await CleanupHostAsync(host, original);
            }
        }
        catch (Exception exception)
        {
            // 仅输出类型和阶段，不输出数据库或代理凭据及业务正文。
            ReportFailure(exception, phase);
            foreach (var cleanupPhase in new[] { "Stop", "Dispose" })
                if (exception.Data[$"Bing.InventoryProbe{cleanupPhase}Exception"] is Exception cleanupException)
                    ReportFailure(cleanupException, cleanupPhase);
            return 1;
        }
    }

    /// <summary>
    /// 停止并释放探针宿主，保留原始失败和独立清理错误。
    /// </summary>
    /// <param name="host">探针创建并拥有的宿主。</param>
    /// <param name="original">此前启动或业务执行的原始异常。</param>
    private static async Task CleanupHostAsync(IHost host, Exception original)
    {
        var failure = original;
        try { await host.StopAsync(CancellationToken.None); }
        catch (Exception exception)
        {
            if (failure == null)
                failure = exception;
            else
                failure.Data["Bing.InventoryProbeStopException"] = exception;
        }
        try
        {
            if (host is IAsyncDisposable asynchronous)
                await asynchronous.DisposeAsync();
            else
                host.Dispose();
        }
        catch (Exception exception)
        {
            if (failure == null)
                failure = exception;
            else
                failure.Data["Bing.InventoryProbeDisposeException"] = exception;
        }
        if (original == null && failure != null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }

    /// <summary>
    /// 输出不含连接、凭据或业务正文的探针失败诊断。
    /// </summary>
    /// <param name="exception">启动、业务或清理阶段的失败。</param>
    /// <param name="phase">失败所在阶段。</param>
    private static void ReportFailure(Exception exception, string phase)
    {
        var number = exception.GetType().GetProperty("Number")?.GetValue(exception) is int value
            ? (int?)value : null;
        var safePhase = phase is "Setup" or "Start" or "AwaitGo" or "Execute" or "AfterCommit" or
            "Cleanup" or "Stop" or "Dispose" ? phase : "Unknown";
        Console.Error.WriteLine($"INVENTORY_PROBE_FAILED|{exception.GetType().FullName}|Phase={safePhase}|" +
            $"Number={number?.ToString(CultureInfo.InvariantCulture)}|Outcome={exception.GetTransactionOutcome()}|" +
            $"DuplicateKey={ClassifyDuplicateKey(exception, number)}|Methods={GetFailureMethods(exception)}");
    }

    /// <summary>
    /// 从实际异常堆栈提取声明类型与方法名，不输出参数或源码路径。
    /// </summary>
    /// <param name="exception">具有实际抛出堆栈的失败。</param>
    /// <returns>仅含方法位置的诊断；没有堆栈时为 Unknown。</returns>
    private static string GetFailureMethods(Exception exception)
    {
        var methods = new List<string>();
        foreach (var frame in new StackTrace(exception, false).GetFrames() ?? Array.Empty<StackFrame>())
        {
            var method = frame.GetMethod();
            var type = method?.DeclaringType;
            if (method == null || type == null)
                continue;
            if (type.IsConstructedGenericType)
                type = type.GetGenericTypeDefinition();
            methods.Add(type.FullName + "." + method.Name);
        }
        return methods.Count == 0 ? "Unknown" : string.Join(";", methods.Distinct(StringComparer.Ordinal));
    }

    /// <summary>
    /// 将 MySQL 重复键名称映射为固定白名单分类，不输出重复值或未知标识。
    /// </summary>
    /// <param name="exception">仅用于读取错误末尾键名的实际异常。</param>
    /// <param name="number">已确认的整数错误编号。</param>
    /// <returns>固定表与键分类；非重复键为 NotDuplicate，未知键为 Unknown。</returns>
    private static string ClassifyDuplicateKey(Exception exception, int? number)
    {
        if (number != 1062)
            return "NotDuplicate";
        const string marker = " for key '";
        var message = exception.Message;
        var start = message.LastIndexOf(marker, StringComparison.Ordinal);
        if (start < 0 || start + marker.Length >= message.Length ||
            !message.EndsWith("'", StringComparison.Ordinal))
            return "Unknown";
        var key = message.Substring(start + marker.Length, message.Length - start - marker.Length - 1);
        return key switch
        {
            "PRIMARY" => "PrimaryWithoutTable",
            "bing_inventory_stock.PRIMARY" => "InventoryStockPrimary",
            "bing_inventory_request.PRIMARY" => "InventoryRequestPrimary",
            "bing_inventory_order.PRIMARY" => "InventoryOrderPrimary",
            "bing_audit_log.PRIMARY" => "AuditLogPrimary",
            "bing_event_consumption.PRIMARY" => "EventConsumptionPrimary",
            "cap.published.PRIMARY" => "CapPublishedPrimary",
            "cap.received.PRIMARY" => "CapReceivedPrimary",
            _ => "Unknown"
        };
    }

    /// <summary>
    /// 读取必要的专用测试环境配置。
    /// </summary>
    /// <param name="name">环境变量名。</param>
    /// <returns>非空配置值。</returns>
    private static string Required(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException("库存探针缺少必要配置。");

    /// <summary>
    /// 写入关键审计后等待父进程中断的测试存储。
    /// </summary>
    private sealed class BlockingAuditingStore : ITransactionalAuditingStore
    {
        /// <inheritdoc />
        public async Task SaveAsync(DbTransaction transaction, string auditId, AuditLogInfo auditInfo,
            CancellationToken cancellationToken = default)
        {
            await new DbAuditingStore(AuditLogDatabase.MySql)
                .SaveAsync(transaction, auditId, auditInfo, cancellationToken);
            Console.WriteLine("ACTIVE");
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
    }
}
