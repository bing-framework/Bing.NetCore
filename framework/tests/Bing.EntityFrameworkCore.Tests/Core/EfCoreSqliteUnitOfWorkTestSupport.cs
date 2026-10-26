using Bing.Data.Enums;
using Bing.Dapper;
using Bing.Dapper.Sqlite;
using Bing.Data.Transaction;
using Bing.Data.Sql;
using Bing.Data.Sql.Configs;
using Bing.Datas.EntityFramework.Core;
using Bing.Domain.Entities;
using Bing.Domain.Entities.Events;
using Bing.EntityFrameworkCore;
using Bing.Uow;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Diagnostics;

namespace Bing.EntityFrameworkCore.Tests.Core;

/// <summary>
/// 为 EF Core SQLite 工作单元测试构建隔离数据库、服务容器和生命周期观测器。
/// </summary>
/// <remarks>
/// 每个测试使用独立的临时 SQLite 文件；测试结束后由调用方负责释放服务提供程序并删除临时文件。
/// </remarks>
internal static class EfCoreSqliteUnitOfWorkTestSupport
{
    /// <summary>
    /// 创建启用 SQLite 数据源、EF Core 工作单元和可选观测组件的测试服务提供程序。
    /// </summary>
    /// <param name="mapEfCore">为 <see langword="true" /> 时注册 EF Core SQL 工作单元映射；否则仅注册查询工厂。</param>
    /// <param name="contextConnectionString">覆盖 EF Core 上下文使用的连接字符串；为 <see langword="null" /> 时使用临时数据库。</param>
    /// <param name="configureDataSource">用于调整默认 SQLite 数据源的可选委托。</param>
    /// <param name="lifecycleCounters">可选的连接与事务生命周期计数器。</param>
    /// <param name="addGoodDataSource">是否额外注册名为 <c>good</c> 的数据源。</param>
    /// <param name="addBingOperations">是否注册 Bing 工作单元保存操作。</param>
    /// <param name="commandInterceptor">可选的 EF Core 命令拦截器。</param>
    /// <param name="addUnmappedEfContext">是否注册未映射到 SQL 工作单元的 EF Core 上下文。</param>
    /// <param name="goodConnectionString">名为 <c>good</c> 的数据源和第二个上下文使用的可选连接字符串。</param>
    /// <param name="addSecondMappedEfContext">是否注册第二个映射工作单元上下文。</param>
    /// <param name="addDetectChangesThrowingContext">是否注册在变更检测读取属性时抛异常的上下文。</param>
    /// <returns>测试服务提供程序、临时数据库连接字符串和数据库文件路径。</returns>
    public static (ServiceProvider Provider, string ConnectionString, string DatabasePath) CreateServices(
        bool mapEfCore = true, string contextConnectionString = null,
        Action<SqlDataSourceDescriptor> configureDataSource = null,
        EfSqliteLifecycleCounters lifecycleCounters = null, bool addGoodDataSource = false,
        bool addBingOperations = false, DbCommandInterceptor commandInterceptor = null,
        bool addUnmappedEfContext = false, string goodConnectionString = null,
        bool addSecondMappedEfContext = false, bool addDetectChangesThrowingContext = false)
    {
        var databasePath = Path.Combine(Path.GetTempPath(), "bing-ef-uow", $"{Guid.NewGuid():N}.db");
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath));
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var services = new ServiceCollection();
        services.AddLogging();
        services.ConfigureSqlMetadata(options =>
        {
            options.DataSources.DefaultDataSourceKey = "orders";
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(EfSqliteUowRecord),
                TableName = "ef_uow_records"
            });
        });
        services.AddSqlCore();
        services.AddSqlDataSource("orders", DatabaseType.Sqlite, connectionString,
            setupAction: configureDataSource);
        if (addGoodDataSource)
            services.AddSqlDataSource("good", DatabaseType.Sqlite, goodConnectionString ?? connectionString);
        services.AddSqliteProvider();
        services.AddUnitOfWorkScopes();
        services.AddScoped<ITransactionActionManager, TransactionActionManager>();
        services.AddSingleton<EfSqliteRecordingDomainEventDispatcher>();
        services.AddSingleton<IDomainEventDispatcher>(provider =>
            provider.GetRequiredService<EfSqliteRecordingDomainEventDispatcher>());
        services.AddDbContext<EfSqliteMappedUnitOfWork>(options =>
        {
            options.UseSqlite(contextConnectionString ?? connectionString);
            if (lifecycleCounters != null)
                options.AddInterceptors(lifecycleCounters.ConnectionInterceptor,
                    lifecycleCounters.TransactionInterceptor);
            if (commandInterceptor != null)
                options.AddInterceptors(commandInterceptor);
        });
        if (addUnmappedEfContext)
        {
            services.AddDbContext<EfSqliteUnmappedUnitOfWork>(options =>
            {
                options.UseSqlite(contextConnectionString ?? connectionString);
                if (lifecycleCounters != null)
                    options.AddInterceptors(lifecycleCounters.ConnectionInterceptor,
                        lifecycleCounters.TransactionInterceptor);
                if (commandInterceptor != null)
                    options.AddInterceptors(commandInterceptor);
            });
        }
        if (addSecondMappedEfContext)
        {
            services.AddDbContext<EfSqliteSecondMappedUnitOfWork>(options =>
            {
                options.UseSqlite(goodConnectionString ?? contextConnectionString ?? connectionString);
                if (lifecycleCounters != null)
                    options.AddInterceptors(lifecycleCounters.ConnectionInterceptor,
                        lifecycleCounters.TransactionInterceptor);
                if (commandInterceptor != null)
                    options.AddInterceptors(commandInterceptor);
            });
        }
        if (addDetectChangesThrowingContext)
        {
            services.AddDbContext<EfSqliteDetectChangesThrowingUnitOfWork>(options =>
            {
                options.UseSqlite(contextConnectionString ?? connectionString);
                if (lifecycleCounters != null)
                    options.AddInterceptors(lifecycleCounters.ConnectionInterceptor,
                        lifecycleCounters.TransactionInterceptor);
                if (commandInterceptor != null)
                    options.AddInterceptors(commandInterceptor);
            });
        }
        if (mapEfCore)
        {
            services.AddEfCoreSqlUnitOfWork<EfSqliteMappedUnitOfWork>("orders");
            if (addSecondMappedEfContext)
                services.AddEfCoreSqlUnitOfWork<EfSqliteSecondMappedUnitOfWork>("good");
        }
        else
            services.AddEfCoreSqlQueryFactory();
        if (addBingOperations)
            services.AddBingOperations();
        return (services.BuildServiceProvider(), connectionString, databasePath);
    }

    /// <summary>
    /// 创建用于 CAP 事务测试的 SQLite 服务提供程序。
    /// </summary>
    /// <param name="connectionString">目标 SQLite 连接字符串。</param>
    /// <param name="addBingOperations">是否注册 Bing 工作单元保存操作。</param>
    /// <returns>已注册 CAP 映射工作单元的测试服务提供程序。</returns>
    public static ServiceProvider CreateCapServices(string connectionString, bool addBingOperations = false)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.ConfigureSqlMetadata(options => options.DataSources.DefaultDataSourceKey = "orders");
        services.AddSqlCore();
        services.AddSqlDataSource("orders", DatabaseType.Sqlite, connectionString);
        services.AddSqliteProvider();
        services.AddUnitOfWorkScopes();
        services.AddScoped<ITransactionActionManager, TransactionActionManager>();
        services.AddDbContext<EfSqliteCapMappedUnitOfWork>(options => options.UseSqlite(connectionString));
        services.AddEfCoreSqlUnitOfWork<EfSqliteCapMappedUnitOfWork>("orders");
        if (addBingOperations)
            services.AddBingOperations();
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 为已注册的 EF Core 上下文创建数据库结构。
    /// </summary>
    /// <param name="serviceProvider">包含 EF Core 上下文的测试服务提供程序。</param>
    public static async Task EnsureCreatedAsync(IServiceProvider serviceProvider)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<EfSqliteMappedUnitOfWork>().Database.EnsureCreatedAsync();
        var secondContext = scope.ServiceProvider.GetService<EfSqliteSecondMappedUnitOfWork>();
        if (secondContext != null)
            await secondContext.Database.EnsureCreatedAsync();
        var throwingContext = scope.ServiceProvider.GetService<EfSqliteDetectChangesThrowingUnitOfWork>();
        if (throwingContext != null)
            await throwingContext.Database.EnsureCreatedAsync();
    }

    /// <summary>
    /// 统计主测试表中的记录数。
    /// </summary>
    /// <param name="connectionString">目标 SQLite 连接字符串。</param>
    /// <returns><c>ef_uow_records</c> 表中的记录数。</returns>
    public static async Task<int> CountRowsAsync(string connectionString)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM ef_uow_records";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    /// <summary>
    /// 删除主测试表中的全部记录。
    /// </summary>
    /// <param name="connectionString">目标 SQLite 连接字符串。</param>
    public static async Task ClearRowsAsync(string connectionString)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM ef_uow_records";
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 统计第二个测试表中的记录数。
    /// </summary>
    /// <param name="connectionString">目标 SQLite 连接字符串。</param>
    /// <returns><c>ef_uow_other_records</c> 表中的记录数。</returns>
    public static async Task<int> CountOtherRowsAsync(string connectionString)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM ef_uow_other_records";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    /// <summary>
    /// 确保主测试表存在，便于直接使用原生连接验证持久化结果。
    /// </summary>
    /// <param name="connectionString">目标 SQLite 连接字符串。</param>
    public static async Task EnsureRecordsTableAsync(string connectionString)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE IF NOT EXISTS ef_uow_records (Id INTEGER NOT NULL PRIMARY KEY, Name TEXT NOT NULL)";
        await command.ExecuteNonQueryAsync();
    }
}

