using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.KeyGeneration;
using Dapper;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>
/// 用真实 SQLite 验证框架生成键入口、显式事务与外键关联。
/// </summary>
public sealed class SqliteEntityKeyIntegrationTest : IDisposable
{
    /// <summary>
    /// 保存实体键集成测试使用的临时数据库文件路径。
    /// </summary>
    private readonly string _file = Path.Combine(Path.GetTempPath(), "bing-keys-" + Guid.NewGuid().ToString("N") + ".db");
    /// <summary>
    /// 保存实体键集成测试使用的服务提供程序。
    /// </summary>
    private readonly ServiceProvider _services;
    /// <summary>
    /// 获取测试数据库的连接字符串。
    /// </summary>
    private string ConnectionString => new SqliteConnectionStringBuilder { DataSource = _file, ForeignKeys = true, Pooling = false }.ToString();
    /// <summary>
    /// 初始化一个 <see cref="SqliteEntityKeyIntegrationTest" /> 类型的实例。
    /// </summary>
    public SqliteEntityKeyIntegrationTest()
    {
        var services = new ServiceCollection();
        services.AddSqlCore();
        services.AddSqlDataSource("default", DatabaseType.Sqlite, ConnectionString);
        services.AddSqliteProvider();
        services.AddEntityKeyPolicies(p => p
            .For<Parent, long>(e => e.Id, EntityKeyGenerationKind.DatabaseIdentity)
            .For<IdentityOnly, long>(e => e.Id, EntityKeyGenerationKind.DatabaseIdentity)
            .For<ComputedRow, long>(e => e.Id, EntityKeyGenerationKind.DatabaseIdentity)
            .For<Child, long>(e => e.Id, EntityKeyGenerationKind.DatabaseIdentity)
            .For<Snow, long>(e => e.Id, EntityKeyGenerationKind.ClientSnowflake)
            .For<GuidRow, Guid>(e => e.Id, EntityKeyGenerationKind.ClientGuid));
        services.AddSingleton<IClientKeyGenerator<long>, Counter>();
        _services = services.BuildServiceProvider();
        using var db = new SqliteConnection(ConnectionString);
        db.Execute("CREATE TABLE identity_only (Id INTEGER PRIMARY KEY AUTOINCREMENT); CREATE TABLE computed_rows (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL); CREATE TABLE parents (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL UNIQUE); CREATE TABLE children (Id INTEGER PRIMARY KEY AUTOINCREMENT, ParentId INTEGER NOT NULL REFERENCES parents(Id), Name TEXT NOT NULL); CREATE TABLE snows (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL); CREATE TABLE guids (Id TEXT PRIMARY KEY, Name TEXT NOT NULL)");
    }
    /// <summary>
    /// 验证自有写作用域返回已提交键映射及实际实体主键。
    /// </summary>
    [Fact]
    public async Task OwnedScope_ReturnsCommittedMap_AndRealEntityKeys()
    {
        using var executor = _services.GetRequiredService<ISqlExecutorFactory>().Create();
        var first = new Parent { Name = "b" }; var second = new Parent { Name = "a" };
        var result = await ((ISqlKeyInsertExecutor)executor).InsertWithKeysAsync<Parent, long>(new[] { new InsertRow<Parent>("row-b", first), new InsertRow<Parent>("row-a", second) });
        Assert.Equal(WriteCompletionState.Committed, result.Completion);
        Assert.Equal("PerEntityIdentity", result.ExecutionPath);
        Assert.Equal(first.Id, result.Keys["row-b"]); Assert.Equal(second.Id, result.Keys["row-a"]);
        using var db = new SqliteConnection(ConnectionString);
        Assert.Equal(first.Id, db.QuerySingle<long>("SELECT Id FROM parents WHERE Name='b'"));
        Assert.Equal(2, db.QuerySingle<int>("SELECT COUNT(*) FROM parents"));
    }
    /// <summary>
    /// 验证显式作用域将父键用于子实体且失败时一并回滚。
    /// </summary>
    /// <param name="fail">是否在测试操作中注入失败。</param>
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ExplicitScope_ParentKeyFeedsChild_AndFailureRollsBack(bool fail)
    {
        await using var scope = await _services.GetRequiredService<ISqlTransactionScopeFactory>().BeginAsync();
        using var executor = scope.CreateExecutor();
        var keys = (ISqlKeyInsertExecutor)executor;
        var parent = new Parent { Name = "parent" };
        var result = await keys.InsertWithKeysAsync<Parent, long>(new[] { new InsertRow<Parent>("parent", parent) });
        Assert.Equal(WriteCompletionState.Flushed, result.Completion);
        using (var outside = new SqliteConnection(ConnectionString)) Assert.Equal(0, outside.QuerySingle<int>("SELECT COUNT(*) FROM parents"));
        var child = new Child { ParentId = fail ? parent.Id + 1000 : result.Keys["parent"], Name = "child" };
        if (fail)
        {
            await Assert.ThrowsAsync<SqliteException>(() => keys.InsertWithKeysAsync<Child, long>(new[] { new InsertRow<Child>("child", child) }));
            await scope.RollbackAsync();
        }
        else
        {
            await keys.InsertWithKeysAsync<Child, long>(new[] { new InsertRow<Child>("child", child) });
            Assert.True(child.Id > 0); await scope.CommitAsync();
        }
        using var db = new SqliteConnection(ConnectionString);
        Assert.Equal(fail ? 0 : 1, db.QuerySingle<int>("SELECT COUNT(*) FROM parents p JOIN children c ON c.ParentId=p.Id"));
        Assert.Equal(fail ? 0 : 1, db.QuerySingle<int>("SELECT COUNT(*) FROM parents"));
    }
    /// <summary>
    /// 验证显式客户端策略初始化单条及批量主键且保留预分配值。
    /// </summary>
    [Fact]
    public async Task ExplicitClientPolicies_InitializeOrdinarySingleAndBatch_InsertPreservesPreallocation()
    {
        using var executor = _services.GetRequiredService<ISqlExecutorFactory>().Create();
        var guid = new GuidRow { Name = "guid" };
        Assert.Equal(1, await executor.InsertAsync(guid)); Assert.NotEqual(Guid.Empty, guid.Id);
        var assigned = new Snow { Id = 9007199254740993, Name = "preallocated" };
        var missing = new Snow { Name = "missing" };
        Assert.Equal(2, await executor.InsertBatchAsync(new[] { assigned, missing }));
        Assert.Equal(9007199254740993, assigned.Id); Assert.True(missing.Id > 0);
        var mapped = new GuidRow { Name = "mapped" };
        var result = await ((ISqlKeyInsertExecutor)executor).InsertWithKeysAsync<GuidRow, Guid>(new[] { new InsertRow<GuidRow>("g", mapped) });
        Assert.Equal(mapped.Id, result.Keys["g"]);
    }
    /// <summary>
    /// 验证非法关联键或强制批量能力要求在写入前被拒绝。
    /// </summary>
    [Fact]
    public async Task InvalidCorrelationOrRequiredBulk_RejectsBeforeAnyWrite()
    {
        using var executor = _services.GetRequiredService<ISqlExecutorFactory>().Create();
        var keys = (ISqlKeyInsertExecutor)executor;
        var rows = new[] { new InsertRow<Parent>("same", new Parent { Name = "a" }), new InsertRow<Parent>("same", new Parent { Name = "b" }) };
        await Assert.ThrowsAsync<ArgumentException>(() => keys.InsertWithKeysAsync<Parent, long>(rows));
        await Assert.ThrowsAsync<NotSupportedException>(() => keys.InsertWithKeysAsync<Parent, long>(rows.Take(1).ToArray(), true));
        await Assert.ThrowsAsync<InvalidOperationException>(() => keys.InsertWithKeysAsync<Parent, long>(new[] { new InsertRow<Parent>("assigned-identity", new Parent { Id = 42, Name = "bad" }) }));
        using var db = new SqliteConnection(ConnectionString);
        Assert.Equal(0, db.QuerySingle<int>("SELECT COUNT(*) FROM parents"));
    }
    /// <summary>
    /// 验证普通子实体批量写入复用显式父事务。
    /// </summary>
    /// <param name="synchronous">是否通过同步执行路径调用。</param>
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task OrdinaryChildBatch_ReusesExplicitParentTransaction(bool synchronous)
    {
        await using var scope = await _services.GetRequiredService<ISqlTransactionScopeFactory>().BeginAsync();
        using var executor = scope.CreateExecutor();
        var parent = new Parent { Name = "batch-parent" };
        await ((ISqlKeyInsertExecutor)executor).InsertWithKeysAsync<Parent,long>(new[] { new InsertRow<Parent>("p",parent) });
        var children = new[] { new Child { ParentId = parent.Id, Name = "a" }, new Child { ParentId = parent.Id, Name = "b" } };
        var affected = synchronous ? executor.InsertBatch(children) : await executor.InsertBatchAsync(children);
        Assert.Equal(2, affected);
        using (var outside = new SqliteConnection(ConnectionString)) Assert.Equal(0, outside.QuerySingle<int>("SELECT COUNT(*) FROM children"));
        await scope.CommitAsync();
        using var db = new SqliteConnection(ConnectionString);
        Assert.Equal(2, db.QuerySingle<int>("SELECT COUNT(*) FROM parents p JOIN children c ON c.ParentId=p.Id"));
    }
    /// <summary>
    /// 验证仅有自增列的表通过默认值插入返回生成键。
    /// </summary>
    [Fact]
    public async Task IdentityOnlyTable_ReturnsDefaultValuesGeneratedKey()
    {
        using var executor = _services.GetRequiredService<ISqlExecutorFactory>().Create();
        var entity = new IdentityOnly();
        var result = await ((ISqlKeyInsertExecutor)executor).InsertWithKeysAsync<IdentityOnly,long>(new[] {new InsertRow<IdentityOnly>("empty",entity)});
        Assert.True(entity.Id > 0); Assert.Equal(entity.Id,result.Keys["empty"]);
        using var db = new SqliteConnection(ConnectionString);
        Assert.Equal(entity.Id,db.QuerySingle<long>("SELECT Id FROM identity_only"));
    }
    /// <summary>
    /// 验证计算列主键映射在各插入入口写入前被拒绝。
    /// </summary>
    [Fact]
    public async Task ComputedIdentityMapping_RejectsInsertEntrypointsBeforeWriting()
    {
        using var executor = _services.GetRequiredService<ISqlExecutorFactory>().Create();
        var syncException = Assert.Throws<InvalidOperationException>(() => executor.Insert(new ComputedRow { Name = "sync" }));
        Assert.Contains(nameof(ComputedRow), syncException.Message, StringComparison.Ordinal);

        var asyncException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.InsertAsync(new ComputedRow { Name = "async" }));
        Assert.Contains(nameof(ComputedRow), asyncException.Message, StringComparison.Ordinal);

