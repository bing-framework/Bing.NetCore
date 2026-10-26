using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Bing.Biz.FileManagement.EntityFrameworkCore;
using FreeSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Biz.FileManagement.Tests;

public sealed class PartLookupStoreTests
{
    private const int PartCount = 6400;
    private readonly ITestOutputHelper _output;

    public PartLookupStoreTests(ITestOutputHelper output) => _output = output;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FindPartAsync_UsesCompositeKeyAndMaterializesOnlyTheMatch(bool freeSql)
    {
        await using var fixture = await FileManagementStoreTestEnvironment.CreateAsync(freeSql);
        var entry = await fixture.CreateEntryAsync("part-lookup-main");
        var otherEntry = await fixture.CreateEntryAsync("part-lookup-other");
        var parts = Enumerable.Range(1, PartCount)
            .Select(number => Part(entry.Session!.Id, number, Sha(number)))
            .Append(Part(otherEntry.Session!.Id, PartCount / 2, "other-session"))
            .ToArray();
        await fixture.SeedPartsAsync(parts);

        fixture.Sql.Clear();
        var listed = await fixture.Store.ListPartsAsync(entry.Session!.Id);
        Assert.Equal(PartCount, listed.Count);
        Assert.Single(fixture.Sql.PartQueries);

        fixture.Sql.Clear();
        var found = await fixture.Store.FindPartAsync(entry.Session.Id, PartCount / 2);
        Assert.NotNull(found);
        Assert.Equal(Sha(PartCount / 2), found!.Sha256);
        var lookupSql = Assert.Single(fixture.Sql.PartQueries);
        AssertBothKeysAreInWhereClause(lookupSql);

        fixture.Sql.Clear();
        Assert.Null(await fixture.Store.FindPartAsync(entry.Session.Id, PartCount + 1));
        Assert.Single(fixture.Sql.PartQueries);

        var isolated = await fixture.Store.FindPartAsync(otherEntry.Session!.Id, PartCount / 2);
        Assert.Equal("other-session", isolated?.Sha256);

        found.Sha256 = "mutated-snapshot";
        Assert.Equal(Sha(PartCount / 2), (await fixture.Store.FindPartAsync(entry.Session.Id, PartCount / 2))?.Sha256);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => fixture.Store.FindPartAsync(entry.Session.Id, 0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => fixture.Store.FindPartAsync(entry.Session.Id, -1));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Store.FindPartAsync(entry.Session.Id, 1, cancellation.Token));

        _output.WriteLine("provider={0}; ListPartsAsync materialized={1}; FindPartAsync materialized={2}; sql={3}",
            freeSql ? "FreeSql/SQLite" : "EF Core/SQLite", listed.Count, found == null ? 0 : 1, lookupSql);
    }

    private static void AssertBothKeysAreInWhereClause(string sql)
    {
        var normalized = sql.Replace("\"", "").Replace("`", "").Replace("[", "").Replace("]", "");
        var where = normalized.IndexOf("WHERE", StringComparison.OrdinalIgnoreCase);
        Assert.True(where >= 0, sql);
        var predicate = normalized[where..];
        Assert.Contains("SessionId", predicate, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Number", predicate, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AND", predicate, StringComparison.OrdinalIgnoreCase);
    }

    private static FileUploadPart Part(Guid sessionId, int number, string sha256) => new()
    {
        SessionId = sessionId,
        Number = number,
        Length = 1,
        Sha256 = sha256
    };

    private static string Sha(int number) => Convert.ToBase64String(
        SHA256.HashData(Encoding.UTF8.GetBytes(number.ToString(System.Globalization.CultureInfo.InvariantCulture))));
}

internal sealed class FileManagementStoreTestEnvironment : IAsyncDisposable
{
    private readonly FileTestEnvironment _fileEnvironment;

    private FileManagementStoreTestEnvironment(FileTestEnvironment fileEnvironment, ServiceProvider provider,
        SqlCommandCapture sql)
    {
        _fileEnvironment = fileEnvironment;
        Provider = provider;
        Sql = sql;
        Store = provider.GetRequiredService<IFileManagementStore>();
        Orm = provider.GetService<IFreeSql>();
        if (Orm != null)
            Orm.Aop.CurdBefore += (_, args) => Sql.Record(args.Sql);
    }

    internal ServiceProvider Provider { get; }
    internal IFileManagementStore Store { get; }
    internal IFreeSql? Orm { get; }
    internal SqlCommandCapture Sql { get; }

