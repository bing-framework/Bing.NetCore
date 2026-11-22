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
using Bing.Dapper;
using Bing.Dapper.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

// 只使用 Executor 的消费程序，模型生成不能依赖 Query 的调用点。
SQLitePCL.Batteries_V2.Init();
var path = Path.Combine(Path.GetTempPath(), "bing-crud-only-" + Guid.NewGuid().ToString("N") + ".db");
try
{
    var services = new ServiceCollection();
    var models = PackageModels.CreateModels();
    services.ConfigureSqlMetadata(options => options.ExecutionMode = SqlExecutionMode.GeneratedOnly);
    services.AddSqlGeneratedModels(models);
    services.AddSqlDataSource("default", DatabaseType.Sqlite, "Data Source=" + path);
    services.AddSqliteProvider();
    services.AddEntityKeyPolicies(keys => keys
        .ForGenerated<IdentityCrudRow, int>(nameof(IdentityCrudRow.Id), EntityKeyGenerationKind.DatabaseIdentity,
            static row => row.Id, static (row, key) => row.Id = key)
        .ForGenerated<IdentityOnlyCrudRow, long>(nameof(IdentityOnlyCrudRow.Id), EntityKeyGenerationKind.DatabaseIdentity,
            static row => row.Id, static (row, key) => row.Id = key));
    using var provider = services.BuildServiceProvider();
    using var scope = provider.CreateScope();
    using var writer = scope.ServiceProvider.GetRequiredService<ISqlExecutorFactory>().Create();
    writer.ExecuteSql("CREATE TABLE package_rows(Id INTEGER PRIMARY KEY, Name TEXT)");
    var row = new CrudRow { Id = 1, Name = "inserted" };
    if (await writer.InsertAsync(row) != 1) throw new Exception("insert");
    row.Name = "updated";
    if (await writer.UpdateAsync(row) != 1) throw new Exception("update");
    var command = writer.CreateUpdateBuilder().Update(new SqlTableReference { TableName = "package_rows" })
        .Set("Name", "returned").Where("Id", 1).Returning("Id", "Name").BuildCommand();
    if ((await writer.ExecuteReturningAsync<CrudRow>(command)).Single().Name != "returned") throw new Exception("returning");
    const string projectionSql = "UPDATE package_rows SET Name = Name WHERE Id = 1 RETURNING Name AS \"display_name\", NULL AS \"optional_note\", Id AS \"row_id\"";
    var projectionCommand = new SqlWriteCommand(projectionSql, Array.Empty<SqlParam>(), SqliteSqlProvider.Instance,
        SqlOperationKind.Update, true);
    if (projectionCommand.Sql != projectionSql) throw new Exception("returning projection SQL");
    var projection = writer.ExecuteReturning<ReturningProjectionDto>(projectionCommand).Single();
    if (projection.Id != 1 || projection.DisplayName != "returned" || projection.OptionalNote != "initial-note")
        throw new Exception("generated returning projection sync");
    const string asyncProjectionSql = "UPDATE package_rows SET Name = Name WHERE Id = 1 RETURNING Id AS \"row_id\", NULL AS \"optional_note\", Name AS \"display_name\"";
    var asyncProjectionCommand = new SqlWriteCommand(asyncProjectionSql, Array.Empty<SqlParam>(), SqliteSqlProvider.Instance,
        SqlOperationKind.Update, true);
    if (asyncProjectionCommand.Sql != asyncProjectionSql) throw new Exception("async returning projection SQL");
    var asyncProjection = (await writer.ExecuteReturningAsync<ReturningProjectionDto>(asyncProjectionCommand)).Single();
    if (asyncProjection.Id != 1 || asyncProjection.DisplayName != "returned" || asyncProjection.OptionalNote != "initial-note")
        throw new Exception("generated returning projection async");
    Console.WriteLine("GENERATED_RETURNING_PROJECTION_NATIVE_PASS");
    if (await writer.DeleteAsync(row) != 1) throw new Exception("delete");

    // 使用真实生成器产物核对主命令链复用 SQL 常量，随后通过严格模式 Executor 执行。
    var model = models.Single(item => item.EntityType == typeof(VersionedCrudRow));
    var builder = new DefaultSqlEntityMutationCommandBuilder(SqliteSqlProvider.Instance, new SqlBuilderServices(
        metadataOptions: new SqlMetadataOptions { ExecutionMode = SqlExecutionMode.GeneratedOnly },
        entityModelMetadataProvider: new RegisteredEntityModelMetadataProvider(models)));
    var crudModel = models.Single(item => item.EntityType == typeof(CrudRow));
    var singleColumnInsert = builder.Insert(new CrudRow { Id = 2, Name = "shape-insert" },
        new SqlInsertOptions { IncludeProperties = new[] { nameof(CrudRow.Id) } });
    const string singleColumnInsertSql = "Insert Into `package_rows` (`Id`) Values (@_p_0)";
    if (!ReferenceEquals(singleColumnInsert.Sql, crudModel.GeneratedMutationShapeSql?.Invoke("bing.sqlite",
            singleColumnInsert.OperationKind, nameof(CrudRow.Id), 1)) || singleColumnInsert.Sql != singleColumnInsertSql ||
        !singleColumnInsert.IsGeneratedExecution ||
        writer.ExecuteWrite(singleColumnInsert) != 1)
        throw new Exception("generated single-column insert shape");
    var combinedRows = new[]
    {
        new CrudRow { Id = 3, Name = "shape-combined-a" },
        new CrudRow { Id = 4, Name = "shape-combined-b" }
    };
    var combinedInsert = builder.InsertCombined(combinedRows);
    const string combinedInsertSql = "Insert Into `package_rows` (`Id`, `Name`) Values (@_p_0, @_p_1), (@_p_2, @_p_3)";
    if (!ReferenceEquals(combinedInsert.Sql, crudModel.GeneratedMutationShapeSql?.Invoke("bing.sqlite",
            combinedInsert.OperationKind, null, combinedRows.Length)) || combinedInsert.Sql != combinedInsertSql ||
        !combinedInsert.IsGeneratedExecution ||
        writer.ExecuteWrite(combinedInsert) != combinedRows.Length)
        throw new Exception("generated combined insert shape");
    var versioned = new VersionedCrudRow { TenantId = 7, Id = 1, Name = null, Version = 1 };
    var commands = new[] { builder.Insert(versioned), builder.Update(versioned), builder.Purge(versioned) };
    foreach (var generated in commands)
        if (!ReferenceEquals(generated.Sql, model.GeneratedMutationSql?.Invoke("bing.sqlite", generated.OperationKind)) ||
            !generated.IsGeneratedExecution)
            throw new Exception("generated SQL and binder were not used");
    writer.ExecuteSql("CREATE TABLE versioned_rows(TenantId INTEGER, Id INTEGER, Name TEXT, Version INTEGER, PRIMARY KEY(TenantId, Id))");
    if (await writer.InsertAsync(versioned) != 1) throw new Exception("generated insert");
    MutationPlanReaderProbe.Verify(builder, versioned);
    versioned.Name = "version-two";
    versioned.Version = 2;
    var original = new SqlUpdateOptions<VersionedCrudRow>().Original(item => item.Version, 1);
    if (await writer.UpdateAsync(versioned, original) != 1) throw new Exception("generated update");
    var conflicted = false;
    try { await writer.UpdateAsync(versioned, original); }
    catch (Bing.Exceptions.ConcurrencyException) { conflicted = true; }
    if (!conflicted) throw new Exception("generated concurrency failure");
    // 复合键并发模型保留多个可写列，筛选 Name 后应命中单列静态形状。
    versioned.Name = "filtered-shape";
    var filteredUpdate = builder.Update(versioned,
        new SqlUpdateOptions { IncludeProperties = new[] { nameof(VersionedCrudRow.Name) } });
    const string filteredUpdateSql = "Update `versioned_rows` Set `Name` = @_p_0 Where `TenantId` = @_p_1 And `Id` = @_p_2 And `Version` = @_p_3";
    if (!ReferenceEquals(filteredUpdate.Sql, model.GeneratedMutationShapeSql?.Invoke("bing.sqlite",
            SqlOperationKind.Update, nameof(VersionedCrudRow.Name), 1)) || filteredUpdate.Sql != filteredUpdateSql ||
        !filteredUpdate.IsGeneratedExecution || writer.ExecuteWrite(filteredUpdate) != 1)
        throw new Exception("filtered generated update");
    Console.WriteLine("GENERATED_MUTATION_SHAPES_NATIVE_PASS");
    if (await writer.DeleteAsync(versioned) != 1) throw new Exception("generated delete");

    // 静态 SQL、参数绑定和标量主键读取必须真正进入 Native 执行链。
    var strictServices = new SqlBuilderServices(
        metadataOptions: new SqlMetadataOptions { ExecutionMode = SqlExecutionMode.GeneratedOnly },
        entityModelMetadataProvider: new RegisteredEntityModelMetadataProvider(models));
    var identityPolicy = EntityKeyPolicy.CreateGenerated<IdentityCrudRow, int>(nameof(IdentityCrudRow.Id),
        EntityKeyGenerationKind.DatabaseIdentity, static row => row.Id, static (row, key) => row.Id = key);
    var identity = new IdentityCrudRow { Name = null };
    var keyCommand = new SqlKeyInsertCommandBuilder(new SqlInsertBuilder(SqliteSqlProvider.Instance, strictServices),
        identityPolicy).Build(identity);
    if (!ReferenceEquals(keyCommand.Sql, models.Single(item => item.EntityType == typeof(IdentityCrudRow))
            .GeneratedKeyInsertSql?.Invoke("bing.sqlite")) || !keyCommand.HasReturning || !keyCommand.IsGeneratedExecution)
        throw new Exception("generated key SQL and binder were not used");
    writer.ExecuteSql("CREATE TABLE identity_crud_rows(Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT UNIQUE)");
    writer.ExecuteSql("CREATE TABLE identity_only_crud_rows(Id INTEGER PRIMARY KEY AUTOINCREMENT)");
    var keys = (ISqlKeyInsertExecutor)writer;
    var identityResult = await keys.InsertWithKeysAsync<IdentityCrudRow, int>(
        new[] { new InsertRow<IdentityCrudRow>("identity", identity) });
    if (identityResult.Completion != WriteCompletionState.Committed || identity.Id <= 0 ||
        identityResult.Keys["identity"] != identity.Id) throw new Exception("generated identity backfill");
    var empty = new IdentityOnlyCrudRow();
    var emptyResult = await keys.InsertWithKeysAsync<IdentityOnlyCrudRow, long>(
        new[] { new InsertRow<IdentityOnlyCrudRow>("empty", empty) });
    if (emptyResult.Completion != WriteCompletionState.Committed || empty.Id <= 0 ||
        emptyResult.Keys["empty"] != empty.Id) throw new Exception("generated default values");
    var failed = false;
    try
    {
        await keys.InsertWithKeysAsync<IdentityCrudRow, int>(new[]
        {
            new InsertRow<IdentityCrudRow>("one", new IdentityCrudRow { Name = "duplicate" }),
            new InsertRow<IdentityCrudRow>("two", new IdentityCrudRow { Name = "duplicate" })
        });
    }
    catch (SqliteException) { failed = true; }
    if (!failed || CountIdentityRows() != 1) throw new Exception("generated identity batch rollback");
    await using (var transaction = await provider.GetRequiredService<ISqlTransactionScopeFactory>().BeginAsync())
    {
        using var shared = transaction.CreateExecutor();
        var sharedResult = await ((ISqlKeyInsertExecutor)shared).InsertWithKeysAsync<IdentityCrudRow, int>(
            new[] { new InsertRow<IdentityCrudRow>("shared", new IdentityCrudRow { Name = "shared" }) });
        if (sharedResult.Completion != WriteCompletionState.Flushed || CountIdentityRows() != 1)
            throw new Exception("generated identity transaction ownership");
        await transaction.RollbackAsync();
    }
    writer.ExecuteSql("UPDATE sqlite_sequence SET seq=2147483647 WHERE name='identity_crud_rows'");
    var overflowRow = new IdentityCrudRow { Name = "overflow" };
    var overflowed = false;
    try { await keys.InsertWithKeysAsync<IdentityCrudRow, int>(new[] { new InsertRow<IdentityCrudRow>("overflow", overflowRow) }); }
    catch (OverflowException) { overflowed = true; }
    if (!overflowed || overflowRow.Id != 0 || CountIdentityRows() != 1)
        throw new Exception("generated identity checked conversion rollback");

    int CountIdentityRows()
    {
        using var connection = new SqliteConnection("Data Source=" + path);
        connection.Open();
        using var count = connection.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM identity_crud_rows";
        return checked((int)(long)count.ExecuteScalar());
    }
    Console.WriteLine("GENERATED_KEY_INSERT_SQL_NATIVE_PASS");
    Console.WriteLine("GENERATED_CRUD_SQL_NATIVE_PASS");
    Console.WriteLine("CRUD_ONLY_PACKAGE_NATIVE_PASS");
}
finally { SqliteConnection.ClearAllPools(); if (File.Exists(path)) File.Delete(path); }