/// <summary>
/// 测试用 EF Core 工作单元上下文，映射主测试表并提供保存回调观测点。
/// </summary>
public class EfSqliteMappedUnitOfWork : UnitOfWorkBase
{
    /// <summary>
    /// 初始化一个 <see cref="EfSqliteMappedUnitOfWork" /> 类的新实例。
    /// </summary>
    /// <param name="options">EF Core 上下文选项。</param>
    /// <param name="serviceProvider">当前依赖注入作用域的服务提供程序。</param>
    public EfSqliteMappedUnitOfWork(DbContextOptions<EfSqliteMappedUnitOfWork> options,
        IServiceProvider serviceProvider) : base(options, serviceProvider)
    {
    }

    /// <summary>
    /// 获取或设置主测试记录集合。
    /// </summary>
    public DbSet<EfSqliteUowRecord> Records { get; set; }

    /// <summary>
    /// 获取提交后保存回调的执行次数。
    /// </summary>
    public int SaveAfterCount { get; private set; }

    /// <summary>
    /// 获取或设置提交后保存回调是否抛出异常。
    /// </summary>
    public bool ThrowOnSaveChangesAfter { get; set; }

    /// <summary>
    /// 获取或设置保存前执行的回调。
    /// </summary>
    public Action<EfSqliteMappedUnitOfWork> BeforeSaveChanges { get; set; }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EfSqliteUowRecord>(builder =>
        {
            builder.ToTable("ef_uow_records");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Name).IsRequired();
        });
    }

    /// <inheritdoc />
    /// <remarks>
    /// 记录事务释放警告，便于测试确认资源清理路径被触发。
    /// </remarks>
    protected override void ConfiguringIgnoreEvent(DbContextOptionsBuilder builder)
    {
        base.ConfiguringIgnoreEvent(builder);
        builder.ConfigureWarnings(warnings => warnings.Log(RelationalEventId.TransactionDisposed));
    }

    /// <inheritdoc />
    protected override void SaveChangesBefore()
    {
        BeforeSaveChanges?.Invoke(this);
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException"><see cref="ThrowOnSaveChangesAfter" /> 为 <see langword="true" /> 时抛出。</exception>
    protected override Task SaveChangesAfter()
    {
        SaveAfterCount++;
        return ThrowOnSaveChangesAfter
            ? Task.FromException(new InvalidOperationException("EF post-commit callback failed."))
            : Task.CompletedTask;
    }
}

