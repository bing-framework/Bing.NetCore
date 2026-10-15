using System.Text;
using Bing.Data.Inventory;
using Bing.Test.Shared;
using MySqlConnector;
using Xunit;

namespace Bing.FreeSQL.Tests.Events;

/// <summary>
/// 验证真实 MySQL 库存身份、事务引擎及旧表迁移。
/// </summary>
/// <remarks>每个用例只操作新建的随机专用测试库，清理要求显式允许数据库重置。</remarks>
[Collection("MySqlOrderedEvents")]
public sealed class InventoryIdentityMySqlTest
{
    /// <summary>
    /// 不区分大小写及非事务默认值不能改变库存表规则。
    /// </summary>
    [IntegrationFact("MySql")]
    public async Task Schema_WhenServerDefaultsAreNonTransactional_ShouldUseExactInnoDbTables()
    {
        await using var database = await Database.CreateAsync();
        await database.AssertSchemaAsync();
    }

    /// <summary>
    /// 不同租户、仓库、SKU 和请求身份独立扣减并保持首次结果。
    /// </summary>
    [IntegrationFact("MySql")]
    public async Task TryDeductAsync_WhenIdentifiersDifferInCaseWhitespaceOrUnicode_ShouldRemainIsolated()
    {
        await using var database = await Database.CreateAsync();
        var variants = new[] { "café", "CAFÉ", "café ", "cafe\u0301", "\ud83e\udde9" };
        var store = new DbInventoryStore(InventoryDatabase.MySql);
        for (var dimension = 0; dimension < 4; dimension++)
        {
            var firstRequests = new List<InventoryDeductionRequest>();
            for (var index = 0; index < variants.Length; index++)
            {
                var keys = new[] { "tenant-" + dimension, "warehouse", "sku", "request-" + index };
                keys[dimension] = variants[index];
                var request = new InventoryDeductionRequest(keys[0], keys[1], keys[2], 1, keys[3]);
                if (dimension != 3 || index == 0)
                    await database.SeedAsync(request.TenantId, request.WarehouseId, request.Sku, 10);
                var result = await database.DeductAsync(store, request);
                Assert.True(result.Applied);
                Assert.False(result.Duplicate);
                Assert.Equal(dimension == 3 ? 9 - index : 9, result.RemainingQuantity);
                firstRequests.Add(request);
            }
            for (var index = 0; index < firstRequests.Count; index++)
            {
                var request = firstRequests[index];
                var duplicate = await database.DeductAsync(store, request);
                Assert.True(duplicate.Applied);
                Assert.True(duplicate.Duplicate);
                Assert.Equal(dimension == 3 ? 9 - index : 9, duplicate.RemainingQuantity);
                Assert.Equal(dimension == 3 ? 5 : 9,
                    await database.ReadQuantityAsync(request.TenantId, request.WarehouseId, request.Sku));
            }
        }
        Assert.Equal(20L, Convert.ToInt64(await database.ScalarAsync(
            "SELECT COUNT(*) FROM bing_inventory_request")));
    }

