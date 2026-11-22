using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Bing.Dapper;
using Bing.Dapper.SqlServer;
using Bing.Data.Enums;
using Bing.Data.KeyGeneration;
using Bing.Data.Sql;
using Bing.Data.Sql.Aot.PackageModels;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Mutations.Builders;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Generated;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Mutations;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

const string testDatabase = "bing_sql_aot_test";
var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__SqlServerConnection");
if (!string.Equals(Environment.GetEnvironmentVariable("RUN_SQLSERVER_INTEGRATION_TESTS"), "true", StringComparison.OrdinalIgnoreCase) ||
    string.IsNullOrWhiteSpace(connectionString) ||
    new SqlConnectionStringBuilder(connectionString).InitialCatalog != testDatabase)
    throw new InvalidOperationException($"SQL Server smoke requires RUN_SQLSERVER_INTEGRATION_TESTS=true and the {testDatabase} database.");

var services = new ServiceCollection();
var models = PackageModels.CreateModels().Concat(SqlServerModels.CreateModels()).ToArray();
services.ConfigureSqlMetadata(options => options.ExecutionMode = SqlExecutionMode.GeneratedOnly);
services.AddSqlGeneratedModels(models);
services.AddSqlDataSource("default", DatabaseType.SqlServer, connectionString);
services.AddSqlServerProvider();
var keyPolicy = EntityKeyPolicy.CreateGenerated<IdentityCrudRow, int>(nameof(IdentityCrudRow.Id),
    EntityKeyGenerationKind.DatabaseIdentity, static row => row.Id, static (row, key) => row.Id = key);
services.AddEntityKeyPolicies(keys => keys.ForGenerated<IdentityCrudRow, int>(nameof(IdentityCrudRow.Id),
    EntityKeyGenerationKind.DatabaseIdentity, static row => row.Id, static (row, key) => row.Id = key));
using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();
using var writer = scope.ServiceProvider.GetRequiredService<ISqlExecutorFactory>().Create();
var queryFactory = scope.ServiceProvider.GetRequiredService<ISqlQueryFactory>();

// 使用空白专用数据库。已有测试表会使本次运行失败，避免继承上一次的状态。
writer.ExecuteSql("CREATE TABLE package_rows ([Id] int NOT NULL PRIMARY KEY, [Name] nvarchar(200) NULL)");
writer.ExecuteSql("CREATE TABLE identity_crud_rows ([Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY, [Name] nvarchar(200) NULL)");

var strictServices = new SqlBuilderServices(
    metadataOptions: new SqlMetadataOptions { ExecutionMode = SqlExecutionMode.GeneratedOnly },
    entityModelMetadataProvider: new RegisteredEntityModelMetadataProvider(models));
var builder = new DefaultSqlEntityMutationCommandBuilder(SqlServerSqlProvider.Instance, strictServices);
var crudModel = models.Single(model => model.EntityType == typeof(CrudRow));
var row = new CrudRow { Id = 1, Name = "inserted" };
var insert = builder.Insert(row);
CheckCommand(insert, "Insert Into [package_rows] ([Id], [Name]) Values (@_p_0, @_p_1)", crudModel);
Check(await writer.ExecuteWriteAsync(insert) == 1, "generated insert");
row.Name = "更新";
var update = builder.Update(row);
CheckCommand(update, "Update [package_rows] Set [Name] = @_p_0 Where [Id] = @_p_1", crudModel);
Check(await writer.ExecuteWriteAsync(update) == 1, "generated update");
using (var query = queryFactory.Create())
{
    var result = await query.From<CrudRow>("r").SelectAll().Where<CrudRow, int>(item => item.Id, 1).ToListAsync<CrudRow>();
    Check(result.Count == 1 && result[0].Name == row.Name, "generated Lambda query");
}
var delete = builder.Purge(row);
CheckCommand(delete, "Delete From [package_rows] Where [Id] = @_p_0", crudModel);
Check(await writer.ExecuteWriteAsync(delete) == 1, "generated delete");
using (var query = queryFactory.Create())
    Check((await query.From<CrudRow>().Where<CrudRow, int>(item => item.Id, 1).ToListAsync<CrudRow>()).Count == 0,
        "delete persisted");
Console.WriteLine("SQLSERVER_GENERATED_CRUD_PASS");

var identityModel = models.Single(model => model.EntityType == typeof(IdentityCrudRow));
var identity = new IdentityCrudRow { Name = "identity" };
var keyCommand = new SqlKeyInsertCommandBuilder(new SqlInsertBuilder(SqlServerSqlProvider.Instance, strictServices), keyPolicy)
    .Build(identity);
