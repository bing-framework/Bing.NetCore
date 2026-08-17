using System.Data;
using System.Data.Common;
using Bing.Data.Transaction;
using Bing.FreeSQL;
using FreeSql;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Xunit;

namespace Bing.FreeSQL.Tests.Core;

/// <summary>
/// FreeSQL 工作单元取消行为单元测试。
/// </summary>
public sealed class UnitOfWorkCancellationTest
{
    /// <summary>
    /// 测试目的：事务动作存在时，预取消令牌必须在创建事务或提交事务动作前终止异步保存。
    /// </summary>
    [Fact]
    public async Task SaveChangesAsync_WhenCancelledBeforeTransactionActions_ShouldNotCommitActions()
    {
        // Arrange
        var transactionActionManager = new RecordingTransactionActionManager();
        var services = new ServiceCollection();
        services.AddSingleton<ITransactionActionManager>(transactionActionManager);
        using var serviceProvider = services.BuildServiceProvider();
        using var orm = CreateOrm();
        using var unitOfWork = new TestUnitOfWork(new FreeSqlWrapper { Orm = orm }, serviceProvider);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act and Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => unitOfWork.SaveChangesAsync(
            cancellationTokenSource.Token));
        Assert.False(transactionActionManager.CommitRequested);
    }

    /// <summary>
    /// 测试目的：同步提交存在事务动作时必须等待并执行动作，不能静默跳过异步回调。
    /// </summary>
    [Fact]
    public void Commit_WhenTransactionActionsAreRegistered_ShouldExecuteActions()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"Bing.FreeSQL.{Guid.NewGuid():N}.db");
        try
        {
            using var orm = CreateSqliteOrm(databasePath);
            orm.CodeFirst.SyncStructure<TransactionalSample>();
            var transactionActionManager = new RecordingTransactionActionManager();
            var services = new ServiceCollection();
            services.AddSingleton<ITransactionActionManager>(transactionActionManager);
            using var serviceProvider = services.BuildServiceProvider();
            using var unitOfWork = new TestUnitOfWork(new FreeSqlWrapper { Orm = orm }, serviceProvider);

            // Act
            unitOfWork.Commit();

            // Assert
            Assert.True(transactionActionManager.CommitRequested);
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    /// <summary>
    /// 测试目的：同步事务动作写入后失败时必须回滚，不能让未完成事务的数据被独立查询读取。
    /// </summary>
    [Fact]
    public void Commit_WhenTransactionActionFails_ShouldRollbackActionChanges()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"Bing.FreeSQL.{Guid.NewGuid():N}.db");
        try
        {
            using var orm = CreateSqliteOrm(databasePath);
            orm.CodeFirst.SyncStructure<TransactionalSample>();
            var transactionActionManager = new RecordingTransactionActionManager(transaction =>
            {
                ExecuteInsertAsync(transaction, "sync-failed").GetAwaiter().GetResult();
                throw new InvalidOperationException("sync transaction action failure");
            });
            var services = new ServiceCollection();
            services.AddSingleton<ITransactionActionManager>(transactionActionManager);
            using var serviceProvider = services.BuildServiceProvider();
            using var unitOfWork = new TestUnitOfWork(new FreeSqlWrapper { Orm = orm }, serviceProvider);

            // Act and Assert
            var exception = Assert.Throws<InvalidOperationException>(() => unitOfWork.Commit());

            // Assert
            Assert.Equal("sync transaction action failure", exception.Message);
            Assert.Equal(0, orm.Select<TransactionalSample>().Count());
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    /// <summary>
    /// 测试目的：事务动作写入后失败时，已开启的 FreeSQL 事务必须回滚，独立连接不能读取未提交写入。
    /// </summary>
    [Fact]
    public async Task SaveChangesAsync_WhenTransactionActionFails_ShouldRollbackActionChanges()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"Bing.FreeSQL.{Guid.NewGuid():N}.db");
        try
        {
            using var orm = CreateSqliteOrm(databasePath);
            orm.CodeFirst.SyncStructure<TransactionalSample>();
            var transactionActionManager = new RecordingTransactionActionManager(async transaction =>
            {
                await ExecuteInsertAsync(transaction, "failed");
                throw new InvalidOperationException("transaction action failure");
            });
            var services = new ServiceCollection();
            services.AddSingleton<ITransactionActionManager>(transactionActionManager);
            using var serviceProvider = services.BuildServiceProvider();
            using var unitOfWork = new TestUnitOfWork(new FreeSqlWrapper { Orm = orm }, serviceProvider);

            // Act
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.SaveChangesAsync());

            // Assert
            Assert.Equal("transaction action failure", exception.Message);
            Assert.Equal(0, orm.Select<TransactionalSample>().Count());
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    /// <summary>
    /// 测试目的：事务动作写入后触发取消时，保存操作必须回滚，不能让取消前的写入对独立查询可见。
    /// </summary>
    [Fact]
    public async Task SaveChangesAsync_WhenCancelledAfterTransactionAction_ShouldRollbackActionChanges()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"Bing.FreeSQL.{Guid.NewGuid():N}.db");
        try
        {
            using var orm = CreateSqliteOrm(databasePath);
            orm.CodeFirst.SyncStructure<TransactionalSample>();
            using var cancellationTokenSource = new CancellationTokenSource();
            var transactionActionManager = new RecordingTransactionActionManager(async transaction =>
            {
                await ExecuteInsertAsync(transaction, "cancelled");
                cancellationTokenSource.Cancel();
            });
            var services = new ServiceCollection();
            services.AddSingleton<ITransactionActionManager>(transactionActionManager);
            using var serviceProvider = services.BuildServiceProvider();
            using var unitOfWork = new TestUnitOfWork(new FreeSqlWrapper { Orm = orm }, serviceProvider);

            // Act and Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => unitOfWork.SaveChangesAsync(
                cancellationTokenSource.Token));
            Assert.Equal(0, orm.Select<TransactionalSample>().Count());
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    /// <summary>
    /// 创建不打开外部连接的 MySQL FreeSQL 实例。
    /// </summary>
    /// <returns>仅用于工作单元构造的 FreeSQL 实例。</returns>
    private static IFreeSql CreateOrm() => new FreeSqlBuilder()
        .UseConnectionFactory(DataType.MySql, () => new MySqlConnection())
        .Build();

    /// <summary>
    /// 创建使用独立临时文件的 SQLite FreeSQL 实例。
    /// </summary>
    /// <param name="databasePath">临时数据库文件路径。</param>
    /// <returns>用于事务可见性验证的 FreeSQL 实例。</returns>
    private static IFreeSql CreateSqliteOrm(string databasePath) => new FreeSqlBuilder()
        .UseConnectionString(DataType.Sqlite, $"Data Source={databasePath}")
        .Build();

    /// <summary>
    /// 在当前事务中插入测试数据。
    /// </summary>
    /// <param name="transaction">当前数据库事务。</param>
    /// <param name="name">写入名称。</param>
    private static async Task ExecuteInsertAsync(IDbTransaction transaction, string name)
    {
        using var command = transaction.Connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "Insert Into transaction_samples (Name) Values (@name)";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@name";
        parameter.Value = name;
        command.Parameters.Add(parameter);
        await ((DbCommand)command).ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 测试工作单元。
    /// </summary>
    private sealed class TestUnitOfWork : Bing.Uow.UnitOfWorkBase
    {
        /// <summary>
        /// 初始化一个 <see cref="TestUnitOfWork"/> 类型的实例。
        /// </summary>
        /// <param name="wrapper">FreeSQL 包装器。</param>
        /// <param name="serviceProvider">服务提供程序。</param>
        public TestUnitOfWork(FreeSqlWrapper wrapper, IServiceProvider serviceProvider)
            : base(wrapper, serviceProvider)
        {
        }
    }

    /// <summary>
    /// 记录事务动作调用情况的测试替身。
    /// </summary>
    private sealed class RecordingTransactionActionManager : ITransactionActionManager
    {
        /// <summary>
        /// 事务动作。
        /// </summary>
        private readonly Func<IDbTransaction, Task> _action;

        /// <summary>
        /// 初始化一个 <see cref="RecordingTransactionActionManager"/> 类型的实例。
        /// </summary>
        /// <param name="action">事务动作。</param>
        public RecordingTransactionActionManager(Func<IDbTransaction, Task> action = null) => _action = action;

        /// <summary>
        /// 表示存在一个待提交的事务动作。
        /// </summary>
        public int Count => 1;

        /// <summary>
        /// 是否请求提交事务动作。
        /// </summary>
        public bool CommitRequested { get; private set; }

        /// <inheritdoc />
        public void Register(Func<IDbTransaction, Task> action)
        {
        }

        /// <inheritdoc />
        public Task CommitAsync(IDbTransaction transaction)
        {
            CommitRequested = true;
            return _action?.Invoke(transaction) ?? Task.CompletedTask;
        }
    }

    /// <summary>
    /// 事务测试实体。
    /// </summary>
    [FreeSql.DataAnnotations.Table(Name = "transaction_samples")]
    private sealed class TransactionalSample
    {
        /// <summary>
        /// 标识。
        /// </summary>
        [FreeSql.DataAnnotations.Column(IsPrimary = true)]
        public int Id { get; set; }

        /// <summary>
        /// 名称。
        /// </summary>
        public string Name { get; set; }
    }
}