using Microsoft.EntityFrameworkCore;

namespace Bing.Biz.FileManagement.EntityFrameworkCore;

public sealed partial class EfFileManagementStore : IFileReadBatchStore
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<FileReadSnapshot>> ReadReadyFilesAsync(string scopeKey,
        IReadOnlyCollection<Guid> fileIds, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(fileIds);
        if (string.IsNullOrWhiteSpace(scopeKey))
            throw new ArgumentException("文件范围不能为空。", nameof(scopeKey));
        if (fileIds.Count > 100)
            throw new ArgumentOutOfRangeException(nameof(fileIds), "单批最多读取 100 个文件。");
        ct.ThrowIfCancellationRequested();

        var distinctIds = fileIds.Distinct().ToArray();
        if (distinctIds.Length == 0)
            return Array.Empty<FileReadSnapshot>();

        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var transaction = await db.Database
            .BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct).ConfigureAwait(false);

        var files = await db.Set<FileRecord>().AsNoTracking()
            .Where(file => file.ScopeKey == scopeKey && file.State == FileState.Ready &&
                           Enumerable.Contains(distinctIds, file.Id))
            .ToListAsync(ct).ConfigureAwait(false);
        if (files.Count == 0)
        {
            await transaction.CommitAsync(ct).ConfigureAwait(false);
            return Array.Empty<FileReadSnapshot>();
        }

        var readyIds = files.Select(file => file.Id).ToArray();
        var bindings = await db.Set<FileBinding>().AsNoTracking()
            .Where(binding => Enumerable.Contains(readyIds, binding.FileId))
            .ToListAsync(ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);

        var bindingsByFile = bindings.GroupBy(binding => binding.FileId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<FileBinding>)group.ToArray());
        return files.Select(file => new FileReadSnapshot
        {
            File = file,
            Bindings = bindingsByFile.TryGetValue(file.Id, out var fileBindings)
                ? fileBindings
                : Array.Empty<FileBinding>()
        }).ToArray();
    }
}
