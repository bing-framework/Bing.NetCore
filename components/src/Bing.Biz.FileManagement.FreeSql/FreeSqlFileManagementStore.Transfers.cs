using System.Data.Common;

namespace Bing.Biz.FileManagement.FreeSql;

public sealed partial class FreeSqlFileManagementStore
{
    private static async Task<bool> PublishTransferAsync(IFreeSql orm, DbTransaction transaction, FileTransferLease lease, FileRecord file, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return await orm.Update<FileTransferAttempt>().Set(x => x.State, FileTransferState.Published).Set(x => x.Version + 1)
            .Where(x => x.Id == lease.Id && x.LeaseToken == lease.Token && x.State == FileTransferState.Active && x.WriterCompleted && x.LeaseUntil > now &&
                x.FileId == file.Id && x.ScopeKey == file.ScopeKey && x.StoreName == file.StoreName && x.Container == file.Container && x.ObjectKey == file.ObjectKey)
            .WithTransaction(transaction).ExecuteAffrowsAsync(ct) == 1;
    }
    public async Task CreateTransferAsync(FileTransferAttempt attempt, CancellationToken ct = default)
        => await _orm.Insert(attempt).ExecuteAffrowsAsync(ct);
    public async Task<bool> RenewTransferAsync(FileTransferLease lease, long leaseUntil, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (leaseUntil <= now) return false;
        return await _orm.Update<FileTransferAttempt>().Set(x => x.LeaseUntil, leaseUntil).Set(x => x.NextCheckAt, leaseUntil).Set(x => x.Version + 1)
            .Where(x => x.Id == lease.Id && x.LeaseToken == lease.Token && x.State == FileTransferState.Active && x.LeaseUntil > now).ExecuteAffrowsAsync(ct) == 1;
    }
    public async Task MarkTransferWrittenAsync(FileTransferLease lease, CancellationToken ct = default)
    {
        var changed = await _orm.Update<FileTransferAttempt>().Set(x => x.WriterCompleted, true).Set(x => x.Version + 1).Set(x => x.NextCheckAt, 0)
            .Where(x => x.Id == lease.Id && x.LeaseToken == lease.Token).ExecuteAffrowsAsync(ct);
        if (changed != 1) throw new InvalidOperationException("复制所有者不匹配。");
    }
    public async Task<IReadOnlyList<FileTransferAttempt>> ListTransfersToCleanAsync(long now, int take, CancellationToken ct = default)
        => await _orm.Select<FileTransferAttempt>().Where(x => x.NextCheckAt <= now &&
            (x.State == FileTransferState.Retired || x.State == FileTransferState.Active && x.LeaseUntil <= now))
            .OrderBy(x => x.NextCheckAt).OrderBy(x => x.Id).Limit(take).ToListAsync(ct);
    public Task<FileTransferAttempt?> RetireTransferAsync(Guid id, string? token, CancellationToken ct = default)
        => InTransactionAsync<FileTransferAttempt?>(async (orm, transaction, cancellationToken) =>
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            await orm.Update<FileTransferAttempt>().Set(x => x.State, FileTransferState.Retired).Set(x => x.NextCheckAt, 0).Set(x => x.Version + 1)
                .Where(x => x.Id == id && x.State == FileTransferState.Active && (token != null ? x.LeaseToken == token : x.LeaseUntil <= now))
                .WithTransaction(transaction).ExecuteAffrowsAsync(cancellationToken);
            return await orm.Select<FileTransferAttempt>().WithTransaction(transaction).Where(x => x.Id == id && x.State == FileTransferState.Retired).FirstAsync(cancellationToken);
        }, ct);
    public async Task<bool> FinishTransferCleanupAsync(Guid id, long expectedVersion, long nextCheckAt, CancellationToken ct = default)
    {
        var current = await _orm.Select<FileTransferAttempt>().Where(x => x.Id == id).FirstAsync(ct);
        if (current == null || current.State != FileTransferState.Retired || current.Version != expectedVersion) return false;
        return await _orm.Update<FileTransferAttempt>().Set(x => x.State, current.WriterCompleted ? FileTransferState.Cleaned : FileTransferState.Retired)
            .Set(x => x.NextCheckAt, nextCheckAt).Set(x => x.Version + 1)
            .Where(x => x.Id == id && x.Version == expectedVersion && x.State == FileTransferState.Retired).ExecuteAffrowsAsync(ct) == 1;
    }
}