/// <summary>
/// 映射第二个测试表的 EF Core 工作单元上下文。
/// </summary>
public sealed class EfSqliteSecondMappedUnitOfWork : UnitOfWorkBase
{
    /// <summary>
    /// 初始化一个 <see cref="EfSqliteSecondMappedUnitOfWork" /> 类的新实例。
    /// </summary>
    /// <param name="options">EF Core 上下文选项。</param>
    /// <param name="serviceProvider">当前依赖注入作用域的服务提供程序。</param>
    public EfSqliteSecondMappedUnitOfWork(DbContextOptions<EfSqliteSecondMappedUnitOfWork> options,
        IServiceProvider serviceProvider) : base(options, serviceProvider)
    {
    }

    /// <summary>
    /// 获取或设置第二个测试表中的记录集合。
    /// </summary>
    public DbSet<EfSqliteUowRecord> Records { get; set; }

    /// <summary>
    /// 获取或设置保存前执行的回调。
    /// </summary>
    public Action<EfSqliteSecondMappedUnitOfWork> BeforeSaveChanges { get; set; }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EfSqliteUowRecord>(builder =>
        {
            builder.ToTable("ef_uow_other_records");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Name).IsRequired();
        });
    }

    /// <inheritdoc />
    protected override void SaveChangesBefore()
    {
        BeforeSaveChanges?.Invoke(this);
    }
}

