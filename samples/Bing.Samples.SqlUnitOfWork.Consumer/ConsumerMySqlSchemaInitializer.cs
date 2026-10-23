using MySqlConnector;

namespace Bing.Samples.SqlUnitOfWork.Consumer;

/// <summary>
/// 在独立 CAP consumer runtime 启动前建立该 Sample 专属的消费观察表。
/// </summary>
public sealed class ConsumerMySqlSchemaInitializer : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var connectionString in new[]
                 {
                     Environment.GetEnvironmentVariable("BING_SQLUOW_MYSQL_ORDERS") ??
                     "Server=127.0.0.1;Port=13307;User ID=cap_app;Password=cap_local_only;Database=bing_sqluow_orders;Pooling=true",
                     Environment.GetEnvironmentVariable("BING_SQLUOW_MYSQL_REPORTING") ??
                     "Server=127.0.0.1;Port=13307;User ID=cap_app;Password=cap_local_only;Database=bing_sqluow_reporting;Pooling=true"
                 })
        {
            await using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = @"CREATE TABLE IF NOT EXISTS CapConsumerFailures (
                Source VARCHAR(64) NOT NULL,
                EventId VARCHAR(191) NOT NULL,
                CreatedAt DATETIME(6) NOT NULL,
                PRIMARY KEY (Source, EventId)
            ) ENGINE=InnoDB;
            CREATE TABLE IF NOT EXISTS CapConsumerInbox (
                Source VARCHAR(64) NOT NULL,
                EventId VARCHAR(191) NOT NULL,
                ConsumerName VARCHAR(191) NOT NULL,
                OperationId VARCHAR(191) NOT NULL,
                TraceId VARCHAR(191) NOT NULL,
                RootId VARCHAR(64) NOT NULL,
                AttemptCount INT NOT NULL DEFAULT 1,
                AppliedAt DATETIME(6) NOT NULL,
                PRIMARY KEY (Source, EventId, ConsumerName)
            ) ENGINE=InnoDB;
            CREATE TABLE IF NOT EXISTS CapConsumerEffects (
                Source VARCHAR(64) NOT NULL,
                EventId VARCHAR(191) NOT NULL,
                ConsumerName VARCHAR(191) NOT NULL,
                OperationId VARCHAR(191) NOT NULL,
                EffectValue VARCHAR(191) NOT NULL,
                AppliedAt DATETIME(6) NOT NULL,
                PRIMARY KEY (Source, EventId, ConsumerName)
            ) ENGINE=InnoDB;
            CREATE TABLE IF NOT EXISTS CapConsumerAttempts (
                AttemptId BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                Source VARCHAR(64) NOT NULL,
                EventId VARCHAR(191) NOT NULL,
                OperationId VARCHAR(191) NOT NULL,
                TraceId VARCHAR(191) NOT NULL,
                RootId VARCHAR(64) NOT NULL,
                ConsumerName VARCHAR(191) NOT NULL,
                Status VARCHAR(32) NOT NULL,
                ErrorText VARCHAR(500) NULL,
                CreatedAt DATETIME(6) NOT NULL,
                INDEX IX_CapConsumerAttempts_Event (Source, EventId, AttemptId)
            ) ENGINE=InnoDB;";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