Check(keyCommand.Sql == "Insert Into [identity_crud_rows] ([Name]) Output [Inserted].[Id] Values (@_p_0)" &&
    ReferenceEquals(keyCommand.Sql, identityModel.GeneratedKeyInsertSql?.Invoke(SqlServerSqlProvider.Instance.Key)) &&
    keyCommand.HasReturning && keyCommand.IsGeneratedExecution, "generated identity OUTPUT SQL and binder");
var keys = await ((ISqlKeyInsertExecutor)writer).InsertWithKeysAsync<IdentityCrudRow, int>(
    new[] { new InsertRow<IdentityCrudRow>("identity", identity) });
Check(keys.Completion == WriteCompletionState.Committed && identity.Id > 0 && keys.Keys["identity"] == identity.Id,
    "identity OUTPUT and backfill");
using (var query = queryFactory.Create())
{
    var result = await query.From<IdentityCrudRow>().Where<IdentityCrudRow, int>(item => item.Id, identity.Id)
        .ToListAsync<IdentityCrudRow>();
    Check(result.Count == 1 && result[0].Name == identity.Name, "identity committed");
}
Console.WriteLine("SQLSERVER_IDENTITY_OUTPUT_PASS");

var transactions = scope.ServiceProvider.GetRequiredService<ISqlTransactionScopeFactory>();
await using (var transaction = await transactions.BeginAsync())
{
    using var transactionalWriter = transaction.CreateExecutor();
    using var transactionalQuery = transaction.CreateQuery();
    Check(await transactionalWriter.InsertAsync(new CrudRow { Id = 2, Name = "shared" }) == 1, "shared insert");
    var result = await transactionalQuery.From<CrudRow>().Where<CrudRow, int>(item => item.Id, 2).ToListAsync<CrudRow>();
    Check(result.Count == 1 && result[0].Name == "shared", "shared read before rollback");
    await transaction.RollbackAsync();
}
using (var query = queryFactory.Create())
    Check((await query.From<CrudRow>().Where<CrudRow, int>(item => item.Id, 2).ToListAsync<CrudRow>()).Count == 0,
        "shared rollback");
Console.WriteLine("SQLSERVER_SHARED_ROLLBACK_PASS");

await CheckExtendedColumnsAsync(writer, queryFactory, builder, models.Single(model => model.EntityType == typeof(ExtendedColumnRow)));
Console.WriteLine("SQLSERVER_EXTENDED_COLUMNS_NATIVE_PASS");
await CheckOutputProjectionAsync(writer, queryFactory, transactions);
Console.WriteLine("SQLSERVER_OUTPUT_PROJECTION_NATIVE_PASS");

static async Task CheckExtendedColumnsAsync(ISqlExecutor writer, ISqlQueryFactory queries,
    DefaultSqlEntityMutationCommandBuilder builder, EntityModelMetadata model)
{
    writer.ExecuteSql("CREATE TABLE extended_columns ([Id] int NOT NULL PRIMARY KEY, [UnicodeText] nvarchar(max) NULL, " +
        "[AnsiText] varchar(max) NULL, [Bytes] varbinary(max) NULL, [Document] xml NULL, [Variant] sql_variant NULL)");
    var row = new ExtendedColumnRow
    {
        Id = 1,
        UnicodeText = new string('字', 4100),
        AnsiText = new string('a', 8100),
        Bytes = Enumerable.Range(0, 8100).Select(index => (byte)(index % 251)).ToArray(),
        Document = "<content>中文</content>",
        Variant = 42
    };
    var insert = builder.Insert(row);
    CheckCommand(insert, "Insert Into [extended_columns] ([Id], [UnicodeText], [AnsiText], [Bytes], [Document], [Variant]) " +
        "Values (@_p_0, @_p_1, @_p_2, @_p_3, @_p_4, @_p_5)", model);
    Check(await writer.ExecuteWriteAsync(insert) == 1, "generated extended insert");
    const string selectSql = "SELECT [Id], [UnicodeText], [AnsiText], [Bytes], [Document], [Variant] FROM extended_columns WHERE [Id] = 1";
    using (var query = queries.Create())
    {
        var loaded = (await query.Sql(selectSql).ToListAsync<ExtendedColumnRow>()).Single();
        Check(loaded.Id == row.Id && loaded.UnicodeText == row.UnicodeText && loaded.AnsiText == row.AnsiText &&
            loaded.Bytes.SequenceEqual(row.Bytes) && loaded.Document == row.Document && loaded.Variant is 42,
            "generated extended read preserves large values and integer variant");
    }

    row.UnicodeText = null;
    row.AnsiText = "short";
    row.Bytes = null;
    row.Document = null;
    row.Variant = "变体";
    var update = builder.Update(row);
    CheckCommand(update, "Update [extended_columns] Set [UnicodeText] = @_p_0, [AnsiText] = @_p_1, [Bytes] = @_p_2, " +
        "[Document] = @_p_3, [Variant] = @_p_4 Where [Id] = @_p_5", model);
    Check(await writer.ExecuteWriteAsync(update) == 1, "generated extended update");
    using (var query = queries.Create())
    {
        var loaded = (await query.Sql(selectSql).ToListAsync<ExtendedColumnRow>()).Single();
        Check(loaded.UnicodeText == null && loaded.AnsiText == "short" && loaded.Bytes == null &&
            loaded.Document == null && loaded.Variant is "变体", "generated extended read preserves NULL and string variant");
    }
}