/// <summary>
/// 在保存前执行变更检测的 EF Core 工作单元上下文。
/// </summary>
public sealed class EfSqliteDetectChangesThrowingUnitOfWork : UnitOfWorkBase
{
    /// <summary>
    /// 初始化一个 <see cref="EfSqliteDetectChangesThrowingUnitOfWork" /> 类的新实例。
    /// </summary>
    /// <param name="options">EF Core 上下文选项。</param>
    /// <param name="serviceProvider">当前依赖注入作用域的服务提供程序。</param>
    public EfSqliteDetectChangesThrowingUnitOfWork(
        DbContextOptions<EfSqliteDetectChangesThrowingUnitOfWork> options, IServiceProvider serviceProvider)
        : base(options, serviceProvider)
    {
    }

    /// <summary>
    /// 获取或设置用于触发属性读取异常的记录集合。
    /// </summary>
    public DbSet<EfSqliteDetectChangesThrowingRecord> Records { get; set; }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EfSqliteDetectChangesThrowingRecord>(builder =>
        {
            builder.ToTable("ef_uow_records");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Name).IsRequired()
                .UsePropertyAccessMode(PropertyAccessMode.Property);
        });
    }

    /// <inheritdoc />
    protected override void SaveChangesBefore()
    {
    }
}

/// <summary>
/// 可在变更检测读取名称属性时抛异常的测试记录。
/// </summary>
public sealed class EfSqliteDetectChangesThrowingRecord : IKey<int>
{
    /// <summary>
    /// 名称属性的实际存储值。
    /// </summary>
    private string _name;

    /// <inheritdoc />
    public int Id { get; set; }

    /// <summary>
    /// 获取或设置记录名称；启用 <see cref="ThrowOnNameRead" /> 时读取会抛出异常。
    /// </summary>
    /// <exception cref="InvalidOperationException">读取名称且 <see cref="ThrowOnNameRead" /> 为 <see langword="true" /> 时抛出。</exception>
    public string Name
    {
        get => ThrowOnNameRead
            ? throw new InvalidOperationException("detect-changes-name-read")
            : _name;
        set => _name = value;
    }

    /// <summary>
    /// 获取或设置读取 <see cref="Name" /> 时是否抛出异常。
    /// </summary>
    [NotMapped]
    public bool ThrowOnNameRead { get; set; }
}

/// <summary>
/// 未注册到 SQL 工作单元映射的 EF Core 上下文。
/// </summary>
public sealed class EfSqliteUnmappedUnitOfWork : UnitOfWorkBase
{
    /// <summary>
    /// 初始化一个 <see cref="EfSqliteUnmappedUnitOfWork" /> 类的新实例。
    /// </summary>
    /// <param name="options">EF Core 上下文选项。</param>
    /// <param name="serviceProvider">当前依赖注入作用域的服务提供程序。</param>
    public EfSqliteUnmappedUnitOfWork(DbContextOptions<EfSqliteUnmappedUnitOfWork> options,
        IServiceProvider serviceProvider) : base(options, serviceProvider)
    {
    }

    /// <summary>
    /// 获取或设置主测试记录集合。
    /// </summary>
    public DbSet<EfSqliteUowRecord> Records { get; set; }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EfSqliteUowRecord>(builder =>
        {
            builder.ToTable("ef_uow_records");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Name).IsRequired();
        });
    }

    /// <inheritdoc />
    protected override void SaveChangesBefore()
    {
    }
}