        var keyException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ((ISqlKeyInsertExecutor)executor).InsertWithKeysAsync<ComputedRow, long>(
                new[] { new InsertRow<ComputedRow>("computed", new ComputedRow { Name = "keys" }) }));
        Assert.Contains(nameof(ComputedRow), keyException.Message, StringComparison.Ordinal);

        using var db = new SqliteConnection(ConnectionString);
        Assert.Equal(0, db.QuerySingle<int>("SELECT COUNT(*) FROM computed_rows"));
    }
    /// <summary>
    /// 表示仅包含数据库生成标识的 SQLite 测试行。
    /// </summary>
    [Table("identity_only")] public sealed class IdentityOnly {
        /// <summary>
        /// 获取或设置记录标识。
        /// </summary>
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)] public long Id { get; set; } }
    /// <summary>
    /// 表示使用计算生成主键的 SQLite 测试行。
    /// </summary>
    [Table("computed_rows")] public sealed class ComputedRow {
        /// <summary>
        /// 获取或设置记录标识。
        /// </summary>
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Computed)] public long Id { get; set; }
        /// <summary>
        /// 获取或设置实体或结果名称。
        /// </summary>
        public string Name { get; set; } }
    /// <summary>
    /// 验证批量主键分配期间取消不会产生写入。
    /// </summary>
    [Fact]
    public async Task OrdinaryBatch_CancellationDuringKeyAllocation_ShouldWriteNothing()
    {
        using var cancellation = new CancellationTokenSource();
        ((Counter)_services.GetRequiredService<IClientKeyGenerator<long>>()).CancelOnNext = cancellation;
        using var executor = _services.GetRequiredService<ISqlExecutorFactory>().Create();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executor.InsertBatchAsync(new[] {new Snow {Name="a"},new Snow {Name="b"}},cancellationToken:cancellation.Token));
        using var db = new SqliteConnection(ConnectionString);
        Assert.Equal(0,db.QuerySingle<int>("SELECT COUNT(*) FROM snows"));
    }
    /// <inheritdoc />
    public void Dispose() { _services.Dispose(); File.Delete(_file); }
    /// <summary>
    /// 表示 SQLite 外键测试中的父记录。
    /// </summary>
    [Table("parents")] public sealed class Parent {
        /// <summary>
        /// 获取或设置记录标识。
        /// </summary>
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)] public long Id { get; set; }
        /// <summary>
        /// 获取或设置实体或结果名称。
        /// </summary>
        public string Name { get; set; } }
    /// <summary>
    /// 表示 SQLite 外键测试中的子记录。
    /// </summary>
    [Table("children")] public sealed class Child {
        /// <summary>
        /// 获取或设置记录标识。
        /// </summary>
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)] public long Id { get; set; }
        /// <summary>
        /// 获取或设置父记录标识。
        /// </summary>
        public long ParentId { get; set; }
        /// <summary>
        /// 获取或设置实体或结果名称。
        /// </summary>
        public string Name { get; set; } }
    /// <summary>
    /// 表示使用雪花标识的 SQLite 测试行。
    /// </summary>
    [Table("snows")] public sealed class Snow {
        /// <summary>
        /// 获取或设置记录标识。
        /// </summary>
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)] public long Id { get; set; }
        /// <summary>
        /// 获取或设置实体或结果名称。
        /// </summary>
        public string Name { get; set; } }
    /// <summary>
    /// 表示使用 GUID 标识的 SQLite 测试行。
    /// </summary>
    [Table("guids")] public sealed class GuidRow {
        /// <summary>
        /// 获取或设置记录标识。
        /// </summary>
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)] public Guid Id { get; set; }
        /// <summary>
        /// 获取或设置实体或结果名称。
        /// </summary>
        public string Name { get; set; } }
    /// <summary>
    /// 为 SQL Provider 测试返回递增 long 类型键的生成器。
    /// </summary>
    private sealed class Counter : IClientKeyGenerator<long> {
        /// <summary>
        /// 保存测试计数器的起始值。
        /// </summary>
        private long _id = 100;
        /// <summary>
        /// 获取或设置下一次执行时需要触发的取消源。
        /// </summary>
        public CancellationTokenSource CancelOnNext { get; set; }
        /// <inheritdoc />
        public string Name => "default";
        /// <inheritdoc />
        public long Next(CancellationToken token = default) { token.ThrowIfCancellationRequested(); CancelOnNext?.Cancel(); return Interlocked.Increment(ref _id); } }
}