static async Task CheckOutputProjectionAsync(ISqlExecutor writer, ISqlQueryFactory queries, ISqlTransactionScopeFactory transactions)
{
    const int id = 3;
    var table = new SqlTableReference { TableName = "package_rows" };
    var columns = new[] { new SqlReturningColumn("Name", alias: "display_name"), new SqlReturningColumn("Id", alias: "row_id") };
    var insertBuilder = writer.CreateInsertBuilder().InsertInto(table).Columns("Id", "Name").Values(id, null);
    insertBuilder.ReturningClause.AddRange(columns);
    var insert = insertBuilder.BuildCommand();
    Check(insert.Sql == "Insert Into [package_rows] ([Id], [Name]) Output [Inserted].[Name] As [display_name], " +
        "[Inserted].[Id] As [row_id] Values (@_p_0, @_p_1)", "insert OUTPUT SQL");
    var added = writer.ExecuteReturning<ReturningProjectionDto>(insert).Single();
    Check(added.Id == id && added.DisplayName == "initial-display" && added.OptionalNote == "initial-note",
        "insert OUTPUT maps aliased columns, NULL and missing columns");

    await using (var transaction = await transactions.BeginAsync())
    {
        using var transactionalWriter = transaction.CreateExecutor();
        var updateBuilder = transactionalWriter.CreateUpdateBuilder().Update(table).Set("Name", "updated").Where("Id", id);
        updateBuilder.ReturningClause.AddRange(columns);
        var update = updateBuilder.BuildCommand();
        Check(update.Sql == "Update [package_rows] Set [Name] = @_p_0 Output [Inserted].[Name] As [display_name], " +
            "[Inserted].[Id] As [row_id] Where [Id]=@_p_1", "update OUTPUT SQL");
        var updated = (await transactionalWriter.ExecuteReturningAsync<ReturningProjectionDto>(update)).Single();
        Check(updated.Id == id && updated.DisplayName == "updated", "update OUTPUT reads the new value");

        var deleteBuilder = transactionalWriter.CreateDeleteBuilder().DeleteFrom(table).Where("Id", id);
        deleteBuilder.ReturningClause.AddRange(columns);
        var delete = deleteBuilder.BuildCommand();
        Check(delete.Sql == "Delete From [package_rows] Output [Deleted].[Name] As [display_name], " +
            "[Deleted].[Id] As [row_id] Where [Id]=@_p_0", "delete OUTPUT SQL");
        var deleted = (await transactionalWriter.ExecuteReturningAsync<ReturningProjectionDto>(delete)).Single();
        Check(deleted.Id == id && deleted.DisplayName == "updated", "delete OUTPUT reads the removed value");
        Check((await transactionalWriter.ExecuteReturningAsync<ReturningProjectionDto>(delete)).Count == 0, "OUTPUT with no matched row");
        await transaction.RollbackAsync();
    }
    using var query = queries.Create();
    var persisted = (await query.From<CrudRow>().Where<CrudRow, int>(item => item.Id, id).ToListAsync<CrudRow>()).Single();
    Check(persisted.Id == id && persisted.Name == null, "OUTPUT leaves the original row after rollback");
}

static void CheckCommand(SqlWriteCommand command, string expected, EntityModelMetadata model)
{
    Check(command.Sql == expected && command.IsGeneratedExecution &&
        ReferenceEquals(command.Sql, model.GeneratedMutationSql?.Invoke(SqlServerSqlProvider.Instance.Key, command.OperationKind)),
        "generated SQL and binder: " + expected);
}

static void Check(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

// 在 SQL Server 消费工程内生成扩展列的 SQL、参数取值和读取代码。
[SqlModel(typeof(ExtendedColumnRow))]
[SqlResult(typeof(ExtendedColumnRow))]
public static partial class SqlServerModels { }

[Table("extended_columns")]
public sealed class ExtendedColumnRow
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None)] public int Id { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string UnicodeText { get; set; }
    [Column(TypeName = "varchar(max)")] public string AnsiText { get; set; }
    [Column(TypeName = "varbinary(max)")] public byte[] Bytes { get; set; }
    [Column(TypeName = "xml")] public string Document { get; set; }
    [Column(TypeName = "sql_variant")] public object Variant { get; set; }
}
