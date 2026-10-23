using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MySqlConnector;

namespace Bing.Samples.SqlUnitOfWork.Infrastructure.Database;

/// <summary>
/// 在宿主启动完成前创建两份本地数据库结构。
/// </summary>
public sealed class DatabaseInitializerHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SampleDatabaseFiles _files;
    private readonly SqlUnitOfWorkSampleOptions _options;

    public DatabaseInitializerHostedService(IServiceScopeFactory scopeFactory, SampleDatabaseFiles files,
        SqlUnitOfWorkSampleOptions options)
    {
        _scopeFactory = scopeFactory;
        _files = files;
        _options = options;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_options.CapMySqlMode)
        {
            await using var ordersConnection = new MySqlConnection(_options.OrdersConnectionString);
            await ordersConnection.OpenAsync(cancellationToken);
            await using var ordersCommand = ordersConnection.CreateCommand();
            ordersCommand.CommandText = CapOrdersSchemaSql;
            await ordersCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var scope = _scopeFactory.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<SampleUnitOfWorkDbContext>();
            await context.Database.EnsureCreatedAsync(cancellationToken);
        }

        if (_options.CapMySqlMode)
        {
            await using var mysqlConnection = new MySqlConnection(_options.ReportingConnectionString);
            await mysqlConnection.OpenAsync(cancellationToken);
            await using var mysqlCommand = mysqlConnection.CreateCommand();
            mysqlCommand.CommandText = @"CREATE TABLE IF NOT EXISTS ReportRecords (
                Id VARCHAR(191) NOT NULL PRIMARY KEY,
                OperationId VARCHAR(191) NOT NULL,
                Value VARCHAR(500) NOT NULL
            ) ENGINE=InnoDB;
            CREATE TABLE IF NOT EXISTS CapBusinessEvents (
                EventId VARCHAR(191) NOT NULL PRIMARY KEY,
                OperationId VARCHAR(191) NOT NULL,
                TraceId VARCHAR(191) NOT NULL,
                RootId VARCHAR(64) NOT NULL,
                Source VARCHAR(64) NOT NULL,
                Topic VARCHAR(191) NOT NULL,
                Scenario VARCHAR(64) NOT NULL,
                CreatedAt DATETIME(6) NOT NULL
            ) ENGINE=InnoDB;";
            await mysqlCommand.ExecuteNonQueryAsync(cancellationToken);
            return;
        }

        await using var connection = new SqliteConnection(_files.ReportingConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"CREATE TABLE IF NOT EXISTS ReportRecords (
            Id TEXT NOT NULL PRIMARY KEY,
            OperationId TEXT NOT NULL,
            Value TEXT NOT NULL
        );";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private const string CapOrdersSchemaSql = @"CREATE TABLE IF NOT EXISTS OrderRecords (
        Id VARCHAR(255) NOT NULL PRIMARY KEY,
        OperationId VARCHAR(255) NOT NULL,
        Layer VARCHAR(255) NOT NULL,
        Sequence INT NOT NULL,
        Value VARCHAR(255) NOT NULL
    ) ENGINE=InnoDB;
    CREATE TABLE IF NOT EXISTS CapBusinessEvents (
        EventId VARCHAR(191) NOT NULL PRIMARY KEY,
        OperationId VARCHAR(191) NOT NULL,
        TraceId VARCHAR(191) NOT NULL,
        RootId VARCHAR(64) NOT NULL,
        Source VARCHAR(64) NOT NULL,
        Topic VARCHAR(191) NOT NULL,
        Scenario VARCHAR(64) NOT NULL,
        CreatedAt DATETIME(6) NOT NULL
    ) ENGINE=InnoDB;";

    private const string CapBusinessEventsTableSql = @"CREATE TABLE IF NOT EXISTS CapBusinessEvents (
        EventId VARCHAR(191) NOT NULL PRIMARY KEY,
        OperationId VARCHAR(191) NOT NULL,
        TraceId VARCHAR(191) NOT NULL,
        RootId VARCHAR(64) NOT NULL,
        Source VARCHAR(64) NOT NULL,
        Topic VARCHAR(191) NOT NULL,
        Scenario VARCHAR(64) NOT NULL,
        CreatedAt DATETIME(6) NOT NULL
    ) ENGINE=InnoDB;";
}