/// <summary>
/// 记录目标 EF Core 上下文的连接、事务和诊断事件计数。
/// </summary>
/// <remarks>
/// 只统计通过 <see cref="Track" /> 指定的上下文和连接，避免并行测试或其他上下文的噪声。
/// </remarks>
internal sealed class EfSqliteLifecycleCounters : IDisposable
{
    /// <summary>
    /// 将 EF Core 诊断事件写入计数的观察者。
    /// </summary>
    private readonly DiagnosticObserver _diagnosticObserver;

    /// <summary>
    /// 对全部诊断监听器的订阅，用于释放时取消登记。
    /// </summary>
    private readonly IDisposable _allListenersSubscription;

    /// <summary>
    /// 对匹配的 EF Core 诊断监听器建立的订阅集合。
    /// </summary>
    private readonly ConcurrentBag<IDisposable> _listenerSubscriptions = new();

    /// <summary>
    /// 观测到的诊断事件说明文本。
    /// </summary>
    private readonly ConcurrentBag<string> _diagnosticDetails = new();

    /// <summary>
    /// 当前跟踪的目标数据库连接。
    /// </summary>
    private DbConnection _targetConnection;

    /// <summary>
    /// 当前跟踪的目标 EF Core 上下文。
    /// </summary>
    private DbContext _targetContext;

    /// <summary>
    /// 目标连接在 EF Core 诊断事件中的标识。
    /// </summary>
    private Guid _targetConnectionId;

    /// <summary>
    /// 目标连接打开次数。
    /// </summary>
    private int _opened;

    /// <summary>
    /// 目标连接关闭次数。
    /// </summary>
    private int _closed;

    /// <summary>
    /// 目标连接上的事务开始次数。
    /// </summary>
    private int _transactionsStarted;

    /// <summary>
    /// 目标连接上的事务提交次数。
    /// </summary>
    private int _transactionsCommitted;

    /// <summary>
    /// 目标连接上的事务回滚次数。
    /// </summary>
    private int _transactionsRolledBack;

    /// <summary>
    /// 目标上下文的事务释放次数。
    /// </summary>
    private int _transactionsDisposed;

    /// <summary>
    /// 初始化一个 <see cref="EfSqliteLifecycleCounters" /> 类的新实例。
    /// </summary>
    /// <remarks>
    /// 构造时创建连接与事务拦截器，并开始监听 EF Core 诊断事件。
    /// </remarks>
    public EfSqliteLifecycleCounters()
    {
        ConnectionInterceptor = new ConnectionLifecycleInterceptor(this);
        TransactionInterceptor = new TransactionLifecycleInterceptor(this);
        _diagnosticObserver = new DiagnosticObserver(this);
        _allListenersSubscription = DiagnosticListener.AllListeners.Subscribe(
            new ListenerObserver(this));
    }

    /// <summary>
    /// 获取记录连接打开与关闭事件的 EF Core 拦截器。
    /// </summary>
    public DbConnectionInterceptor ConnectionInterceptor { get; }

    /// <summary>
    /// 获取记录事务开始、提交和回滚事件的 EF Core 拦截器。
    /// </summary>
    public DbTransactionInterceptor TransactionInterceptor { get; }

    /// <summary>
    /// 获取目标连接已打开的累计次数。
    /// </summary>
    public int Opened => Volatile.Read(ref _opened);

    /// <summary>
    /// 获取目标连接已关闭的累计次数。
    /// </summary>
    public int Closed => Volatile.Read(ref _closed);

    /// <summary>
    /// 获取目标连接上已开始的事务累计次数。
    /// </summary>
    public int TransactionsStarted => Volatile.Read(ref _transactionsStarted);

    /// <summary>
    /// 获取目标连接上已提交的事务累计次数。
    /// </summary>
    public int TransactionsCommitted => Volatile.Read(ref _transactionsCommitted);

    /// <summary>
    /// 获取目标连接上已回滚的事务累计次数。
    /// </summary>
    public int TransactionsRolledBack => Volatile.Read(ref _transactionsRolledBack);

    /// <summary>
    /// 获取目标上下文中已释放的事务累计次数。
    /// </summary>
    public int TransactionsDisposed => Volatile.Read(ref _transactionsDisposed);

    /// <summary>
    /// 获取观测到的诊断事件说明，使用分号分隔。
    /// </summary>
    public string ObservedDiagnosticEvents => string.Join(";", _diagnosticDetails);

