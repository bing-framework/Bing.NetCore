using System.Reflection;
using System.Globalization;
using System.Runtime.CompilerServices;
using Bing.Data.Transaction;
using Microsoft.Extensions.Hosting;
using MySqlConnector;
using Xunit;

#if BING_ADMIN_EFCORE_TESTS
namespace Bing.Admin.EFCore.Tests;
#else
namespace Bing.Admin.FreeSQL.Tests;
#endif

/// <summary>
/// 验证库存独立进程探针的宿主停止和资源释放契约。
/// </summary>
public sealed class AdminInventoryProbeCleanupTest
{
    /// <summary>
    /// 串行化本类短暂替换标准错误输出的诊断调用。
    /// </summary>
    private static readonly object DiagnosticErrorLock = new();

    /// <summary>
    /// 已知重复键只输出固定分类、实际错误编号和安全方法位置。
    /// </summary>
    /// <param name="key">固定白名单中的表与键名称。</param>
    /// <param name="category">允许输出的固定分类。</param>
    [Theory]
    [InlineData("PRIMARY", "PrimaryWithoutTable")]
    [InlineData("bing_inventory_stock.PRIMARY", "InventoryStockPrimary")]
    [InlineData("bing_inventory_request.PRIMARY", "InventoryRequestPrimary")]
    [InlineData("bing_inventory_order.PRIMARY", "InventoryOrderPrimary")]
    [InlineData("bing_audit_log.PRIMARY", "AuditLogPrimary")]
    [InlineData("bing_event_consumption.PRIMARY", "EventConsumptionPrimary")]
    [InlineData("cap.published.PRIMARY", "CapPublishedPrimary")]
    [InlineData("cap.received.PRIMARY", "CapReceivedPrimary")]
    public void ReportFailure_WhenDuplicateKeyIsKnown_ShouldEmitOnlyWhitelistedCategory(string key, string category)
    {
        const string secret = "diagnostic-secret-password-and-duplicate-value";
        var exception = CreateDiagnosticMySqlFailure(1062,
            $"Duplicate entry '{secret}; SQL=INSERT; C:\\secret\\probe.cs' for key '{key}'");
        exception.Data["Secret"] = secret;
        var output = CaptureThrownDiagnostic(exception, secret);

        Assert.Contains("MySqlConnector.MySqlException", output);
        Assert.Contains("Number=1062|Outcome=RolledBack", output);
        Assert.Contains("DuplicateKey=" + category + "|Methods=", output);
        Assert.Contains("AdminInventoryProbeCleanupTest.ThrowDiagnosticFailure", output);
        Assert.DoesNotContain(secret, output);
        Assert.DoesNotContain("SQL=", output);
        Assert.DoesNotContain("C:\\secret", output);
        Assert.DoesNotContain("secretArgument", output);
        Assert.DoesNotContain("(", output);
    }

    /// <summary>
    /// 未知键不输出其名称，也不把重复值中的白名单文本误认成键。
    /// </summary>
    [Fact]
    public void ReportFailure_WhenDuplicateKeyIsUnknown_ShouldHideKeyAndDuplicateValue()
    {
        const string secret = "secret-unknown-schema-key";
        var exception = CreateDiagnosticMySqlFailure(1062,
            $"Duplicate entry 'secret-value for key 'cap.published.PRIMARY'' for key '{secret}'");

        var output = CaptureThrownDiagnostic(exception, "secret-argument");

        Assert.Contains("DuplicateKey=Unknown|Methods=", output);
        Assert.DoesNotContain(secret, output);
        Assert.DoesNotContain("secret-value", output);
        Assert.DoesNotContain("secret-argument", output);
        Assert.DoesNotContain("CapPublishedPrimary", output);
        Assert.DoesNotContain("Duplicate entry", output);
    }

    /// <summary>
    /// 非重复键编号不从异常正文推断分类，未知阶段也不原样输出。
    /// </summary>
    [Fact]
    public void ReportFailure_WhenNumberIsNotDuplicate_ShouldIgnoreMessageAndUnknownPhase()
    {
        var exception = CreateDiagnosticMySqlFailure(1213,
            "secret-message for key 'cap.published.PRIMARY'");

        var output = CaptureReportFailure(exception, "secret-phase");

        Assert.Contains("Phase=Unknown|Number=1213", output);
        Assert.Contains("DuplicateKey=NotDuplicate|Methods=Unknown", output);
        Assert.DoesNotContain("secret-message", output);
        Assert.DoesNotContain("secret-phase", output);
        Assert.DoesNotContain("CapPublishedPrimary", output);
    }

