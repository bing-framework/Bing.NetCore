using Bing.Biz.FileManagement;
using FreeSql;

namespace Bing.Biz.FileManagement.FreeSql;

public sealed partial class FreeSqlFileManagementStore : IFileReadBatchStore
{
    /// <inheritdoc />
    public Task<IReadOnlyList<FileReadSnapshot>> ReadReadyFilesAsync(string scopeKey,
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
            return Task.FromResult<IReadOnlyList<FileReadSnapshot>>(Array.Empty<FileReadSnapshot>());

        return InTransactionAsync<IReadOnlyList<FileReadSnapshot>>(async (orm, transaction, token) =>
        {
            var files = await orm.Select<FileRecord>()
                .WithTransaction(transaction)
                .Where(file => file.ScopeKey == scopeKey && file.State == FileState.Ready &&
                               Enumerable.Contains(distinctIds, file.Id))
                .ToListAsync(token)
                .ConfigureAwait(false);
            if (files.Count == 0)
                return Array.Empty<FileReadSnapshot>();

            var readyIds = files.Select(file => file.Id).ToArray();
            var bindings = await orm.Select<FileBinding>()
                .WithTransaction(transaction)
                .Where(binding => Enumerable.Contains(readyIds, binding.FileId))
                .ToListAsync(token)
                .ConfigureAwait(false);
            var bindingsByFile = bindings.GroupBy(binding => binding.FileId)
                .ToDictionary(group => group.Key, group => (IReadOnlyList<FileBinding>)group.ToArray());

            return files.Select(file => new FileReadSnapshot
            {
                File = file,
                Bindings = bindingsByFile.TryGetValue(file.Id, out var fileBindings)
                    ? fileBindings
                    : Array.Empty<FileBinding>()
            }).ToArray();
        }, ct);
    }
}
