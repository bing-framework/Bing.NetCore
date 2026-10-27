using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using System.Reflection;
using Bing.Biz.FileManagement.EntityFrameworkCore;
using Bing.FileStorage;
using FreeSql;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Biz.FileManagement.Tests;

public sealed class FileReadBatchStoreTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadReadyFilesAsync_FiltersScopeStateAndBindingsAndReturnsDetachedSnapshots(bool freeSql)
    {
        await using var fixture = await FileReadBatchTestFixture.CreateAsync(freeSql);
        var rows = Enumerable.Range(1, 4).Select(index => ReadBatchData.Record(index,
            index == 4 ? "scope-other" : "scope-read-batch",
            index == 3 ? FileState.Uploading : FileState.Ready,
            withSession: true,
            withBinding: index is 1 or 2)).ToArray();
        await fixture.SeedAsync(rows);

        fixture.Sql.Clear();
        var snapshots = await fixture.Store.ReadReadyFilesAsync("scope-read-batch",
            new[] { rows[0].File.Id, rows[1].File.Id, rows[2].File.Id, rows[3].File.Id, rows[0].File.Id, Guid.NewGuid() });
        Assert.Equal(2, snapshots.Count);
        Assert.Equal(new[] { rows[0].File.Id, rows[1].File.Id }.OrderBy(id => id),
            snapshots.Select(snapshot => snapshot.File.Id).OrderBy(id => id));
        Assert.Equal(new[] { "business-1" }, Assert.Single(snapshots, x => x.File.Id == rows[0].File.Id)
            .Bindings.Select(binding => binding.BusinessId));
        Assert.Equal(new[] { "business-2" }, Assert.Single(snapshots, x => x.File.Id == rows[1].File.Id)
            .Bindings.Select(binding => binding.BusinessId));
        Assert.Equal(2, fixture.Sql.SelectStatements.Count);
        Assert.DoesNotContain(fixture.Sql.SelectStatements, statement =>
            statement.Contains("BingFileUploadSessions", StringComparison.OrdinalIgnoreCase));

        var first = Assert.Single(snapshots, x => x.File.Id == rows[0].File.Id);
        first.File.FileName = "mutated.txt";
        first.Bindings[0].BusinessId = "mutated-business";
        var persisted = await fixture.Store.ReadReadyFilesAsync("scope-read-batch", new[] { rows[0].File.Id });
        Assert.Equal(rows[0].File.FileName, Assert.Single(persisted).File.FileName);
        Assert.Equal("business-1", Assert.Single(Assert.Single(persisted).Bindings).BusinessId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadReadyFilesAsync_UsesTwoSelectsForOneFiftyAndOneHundredIds(bool freeSql)
    {
        await using var fixture = await FileReadBatchTestFixture.CreateAsync(freeSql);
        var rows = Enumerable.Range(1, 100).Select(index => ReadBatchData.Record(index,
            "scope-read-batch", FileState.Ready, withSession: true, withBinding: index % 2 == 0)).ToArray();
        await fixture.SeedAsync(rows);

        foreach (var count in new[] { 1, 50, 100 })
        {
            fixture.Sql.Clear();
            var snapshots = await fixture.Store.ReadReadyFilesAsync("scope-read-batch",
                rows.Take(count).Select(row => row.File.Id).ToArray());
            Assert.Equal(count, snapshots.Count);
            Assert.Equal(2, fixture.Sql.SelectStatements.Count);
            Assert.DoesNotContain(fixture.Sql.SelectStatements, statement =>
                statement.Contains("BingFileUploadSessions", StringComparison.OrdinalIgnoreCase));
        }

        fixture.Sql.Clear();
        Assert.Empty(await fixture.Store.ReadReadyFilesAsync("scope-no-results", rows.Take(50).Select(row => row.File.Id).ToArray()));
        Assert.Single(fixture.Sql.SelectStatements);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadReadyFilesAsync_ValidatesBeforeDatabaseAccessAndSkipsEmptyBatch(bool freeSql)
    {
        await using var fixture = await FileReadBatchTestFixture.CreateAsync(freeSql);

        fixture.Sql.Clear();
        Assert.Empty(await fixture.Store.ReadReadyFilesAsync("scope-read-batch", Array.Empty<Guid>()));
        Assert.Empty(fixture.Sql.SelectStatements);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            fixture.Store.ReadReadyFilesAsync("scope-read-batch", null!));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            fixture.Store.ReadReadyFilesAsync("  ", new[] { Guid.NewGuid() }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            fixture.Store.ReadReadyFilesAsync("scope-read-batch", Enumerable.Range(0, 101)
                .Select(_ => Guid.NewGuid()).ToArray()));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Store.ReadReadyFilesAsync("scope-read-batch", new[] { Guid.NewGuid() }, cancellation.Token));
        Assert.Empty(fixture.Sql.SelectStatements);
    }
}