    /// <summary>
    /// 不完整或带尾部正文的重复键消息只返回未知分类，不中断安全诊断。
    /// </summary>
    /// <param name="message">不能确认末尾键名的测试消息。</param>
    [Theory]
    [InlineData("Duplicate entry 'secret-value' for key '")]
    [InlineData("Duplicate entry 'secret-value' for key 'cap.published.PRIMARY' SQL=secret-value")]
    public void ReportFailure_WhenKeySuffixIsMalformed_ShouldEmitUnknownWithoutMessage(string message)
    {
        var exception = CreateDiagnosticMySqlFailure(1062, message);

        var output = CaptureReportFailure(exception, "Execute");

        Assert.Contains("DuplicateKey=Unknown|Methods=Unknown", output);
        Assert.DoesNotContain("secret-value", output);
        Assert.DoesNotContain("SQL=", output);
        Assert.DoesNotContain("CapPublishedPrimary", output);
    }

    /// <summary>
    /// 使用已有驱动内部构造形状创建真实异常，不连接数据库。
    /// </summary>
    /// <param name="number">用于验证分类的错误编号。</param>
    /// <param name="message">包含测试秘密标记的异常正文。</param>
    /// <returns>携带明确回滚结果的实际驱动异常。</returns>
    private static MySqlException CreateDiagnosticMySqlFailure(int number, string message)
    {
        var constructor = typeof(MySqlException).GetConstructor(BindingFlags.Instance | BindingFlags.Public |
            BindingFlags.NonPublic, null, new[] { typeof(MySqlErrorCode), typeof(string) }, null);
        Assert.NotNull(constructor);
        var exception = (MySqlException)constructor.Invoke(new object[] { (MySqlErrorCode)number, message });
        exception.SetTransactionOutcome(TransactionOutcome.RolledBack);
        return exception;
    }

    /// <summary>
    /// 实际抛出驱动异常后调用探针诊断，保留可核对的方法堆栈。
    /// </summary>
    /// <param name="exception">包含安全测试标记的实际驱动异常。</param>
    /// <param name="secretArgument">不得进入诊断的方法参数值。</param>
    /// <returns>实际探针写入的标准错误文本。</returns>
    private static string CaptureThrownDiagnostic(MySqlException exception, string secretArgument)
    {
        try
        {
            ThrowDiagnosticFailure(exception, secretArgument);
        }
        catch (MySqlException actual)
        {
            return CaptureReportFailure(actual, "Execute");
        }
        throw new InvalidOperationException("测试没有抛出预期的驱动异常。");
    }

    /// <summary>
    /// 固定实际抛出方法帧，防止优化内联使诊断职责断言失去方法位置。
    /// </summary>
    /// <param name="exception">需要实际抛出的驱动异常。</param>
    /// <param name="secretArgument">只用于验证不输出参数的测试标记。</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowDiagnosticFailure(MySqlException exception, string secretArgument)
    {
        _ = secretArgument;
        throw exception;
    }

    /// <summary>
    /// 反射调用已编译探针的实际诊断方法，并在 finally 恢复标准错误。
    /// </summary>
    /// <param name="exception">交给实际探针分类的异常。</param>
    /// <param name="phase">交给实际探针分类的阶段。</param>
    /// <returns>不经过复制诊断算法的实际输出。</returns>
    private static string CaptureReportFailure(Exception exception, string phase)
    {
        var program = LoadProbeProgram();
        var method = program.GetMethod("ReportFailure", BindingFlags.Static | BindingFlags.NonPublic,
            binder: null, types: new[] { typeof(Exception), typeof(string) }, modifiers: null);
        Assert.NotNull(method);
        lock (DiagnosticErrorLock)
        {
            var previous = Console.Error;
            using var writer = new StringWriter(CultureInfo.InvariantCulture);
            try
            {
                Console.SetError(writer);
                method.Invoke(null, new object[] { exception, phase });
                return writer.ToString();
            }
            finally
            {
                Console.SetError(previous);
            }
        }
    }