    /// <summary>
    /// 设置要统计的 EF Core 上下文及其数据库连接。
    /// </summary>
    /// <param name="context">目标 EF Core 上下文。</param>
    /// <exception cref="ArgumentNullException"><paramref name="context" /> 为 <see langword="null" /> 时抛出。</exception>
    public void Track(DbContext context)
    {
        _targetContext = context ?? throw new ArgumentNullException(nameof(context));
        _targetConnection = context.Database.GetDbConnection();
        _targetConnectionId = Guid.Empty;
    }

    /// <inheritdoc />
    /// <remarks>
    /// 取消全部诊断订阅并释放计数器。
    /// </remarks>
    public void Dispose()
    {
        _allListenersSubscription.Dispose();
        while (_listenerSubscriptions.TryTake(out var subscription))
            subscription.Dispose();
    }

    /// <summary>
    /// 判断连接是否为当前跟踪的目标连接。
    /// </summary>
    /// <param name="connection">待判断的数据库连接。</param>
    /// <returns>属于目标连接时返回 <see langword="true" />；否则返回 <see langword="false" />。</returns>
    private bool IsTarget(DbConnection connection) => ReferenceEquals(connection, _targetConnection);

    /// <summary>
    /// 判断诊断事件中的连接标识是否属于当前跟踪的目标连接。
    /// </summary>
    /// <param name="connectionId">诊断事件携带的连接标识。</param>
    /// <returns>标识非空且与目标连接一致时返回 <see langword="true" />；否则返回 <see langword="false" />。</returns>
    private bool IsTargetConnection(Guid connectionId) => connectionId != Guid.Empty &&
        connectionId == _targetConnectionId;

