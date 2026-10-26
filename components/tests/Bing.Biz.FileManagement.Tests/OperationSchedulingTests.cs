using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using Bing.Biz.FileManagement.AspNetCore;
using Bing.FileStorage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Biz.FileManagement.Tests;

/// <summary>通过实际 HostedService、SQLite 与内容 IO 验证持续调度，使用可取消闸门控制交错。</summary>
public sealed class OperationSchedulingTests
{
    private readonly ITestOutputHelper _output;
    public OperationSchedulingTests(ITestOutputHelper output) { _output = output; }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task LongTask_DoesNotBlockNewShortTasksOrMaintenance_AndRenewsLease(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql); await env.InitializeAsync();
        var files = env.Get<FileManagementService>(); var store = env.Get<IFileManagementStore>();
        var toDelete = await env.UploadAsync(Array.Empty<byte>());
        var expiring = await files.CreateUploadAsync(NewUpload()); await env.RunAsync();
        var longUpload = await PrepareCompletionAsync(env);
        var probe = new SchedulingProbe(1, 1); probe.BlockedSessions.Add(longUpload.Id.ToString("N"));
        await using var worker = await SchedulingHarness.StartAsync(env, probe, 1, 1);
        await probe.Saves.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));

        // 两项请求都在长任务开始阻塞后才进入数据库，不能只证明同一旧批次内的并行。
        var later = await files.CreateUploadAsync(NewUpload());
        await WaitUntilAsync(async () => (await store.FindAsync(later.FileId))!.File.State == FileState.Uploading);
        await files.DeleteAsync(toDelete.FileId);
        await WaitUntilAsync(async () => (await store.FindAsync(toDelete.FileId))!.File.State == FileState.Deleted);
        var expired = (await store.FindSessionAsync(expiring.Id))!; expired.Session!.ExpiresAt = 0;
        Assert.True(await store.TryUpdateAsync(expired, expired.File.Version));
        await WaitUntilAsync(async () => (await store.FindAsync(expiring.FileId))!.File.State == FileState.Aborted);
        await probe.Renewed.Task.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.Equal(FileState.Completing, (await store.FindAsync(longUpload.FileId))!.File.State);
        Assert.False(probe.Saves.Release.Task.IsCompleted);

        probe.Saves.Release.TrySetResult(true);
        await WaitUntilAsync(async () => (await store.FindAsync(longUpload.FileId))!.File.State == FileState.Ready);
        await worker.StopAsync();
        Assert.Equal(1, probe.Saves.MaxActive); Assert.Equal(0, probe.Saves.Active);
        Assert.True(probe.MaintenanceCalls > 0); AssertScopes(probe);
        _output.WriteLine("Requested long concurrency=1; max actual long IO=1; later short tasks and maintenance progressed before release.");
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task BothQueues_RespectConfiguredBounds_WithoutPreclaimingWaitingWork(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql); await env.InitializeAsync();
        var files = env.Get<FileManagementService>(); var store = env.Get<IFileManagementStore>();
        var deletes = new List<UploadSessionView>();
        for (var i = 0; i < 5; i++) deletes.Add(await env.UploadAsync(Array.Empty<byte>()));
        var completions = new List<UploadSessionView>();
        // 先完成所有初始化，再一次性排队，避免手动批处理提前执行前一项 Complete。
        for (var i = 0; i < 5; i++) completions.Add(await files.CreateUploadAsync(NewUpload()));
        await env.RunAsync();
        var probe = new SchedulingProbe(2, 2);
        foreach (var upload in completions)
        { await files.CompleteUploadAsync(upload.Id); probe.BlockedSessions.Add(upload.Id.ToString("N")); }
        foreach (var upload in deletes)
        {
            var entry = (await store.FindAsync(upload.FileId))!;
            probe.BlockedDeleteKeys.Add(entry.File.ObjectKey); await files.DeleteAsync(upload.FileId);
        }
        await using var worker = await SchedulingHarness.StartAsync(env, probe, 2, 2);
        await Task.WhenAll(probe.Saves.Entered.Task, probe.Deletes.Entered.Task).WaitAsync(TimeSpan.FromSeconds(20));
        var operations = await ReviewRows.ReadAsync<FileOperation>(env);
        Assert.Equal(2, operations.Count(x => x.Kind == FileOperationKind.Complete && x.State == FileOperationState.Running));
        Assert.Equal(2, operations.Count(x => x.Kind == FileOperationKind.Delete && x.State == FileOperationState.Running));
        Assert.Equal(3, operations.Count(x => x.Kind == FileOperationKind.Complete && x.State == FileOperationState.Pending && x.Attempts == 0));
        Assert.Equal(3, operations.Count(x => x.Kind == FileOperationKind.Delete && x.State == FileOperationState.Pending && x.Attempts == 0));
        await Assert.ThrowsAsync<InvalidOperationException>(() => worker.Provider.GetRequiredService<FileOperationScheduler>().RunAsync());

        probe.Saves.Release.TrySetResult(true); probe.Deletes.Release.TrySetResult(true);
        await WaitUntilAsync(async () =>
        {
            foreach (var upload in completions) if ((await store.FindAsync(upload.FileId))!.File.State != FileState.Ready) return false;
            foreach (var upload in deletes) if ((await store.FindAsync(upload.FileId))!.File.State != FileState.Deleted) return false;
            return true;
        });
        await worker.StopAsync();
        Assert.Equal(2, probe.Saves.MaxActive); Assert.Equal(2, probe.Deletes.MaxActive);
        Assert.Equal(0, probe.Saves.Active); Assert.Equal(0, probe.Deletes.Active); AssertScopes(probe);
        _output.WriteLine("Requested long=2, short=2; max actual gated IO long=2, short=2; queued unclaimed=3 per lane.");
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Cancellation_ReleasesScopes_AndExpiredLeaseCompletesAfterRestart(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql); await env.InitializeAsync();
        var upload = await PrepareCompletionAsync(env);
        var probe = new SchedulingProbe(1, 1); probe.BlockedSessions.Add(upload.Id.ToString("N"));
        await using (var worker = await SchedulingHarness.StartAsync(env, probe, 1, 1))
        {
            await probe.Saves.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await worker.StopAsync();
            Assert.Equal(1, probe.Saves.Cancellations); Assert.Equal(0, probe.Saves.Active); AssertScopes(probe);
            Assert.Equal(FileState.Completing, (await env.Get<IFileManagementStore>().FindAsync(upload.FileId))!.File.State);
            var operation = Assert.Single(await ReviewRows.ReadAsync<FileOperation>(env), x => x.Kind == FileOperationKind.Complete);
            Assert.Equal(FileOperationState.Running, operation.State); Assert.Equal(1, operation.Attempts);
        }
        // 仅推进持久租约的逻辑时间，不忽略实际 IO 取消，也不等待租约自然过期。
        await ReviewRows.SqlAsync(env, "UPDATE BingFileOperations SET LeaseUntil=0 WHERE State=1");
        await env.RestartAsync();
        var after = new SchedulingProbe(1, 1);
        await using (var restarted = await SchedulingHarness.StartAsync(env, after, 1, 1))
        {
            await WaitUntilAsync(async () => (await env.Get<IFileManagementStore>().FindAsync(upload.FileId))!.File.State == FileState.Ready);
            await WaitUntilAsync(async () => (await ReviewRows.ReadAsync<FileOperation>(env)).Any(x => x.Kind == FileOperationKind.Complete && x.State == FileOperationState.Succeeded));
            await restarted.StopAsync(); AssertScopes(after);
        }
        var final = (await env.Get<IFileManagementStore>().FindAsync(upload.FileId))!;
        var attempts = await ReviewRows.ReadAsync<FileTransferAttempt>(env);
        var published = Assert.Single(attempts, x => x.State == FileTransferState.Published);
        Assert.Equal(final.File.ObjectKey, published.ObjectKey);
        foreach (var stale in attempts.Where(x => x.State != FileTransferState.Published))
        {
            Assert.Equal(FileTransferState.Retired, stale.State); // 未确认写完，继续保留迟到写入回收凭据。
            Assert.Null(await env.Get<IFileStorageFactory>().GetStorage(stale.StoreName, final.File.Scope()).GetPropertiesAsync(new(stale.Container, stale.ObjectKey)));
        }
        Assert.Equal(2, Assert.Single(await ReviewRows.ReadAsync<FileOperation>(env), x => x.Kind == FileOperationKind.Complete).Attempts);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CompetingSchedulers_AndQueryFailure_KeepOneLeaseWinner(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql); await env.InitializeAsync();
        var upload = await PrepareCompletionAsync(env);
        var probe = new SchedulingProbe(1, 1) { FailNextQuery = 1 };
        probe.BlockedSessions.Add(upload.Id.ToString("N"));
        await using var first = await SchedulingHarness.StartAsync(env, probe, 1, 1);
        await using var second = await SchedulingHarness.StartAsync(env, probe, 1, 1);
        await probe.Saves.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
        await WaitUntilAsync(() => Task.FromResult(probe.QueueQueries >= 8));
        Assert.Equal(1, probe.Saves.TotalEntered); Assert.Equal(1, probe.Saves.MaxActive);
        probe.Saves.Release.TrySetResult(true);
        await WaitUntilAsync(async () => (await env.Get<IFileManagementStore>().FindAsync(upload.FileId))!.File.State == FileState.Ready);
        await WaitUntilAsync(async () => (await ReviewRows.ReadAsync<FileOperation>(env)).Any(x => x.Kind == FileOperationKind.Complete && x.State == FileOperationState.Succeeded));
        await first.StopAsync(); await second.StopAsync(); AssertScopes(probe);
        var op = Assert.Single(await ReviewRows.ReadAsync<FileOperation>(env), x => x.Kind == FileOperationKind.Complete);
        Assert.Equal(1, op.Attempts); Assert.Equal(1, probe.Claims.Count(x => x.OperationId == op.Id));
        Assert.Equal(0, probe.FailNextQuery);
    }

    [MinioUploadTheory]
    [InlineData(false)] [InlineData(true)]
    public async Task Minio_HostedScheduler_CompletesSignedUploadAndDeletion(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql, minio: true); await env.InitializeAsync();
        var probe = new SchedulingProbe(1, 1);
        await using var worker = await SchedulingHarness.StartAsync(env, probe, 1, 1, wrapStorage: false);
        var files = env.Get<FileManagementService>(); var store = env.Get<IFileManagementStore>();
        var upload = await files.CreateUploadAsync(new() { RequestId = Guid.NewGuid(), FileName = "scheduled.bin", Length = 1 });
        await WaitUntilAsync(async () => (await store.FindAsync(upload.FileId))!.File.State == FileState.Uploading);
        var bytes = new byte[] { 42 };
        var signed = await files.CreatePartRequestAsync(upload.Id, new() { Number = 1, Sha256 = Convert.ToBase64String(SHA256.HashData(bytes)) });
        using var http = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Put, signed!.Url) { Content = new ByteArrayContent(bytes) };
        request.Headers.TryAddWithoutValidation("x-amz-checksum-sha256", signed.Headers["x-amz-checksum-sha256"]);
        using var response = await http.SendAsync(request); response.EnsureSuccessStatusCode();
        await files.CompleteUploadAsync(upload.Id);
        await WaitUntilAsync(async () => (await store.FindAsync(upload.FileId))!.File.State == FileState.Ready);
        var ready = (await store.FindAsync(upload.FileId))!.File;
        var storage = env.Get<IFileStorageFactory>().GetStorage(ready.StoreName, ready.Scope());
        Assert.Equal(1, (await storage.GetPropertiesAsync(ready.Address()))!.Length);
        await files.DeleteAsync(upload.FileId);
        await WaitUntilAsync(async () => (await store.FindAsync(upload.FileId))!.File.State == FileState.Deleted);
        Assert.Null(await storage.GetPropertiesAsync(ready.Address()));
        await worker.StopAsync(); AssertScopes(probe);
    }

    [Theory]
    [InlineData(0, 1, 100)] [InlineData(17, 1, 100)]
    [InlineData(1, 0, 100)] [InlineData(1, 17, 100)]
    [InlineData(1, 1, 99)] [InlineData(1, 1, 60001)]
    public void Configuration_RejectsUnboundedOrBusyPolling(int shortConcurrency, int longConcurrency, int pollMilliseconds)
        => Assert.Throws<ArgumentException>(() => new ServiceCollection().AddBingFileManagement(o =>
        { o.ShortOperationConcurrency = shortConcurrency; o.LongOperationConcurrency = longConcurrency; o.OperationPollInterval = TimeSpan.FromMilliseconds(pollMilliseconds); }));

    private static CreateFileUpload NewUpload() => new() { RequestId = Guid.NewGuid(), FileName = "empty.bin", Length = 0 };
    private static async Task<UploadSessionView> PrepareCompletionAsync(FileTestEnvironment env)
    {
        var files = env.Get<FileManagementService>(); var upload = await files.CreateUploadAsync(NewUpload());
        await env.RunAsync(); await files.CompleteUploadAsync(upload.Id); return upload;
    }
    private static void AssertScopes(SchedulingProbe probe)
    {
        Assert.Equal(probe.ScopesCreated, probe.ScopesDisposed);
        var claims = probe.Claims.ToArray(); Assert.NotEmpty(claims);
        Assert.Equal(claims.Length, claims.Select(x => x.ScopeId).Distinct().Count());
    }
    private static async Task WaitUntilAsync(Func<Task<bool>> predicate)
    {
        var timer = Stopwatch.StartNew();
        while (!await predicate())
        {
            Assert.True(timer.Elapsed < TimeSpan.FromSeconds(20), "调度条件未在受控观察窗口内成立。");
            await Task.Delay(25);
        }
    }
}

