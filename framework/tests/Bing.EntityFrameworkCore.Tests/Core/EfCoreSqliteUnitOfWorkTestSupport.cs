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

internal static class EfCoreSqliteUnitOfWorkTestSupport
{
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

    public static async Task<int> CountRowsAsync(string connectionString)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM ef_uow_records";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    public static async Task ClearRowsAsync(string connectionString)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM ef_uow_records";
        await command.ExecuteNonQueryAsync();
    }

    public static async Task<int> CountOtherRowsAsync(string connectionString)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM ef_uow_other_records";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    public static async Task EnsureRecordsTableAsync(string connectionString)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE IF NOT EXISTS ef_uow_records (Id INTEGER NOT NULL PRIMARY KEY, Name TEXT NOT NULL)";
        await command.ExecuteNonQueryAsync();
    }
}

public class EfSqliteMappedUnitOfWork : UnitOfWorkBase
{
    public EfSqliteMappedUnitOfWork(DbContextOptions<EfSqliteMappedUnitOfWork> options,
        IServiceProvider serviceProvider) : base(options, serviceProvider)
    {
    }

    public DbSet<EfSqliteUowRecord> Records { get; set; }

    public int SaveAfterCount { get; private set; }

    public bool ThrowOnSaveChangesAfter { get; set; }

    public Action<EfSqliteMappedUnitOfWork> BeforeSaveChanges { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EfSqliteUowRecord>(builder =>
        {
            builder.ToTable("ef_uow_records");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Name).IsRequired();
        });
    }

    protected override void ConfiguringIgnoreEvent(DbContextOptionsBuilder builder)
    {
        base.ConfiguringIgnoreEvent(builder);
        builder.ConfigureWarnings(warnings => warnings.Log(RelationalEventId.TransactionDisposed));
    }

    protected override void SaveChangesBefore()
    {
        BeforeSaveChanges?.Invoke(this);
    }

    protected override Task SaveChangesAfter()
    {
        SaveAfterCount++;
        return ThrowOnSaveChangesAfter
            ? Task.FromException(new InvalidOperationException("EF post-commit callback failed."))
            : Task.CompletedTask;
    }
}

public sealed class EfSqliteSecondMappedUnitOfWork : UnitOfWorkBase
{
    public EfSqliteSecondMappedUnitOfWork(DbContextOptions<EfSqliteSecondMappedUnitOfWork> options,
        IServiceProvider serviceProvider) : base(options, serviceProvider)
    {
    }

    public DbSet<EfSqliteUowRecord> Records { get; set; }

    public Action<EfSqliteSecondMappedUnitOfWork> BeforeSaveChanges { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EfSqliteUowRecord>(builder =>
        {
            builder.ToTable("ef_uow_other_records");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Name).IsRequired();
        });
    }

    protected override void SaveChangesBefore()
    {
        BeforeSaveChanges?.Invoke(this);
    }
}

public sealed class EfSqliteDetectChangesThrowingUnitOfWork : UnitOfWorkBase
{
    public EfSqliteDetectChangesThrowingUnitOfWork(
        DbContextOptions<EfSqliteDetectChangesThrowingUnitOfWork> options, IServiceProvider serviceProvider)
        : base(options, serviceProvider)
    {
    }

    public DbSet<EfSqliteDetectChangesThrowingRecord> Records { get; set; }

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

    protected override void SaveChangesBefore()
    {
    }
}

public sealed class EfSqliteDetectChangesThrowingRecord : IKey<int>
{
    private string _name;

    public int Id { get; set; }

    public string Name
    {
        get => ThrowOnNameRead
            ? throw new InvalidOperationException("detect-changes-name-read")
            : _name;
        set => _name = value;
    }

    [NotMapped]
    public bool ThrowOnNameRead { get; set; }
}

public sealed class EfSqliteUnmappedUnitOfWork : UnitOfWorkBase
{
    public EfSqliteUnmappedUnitOfWork(DbContextOptions<EfSqliteUnmappedUnitOfWork> options,
        IServiceProvider serviceProvider) : base(options, serviceProvider)
    {
    }

    public DbSet<EfSqliteUowRecord> Records { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EfSqliteUowRecord>(builder =>
        {
            builder.ToTable("ef_uow_records");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Name).IsRequired();
        });
    }

    protected override void SaveChangesBefore()
    {
    }
}