    /// <summary>
    /// 加载当前 ORM 的已编译库存探针类型，不执行探针入口。
    /// </summary>
    /// <returns>提供实际诊断及清理方法的探针类型。</returns>
    private static Type LoadProbeProgram()
    {
#if BING_ADMIN_EFCORE_TESTS
        const string assemblyName = "Bing.Admin.EFCore.InventoryProbe.dll";
#else
        const string assemblyName = "Bing.Admin.FreeSQL.InventoryProbe.dll";
#endif
        var path = Path.Combine(AppContext.BaseDirectory, "InventoryProbe", assemblyName);
        Assert.True(File.Exists(path), "库存独立进程探针尚未复制。");
        return Assembly.LoadFrom(path).GetType("Bing.Admin.InventoryProbe.Program", throwOnError: true);
    }
    /// <summary>
    /// 原始业务失败时保留停止和释放异常并完成两项清理尝试。
    /// </summary>
    [Fact]
    public async Task CleanupHostAsync_WhenExecutionAndCleanupFail_ShouldPreserveOriginalAndBothCleanupFailures()
    {
        var original = new InvalidOperationException("原始业务失败。");
        var stopFailure = new ApplicationException("宿主停止失败。");
        var disposeFailure = new InvalidOperationException("异步释放失败。");
        var host = new AsyncTestHost(stopFailure, disposeFailure);

        await InvokeCleanupHostAsync(host, original);

        Assert.Same(stopFailure, original.Data["Bing.InventoryProbeStopException"]);
        Assert.Same(disposeFailure, original.Data["Bing.InventoryProbeDisposeException"]);
        Assert.Equal(1, host.StopCalls);
        Assert.Equal(CancellationToken.None, host.StopToken);
        Assert.Equal(1, host.AsyncDisposeCalls);
        Assert.Equal(0, host.DisposeCalls);
    }

    /// <summary>
    /// 没有业务失败时抛出原始停止异常并附加释放异常。
    /// </summary>
    [Fact]
    public async Task CleanupHostAsync_WhenOnlyCleanupFails_ShouldThrowStopFailureAndPreserveDisposeFailure()
    {
        var stopFailure = new InvalidOperationException("宿主停止失败。");
        var disposeFailure = new ApplicationException("异步释放失败。");
        var host = new AsyncTestHost(stopFailure, disposeFailure);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => InvokeCleanupHostAsync(host, null));