internal sealed class SchedulingHarness : IAsyncDisposable
{
    private readonly IHostedService _worker;
    private bool _stopped;
    internal ServiceProvider Provider { get; }
    private SchedulingHarness(ServiceProvider provider, IHostedService worker) { Provider = provider; _worker = worker; }
    internal static async Task<SchedulingHarness> StartAsync(FileTestEnvironment env, SchedulingProbe probe, int shortConcurrency, int longConcurrency, bool wrapStorage = true)
    {
        var services = env.Configure(new ServiceCollection());
        var options = (FileManagementOptions)services.Single(x => x.ServiceType == typeof(FileManagementOptions)).ImplementationInstance!;
        options.ShortOperationConcurrency = shortConcurrency; options.LongOperationConcurrency = longConcurrency;
        options.OperationPollInterval = TimeSpan.FromMilliseconds(100); options.OperationLease = TimeSpan.FromSeconds(10);
        var original = services.Last(x => x.ServiceType == typeof(IFileManagementStore)); services.Remove(original);
        services.AddScoped<IFileManagementStore>(sp => SchedulingStoreProxy.Wrap(
            (IFileManagementStore)(original.ImplementationFactory?.Invoke(sp) ?? ActivatorUtilities.CreateInstance(sp, original.ImplementationType!)), probe));
        if (wrapStorage) services.AddSingleton<IFileStorageFactory>(new SchedulingStorageFactory(env.Get<IFileStorageFactory>(), probe));
        services.AddBingFileManagementWeb();
        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var worker = provider.GetServices<IHostedService>().Single(service =>
            service.GetType().FullName == "Bing.Biz.FileManagement.AspNetCore.FileManagementWorker");
        await worker.StartAsync(CancellationToken.None);
        return new SchedulingHarness(provider, worker);
    }
    internal async Task StopAsync()
    {
        if (_stopped) return;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await _worker.StopAsync(timeout.Token);
        await ((BackgroundService)_worker).ExecuteTask!.WaitAsync(timeout.Token);
        _stopped = true;
    }
    public async ValueTask DisposeAsync() { try { await StopAsync(); } finally { await Provider.DisposeAsync(); } }
}

