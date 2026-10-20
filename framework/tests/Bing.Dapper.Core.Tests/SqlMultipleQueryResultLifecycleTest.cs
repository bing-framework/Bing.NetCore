using Dapper;
using Bing.Data.Sql;
using Xunit;

namespace Bing.Dapper.Core.Tests;

/// <summary>
/// 多结果集结果对象的内部生命周期测试。
/// </summary>
public sealed class SqlMultipleQueryResultLifecycleTest
{
    /// <summary>
    /// 测试目的：公开异步释放链必须只释放 reader、异步 callback 和租约一次，并在 callback 开始时解除两个委托引用。
    /// </summary>
    [Fact]
    public async Task DisposeAsync_WhenStarted_ShouldReleaseReaderCallbacksAndLeaseExactlyOnce()
    {
        // Arrange
        var reader = CreateReader();
        var lease = new CountingLease();
        var readerDisposeCount = 0;
        var callbackStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackContinue = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var syncCallbackCount = 0;
        var asyncCallbackCount = 0;
        var result = new SqlMultipleQueryResult(reader, lease,
            (_, _) => syncCallbackCount++, async (_, _) =>
            {
                asyncCallbackCount++;
                callbackStarted.SetResult(true);
                await callbackContinue.Task.ConfigureAwait(false);
            }, _ => false, _ => readerDisposeCount++, _ =>
            {
                readerDisposeCount++;
                return Task.CompletedTask;
            });

        // Act
        var completion = result.DisposeAsync().AsTask();
        await callbackStarted.Task;

        // Assert
        Assert.Null(GetPrivateField(result, "_complete"));
        Assert.Null(GetPrivateField(result, "_completeAsync"));
        callbackContinue.SetResult(true);
        await completion;
        await result.DisposeAsync();
        Assert.Equal(0, syncCallbackCount);
        Assert.Equal(1, asyncCallbackCount);
        Assert.Equal(1, readerDisposeCount);
        Assert.Equal(1, lease.DisposeCount);
    }

    /// <summary>
    /// 测试目的：公开异步释放完成后，completion callback 捕获对象不得继续由结果对象保留。
    /// </summary>
    [Fact]
    public async Task DisposeAsync_WhenCompleted_ShouldReleaseCallbackCapture()
    {
        // Arrange
        var reference = await CreateAndDisposeResultAsync();

        // Act
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Assert
        Assert.False(reference.IsAlive);
    }

    /// <summary>
    /// 测试目的：公开同步释放链必须保留 reader 主异常，并按 callback、lease 顺序聚合清理异常。
    /// </summary>
    [Fact]
    public void Dispose_WhenCleanupFails_ShouldPreservePrimaryAndCleanupExceptionOrder()
    {
        // Arrange
        var primary = new InvalidOperationException("reader dispose failed");
        var completionException = new ApplicationException("completion failed");
        var leaseException = new ObjectDisposedException("lease");
        var lease = new CountingLease(leaseException);
        var reader = CreateReader();
        var readerDisposeCount = 0;
        var callbackCount = 0;
        var result = new SqlMultipleQueryResult(reader, lease, (_, _) =>
        {
            callbackCount++;
            throw completionException;
        }, (_, _) => Task.CompletedTask, _ => false, _ =>
        {
            readerDisposeCount++;
            throw primary;
        }, _ => Task.CompletedTask);

        // Act
        var exception = Assert.Throws<AggregateException>(() => result.Dispose());

        // Assert
        Assert.Collection(exception.InnerExceptions,
            current => Assert.Same(primary, current),
            current => Assert.Same(completionException, current),
            current => Assert.Same(leaseException, current));
        Assert.Equal(1, readerDisposeCount);
        Assert.Equal(1, callbackCount);
        Assert.Equal(1, lease.DisposeCount);
        Assert.Null(GetPrivateField(result, "_complete"));
        Assert.Null(GetPrivateField(result, "_completeAsync"));
    }

    /// <summary>
    /// 创建未初始化的 Dapper 多结果读取器测试对象。
    /// </summary>
    /// <returns>未初始化的多结果读取器。</returns>
    private static SqlMapper.GridReader CreateReader() =>
        (SqlMapper.GridReader)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(
            typeof(SqlMapper.GridReader));

    /// <summary>
    /// 读取结果对象中指定的私有字段。
    /// </summary>
    /// <param name="result">要检查的多结果查询结果。</param>
    /// <param name="name">私有字段名称。</param>
    /// <returns>字段值；字段不存在时返回 <see langword="null"/>。</returns>
    private static object GetPrivateField(SqlMultipleQueryResult result, string name) =>
        typeof(SqlMultipleQueryResult).GetField(name,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(result);

    /// <summary>
    /// 创建并异步释放持有回调对象的查询结果。
    /// </summary>
    /// <returns>指向回调对象的弱引用。</returns>
    private static async Task<WeakReference> CreateAndDisposeResultAsync()
    {
        var holder = new object();
        var reference = new WeakReference(holder);
        var result = new SqlMultipleQueryResult(CreateReader(), new CountingLease(), (_, _) => holder.GetHashCode(),
            (_, _) => Task.CompletedTask, _ => false, _ => { }, _ => Task.CompletedTask);
        await result.DisposeAsync();
        return reference;
    }

    /// <summary>
    /// 记录释放次数并可选择抛出异常的测试租约。
    /// </summary>
    private sealed class CountingLease : IDisposable
    {
        /// <summary>
        /// 租约释放时要抛出的异常。
        /// </summary>
        private readonly Exception _exception;

        /// <summary>
        /// 初始化一个 <see cref="CountingLease"/> 类型的实例。
        /// </summary>
        /// <param name="exception">释放时要抛出的异常；未指定时不抛出异常。</param>
        public CountingLease(Exception exception = null) => _exception = exception;

        /// <summary>
        /// 获取或设置租约释放次数。
        /// </summary>
        public int DisposeCount { get; private set; }

        /// <inheritdoc />
        public void Dispose()
        {
            DisposeCount++;
            if (_exception != null)
                throw _exception;
        }
    }
}