    /// <summary>
    /// 记录目标连接打开事件。
    /// </summary>
    /// <param name="connection">打开事件的数据库连接。</param>
    /// <param name="eventData">EF Core 连接结束事件数据。</param>
    private void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        if (!IsTarget(connection))
            return;
        _targetConnectionId = eventData.ConnectionId;
        Interlocked.Increment(ref _opened);
    }

    /// <summary>
    /// 记录目标连接关闭事件。
    /// </summary>
    /// <param name="connection">关闭事件的数据库连接。</param>
    private void ConnectionClosed(DbConnection connection)
    {
        if (IsTarget(connection))
            Interlocked.Increment(ref _closed);
    }

    /// <summary>
    /// 记录目标连接上的事务开始事件。
    /// </summary>
    /// <param name="connection">事务所在连接。</param>
    /// <param name="eventData">EF Core 事务结束事件数据。</param>
    private void TransactionStarted(DbConnection connection, TransactionEndEventData eventData)
    {
        if (IsTarget(connection))
        {
            _targetConnectionId = eventData.ConnectionId;
            Interlocked.Increment(ref _transactionsStarted);
        }
    }

    /// <summary>
    /// 记录目标连接上的事务提交事件。
    /// </summary>
    /// <param name="transaction">已提交的事务。</param>
    /// <param name="eventData">EF Core 事务结束事件数据。</param>
    private void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData)
    {
        if (IsTargetConnection(eventData.ConnectionId))
            Interlocked.Increment(ref _transactionsCommitted);
    }

    /// <summary>
    /// 记录目标连接上的事务回滚事件。
    /// </summary>
    /// <param name="transaction">已回滚的事务。</param>
    /// <param name="eventData">EF Core 事务结束事件数据。</param>
    private void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData)
    {
        if (IsTargetConnection(eventData.ConnectionId))
            Interlocked.Increment(ref _transactionsRolledBack);
    }

    /// <summary>
    /// 把连接生命周期事件转发给所属计数器。
    /// </summary>
    private sealed class ConnectionLifecycleInterceptor : DbConnectionInterceptor
    {
        /// <summary>
        /// 接收连接事件的目标计数器。
        /// </summary>
        private readonly EfSqliteLifecycleCounters _owner;

        /// <summary>
        /// 初始化一个 <see cref="ConnectionLifecycleInterceptor" /> 类的新实例。
        /// </summary>
        /// <param name="owner">接收连接事件的目标计数器。</param>
        public ConnectionLifecycleInterceptor(EfSqliteLifecycleCounters owner) => _owner = owner;

        /// <inheritdoc />
        public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData) =>
            _owner.ConnectionOpened(connection, eventData);

        /// <inheritdoc />
        public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            _owner.ConnectionOpened(connection, eventData);
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public override void ConnectionClosed(DbConnection connection, ConnectionEndEventData eventData) =>
            _owner.ConnectionClosed(connection);

        /// <inheritdoc />
        public override Task ConnectionClosedAsync(DbConnection connection, ConnectionEndEventData eventData)
        {
            _owner.ConnectionClosed(connection);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 把事务生命周期事件转发给所属计数器。
    /// </summary>
    private sealed class TransactionLifecycleInterceptor : DbTransactionInterceptor
    {
        /// <summary>
        /// 接收事务事件的目标计数器。
        /// </summary>
        private readonly EfSqliteLifecycleCounters _owner;

        /// <summary>
        /// 初始化一个 <see cref="TransactionLifecycleInterceptor" /> 类的新实例。
        /// </summary>
        /// <param name="owner">接收事务事件的目标计数器。</param>
        public TransactionLifecycleInterceptor(EfSqliteLifecycleCounters owner) => _owner = owner;

        /// <inheritdoc />
        public override DbTransaction TransactionStarted(DbConnection connection, TransactionEndEventData eventData,
            DbTransaction result)
        {
            _owner.TransactionStarted(connection, eventData);
            return result;
        }

        /// <inheritdoc />
        public override ValueTask<DbTransaction> TransactionStartedAsync(DbConnection connection,
            TransactionEndEventData eventData, DbTransaction result,
            CancellationToken cancellationToken = default)
        {
            _owner.TransactionStarted(connection, eventData);
            return ValueTask.FromResult(result);
        }

        /// <inheritdoc />
        public override void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData) =>
            _owner.TransactionCommitted(transaction, eventData);

        /// <inheritdoc />
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            _owner.TransactionCommitted(transaction, eventData);
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public override void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData) =>
            _owner.TransactionRolledBack(transaction, eventData);

        /// <inheritdoc />
        public override Task TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            _owner.TransactionRolledBack(transaction, eventData);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 订阅 EF Core 诊断监听器并转发给计数器的观察者。
    /// </summary>
    private sealed class ListenerObserver : IObserver<DiagnosticListener>
    {
        /// <summary>
        /// 接收诊断监听器订阅的目标计数器。
        /// </summary>
        private readonly EfSqliteLifecycleCounters _owner;

        /// <summary>
        /// 初始化一个 <see cref="ListenerObserver" /> 类的新实例。
        /// </summary>
        /// <param name="owner">接收诊断监听器订阅的目标计数器。</param>
        public ListenerObserver(EfSqliteLifecycleCounters owner) => _owner = owner;

        /// <inheritdoc />
        public void OnCompleted() { }

        /// <inheritdoc />
        public void OnError(Exception error) { }

        /// <inheritdoc />
        public void OnNext(DiagnosticListener listener)
        {
            if (string.Equals(listener.Name, "Microsoft.EntityFrameworkCore", StringComparison.Ordinal))
                _owner._listenerSubscriptions.Add(listener.Subscribe(_owner._diagnosticObserver));
        }
    }

    /// <summary>
    /// 把 EF Core 诊断事件转发给计数器的观察者。
    /// </summary>
    private sealed class DiagnosticObserver : IObserver<KeyValuePair<string, object>>
    {
        /// <summary>
        /// 接收诊断事件的目标计数器。
        /// </summary>
        private readonly EfSqliteLifecycleCounters _owner;

        /// <summary>
        /// 初始化一个 <see cref="DiagnosticObserver" /> 类的新实例。
        /// </summary>
        /// <param name="owner">接收诊断事件的目标计数器。</param>
        public DiagnosticObserver(EfSqliteLifecycleCounters owner) => _owner = owner;

        /// <inheritdoc />
        public void OnCompleted() { }

        /// <inheritdoc />
        public void OnError(Exception error) { }

        /// <inheritdoc />
        public void OnNext(KeyValuePair<string, object> value)
        {
            if (value.Key.Contains("TransactionDisposed", StringComparison.Ordinal) &&
                value.Value is TransactionEventData eventData)
            {
                _owner._diagnosticDetails.Add($"TransactionDisposed context match: " +
                    ReferenceEquals(eventData.Context, _owner._targetContext));
                if (ReferenceEquals(eventData.Context, _owner._targetContext))
                    Interlocked.Increment(ref _owner._transactionsDisposed);
            }
        }
    }
}