public sealed class SchedulingProbe
{
    public SchedulingProbe(int expectedSaves, int expectedDeletes) { Saves = new(expectedSaves); Deletes = new(expectedDeletes); }
    public StorageGate Saves { get; }
    public StorageGate Deletes { get; }
    public HashSet<string> BlockedSessions { get; } = new();
    public HashSet<string> BlockedDeleteKeys { get; } = new();
    public ConcurrentQueue<(Guid OperationId, Guid ScopeId)> Claims { get; } = new();
    public TaskCompletionSource<bool> Renewed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int ScopesCreated, ScopesDisposed, QueueQueries, MaintenanceCalls, FailNextQuery;
}

public sealed class StorageGate
{
    private readonly int _expected;
    public StorageGate(int expected) { _expected = expected; }
    public TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int Active, MaxActive, TotalEntered, Cancellations;
    public async Task<T> RunAsync<T>(Func<Task<T>> commit, CancellationToken ct)
    {
        var active = Interlocked.Increment(ref Active); Interlocked.Increment(ref TotalEntered);
        int peak;
        do { peak = Volatile.Read(ref MaxActive); } while (active > peak && Interlocked.CompareExchange(ref MaxActive, active, peak) != peak);
        if (active >= _expected) Entered.TrySetResult(true);
        try { await Release.Task.WaitAsync(ct); ct.ThrowIfCancellationRequested(); return await commit(); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { Interlocked.Increment(ref Cancellations); throw; }
        finally { Interlocked.Decrement(ref Active); }
    }
}

public class SchedulingStoreProxy : DispatchProxy, IDisposable
{
    private IFileManagementStore _inner = null!;
    private SchedulingProbe _probe = null!;
    private readonly Guid _scopeId = Guid.NewGuid();
    private int _disposed;
    public static IFileManagementStore Wrap(IFileManagementStore inner, SchedulingProbe probe)
    {
        var result = Create<IFileManagementStore, SchedulingStoreProxy>(); var proxy = (SchedulingStoreProxy)result;
        proxy._inner = inner; proxy._probe = probe; Interlocked.Increment(ref probe.ScopesCreated); return result;
    }
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method!.Name == nameof(IFileManagementStore.ListDueOperationsAsync))
        {
            Interlocked.Increment(ref _probe.QueueQueries);
            if (Interlocked.Exchange(ref _probe.FailNextQuery, 0) == 1) throw new IOException("受控查询故障。");
        }
        if (method.Name == nameof(IFileManagementStore.ListExpiredSessionsAsync)) Interlocked.Increment(ref _probe.MaintenanceCalls);
        object? result;
        try { result = method.Invoke(_inner, args); }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        { ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); throw; }
        if (method.Name == nameof(IFileManagementStore.TryClaimAsync)) return OnClaimAsync((Task<bool>)result!, (Guid)args![0]!);
        if (method.Name == nameof(IFileManagementStore.RenewLeaseAsync)) return OnRenewAsync((Task<bool>)result!);
        return result;
    }
    private async Task<bool> OnClaimAsync(Task<bool> task, Guid id)
    { var result = await task; if (result) _probe.Claims.Enqueue((id, _scopeId)); return result; }
    private async Task<bool> OnRenewAsync(Task<bool> task)
    { var result = await task; if (result) _probe.Renewed.TrySetResult(true); return result; }
    public void Dispose() { if (Interlocked.Exchange(ref _disposed, 1) == 0) Interlocked.Increment(ref _probe.ScopesDisposed); }
}