internal sealed class EfSqliteLifecycleCounters : IDisposable
{
    private readonly DiagnosticObserver _diagnosticObserver;
    private readonly IDisposable _allListenersSubscription;
    private readonly ConcurrentBag<IDisposable> _listenerSubscriptions = new();
    private readonly ConcurrentBag<string> _diagnosticDetails = new();
    private DbConnection _targetConnection;
    private DbContext _targetContext;
    private Guid _targetConnectionId;
    private int _opened;
    private int _closed;
    private int _transactionsStarted;
    private int _transactionsCommitted;
    private int _transactionsRolledBack;
    private int _transactionsDisposed;

    public EfSqliteLifecycleCounters()
    {
        ConnectionInterceptor = new ConnectionLifecycleInterceptor(this);
        TransactionInterceptor = new TransactionLifecycleInterceptor(this);
        _diagnosticObserver = new DiagnosticObserver(this);
        _allListenersSubscription = DiagnosticListener.AllListeners.Subscribe(
            new ListenerObserver(this));
    }

    public DbConnectionInterceptor ConnectionInterceptor { get; }
    public DbTransactionInterceptor TransactionInterceptor { get; }
    public int Opened => Volatile.Read(ref _opened);
    public int Closed => Volatile.Read(ref _closed);
    public int TransactionsStarted => Volatile.Read(ref _transactionsStarted);
    public int TransactionsCommitted => Volatile.Read(ref _transactionsCommitted);
    public int TransactionsRolledBack => Volatile.Read(ref _transactionsRolledBack);
    public int TransactionsDisposed => Volatile.Read(ref _transactionsDisposed);
    public string ObservedDiagnosticEvents => string.Join(";", _diagnosticDetails);

    public void Track(DbContext context)
    {
        _targetContext = context ?? throw new ArgumentNullException(nameof(context));
        _targetConnection = context.Database.GetDbConnection();
        _targetConnectionId = Guid.Empty;
    }

    public void Dispose()
    {
        _allListenersSubscription.Dispose();
        while (_listenerSubscriptions.TryTake(out var subscription))
            subscription.Dispose();
    }

    private bool IsTarget(DbConnection connection) => ReferenceEquals(connection, _targetConnection);

    private bool IsTargetConnection(Guid connectionId) => connectionId != Guid.Empty &&
        connectionId == _targetConnectionId;