    internal static async Task<FileManagementStoreTestEnvironment> CreateAsync(bool freeSql)
    {
        var fileEnvironment = new FileTestEnvironment(freeSql: freeSql);
        var sql = new SqlCommandCapture();
        var services = fileEnvironment.Configure(new ServiceCollection());
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Information).AddProvider(new SqlLoggerProvider(sql)));
        var provider = services.BuildServiceProvider();
        try
        {
            await FileTestEnvironment.InitializeDatabaseAsync(provider);
            return new FileManagementStoreTestEnvironment(fileEnvironment, provider, sql);
        }
        catch
        {
            await provider.DisposeAsync();
            await fileEnvironment.DisposeAsync();
            throw;
        }
    }

    internal async Task<FileEntry> CreateEntryAsync(string requestKey)
    {
        var entry = CreateEntry(requestKey);
        if (!await Store.TryCreateAsync(entry))
            throw new InvalidOperationException("Failed to create a test file entry.");
        return entry;
    }

    internal async Task SeedPartsAsync(IReadOnlyCollection<FileUploadPart> parts)
    {
        if (Orm != null)
        {
            foreach (var batch in parts.Chunk(100))
                await Orm.Insert(batch).ExecuteAffrowsAsync();
            return;
        }

        await using var db = await Provider.GetRequiredService<IDbContextFactory<FileManagementDbContext>>()
            .CreateDbContextAsync();
        db.AddRange(parts);
        await db.SaveChangesAsync();
    }

    internal async Task SeedOperationsAsync(IReadOnlyCollection<FileOperation> operations)
    {
        if (Orm != null)
        {
            foreach (var batch in operations.Chunk(100))
                await Orm.Insert(batch).ExecuteAffrowsAsync();
            return;
        }

        await using var db = await Provider.GetRequiredService<IDbContextFactory<FileManagementDbContext>>()
            .CreateDbContextAsync();
        db.AddRange(operations);
        await db.SaveChangesAsync();
    }

    internal static FileEntry CreateEntry(string requestKey)
    {
        var fileId = Guid.NewGuid();
        return new FileEntry
        {
            File = new FileRecord
            {
                Id = fileId,
                RequestKey = requestKey,
                ScopeKey = "scope-a",
                ScopeKind = Bing.FileStorage.StorageScopeKind.Host,
                StoreName = "local-v1",
                Container = "attachments",
                ObjectKey = fileId.ToString("N"),
                FileName = requestKey + ".txt",
                Length = 1,
                ContentType = "text/plain",
                Visibility = FileVisibility.Private,
                State = FileState.Uploading,
                CreatorId = "user-a",
                CreatedAt = 1,
                Version = 1
            },
            Session = new FileUploadSession
            {
                Id = Guid.NewGuid(),
                FileId = fileId,
                OwnerId = "user-a",
                TotalLength = 1,
                PartSize = 1,
                State = FileState.Uploading,
                WriteMode = Bing.FileStorage.FileWriteMode.Overwrite,
                ExpiresAt = 10000,
                AbsoluteExpiresAt = 20000,
                LastActivityAt = 1,
                Version = 1
            },
            Bindings = new List<FileBinding>()
        };
    }

    public async ValueTask DisposeAsync()
    {
        await Provider.DisposeAsync();
        await _fileEnvironment.DisposeAsync();
    }
}

internal sealed class SqlCommandCapture
{
    private readonly ConcurrentQueue<string> _commands = new();

    internal IReadOnlyList<string> PartQueries => _commands
        .Where(command => command.Contains("BingFileUploadParts", StringComparison.OrdinalIgnoreCase))
        .ToArray();

    internal void Record(string command) => _commands.Enqueue(command);

    internal void Clear()
    {
        while (_commands.TryDequeue(out _)) { }
    }
}

internal sealed class SqlLoggerProvider : ILoggerProvider
{
    private readonly SqlCommandCapture _capture;

    internal SqlLoggerProvider(SqlCommandCapture capture) => _capture = capture;

    public ILogger CreateLogger(string categoryName) => new SqlLogger(categoryName, _capture);
    public void Dispose() { }

    private sealed class SqlLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly SqlCommandCapture _capture;

        internal SqlLogger(string categoryName, SqlCommandCapture capture)
        {
            _categoryName = categoryName;
            _capture = capture;
        }

        IDisposable ILogger.BeginScope<TState>(TState state) => null!;

        public bool IsEnabled(LogLevel logLevel) =>
            logLevel >= LogLevel.Information && _categoryName == DbLoggerCategory.Database.Command.Name;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
                _capture.Record(formatter(state, exception));
        }
    }
}