    /// <summary>
    /// 旧字符集迁移保留首次收据和精确身份，并恢复事务回滚能力。
    /// </summary>
    [IntegrationFact("MySql")]
    public async Task Migration_WhenLegacyLatin1TablesExist_ShouldPreserveReceiptsAndRollbackNewWrites()
    {
        await using var database = await Database.CreateAsync(legacy: true);
        const string tenant = "café";
        await database.SeedAsync(tenant, "dépôt", "pièce", 8, binary: false);
        await database.ExecuteAsync("INSERT INTO bing_inventory_request " +
            "(TenantId, RequestId, WarehouseId, Sku, RequestedQuantity, Status, QuantityAfter, CreatedAtUtc) " +
            "VALUES (@tenant, @request, @warehouse, @sku, 2, 'Applied', 8, UTC_TIMESTAMP(6))",
            ("@tenant", tenant), ("@request", "déjà"), ("@warehouse", "dépôt"), ("@sku", "pièce"));
        // IF NOT EXISTS 不迁移旧表；只有显式部署脚本才改变身份和事务引擎。
        foreach (var sql in InventorySchema.GetCreateStatements(InventoryDatabase.MySql))
            await database.ExecuteAsync(sql);
        Assert.Equal("MyISAM", await database.ScalarAsync(
            "SELECT ENGINE FROM information_schema.TABLES " +
            "WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'bing_inventory_stock'"));

        var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,
            "InventoryMigrations", "002-exact-identities.mysql.sql"), Encoding.UTF8);
        await database.ExecuteAsync(script);
        await database.AssertSchemaAsync();
        var store = new DbInventoryStore(InventoryDatabase.MySql);
        var duplicate = await database.DeductAsync(store,
            new InventoryDeductionRequest(tenant, "dépôt", "pièce", 2, "déjà"));
        Assert.True(duplicate.Duplicate);
        Assert.Equal(8, duplicate.RemainingQuantity);

        var applied = await database.DeductAsync(store,
            new InventoryDeductionRequest(tenant, "dépôt", "pièce", 1, "DÉJÀ"));
        Assert.False(applied.Duplicate);
        Assert.Equal(7, applied.RemainingQuantity);
        await using (var transaction = await database.Connection.BeginTransactionAsync(database.Token))
        {
            var cancelled = await store.TryDeductAsync(transaction,
                new InventoryDeductionRequest(tenant, "dépôt", "pièce", 1, "rollback"), database.Token);
            Assert.True(cancelled.Applied);
            await transaction.RollbackAsync(database.Token);
        }
        Assert.Equal(7, await database.ReadQuantityAsync(tenant, "dépôt", "pièce"));
        Assert.Equal(2L, Convert.ToInt64(await database.ScalarAsync(
            "SELECT COUNT(*) FROM bing_inventory_request")));
    }

    /// <summary>
    /// 提供独占测试数据库和有界数据库操作。
    /// </summary>
    private sealed class Database : IAsyncDisposable
    {
        /// <summary>
        /// 用于创建及清理本实例所属测试库的连接。
        /// </summary>
        private readonly MySqlConnection _admin;
        /// <summary>
        /// 本实例唯一拥有的随机测试库名称。
        /// </summary>
        private readonly string _name;
        /// <summary>
        /// 限制测试数据库操作时长的取消源。
        /// </summary>
        private readonly CancellationTokenSource _timeout = new(TimeSpan.FromSeconds(45));

        /// <summary>
        /// 初始化一个 <see cref="Database" /> 类型的实例。
        /// </summary>
        /// <param name="admin">连接至已校验测试环境的管理连接。</param>
        /// <param name="connection">连接至本实例拥有数据库的连接。</param>
        /// <param name="name">随机测试库名称。</param>
        private Database(MySqlConnection admin, MySqlConnection connection, string name)
        {
            _admin = admin;
            Connection = connection;
            _name = name;
        }

        /// <summary>
        /// 获取当前测试库存连接。
        /// </summary>
        internal MySqlConnection Connection { get; }
        /// <summary>
        /// 获取当前测试数据库操作令牌。
        /// </summary>
        internal CancellationToken Token => _timeout.Token;

        /// <summary>
        /// 创建独占数据库和库存表。
        /// </summary>
        /// <param name="legacy">是否创建供迁移验证的 latin1/MyISAM 旧表。</param>
        /// <returns>只拥有本次新建测试库的夹具。</returns>
        internal static async Task<Database> CreateAsync(bool legacy = false)
        {
            var builder = new MySqlConnectionStringBuilder(IntegrationTestConnectionStringResolver.Resolve("MySql"))
                { Pooling = false };
            IntegrationDatabaseSafetyValidator.EnsureDatabaseOperationAllowed(builder.ConnectionString, "MySql");
            var admin = new MySqlConnection(builder.ConnectionString);
            var name = "bing_inv_identity_" + Guid.NewGuid().ToString("N") + "_test";
            builder.Database = name;
            IntegrationDatabaseSafetyValidator.EnsureResetAllowed(builder.ConnectionString, "MySql");
            var database = new Database(admin, new MySqlConnection(builder.ConnectionString), name);
            var created = false;
            try
            {
                await admin.OpenAsync(database.Token);
                using (var create = new MySqlCommand($"CREATE DATABASE `{name}` " +
                    "CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci", admin))
                    await create.ExecuteNonQueryAsync(database.Token);
                created = true;
                await database.Connection.OpenAsync(database.Token);
                await database.ExecuteAsync("SET SESSION default_storage_engine = MyISAM");
                var statements = legacy ? new[]
                {
                    "CREATE TABLE bing_inventory_stock (TenantId VARCHAR(128) NOT NULL, " +
                    "WarehouseId VARCHAR(128) NOT NULL, Sku VARCHAR(256) NOT NULL, Quantity BIGINT NOT NULL, " +
                    "PRIMARY KEY (TenantId, WarehouseId, Sku)) ENGINE=MyISAM DEFAULT CHARSET=latin1",
                    "CREATE TABLE bing_inventory_request (TenantId VARCHAR(128) NOT NULL, RequestId VARCHAR(128) NOT NULL, " +
                    "WarehouseId VARCHAR(128) NOT NULL, Sku VARCHAR(256) NOT NULL, RequestedQuantity BIGINT NOT NULL, " +
                    "Status VARCHAR(16) NOT NULL, QuantityAfter BIGINT NULL, CreatedAtUtc DATETIME(6) NOT NULL, " +
                    "PRIMARY KEY (TenantId, RequestId)) ENGINE=MyISAM DEFAULT CHARSET=latin1"
                } : InventorySchema.GetCreateStatements(InventoryDatabase.MySql);
                foreach (var statement in statements)
                    await database.ExecuteAsync(statement);
                return database;
            }
            catch (Exception original)
            {
                try
                {
                    if (created)
                        await database.DisposeAsync();
                    else
                    {
                        try
                        {
                            await database.Connection.DisposeAsync();
                        }
                        finally
                        {
                            await admin.DisposeAsync();
                            database._timeout.Dispose();
                        }
                    }
                }
                catch (Exception cleanup)
                {
                    original.Data["Bing.InventoryTestCleanupError"] = cleanup;
                }
                throw;
            }
        }

        /// <summary>
        /// 验证部署后的全部库存身份列和引擎。
        /// </summary>
        internal async Task AssertSchemaAsync()
        {
            foreach (var table in new[] { "bing_inventory_stock", "bing_inventory_request" })
                Assert.Equal("InnoDB", await ScalarAsync("SELECT ENGINE FROM information_schema.TABLES " +
                    "WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @table", ("@table", table)));
            using var command = new MySqlCommand("SELECT TABLE_NAME, COLUMN_NAME, COLUMN_TYPE " +
                "FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND " +
                "COLUMN_NAME IN ('TenantId', 'WarehouseId', 'RequestId', 'Sku') ORDER BY TABLE_NAME, COLUMN_NAME", Connection);
            using var reader = await command.ExecuteReaderAsync(Token);
            var columns = new List<string>();
            while (await reader.ReadAsync(Token))
                columns.Add(reader.GetString(0) + "/" + reader.GetString(1) + "/" + reader.GetString(2));
            Assert.Equal(new[]
            {
                "bing_inventory_request/RequestId/varbinary(512)", "bing_inventory_request/Sku/varbinary(1024)",
                "bing_inventory_request/TenantId/varbinary(512)", "bing_inventory_request/WarehouseId/varbinary(512)",
                "bing_inventory_stock/Sku/varbinary(1024)", "bing_inventory_stock/TenantId/varbinary(512)",
                "bing_inventory_stock/WarehouseId/varbinary(512)"
            }, columns);
        }

        /// <summary>
        /// 写入一行测试库存。
        /// </summary>
        /// <param name="tenant">租户身份。</param>
        /// <param name="warehouse">仓库身份。</param>
        /// <param name="sku">SKU 身份。</param>
        /// <param name="quantity">初始数量。</param>
        /// <param name="binary">是否按新表规则绑定 UTF-8 字节。</param>
        internal Task SeedAsync(string tenant, string warehouse, string sku, long quantity, bool binary = true) =>
            ExecuteAsync("INSERT INTO bing_inventory_stock (TenantId, WarehouseId, Sku, Quantity) " +
                "VALUES (@tenant, @warehouse, @sku, @quantity)",
                ("@tenant", binary ? (object)Encoding.UTF8.GetBytes(tenant) : tenant),
                ("@warehouse", binary ? (object)Encoding.UTF8.GetBytes(warehouse) : warehouse),
                ("@sku", binary ? (object)Encoding.UTF8.GetBytes(sku) : sku), ("@quantity", quantity));

        /// <summary>
        /// 使用独立事务提交一次真实扣减。
        /// </summary>
        /// <param name="store">待验证的数据库库存存储。</param>
        /// <param name="request">当前扣减请求。</param>
        /// <returns>事务提交后的扣减结果。</returns>
        internal async Task<InventoryDeductionResult> DeductAsync(IInventoryStore store, InventoryDeductionRequest request)
        {
            await using var transaction = await Connection.BeginTransactionAsync(Token);
            var result = await store.TryDeductAsync(transaction, request, Token);
            await transaction.CommitAsync(Token);
            return result;
        }

        /// <summary>
        /// 读取精确库存身份的余额。
        /// </summary>
        /// <param name="tenant">租户身份。</param>
        /// <param name="warehouse">仓库身份。</param>
        /// <param name="sku">SKU 身份。</param>
        /// <returns>已提交的库存余额。</returns>
        internal async Task<long> ReadQuantityAsync(string tenant, string warehouse, string sku) =>
            Convert.ToInt64(await ScalarAsync("SELECT Quantity FROM bing_inventory_stock " +
                "WHERE TenantId = @tenant AND WarehouseId = @warehouse AND Sku = @sku",
                ("@tenant", Encoding.UTF8.GetBytes(tenant)), ("@warehouse", Encoding.UTF8.GetBytes(warehouse)),
                ("@sku", Encoding.UTF8.GetBytes(sku))));

        /// <summary>
        /// 执行测试初始化或迁移语句。
        /// </summary>
        /// <param name="sql">需要执行的 SQL。</param>
        /// <param name="parameters">需要绑定的参数。</param>
        internal async Task ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
        {
            using var command = CreateCommand(sql, parameters);
            await command.ExecuteNonQueryAsync(Token);
        }

        /// <summary>
        /// 读取测试查询的单个结果。
        /// </summary>
        /// <param name="sql">需要执行的 SQL。</param>
        /// <param name="parameters">需要绑定的参数。</param>
        /// <returns>查询返回的首个值。</returns>
        internal async Task<object> ScalarAsync(string sql, params (string Name, object Value)[] parameters)
        {
            using var command = CreateCommand(sql, parameters);
            return await command.ExecuteScalarAsync(Token);
        }

        /// <summary>
        /// 创建当前测试库的参数化命令。
        /// </summary>
        /// <param name="sql">需要执行的 SQL。</param>
        /// <param name="parameters">需要绑定的参数。</param>
        /// <returns>由调用方法释放的命令。</returns>
        private MySqlCommand CreateCommand(string sql, (string Name, object Value)[] parameters)
        {
            var command = new MySqlCommand(sql, Connection);
            foreach (var parameter in parameters)
                command.Parameters.AddWithValue(parameter.Name, parameter.Value);
            return command;
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            try
            {
                await Connection.DisposeAsync();
            }
            finally
            {
                try
                {
                    // 名称仅由本夹具生成并通过安全门控；不删除配置的共享测试库。
                    using var drop = new MySqlCommand($"DROP DATABASE `{_name}`", _admin);
                    await drop.ExecuteNonQueryAsync(CancellationToken.None);
                }
                finally
                {
                    await _admin.DisposeAsync();
                    _timeout.Dispose();
                }
            }
        }
    }
}