    private void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        if (!IsTarget(connection))
            return;
        _targetConnectionId = eventData.ConnectionId;
        Interlocked.Increment(ref _opened);
    }

    private void ConnectionClosed(DbConnection connection)
    {
        if (IsTarget(connection))
            Interlocked.Increment(ref _closed);
    }

    private void TransactionStarted(DbConnection connection, TransactionEndEventData eventData)
    {
        if (IsTarget(connection))
        {
            _targetConnectionId = eventData.ConnectionId;
            Interlocked.Increment(ref _transactionsStarted);
        }
    }

    private void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData)
    {
        if (IsTargetConnection(eventData.ConnectionId))
            Interlocked.Increment(ref _transactionsCommitted);
    }

    private void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData)
    {
        if (IsTargetConnection(eventData.ConnectionId))
            Interlocked.Increment(ref _transactionsRolledBack);
    }

    private sealed class ConnectionLifecycleInterceptor : DbConnectionInterceptor
    {
        private readonly EfSqliteLifecycleCounters _owner;

        public ConnectionLifecycleInterceptor(EfSqliteLifecycleCounters owner) => _owner = owner;

        public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData) =>
            _owner.ConnectionOpened(connection, eventData);

        public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            _owner.ConnectionOpened(connection, eventData);
            return Task.CompletedTask;
        }

        public override void ConnectionClosed(DbConnection connection, ConnectionEndEventData eventData) =>
            _owner.ConnectionClosed(connection);

        public override Task ConnectionClosedAsync(DbConnection connection, ConnectionEndEventData eventData)
        {
            _owner.ConnectionClosed(connection);
            return Task.CompletedTask;
        }
    }

    private sealed class TransactionLifecycleInterceptor : DbTransactionInterceptor
    {
        private readonly EfSqliteLifecycleCounters _owner;

        public TransactionLifecycleInterceptor(EfSqliteLifecycleCounters owner) => _owner = owner;

        public override DbTransaction TransactionStarted(DbConnection connection, TransactionEndEventData eventData,
            DbTransaction result)
        {
            _owner.TransactionStarted(connection, eventData);
            return result;
        }

        public override ValueTask<DbTransaction> TransactionStartedAsync(DbConnection connection,
            TransactionEndEventData eventData, DbTransaction result,
            CancellationToken cancellationToken = default)
        {
            _owner.TransactionStarted(connection, eventData);
            return ValueTask.FromResult(result);
        }

        public override void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData) =>
            _owner.TransactionCommitted(transaction, eventData);

        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            _owner.TransactionCommitted(transaction, eventData);
            return Task.CompletedTask;
        }

        public override void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData) =>
            _owner.TransactionRolledBack(transaction, eventData);

        public override Task TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            _owner.TransactionRolledBack(transaction, eventData);
            return Task.CompletedTask;
        }
    }

    private sealed class ListenerObserver : IObserver<DiagnosticListener>
    {
        private readonly EfSqliteLifecycleCounters _owner;

        public ListenerObserver(EfSqliteLifecycleCounters owner) => _owner = owner;
        public void OnCompleted() { }
        public void OnError(Exception error) { }

        public void OnNext(DiagnosticListener listener)
        {
            if (string.Equals(listener.Name, "Microsoft.EntityFrameworkCore", StringComparison.Ordinal))
                _owner._listenerSubscriptions.Add(listener.Subscribe(_owner._diagnosticObserver));
        }
    }

    private sealed class DiagnosticObserver : IObserver<KeyValuePair<string, object>>
    {
        private readonly EfSqliteLifecycleCounters _owner;

        public DiagnosticObserver(EfSqliteLifecycleCounters owner) => _owner = owner;
        public void OnCompleted() { }
        public void OnError(Exception error) { }

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

public sealed class EfSqliteUowRecord : IKey<int>, IHasDomainEvents
{
    private readonly List<DomainEvent> _domainEvents = new();

    public int Id { get; set; }
    public string Name { get; set; }

    public void AddDomainEvent(DomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public IReadOnlyCollection<DomainEvent> GetDomainEvents() => _domainEvents.AsReadOnly();
    public void RemoveDomainEvent(DomainEvent domainEvent) => _domainEvents.Remove(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();
}

internal sealed class EfSqliteTestDomainEvent : DomainEvent
{
    public EfSqliteTestDomainEvent(int sequence) => Sequence = sequence;
    public int Sequence { get; }
}

internal sealed class EfSqliteRecordingDomainEventDispatcher : IDomainEventDispatcher
{
    public List<int> Sequences { get; } = new();
    public Func<DomainEvent, Task> OnDispatch { get; set; }

    public bool Register<TDomainEvent, TDomainEventHandler>()
        where TDomainEvent : DomainEvent
        where TDomainEventHandler : IDomainEventHandler<TDomainEvent> =>
        throw new NotSupportedException();

    public bool Register<TDomainEvent>(Type handlerType) where TDomainEvent : DomainEvent =>
        throw new NotSupportedException();

    public bool Register(Type eventType, Type handlerType) => throw new NotSupportedException();

    public async Task DispatchAsync(DomainEvent domainEvent)
    {
        Sequences.Add(((EfSqliteTestDomainEvent)domainEvent).Sequence);
        if (OnDispatch != null)
            await OnDispatch(domainEvent);
    }

    public void Dispose()
    {
    }
}

public sealed class EfSqliteCapMappedUnitOfWork : UnitOfWorkBase
{
    public EfSqliteCapMappedUnitOfWork(DbContextOptions<EfSqliteCapMappedUnitOfWork> options,
        IServiceProvider serviceProvider) : base(options, serviceProvider)
    {
    }

    public DbSet<EfSqliteUowRecord> Records { get; set; }
    public int TransactionCommitCallCount { get; private set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EfSqliteUowRecord>(builder =>
        {
            builder.ToTable("ef_uow_records");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Name).IsRequired();
        });
    }

    protected override void SaveChangesBefore()
    {
    }

    protected override int TransactionCommit(ITransactionActionManager transactionActionManager)
    {
        TransactionCommitCallCount++;
        return base.TransactionCommit(transactionActionManager);
    }

    protected override async Task<int> TransactionCommitAsync(ITransactionActionManager transactionActionManager,
        CancellationToken cancellationToken)
    {
        TransactionCommitCallCount++;
        return await base.TransactionCommitAsync(transactionActionManager, cancellationToken);
    }
}