/// <summary>
/// 测试用工作单元记录实体，同时承载领域事件集合。
/// </summary>
public sealed class EfSqliteUowRecord : IKey<int>, IHasDomainEvents
{
    /// <summary>
    /// 当前实体尚未派发的领域事件。
    /// </summary>
    private readonly List<DomainEvent> _domainEvents = new();

    /// <inheritdoc />
    public int Id { get; set; }

    /// <summary>
    /// 获取或设置记录名称。
    /// </summary>
    public string Name { get; set; }

    /// <inheritdoc />
    public void AddDomainEvent(DomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    /// <inheritdoc />
    public IReadOnlyCollection<DomainEvent> GetDomainEvents() => _domainEvents.AsReadOnly();

    /// <inheritdoc />
    public void RemoveDomainEvent(DomainEvent domainEvent) => _domainEvents.Remove(domainEvent);

    /// <inheritdoc />
    public void ClearDomainEvents() => _domainEvents.Clear();
}

/// <summary>
/// 携带递增序号的测试领域事件。
/// </summary>
internal sealed class EfSqliteTestDomainEvent : DomainEvent
{
    /// <summary>
    /// 初始化一个 <see cref="EfSqliteTestDomainEvent" /> 类的新实例。
    /// </summary>
    /// <param name="sequence">用于断言派发顺序的序号。</param>
    public EfSqliteTestDomainEvent(int sequence) => Sequence = sequence;

    /// <summary>
    /// 获取用于断言派发顺序的序号。
    /// </summary>
    public int Sequence { get; }
}

/// <summary>
/// 记录领域事件派发顺序并允许注入额外行为的测试派发器。
/// </summary>
internal sealed class EfSqliteRecordingDomainEventDispatcher : IDomainEventDispatcher
{
    /// <summary>
    /// 获取已派发事件的序号列表。
    /// </summary>
    public List<int> Sequences { get; } = new();

    /// <summary>
    /// 获取或设置每次派发后执行的可选回调。
    /// </summary>
    public Func<DomainEvent, Task> OnDispatch { get; set; }

    /// <inheritdoc />
    public bool Register<TDomainEvent, TDomainEventHandler>()
        where TDomainEvent : DomainEvent
        where TDomainEventHandler : IDomainEventHandler<TDomainEvent> =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public bool Register<TDomainEvent>(Type handlerType) where TDomainEvent : DomainEvent =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public bool Register(Type eventType, Type handlerType) => throw new NotSupportedException();

    /// <inheritdoc />
    public async Task DispatchAsync(DomainEvent domainEvent)
    {
        Sequences.Add(((EfSqliteTestDomainEvent)domainEvent).Sequence);
        if (OnDispatch != null)
            await OnDispatch(domainEvent);
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }
}

/// <summary>
/// 用于 CAP 事务访问测试的映射工作单元上下文。
/// </summary>
public sealed class EfSqliteCapMappedUnitOfWork : UnitOfWorkBase
{
    /// <summary>
    /// 初始化一个 <see cref="EfSqliteCapMappedUnitOfWork" /> 类的新实例。
    /// </summary>
    /// <param name="options">EF Core 上下文选项。</param>
    /// <param name="serviceProvider">当前依赖注入作用域的服务提供程序。</param>
    public EfSqliteCapMappedUnitOfWork(DbContextOptions<EfSqliteCapMappedUnitOfWork> options,
        IServiceProvider serviceProvider) : base(options, serviceProvider)
    {
    }

    /// <summary>
    /// 获取或设置主测试记录集合。
    /// </summary>
    public DbSet<EfSqliteUowRecord> Records { get; set; }

    /// <summary>
    /// 获取事务提交方法被调用的累计次数。
    /// </summary>
    public int TransactionCommitCallCount { get; private set; }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EfSqliteUowRecord>(builder =>
        {
            builder.ToTable("ef_uow_records");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Name).IsRequired();
        });
    }

    /// <inheritdoc />
    protected override void SaveChangesBefore()
    {
    }

    /// <inheritdoc />
    protected override int TransactionCommit(ITransactionActionManager transactionActionManager)
    {
        TransactionCommitCallCount++;
        return base.TransactionCommit(transactionActionManager);
    }

    /// <inheritdoc />
    protected override async Task<int> TransactionCommitAsync(ITransactionActionManager transactionActionManager,
        CancellationToken cancellationToken)
    {
        TransactionCommitCallCount++;
        return await base.TransactionCommitAsync(transactionActionManager, cancellationToken);
    }
}