        Assert.Same(stopFailure, actual);
        Assert.Same(disposeFailure, actual.Data["Bing.InventoryProbeDisposeException"]);
        Assert.Equal(1, host.StopCalls);
        Assert.Equal(1, host.AsyncDisposeCalls);
        Assert.Equal(0, host.DisposeCalls);
    }

    /// <summary>
    /// 异步释放尚未结束时保持等待且不调用同步释放路径。
    /// </summary>
    [Fact]
    public async Task CleanupHostAsync_WhenAsyncDisposeIsPending_ShouldAwaitItWithoutSyncDispose()
    {
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var host = new AsyncTestHost(disposeRelease: release.Task);
        var cleanup = InvokeCleanupHostAsync(host, null);

        try
        {
            await host.DisposeEntered.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(cleanup.IsCompleted);
            Assert.Equal(1, host.StopCalls);
            Assert.Equal(1, host.AsyncDisposeCalls);
            Assert.Equal(0, host.AsyncDisposeCompletions);
            Assert.Equal(0, host.DisposeCalls);
        }
        finally
        {
            release.TrySetResult(true);
        }

        await cleanup.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, host.AsyncDisposeCompletions);
        Assert.Equal(1, host.AsyncDisposeCalls);
        Assert.Equal(0, host.DisposeCalls);
    }

    /// <summary>
    /// 宿主只支持同步释放时停止并释放一次。
    /// </summary>
    [Fact]
    public async Task CleanupHostAsync_WhenHostSupportsOnlySyncDispose_ShouldUseSyncPathOnce()
    {
        var host = new TestHost();

        await InvokeCleanupHostAsync(host, null);

        Assert.Equal(1, host.StopCalls);
        Assert.Equal(CancellationToken.None, host.StopToken);
        Assert.Equal(1, host.DisposeCalls);
    }

    /// <summary>
    /// 加载当前 ORM 的实际探针并调用其宿主清理方法。
    /// </summary>
    /// <param name="host">验证清理行为的最小宿主。</param>
    /// <param name="original">原始业务异常；业务成功时为空。</param>
    /// <remarks>直接调用已编译探针的方法，不复制清理算法，也不执行探针入口或连接数据库。</remarks>
    private static async Task InvokeCleanupHostAsync(IHost host, Exception original)
    {
#if BING_ADMIN_EFCORE_TESTS
        const string assemblyName = "Bing.Admin.EFCore.InventoryProbe.dll";
#else
        const string assemblyName = "Bing.Admin.FreeSQL.InventoryProbe.dll";
#endif
        var path = Path.Combine(AppContext.BaseDirectory, "InventoryProbe", assemblyName);
        Assert.True(File.Exists(path), "库存独立进程探针尚未复制。");
        var assembly = Assembly.LoadFrom(path);
        var program = assembly.GetType("Bing.Admin.InventoryProbe.Program", throwOnError: true);
        var method = program.GetMethod("CleanupHostAsync", BindingFlags.Static | BindingFlags.NonPublic,
            binder: null, types: new[] { typeof(IHost), typeof(Exception) }, modifiers: null);
        Assert.NotNull(method);
        var cleanup = Assert.IsAssignableFrom<Task>(method.Invoke(null, new object[] { host, original }));
        await cleanup;
    }

    /// <summary>
    /// 记录停止及同步释放次数的最小宿主。
    /// </summary>
    private class TestHost : IHost
    {
        /// <summary>
        /// 停止阶段需要传播的预设异常。
        /// </summary>
        private readonly Exception _stopFailure;

        /// <summary>
        /// 初始化一个 <see cref="TestHost" /> 类型的实例。
        /// </summary>
        /// <param name="stopFailure">停止阶段预设异常；为空时停止成功。</param>
        internal TestHost(Exception stopFailure = null) => _stopFailure = stopFailure;

        /// <inheritdoc />
        public IServiceProvider Services => throw new NotSupportedException("清理测试不解析服务。");

        /// <summary>
        /// 获取停止调用次数。
        /// </summary>
        internal int StopCalls { get; private set; }

        /// <summary>
        /// 获取停止时收到的取消令牌。
        /// </summary>
        internal CancellationToken StopToken { get; private set; }

        /// <summary>
        /// 获取同步释放调用次数。
        /// </summary>
        internal int DisposeCalls { get; private set; }

        /// <inheritdoc />
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        /// <inheritdoc />
        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            StopCalls++;
            StopToken = cancellationToken;
            return _stopFailure == null ? Task.CompletedTask : Task.FromException(_stopFailure);
        }

        /// <inheritdoc />
        public void Dispose() => DisposeCalls++;
    }

    /// <summary>
    /// 提供可等待、可失败异步释放的最小宿主。
    /// </summary>
    private sealed class AsyncTestHost : TestHost, IAsyncDisposable
    {
        /// <summary>
        /// 异步释放阶段需要传播的预设异常。
        /// </summary>
        private readonly Exception _disposeFailure;

        /// <summary>
        /// 允许异步释放完成的可选等待任务。
        /// </summary>
        private readonly Task _disposeRelease;

        /// <summary>
        /// 异步释放开始执行的完成信号。
        /// </summary>
        private readonly TaskCompletionSource<bool> _disposeEntered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 初始化一个 <see cref="AsyncTestHost" /> 类型的实例。
        /// </summary>
        /// <param name="stopFailure">停止阶段预设异常；为空时停止成功。</param>
        /// <param name="disposeFailure">异步释放预设异常；为空时释放成功。</param>
        /// <param name="disposeRelease">异步释放完成前等待的任务；为空时无需等待。</param>
        internal AsyncTestHost(Exception stopFailure = null, Exception disposeFailure = null,
            Task disposeRelease = null) : base(stopFailure)
        {
            _disposeFailure = disposeFailure;
            _disposeRelease = disposeRelease;
        }

        /// <summary>
        /// 获取异步释放进入信号的等待任务。
        /// </summary>
        internal Task DisposeEntered => _disposeEntered.Task;

        /// <summary>
        /// 获取异步释放调用次数。
        /// </summary>
        internal int AsyncDisposeCalls { get; private set; }

        /// <summary>
        /// 获取异步释放成功完成次数。
        /// </summary>
        internal int AsyncDisposeCompletions { get; private set; }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            AsyncDisposeCalls++;
            _disposeEntered.TrySetResult(true);
            if (_disposeRelease != null)
                await _disposeRelease;
            if (_disposeFailure != null)
                throw _disposeFailure;
            AsyncDisposeCompletions++;
        }
    }
}