internal sealed class FileReadBatchTestFixture : IAsyncDisposable
{
    private readonly FileTestEnvironment _environment;

    private FileReadBatchTestFixture(FileTestEnvironment environment, ServiceProvider provider,
        FileReadBatchSqlCapture sql)
    {
        _environment = environment;
        Provider = provider;
        Store = provider.GetRequiredService<IFileManagementStore>() as IFileReadBatchStore
            ?? throw new InvalidOperationException("文件仓储未注册批量读取能力。");
        Sql = sql;
    }

    internal ServiceProvider Provider { get; }
    internal IFileReadBatchStore Store { get; }
    internal FileReadBatchSqlCapture Sql { get; }

    internal static async Task<FileReadBatchTestFixture> CreateAsync(bool freeSql)
    {
        var environment = new FileTestEnvironment(freeSql: freeSql);
        var provider = environment.Configure(new ServiceCollection()).BuildServiceProvider();
        try
        {
            await FileTestEnvironment.InitializeDatabaseAsync(provider);
            var sql = new FileReadBatchSqlCapture(provider, Path.Combine(environment.Root, "files.db"));
            return new FileReadBatchTestFixture(environment, provider, sql);
        }
        catch
        {
            await provider.DisposeAsync();
            await environment.DisposeAsync();
            throw;
        }
    }

    internal async Task SeedAsync(IReadOnlyCollection<FileEntry> entries)
    {
        if (Provider.GetService<IFreeSql>() is { } orm)
        {
            var files = entries.Select(entry => entry.File).ToArray();
            var sessions = entries.Select(entry => entry.Session!).ToArray();
            var bindings = entries.SelectMany(entry => entry.Bindings).ToArray();
            foreach (var batch in files.Chunk(100)) await orm.Insert(batch).ExecuteAffrowsAsync();
            foreach (var batch in sessions.Chunk(100)) await orm.Insert(batch).ExecuteAffrowsAsync();
            foreach (var batch in bindings.Chunk(100)) await orm.Insert(batch).ExecuteAffrowsAsync();
            return;
        }

        await using var db = await Provider.GetRequiredService<IDbContextFactory<FileManagementDbContext>>()
            .CreateDbContextAsync();
        db.AddRange(entries.Select(entry => entry.File));
        db.AddRange(entries.Select(entry => entry.Session!));
        db.AddRange(entries.SelectMany(entry => entry.Bindings));
        await db.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        Sql.Dispose();
        await Provider.DisposeAsync();
        await _environment.DisposeAsync();
    }
}

internal static class ReadBatchData
{
    internal static FileEntry Record(int index, string scope, FileState state, bool withSession, bool withBinding)
    {
        var id = Guid.Parse($"00000000-0000-0000-0000-{index:x12}");
        var entry = new FileEntry
        {
            File = new FileRecord
            {
                Id = id,
                RequestKey = "read-batch-" + index,
                ScopeKey = scope,
                ScopeKind = StorageScopeKind.Host,
                StoreName = "local-v1",
                Container = "attachments",
                ObjectKey = id.ToString("N"),
                FileName = $"read-batch-{index}.txt",
                Length = 1,
                ContentType = "text/plain",
                Visibility = FileVisibility.Private,
                State = state,
                CreatorId = "reader",
                CreatedAt = index,
                Version = 1
            }
        };
        if (withSession)
            entry.Session = new FileUploadSession
            {
                Id = Guid.NewGuid(),
                FileId = id,
                OwnerId = "reader",
                TotalLength = 1,
                PartSize = 1,
                State = state,
                WriteMode = FileWriteMode.Overwrite,
                ExpiresAt = 100,
                AbsoluteExpiresAt = 200,
                LastActivityAt = 1,
                Version = 1
            };
        if (withBinding)
            entry.Bindings.Add(new FileBinding
            {
                Id = Guid.NewGuid(),
                FileId = id,
                BusinessType = "orders",
                BusinessId = "business-" + index,
                CreatorId = "reader",
                CreatedAt = index
            });
        return entry;
    }
}