internal sealed class SchedulingStorageFactory : IFileStorageFactory
{
    private readonly IFileStorageFactory _inner; private readonly SchedulingProbe _probe;
    public SchedulingStorageFactory(IFileStorageFactory inner, SchedulingProbe probe) { _inner = inner; _probe = probe; }
    public string DefaultStore => _inner.DefaultStore;
    public IFileStorage GetStorage(string storeName, StorageScope scope)
    {
        var storage = DispatchProxy.Create<IFileStorage, SchedulingStorageProxy>();
        var proxy = (SchedulingStorageProxy)storage; proxy.Inner = _inner.GetStorage(storeName, scope); proxy.Probe = _probe; return storage;
    }
}

public class SchedulingStorageProxy : DispatchProxy
{
    public IFileStorage Inner { get; set; } = null!;
    public SchedulingProbe Probe { get; set; } = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method!.Name == nameof(IFileStorage.SaveAsync) && ((FileWriteRequest)args![0]!).Metadata.TryGetValue("bing-session", out var session) && Probe.BlockedSessions.Contains(session))
            return Probe.Saves.RunAsync(() => (Task<FileWriteResult>)method.Invoke(Inner, args)!, (CancellationToken)args![2]!);
        if (method.Name == nameof(IFileStorage.DeleteAsync) && Probe.BlockedDeleteKeys.Contains(((FileAddress)args![0]!).Key))
            return Probe.Deletes.RunAsync(async () => { await (Task)method.Invoke(Inner, args)!; return true; }, (CancellationToken)args![1]!);
        try { return method.Invoke(Inner, args); }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        { ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); throw; }
    }
}
