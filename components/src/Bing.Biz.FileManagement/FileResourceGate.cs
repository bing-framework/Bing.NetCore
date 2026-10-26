using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bing.Biz.FileManagement;

/// <summary>跨宿主资源准入；不把数据库租约误称为对已发出的远端 IO 的强制中断。</summary>
public sealed class FileResourceGate
{
    private readonly IFileManagementStore _store;
    private readonly FileManagementOptions _options;
    private readonly ILogger _logger;
    public FileResourceGate(IFileManagementStore store, FileManagementOptions options, ILogger<FileResourceGate>? logger = null)
    { _store = store; _options = options; _logger = logger ?? NullLogger<FileResourceGate>.Instance; }

    public async Task<FileIoPermit?> TryEnterAsync(IEnumerable<string> storeNames, bool background, CancellationToken ct = default)
    {
        var leases = new List<FileIoLease>();
        try
        {
            foreach (var name in storeNames.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal))
            {
                if (!_options.StoreIoLimits.TryGetValue(name, out var limits)) continue;
                var limit = background ? limits.BackgroundOperations : limits.LocalParts;
                if (limit == 0) continue;
                var now = FileManagementService.Now;
                var lease = new FileIoLease { Id = Guid.NewGuid(), Token = Guid.NewGuid().ToString("N"),
                    PoolKey = (background ? "background:" : "parts:") + name, ExpiresAt = now + (long)_options.ResourceLease.TotalMilliseconds };
                if (!await _store.TryAcquireIoLeaseAsync(lease, limit, now, ct))
                {
                    await ReleaseAsync(leases); return null;
                }
                leases.Add(lease);
            }
            return new FileIoPermit(_store, leases, _options.ResourceLease, _logger, ct);
        }
        catch { await ReleaseAsync(leases); throw; }
    }
    private async Task ReleaseAsync(IEnumerable<FileIoLease> leases)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        foreach (var lease in leases)
            try { await _store.ReleaseIoLeaseAsync(lease.Id, lease.Token, timeout.Token); }
            catch (Exception ex) { _logger.LogWarning("资源槽释放未确认：{ErrorType}，等待租约到期。", ex.GetType().Name); }
    }
}

public sealed class FileIoPermit : IAsyncDisposable
{
    private readonly IFileManagementStore _store;
    private readonly IReadOnlyList<FileIoLease> _leases;
    private readonly CancellationTokenSource _work, _stop;
    private readonly ILogger _logger;
    private readonly Task _heartbeat;
    private int _disposed;
    internal FileIoPermit(IFileManagementStore store, IReadOnlyList<FileIoLease> leases, TimeSpan lifetime, ILogger logger, CancellationToken ct)
    {
        _store = store; _leases = leases; _logger = logger;
        _work = CancellationTokenSource.CreateLinkedTokenSource(ct); _stop = new();
        _heartbeat = leases.Count == 0 ? Task.CompletedTask : RenewAsync(lifetime);
    }
    public CancellationToken Token => _work.Token;
    private async Task RenewAsync(TimeSpan lifetime)
    {
        try
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(lifetime.TotalMilliseconds / 3), _stop.Token);
                foreach (var lease in _leases)
                    if (!await _store.RenewIoLeaseAsync(lease.Id, lease.Token, FileManagementService.Now + (long)lifetime.TotalMilliseconds, _stop.Token))
                    { _work.Cancel(); return; }
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        catch { _work.Cancel(); }
    }
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _stop.Cancel(); await _heartbeat;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        foreach (var lease in _leases)
            try { await _store.ReleaseIoLeaseAsync(lease.Id, lease.Token, timeout.Token); }
            catch (Exception ex) { _logger.LogWarning("资源槽释放未确认：{ErrorType}，等待租约到期。", ex.GetType().Name); }
        _work.Dispose(); _stop.Dispose();
    }
}