/// <summary>捕获真实 EF Core DiagnosticListener 命令或单个 FreeSql 实例的 CurdBefore SQL。</summary>
internal sealed class FileReadBatchSqlCapture : IDisposable
{
    private readonly ConcurrentQueue<string> _statements = new();
    private readonly string _databasePath;
    private readonly IFreeSql? _orm;
    private readonly IDisposable? _allListeners;
    private IDisposable? _efListener;

    internal FileReadBatchSqlCapture(IServiceProvider provider, string databasePath)
    {
        _databasePath = Path.GetFullPath(databasePath);
        _orm = provider.GetService<IFreeSql>();
        if (_orm != null)
            _orm.Aop.CurdBefore += OnFreeSqlCommand;
        else
            _allListeners = DiagnosticListener.AllListeners.Subscribe(new Observer<DiagnosticListener>(listener =>
            {
                if (listener.Name == "Microsoft.EntityFrameworkCore")
                    _efListener = listener.Subscribe(new Observer<KeyValuePair<string, object?>>(OnEfCommand),
                        (name, _, _) => name == "Microsoft.EntityFrameworkCore.Database.Command.CommandExecuting");
            }));
    }

    internal IReadOnlyList<string> SelectStatements => _statements
        .Where(statement => IsSelect(statement) && IsFileManagementStatement(statement))
        .ToArray();

    internal void Clear()
    {
        while (_statements.TryDequeue(out _)) { }
    }

    private void OnFreeSqlCommand(object? sender, global::FreeSql.Aop.CurdBeforeEventArgs args)
    {
        if (args.Sql != null && IsSelect(args.Sql) && IsFileManagementStatement(args.Sql))
            _statements.Enqueue(args.Sql);
    }

    private void OnEfCommand(KeyValuePair<string, object?> eventData)
    {
        if (eventData.Value == null)
            return;
        var command = eventData.Value.GetType().GetProperty("Command", BindingFlags.Instance | BindingFlags.Public)
            ?.GetValue(eventData.Value) as DbCommand;
        if (command?.Connection is not SqliteConnection connection || !SamePath(connection.DataSource, _databasePath))
            return;
        var sql = command.CommandText;
        if (IsSelect(sql) && IsFileManagementStatement(sql))
            _statements.Enqueue(sql);
    }

    private static bool IsSelect(string sql)
    {
        var trimmed = sql.TrimStart();
        return trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("WITH", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFileManagementStatement(string sql) =>
        sql.Contains("BingFileRecords", StringComparison.OrdinalIgnoreCase) ||
        sql.Contains("BingFileBindings", StringComparison.OrdinalIgnoreCase) ||
        sql.Contains("BingFileUploadSessions", StringComparison.OrdinalIgnoreCase);

    private static bool SamePath(string source, string target)
    {
        try { return string.Equals(Path.GetFullPath(source), target, StringComparison.OrdinalIgnoreCase); }
        catch { return false; }
    }

    public void Dispose()
    {
        _efListener?.Dispose();
        _allListeners?.Dispose();
        if (_orm != null)
            _orm.Aop.CurdBefore -= OnFreeSqlCommand;
    }

    private sealed class Observer<T> : IObserver<T>
    {
        private readonly Action<T> _next;
        internal Observer(Action<T> next) => _next = next;
        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void OnNext(T value) => _next(value);
    }
}
