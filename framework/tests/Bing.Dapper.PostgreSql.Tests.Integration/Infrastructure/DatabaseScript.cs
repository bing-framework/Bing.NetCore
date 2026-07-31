using Npgsql;

namespace Bing.Dapper.Tests.Infrastructure;

/// <summary>
/// PostgreSQL 集成测试数据库脚本。
/// </summary>
public static class DatabaseScript
{
    /// <summary>
    /// 初始化 PostgreSQL 集成测试所需的专用 schema 和表。
    /// </summary>
    /// <param name="connection">已打开的 PostgreSQL 连接。</param>
    /// <returns>异步初始化任务。</returns>
    public static async Task InitializeAsync(NpgsqlConnection connection)
    {
        if (connection == null)
            throw new ArgumentNullException(nameof(connection));
        await ExecuteAsync(connection, "Create Schema If Not Exists integration_primary;");
        await ExecuteAsync(connection, "Create Schema If Not Exists integration_reporting;");
        await ExecuteAsync(connection, @"
Create Table If Not Exists public.integration_products(
    id uuid Primary Key,
    code text Not Null,
    user_id text Null,
    name text Null,
    amount numeric(24,6) Null,
    occurred_at timestamp without time zone Not Null,
    payload jsonb Null
);");
        await ExecuteAsync(connection,
            "Alter Table public.integration_products Add Column If Not Exists user_id text Null;");
        await ExecuteAsync(connection,
            "Alter Table public.integration_products Add Column If Not Exists version integer Not Null Default 1;");
        await ExecuteAsync(connection,
            "Alter Table public.integration_products Alter Column amount Drop Not Null;");
        await ExecuteAsync(connection, @"
Create Table If Not Exists public.integration_product_items(
    item_id uuid Primary Key,
    product_id uuid Not Null,
    sku text Not Null,
    quantity integer Not Null,
    enabled boolean Not Null Default true
);");
        await ExecuteAsync(connection, @"
Create Table If Not Exists public.integration_product_updates(
    id uuid Primary Key,
    name text Null
);");
        await ExecuteAsync(connection,
            "Create Index If Not Exists ix_integration_product_items_product_id On public.integration_product_items(product_id);");
        await ExecuteAsync(connection, @"
Create Table If Not Exists integration_primary.integration_samples(
    id integer Generated Always As Identity Primary Key,
    name text Not Null
);");
        await ExecuteAsync(connection, @"
Create Table If Not Exists integration_reporting.integration_samples(
    id integer Generated Always As Identity Primary Key,
    name text Not Null
);");
    }

    /// <summary>
    /// 清理 PostgreSQL 集成测试数据，保留受控测试对象结构。
    /// </summary>
    /// <param name="connection">已打开的 PostgreSQL 连接。</param>
    /// <returns>异步清理任务。</returns>
    public static async Task ResetAsync(NpgsqlConnection connection)
    {
        if (connection == null)
            throw new ArgumentNullException(nameof(connection));
        await ExecuteAsync(connection,
            "Truncate Table public.integration_product_items, public.integration_product_updates, public.integration_products, integration_primary.integration_samples, integration_reporting.integration_samples Restart Identity;");
    }

    /// <summary>
    /// 执行 PostgreSQL 测试数据库脚本。
    /// </summary>
    /// <param name="connection">已打开的 PostgreSQL 连接。</param>
    /// <param name="sql">固定的测试数据库脚本。</param>
    /// <returns>异步执行任务。</returns>
    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}