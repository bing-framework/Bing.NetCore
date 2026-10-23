using Bing.Data.Enums;
using Bing.Data.Transaction;
using Bing.Dapper;
using Bing.Dapper.MySql;
using Bing.Dapper.Sqlite;
using Bing.EntityFrameworkCore;
using Bing.Datas.EntityFramework.MySql;
using Bing.Events.Cap;
using Bing.Events;
using Bing.Events.Handlers;
using Bing.Samples.SqlUnitOfWork.Application;
using Bing.Samples.SqlUnitOfWork.Infrastructure.Background;
using Bing.Samples.SqlUnitOfWork.Infrastructure.Database;
using Bing.Samples.SqlUnitOfWork.Infrastructure.Diagnostics;
using Bing.Samples.SqlUnitOfWork.Infrastructure.Events;
using Bing.Samples.SqlUnitOfWork.Infrastructure.Operations;
using Bing.Samples.SqlUnitOfWork.Jobs;
using Bing.Samples.SqlUnitOfWork.Repositories;
using Bing.Uow;
using Hangfire;
using Hangfire.MemoryStorage;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace Bing.Samples.SqlUnitOfWork;

/// <summary>
/// 注册示例所需的数据源、业务服务和后台执行器。
/// </summary>
public sealed class Startup
{
    private readonly IConfiguration _configuration;

    public Startup(IConfiguration configuration) => _configuration = configuration;

    public void ConfigureServices(IServiceCollection services)
    {
        var storageMode = _configuration["SqlUow:Storage"] ?? "Dapper";
        if (!string.Equals(storageMode, "Dapper", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(storageMode, "EfBridge", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(storageMode, "CapMySql", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(storageMode, "CapMySqlEfBridge", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "SqlUow:Storage 仅支持 Dapper、EfBridge、CapMySql 或 CapMySqlEfBridge。");
        }

        var directoryPath = Path.Combine(Path.GetTempPath(), $"Bing.Samples.SqlUnitOfWork-{Guid.NewGuid():N}");
        var capMySqlMode = storageMode.StartsWith("CapMySql", StringComparison.OrdinalIgnoreCase);
        var operationsEnabled = bool.TryParse(_configuration["SqlUow:OperationsEnabled"], out var configuredOperationsEnabled) &&
                                configuredOperationsEnabled;
        var efBridgeEnabled = string.Equals(storageMode, "EfBridge", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(storageMode, "CapMySqlEfBridge", StringComparison.OrdinalIgnoreCase);
        var ordersConnectionString = capMySqlMode
            ? _configuration["BING_SQLUOW_MYSQL_ORDERS"] ??
              "Server=127.0.0.1;Port=13307;User ID=cap_app;Password=cap_local_only;Database=bing_sqluow_orders;Pooling=true"
            : $"Data Source={Path.Combine(directoryPath, "orders.db")};Pooling=False";
        var reportingConnectionString = capMySqlMode
            ? _configuration["BING_SQLUOW_MYSQL_REPORTING"] ??
              "Server=127.0.0.1;Port=13307;User ID=cap_app;Password=cap_local_only;Database=bing_sqluow_reporting;Pooling=true"
            : $"Data Source={Path.Combine(directoryPath, "reporting.db")};Pooling=False";
        var rabbitMqConnectionString = _configuration["BING_SQLUOW_CAP_RABBITMQ"] ??
                                       "amqp://cap_local:cap_local_only@127.0.0.1:5673/sqluow";
        var mySqlAdminConnectionString = _configuration["BING_SQLUOW_MYSQL_ADMIN"] ??
                                        "Server=127.0.0.1;Port=13307;User ID=root;Password=cap_root_local_only;Pooling=false";
        var processEvidencePath = _configuration["SqlUow:ProcessEvidencePath"] ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Bing.Samples.SqlUnitOfWork", "operations.jsonl");

        services.AddSingleton(_ => new SampleDatabaseFiles(directoryPath));
        services.AddSingleton(new SqlUnitOfWorkSampleOptions(efBridgeEnabled, capMySqlMode,
            ordersConnectionString, reportingConnectionString, rabbitMqConnectionString,
            mySqlAdminConnectionString, processEvidencePath)
        {
            OperationsEnabled = operationsEnabled
        });
        services.AddControllers();
        services.ConfigureSqlMetadata(options => options.DataSources.DefaultDataSourceKey = "orders");
        if (capMySqlMode)
        {
            services.AddMySqlProvider();
            services.AddSqlDataSource("orders", DatabaseType.MySql, ordersConnectionString);
            services.AddSqlDataSource("reporting", DatabaseType.MySql, reportingConnectionString);
        }
        else
        {
            services.AddSqliteProvider();
            services.AddSqlDataSource("orders", DatabaseType.Sqlite, ordersConnectionString);
            services.AddSqlDataSource("reporting", DatabaseType.Sqlite, reportingConnectionString);
        }
        if (operationsEnabled)
            services.AddBingOperations();
        else
            services.AddUnitOfWorkScopes();
        services.AddScoped<ITransactionActionManager, TransactionActionManager>();
        services.AddScoped<ISimpleEventBus, ScopedSampleMemoryEventBus>();
        services.AddScoped<IEventHandler<SqlUnitOfWorkMemoryEvent>, SqlUnitOfWorkMemoryEventHandler>();
        services.AddSingleton<SqlUnitOfWorkMemoryEventStore>();
        services.AddDbContext<SampleUnitOfWorkDbContext>(options =>
        {
            if (capMySqlMode)
                options.UseMySql(ordersConnectionString, ServerVersion.AutoDetect(ordersConnectionString));
            else
                options.UseSqlite(ordersConnectionString);
        });

        if (efBridgeEnabled)
            services.AddEfCoreSqlUnitOfWork<SampleUnitOfWorkDbContext>("orders");

        if (capMySqlMode)
        {
            services.AddCapUnitOfWorkMessagePublisher(builder => builder
                .AddDataSource("orders", DatabaseType.MySql, ordersConnectionString,
                    (options, connectionString) => SqlUnitOfWorkCapRuntimeConfiguration.Configure(options,
                        connectionString, rabbitMqConnectionString, "sqluow-orders"), publisherOnly: true)
                .AddDataSource("reporting", DatabaseType.MySql, reportingConnectionString,
                    (options, connectionString) => SqlUnitOfWorkCapRuntimeConfiguration.Configure(options,
                        connectionString, rabbitMqConnectionString, "sqluow-reporting"), publisherOnly: true));
        }

        services.AddScoped<OrdersRepository>();
        services.AddScoped<ReportingRepository>();
        services.AddScoped<SqlUnitOfWorkApplicationService>();
        services.AddScoped<OperationScenarioService>();
        services.AddScoped<CapEventDiagnosticsService>();
        services.AddSingleton<ScenarioOperationStore>();
        services.AddSingleton<ScenarioWorkQueue>();
        services.AddHostedService<DatabaseInitializerHostedService>();
        services.AddSingleton<ScenarioBackgroundWorker>();
        services.AddHostedService(provider => provider.GetRequiredService<ScenarioBackgroundWorker>());
        services.AddScoped<SqlUnitOfWorkJob>();

        services.AddHangfire(configuration =>
        {
            configuration.UseMemoryStorage();
            configuration.UseFilter(new AutomaticRetryAttribute { Attempts = 0 });
        });
        services.AddHangfireServer(options => options.WorkerCount = 1);
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment environment)
    {
        if (environment.IsDevelopment())
            app.UseDeveloperExceptionPage();

        app.UseRouting();
        app.UseEndpoints(endpoints => endpoints.MapControllers());
    }

}
