using System.Data;
using Bing.Dapper;
using Bing.Dapper.Oracle;
using Bing.Data.Enums;
using Bing.Data.KeyGeneration;
using Bing.Data.Sql;
using Bing.Data.Sql.Aot.PackageModels;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Mutations.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Mutations;
using Bing.Data.Sql.Mutations;
using Microsoft.Extensions.DependencyInjection;
using Oracle.ManagedDataAccess.Client;

const string testSchema = "BING_SQL_AOT_TEST";
var connection = Environment.GetEnvironmentVariable("ConnectionStrings__OracleConnection");
if (!string.Equals(Environment.GetEnvironmentVariable("RUN_ORACLE_INTEGRATION_TESTS"), "true", StringComparison.OrdinalIgnoreCase) ||
    string.IsNullOrWhiteSpace(connection) ||
    !string.Equals(new OracleConnectionStringBuilder(connection).UserID, testSchema, StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException($"Oracle smoke requires RUN_ORACLE_INTEGRATION_TESTS=true and the {testSchema} schema.");

var services = new ServiceCollection();
var models = PackageModels.CreateModels();
services.ConfigureSqlMetadata(options => options.ExecutionMode = SqlExecutionMode.GeneratedOnly);
services.AddSqlGeneratedModels(models);
services.AddSqlDataSource("default", DatabaseType.Oracle, connection);
services.AddOracleProvider();
var identityPolicy = EntityKeyPolicy.CreateGenerated<IdentityCrudRow, int>(nameof(IdentityCrudRow.Id),
    EntityKeyGenerationKind.DatabaseIdentity, static row => row.Id, static (row, key) => row.Id = key);
var identityOnlyPolicy = EntityKeyPolicy.CreateGenerated<IdentityOnlyCrudRow, long>(nameof(IdentityOnlyCrudRow.Id),
    EntityKeyGenerationKind.DatabaseIdentity, static row => row.Id, static (row, key) => row.Id = key);
services.AddEntityKeyPolicies(keys => keys
    .ForGenerated<IdentityCrudRow, int>(nameof(IdentityCrudRow.Id), EntityKeyGenerationKind.DatabaseIdentity,
        static row => row.Id, static (row, key) => row.Id = key)
    .ForGenerated<IdentityOnlyCrudRow, long>(nameof(IdentityOnlyCrudRow.Id), EntityKeyGenerationKind.DatabaseIdentity,
        static row => row.Id, static (row, key) => row.Id = key));
using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();
var serviceProvider = scope.ServiceProvider;
using var writer = serviceProvider.GetRequiredService<ISqlExecutorFactory>().Create();
var queryFactory = serviceProvider.GetRequiredService<ISqlQueryFactory>();

// 测试用户必须拥有空 schema，表名和列名沿用生成模型的大小写。
writer.ExecuteSql("CREATE TABLE \"package_rows\" (\"Id\" NUMBER(9) NOT NULL PRIMARY KEY, \"Name\" NVARCHAR2(200))");
var strictServices = new SqlBuilderServices(
    metadataOptions: new SqlMetadataOptions { ExecutionMode = SqlExecutionMode.GeneratedOnly },
    entityModelMetadataProvider: new RegisteredEntityModelMetadataProvider(models));
var builder = new DefaultSqlEntityMutationCommandBuilder(OracleSqlProvider.Instance, strictServices);
var model = models.Single(item => item.EntityType == typeof(CrudRow));
var row = new CrudRow { Id = 1, Name = "inserted" };

var insert = builder.Insert(row);
CheckCommand(insert, "Insert Into \"package_rows\" (\"Id\", \"Name\") Values (:p_0, :p_1)", model);
Check(await writer.ExecuteWriteAsync(insert) == 1, "generated insert");
row.Name = "更新";
var update = builder.Update(row);
CheckCommand(update, "Update \"package_rows\" Set \"Name\" = :p_0 Where \"Id\" = :p_1", model);
Check(await writer.ExecuteWriteAsync(update) == 1, "generated update");
using (var query = queryFactory.Create())
{
    var rows = await query.From<CrudRow>("r").SelectAll().Where<CrudRow, int>(item => item.Id, 1).ToListAsync<CrudRow>();
    Check(rows.Count == 1 && rows[0].Id == row.Id && rows[0].Name == row.Name, "generated Lambda query");
}
var delete = builder.Purge(row);
CheckCommand(delete, "Delete From \"package_rows\" Where \"Id\" = :p_0", model);
Check(await writer.ExecuteWriteAsync(delete) == 1, "generated delete");
using (var query = queryFactory.Create())
    Check((await query.From<CrudRow>().Where<CrudRow, int>(item => item.Id, 1).ToListAsync<CrudRow>()).Count == 0,
        "delete persisted");
Console.WriteLine("ORACLE_GENERATED_CRUD_PASS");

var transactions = serviceProvider.GetRequiredService<ISqlTransactionScopeFactory>();
await using (var transaction = await transactions.BeginAsync())
{
    using var transactionalWriter = transaction.CreateExecutor();
    using var transactionalQuery = transaction.CreateQuery();
    Check(await transactionalWriter.InsertAsync(new CrudRow { Id = 2, Name = "shared" }) == 1, "shared insert");
    var rows = await transactionalQuery.From<CrudRow>().Where<CrudRow, int>(item => item.Id, 2).ToListAsync<CrudRow>();
    Check(rows.Count == 1 && rows[0].Name == "shared", "shared read before rollback");
    await transaction.RollbackAsync();
}
using (var query = queryFactory.Create())
    Check((await query.From<CrudRow>().Where<CrudRow, int>(item => item.Id, 2).ToListAsync<CrudRow>()).Count == 0,
        "shared rollback");
Console.WriteLine("ORACLE_SHARED_ROLLBACK_PASS");

writer.ExecuteSql("CREATE TABLE \"identity_crud_rows\" (\"Id\" NUMBER(9) GENERATED ALWAYS AS IDENTITY PRIMARY KEY, \"Name\" NVARCHAR2(200) UNIQUE)");
writer.ExecuteSql("CREATE TABLE \"identity_only_crud_rows\" (\"Id\" NUMBER(19) GENERATED ALWAYS AS IDENTITY (START WITH 9007199254740993) PRIMARY KEY)");
var first = new IdentityCrudRow { Name = "键一" };
var second = new IdentityCrudRow { Name = "键二" };
var only = new IdentityOnlyCrudRow();
CheckIdentityCommand(first, identityPolicy,
    "Insert Into \"identity_crud_rows\" (\"Name\") Values (:p_0) Returning \"Id\" Into :bing_identity_key");
CheckIdentityCommand(only, identityOnlyPolicy,
    "Insert Into \"identity_only_crud_rows\" (\"Id\") Values (DEFAULT) Returning \"Id\" Into :bing_identity_key");
var keyWriter = (ISqlKeyInsertExecutor)writer;
var keys = await keyWriter.InsertWithKeysAsync<IdentityCrudRow, int>(
    new[] { new InsertRow<IdentityCrudRow>("second", second), new InsertRow<IdentityCrudRow>("first", first) });
Check(keys.Completion == WriteCompletionState.Committed && keys.AffectedRows == 2 &&
    second.Id > 0 && first.Id > second.Id && keys.Keys["first"] == first.Id && keys.Keys["second"] == second.Id,
    "identity output belongs to each input row");
using (var query = queryFactory.Create())
{
    var rows = await query.From<IdentityCrudRow>().ToListAsync<IdentityCrudRow>();
    Check(rows.Count == 2 && rows.Any(row => row.Id == first.Id && row.Name == first.Name) &&
        rows.Any(row => row.Id == second.Id && row.Name == second.Name), "identity batch committed");
}
var onlyKeys = await keyWriter.InsertWithKeysAsync<IdentityOnlyCrudRow, long>(
    new[] { new InsertRow<IdentityOnlyCrudRow>("only", only) });
Check(onlyKeys.Completion == WriteCompletionState.Committed && only.Id == 9_007_199_254_740_993L &&
    onlyKeys.Keys["only"] == only.Id, "default-only identity retained Int64 precision");
var rolledBack = new IdentityCrudRow { Name = "rollback" };
await using (var transaction = await transactions.BeginAsync())
{
    using var transactionalWriter = transaction.CreateExecutor();
    using var transactionalQuery = transaction.CreateQuery();
    var flushed = await ((ISqlKeyInsertExecutor)transactionalWriter).InsertWithKeysAsync<IdentityCrudRow, int>(
        new[] { new InsertRow<IdentityCrudRow>("rollback", rolledBack) });
    Check(flushed.Completion == WriteCompletionState.Flushed && rolledBack.Id > 0 &&
        flushed.Keys["rollback"] == rolledBack.Id, "identity flushed through shared transaction");
    Check((await transactionalQuery.From<IdentityCrudRow>().Where<IdentityCrudRow, int>(row => row.Id, rolledBack.Id)
        .ToListAsync<IdentityCrudRow>()).Count == 1, "shared read sees the identity row");
    await transaction.RollbackAsync();
}
using (var query = queryFactory.Create())
    Check((await query.From<IdentityCrudRow>().Where<IdentityCrudRow, int>(row => row.Id, rolledBack.Id)
        .ToListAsync<IdentityCrudRow>()).Count == 0, "identity shared rollback");
Console.WriteLine("ORACLE_IDENTITY_RETURNING_PASS");

await CheckDmlReturningIntoAsync();
Console.WriteLine("ORACLE_DML_RETURNING_INTO_PASS");
await CheckBuilderReturningAsync();
Console.WriteLine("ORACLE_BUILDER_RETURNING_PASS");
await CheckBuilderBulkReturningAsync();
await CheckBulkReturningTypedColumnsAsync();
Console.WriteLine("ORACLE_BUILDER_BULK_RETURNING_PASS");

async Task CheckDmlReturningIntoAsync()
{
    const string insertSql = "INSERT INTO \"identity_crud_rows\" (\"Name\") VALUES (:new_name) RETURNING \"Id\", \"Name\" INTO :out_id, :out_name";
    const string updateSql = "UPDATE \"identity_crud_rows\" SET \"Name\" = :new_name WHERE \"Id\" = :row_id RETURNING \"Id\", \"Name\" INTO :out_id, :out_name";
    const string deleteSql = "DELETE FROM \"identity_crud_rows\" WHERE \"Id\" = :row_id RETURNING \"Id\", \"Name\" INTO :out_id, :out_name";
    const string original = "写入回传";
    const string changed = "事务内更新";
    var outId = new SqlParam("out_id", null, DbType.Decimal, ParameterDirection.Output);
    var outName = new SqlParam("out_name", null, DbType.String, ParameterDirection.Output, size: 200);
    var inserted = writer.ExecuteSqlWithOutput(insertSql, new[] { new SqlParam("new_name", original, DbType.String), outId, outName });
    Check(inserted.AffectedRows == 1 && inserted.OutputParameters.GetValue<string>(":OUT_NAME") == original,
        "INSERT returned two typed columns");
    var id = inserted.OutputParameters.GetValue<int>("out_id");
    Check(id > 0, "INSERT returned the generated key");
    var rowId = new SqlParam("row_id", id, DbType.Int32);

    await using (var transaction = await transactions.BeginAsync())
    {
        using var transactionalWriter = transaction.CreateExecutor();
        using var transactionalQuery = transaction.CreateQuery();
        var updated = await transactionalWriter.ExecuteSqlWithOutputAsync(updateSql,
            new[] { new SqlParam("new_name", changed, DbType.String), rowId, outId, outName });
        Check(updated.AffectedRows == 1 && updated.OutputParameters.GetValue<int>("out_id") == id &&
            updated.OutputParameters.GetValue<string>("out_name") == changed, "UPDATE returned its new row");
        Check((await transactionalQuery.From<IdentityCrudRow>().Where<IdentityCrudRow, int>(row => row.Id, id)
            .ToListAsync<IdentityCrudRow>()).Single().Name == changed, "shared read sees the updated row");
        await transaction.RollbackAsync();
    }
    var deleted = await writer.ExecuteSqlWithOutputAsync(deleteSql, new[] { rowId, outId, outName });
    Check(deleted.AffectedRows == 1 && deleted.OutputParameters.GetValue<int>("out_id") == id &&
        deleted.OutputParameters.GetValue<string>("out_name") == original, "DELETE returned the restored row");
    var missing = writer.ExecuteSqlWithOutput(deleteSql, new[] { rowId, outId, outName });
    Check(missing.AffectedRows == 0 && inserted.OutputParameters.GetValue<int>("out_id") == id &&
        inserted.OutputParameters.GetValue<string>("out_name") == original && outId.Value is null && outName.Value is null,
        "no matching row and reusable output descriptors leave the prior snapshot intact");
}

async Task CheckBuilderReturningAsync()
{
    writer.ExecuteSql("CREATE TABLE \"builder_returning_rows\" (\"Id\" NUMBER(9) GENERATED ALWAYS AS IDENTITY PRIMARY KEY, \"Name\" NVARCHAR2(200))");
    var table = new SqlTableReference { TableName = "builder_returning_rows" };
    var columns = new[]
    {
        new OracleReturningColumn("Id", DbType.Decimal),
        new OracleReturningColumn("Name", DbType.String, size: 200, name: "returned_name")
    };
    var insert = writer.CreateInsertBuilder().InsertInto(table).Columns("Name").Values("构建器新增").BuildCommand()
        .ReturningInto(columns);
    var (id, name) = writer.ExecuteReturning(insert, ReadBuilderRow).Single();
    Check(id > 0 && name == "构建器新增", "Builder insert returned identity and Unicode columns");

    var update = writer.CreateUpdateBuilder().Update(table).Where("Id", id).Set("Name", "共享事务更新").BuildCommand()
        .ReturningInto(columns);
    var delete = writer.CreateDeleteBuilder().DeleteFrom(table).Where("Id", id).BuildCommand().ReturningInto(columns);
    await using (var transaction = await transactions.BeginAsync())
    {
        using var transactionalWriter = transaction.CreateExecutor();
        using var transactionalQuery = transaction.CreateQuery();
        Check((await transactionalWriter.ExecuteReturningAsync(update, ReadBuilderRow)).Single() == (id, "共享事务更新"),
            "Builder update uses bind names even when WHERE was configured before SET");
        Check(await transactionalQuery.Sql("SELECT COUNT(*) FROM \"builder_returning_rows\" WHERE \"Name\" = '共享事务更新'").ScalarAsync<int>() == 1,
            "shared query observes the Builder update");
        await transaction.RollbackAsync();
    }

    writer.ExecuteSql("INSERT INTO \"builder_returning_rows\" (\"Name\") VALUES ('other')");
    var updateMany = writer.CreateUpdateBuilder().Update(table).Set("Name", "should roll back").AllowAllRows().BuildCommand()
        .ReturningInto(columns);
    var deleteMany = writer.CreateDeleteBuilder().DeleteFrom(table).AllowAllRows().BuildCommand().ReturningInto(columns);
    await CheckRejectedAsync(writer, updateMany);
    await CheckRejectedAsync(writer, deleteMany);
    using (var query = queryFactory.Create())
    {
        Check(await query.Sql("SELECT COUNT(*) FROM \"builder_returning_rows\"").ScalarAsync<int>() == 2 &&
            await query.Sql("SELECT COUNT(*) FROM \"builder_returning_rows\" WHERE \"Name\" = 'should roll back'").ScalarAsync<int>() == 0,
            "multi-row statements were rolled back in the database");
    }
    await using (var transaction = await transactions.BeginAsync())
    {
        using var transactionalWriter = transaction.CreateExecutor();
        using var transactionalQuery = transaction.CreateQuery();
        transactionalWriter.ExecuteSql("INSERT INTO \"builder_returning_rows\" (\"Name\") VALUES ('prior write')");
        await CheckRejectedAsync(transactionalWriter, deleteMany);
        Check(await transactionalQuery.Sql("SELECT COUNT(*) FROM \"builder_returning_rows\"").ScalarAsync<int>() == 3,
            "multi-row failure preserves earlier successful statements in the shared transaction");
        await transaction.RollbackAsync();
    }
    Check((await writer.ExecuteReturningAsync(delete, ReadBuilderRow)).Single() == (id, "构建器新增"),
        "Builder delete returned the original value after rollback");
    Check(writer.ExecuteReturning<object>(delete, static _ => throw new InvalidOperationException("projected an empty result")).Count == 0,
        "Builder delete with no match never calls the projector");

    async Task CheckRejectedAsync(ISqlExecutor executor, OracleReturningCommand command)
    {
        try
        {
            await executor.ExecuteReturningAsync(command, ReadBuilderRow);
        }
        catch (OracleException error) when (error.Number == 1422)
        {
            return;
        }
        throw new InvalidOperationException("Builder returning accepted a multi-row write");
    }
}

async Task CheckBulkReturningTypedColumnsAsync()
{
    writer.ExecuteSql("""
        CREATE TABLE "builder_bulk_typed_rows" (
            "Id" NUMBER(9) PRIMARY KEY, "Tag" NUMBER(9), "FixedText" CHAR(4 CHAR),
            "NationalText" NCHAR(4), "Weight" BINARY_FLOAT)
        """);
    writer.ExecuteSql("""
        INSERT ALL
            INTO "builder_bulk_typed_rows" VALUES (1, 0, 'A', N'雪Ω', TO_BINARY_FLOAT('1.23456789'))
            INTO "builder_bulk_typed_rows" VALUES (2, 0, NULL, NULL, NULL)
            INTO "builder_bulk_typed_rows" VALUES (3, 0, '四AΩZ', N'文Ω界雪', TO_BINARY_FLOAT('3.4028235E38'))
            INTO "builder_bulk_typed_rows" VALUES (4, 0, NULL, NULL, TO_BINARY_FLOAT('-3.4028235E38'))
            INTO "builder_bulk_typed_rows" VALUES (5, 0, NULL, NULL, TO_BINARY_FLOAT('1.401298464324817E-45'))
            INTO "builder_bulk_typed_rows" VALUES (6, 0, NULL, NULL, TO_BINARY_FLOAT('INF'))
            INTO "builder_bulk_typed_rows" VALUES (7, 0, NULL, NULL, TO_BINARY_FLOAT('-INF'))
            INTO "builder_bulk_typed_rows" VALUES (8, 0, NULL, NULL, TO_BINARY_FLOAT('NaN'))
        SELECT 1 FROM DUAL
        """);
    var table = new SqlTableReference { TableName = "builder_bulk_typed_rows" };
    var columns = new[]
    {
        new OracleReturningColumn("Id", DbType.Int32),
        new OracleReturningColumn("FixedText", DbType.AnsiStringFixedLength, size: 4),
        new OracleReturningColumn("NationalText", DbType.StringFixedLength, size: 4),
        new OracleReturningColumn("Weight", DbType.Single)
    };
    var update = writer.CreateUpdateBuilder().Update(table).Where("Tag", 0).Set("Tag", 1).BuildCommand()
        .ReturningManyInto(8, columns);
    var delete = writer.CreateDeleteBuilder().DeleteFrom(table).Where("Tag", 1).BuildCommand()
        .ReturningManyInto(8, columns);

    CheckTypedRows(writer.ExecuteReturning(update, ReadTypedRow));
    CheckTypedRows(await writer.ExecuteReturningAsync(delete, ReadTypedRow));

    static (int Id, string Text, string NationalText, float? Weight) ReadTypedRow(ISqlOutputParameterAccessor output) =>
        (output.GetValue<int>("Id"), output.GetValue<string>("FixedText"),
            output.GetValue<string>("NationalText"), output.GetValue<float?>("Weight"));

    static void CheckTypedRows(List<(int Id, string Text, string NationalText, float? Weight)> rows)
    {
        Check(rows.Count == 8, "typed bulk output returns all rows");
        var byId = rows.ToDictionary(row => row.Id);
        Check(byId[1] == (1, "A   ", "雪Ω  ", 1.2345679f) && byId[2] == (2, null, null, null) &&
              byId[3] == (3, "四AΩZ", "文Ω界雪", float.MaxValue),
            "typed bulk output preserves CHAR/NCHAR padding, multibyte text, float and NULL");
        Check(byId[4].Weight == float.MinValue && byId[5].Weight == float.Epsilon &&
              byId[6].Weight == float.PositiveInfinity && byId[7].Weight == float.NegativeInfinity &&
              byId[8].Weight.HasValue && float.IsNaN(byId[8].Weight.Value),
            "typed bulk output preserves the BINARY_FLOAT range and special values");
    }
}

async Task CheckBuilderBulkReturningAsync()
{
    writer.ExecuteSql("CREATE TABLE \"builder_bulk_rows\" (\"Id\" NUMBER(19) PRIMARY KEY, \"Name\" NVARCHAR2(80), \"Tag\" NUMBER(9), \"Token\" RAW(4), \"WrittenOn\" DATE)");
    writer.ExecuteSql("INSERT INTO \"builder_bulk_rows\" VALUES (9007199254740993, N'保持原值', 0, HEXTORAW('0102A0FF'), DATE '2025-02-03')");
    writer.ExecuteSql("INSERT INTO \"builder_bulk_rows\" VALUES (2, NULL, 0, NULL, NULL)");
    writer.ExecuteSql("INSERT INTO \"builder_bulk_rows\" VALUES (3, N'单独一行', 2, NULL, NULL)");
    var table = new SqlTableReference { TableName = "builder_bulk_rows" };
    var columns = new[]
    {
        new OracleReturningColumn("Id", DbType.Decimal),
        new OracleReturningColumn("Name", DbType.String, size: 80, name: "returned_name"),
        new OracleReturningColumn("Token", DbType.Binary, size: 4),
        new OracleReturningColumn("WrittenOn", DbType.Date)
    };
    var update = writer.CreateUpdateBuilder().Update(table).Where("Tag", 0).Set("Tag", 1).BuildCommand()
        .ReturningManyInto(2, columns);
    var delete = writer.CreateDeleteBuilder().DeleteFrom(table).Where("Tag", 1).BuildCommand()
        .ReturningManyInto(2, columns);

    CheckRows(writer.ExecuteReturning(update, ReadBulkRow));
    await using (var transaction = await transactions.BeginAsync())
    {
        using var transactionalWriter = transaction.CreateExecutor();
        using var transactionalQuery = transaction.CreateQuery();
        CheckRows(await transactionalWriter.ExecuteReturningAsync(delete, ReadBulkRow));
        Check(await transactionalQuery.Sql("SELECT COUNT(*) FROM \"builder_bulk_rows\"").ScalarAsync<int>() == 1,
            "shared query sees the bulk delete before rollback");
        await transaction.RollbackAsync();
    }

    var oversizedUpdate = writer.CreateUpdateBuilder().Update(table).Set("Tag", 9).AllowAllRows().BuildCommand()
        .ReturningManyInto(2, columns);
    var oversizedDelete = writer.CreateDeleteBuilder().DeleteFrom(table).AllowAllRows().BuildCommand()
        .ReturningManyInto(2, columns);
    await CheckBulkCapacityAsync(writer, oversizedUpdate);
    using (var query = queryFactory.Create())
    {
        Check(await query.Sql("SELECT COUNT(*) FROM \"builder_bulk_rows\" WHERE \"Tag\" = 9").ScalarAsync<int>() == 0,
            "over-capacity update leaves no partial writes");
    }
    await using (var transaction = await transactions.BeginAsync())
    {
        using var transactionalWriter = transaction.CreateExecutor();
        using var transactionalQuery = transaction.CreateQuery();
        transactionalWriter.ExecuteSql("INSERT INTO \"builder_bulk_rows\" VALUES (4, N'先前写入', 2, NULL, NULL)");
        await CheckBulkCapacityAsync(transactionalWriter, oversizedDelete);
        Check(await transactionalQuery.Sql("SELECT COUNT(*) FROM \"builder_bulk_rows\"").ScalarAsync<int>() == 4,
            "over-capacity delete leaves the shared transaction's prior writes intact");
        await transaction.RollbackAsync();
    }
    CheckRows(await writer.ExecuteReturningAsync(delete, ReadBulkRow));
    Check(writer.ExecuteReturning<object>(delete, static _ => throw new InvalidOperationException("projected an empty batch")).Count == 0,
        "reused bulk command returns no rows when nothing matches");

    static (long Id, string Name, string Token, DateTime? WrittenOn) ReadBulkRow(ISqlOutputParameterAccessor output)
    {
        var token = output.GetValue<byte[]>("Token");
        return (output.GetValue<long>("Id"), output.GetValue<string>("returned_name"),
            token == null ? null : Convert.ToHexString(token), output.GetValue<DateTime?>("WrittenOn"));
    }

    static void CheckRows(List<(long Id, string Name, string Token, DateTime? WrittenOn)> rows) => Check(rows.Count == 2 &&
        rows.Contains((9007199254740993, "保持原值", "0102A0FF", new DateTime(2025, 2, 3))) && rows.Contains((2, null, null, null)),
        "bulk output preserves complete rows, Int64 precision, Unicode, RAW, DATE and NULL");

    static async Task CheckBulkCapacityAsync(ISqlExecutor executor, OracleReturningCommand command)
    {
        try
        {
            await executor.ExecuteReturningAsync<object>(command,
                static _ => throw new InvalidOperationException("projected an over-capacity batch"));
        }
        catch (OracleException error) when (error.Number == 20060)
        {
            return;
        }
        throw new InvalidOperationException("bulk returning accepted an over-capacity write");
    }
}

static (int Id, string Name) ReadBuilderRow(ISqlOutputParameterAccessor values) =>
    (values.GetValue<int>("Id"), values.GetValue<string>("returned_name"));

void CheckIdentityCommand<TEntity>(TEntity entity, EntityKeyPolicy policy, string expected) where TEntity : class
{
    var command = new SqlKeyInsertCommandBuilder(new SqlInsertBuilder(OracleSqlProvider.Instance, strictServices), policy)
        .Build(entity);
    var model = models.Single(item => item.EntityType == typeof(TEntity));
    Check(command.Sql == expected && !command.HasReturning && command.IsGeneratedExecution &&
        ReferenceEquals(command.Sql, model.GeneratedKeyInsertSql?.Invoke(OracleSqlProvider.Instance.Key)),
        "generated identity SQL and binder: " + expected + ". Actual: " + command.Sql);
}

static void CheckCommand(SqlWriteCommand command, string expected, EntityModelMetadata model)
{
    Check(command.Sql == expected && command.IsGeneratedExecution &&
        ReferenceEquals(command.Sql, model.GeneratedMutationSql?.Invoke(OracleSqlProvider.Instance.Key, command.OperationKind)),
        "generated SQL and binder: " + expected + ". Actual: " + command.Sql);
}

static void Check(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
