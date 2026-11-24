using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.Enums;
using Bing.Data.KeyGeneration;
using Bing.Data.Sql;
using Bing.Data.Sql.Aot.PackageModels;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Mutations.Batching;
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
    // 既有 Returning Lambda 保留匿名别名和 DTO 初始化器，不创建或编译投影委托。
    var anonymousCommand = writer.CreateUpdateBuilder().Update<CrudRow>()
        .Set("Name", "anonymous-returned").Where("Id", 1)
        .Returning<CrudRow>(item => new { DisplayName = item.Name, item.Id }).BuildCommand();
    if (anonymousCommand.Sql != "Update `package_rows` Set `Name` = @_p_0 Where `Id`=@_p_1 Returning `Name` As `DisplayName`, `Id` As `Id`")
        throw new Exception("anonymous returning builder SQL");
    var anonymousProjection = writer.ExecuteReturning<ReturningProjectionDto>(anonymousCommand).Single();
    if (anonymousProjection.Id != 1 || anonymousProjection.DisplayName != "anonymous-returned" ||
        anonymousProjection.OptionalNote != "initial-note") throw new Exception("anonymous returning builder result");
    var dtoCommand = writer.CreateUpdateBuilder().Update<CrudRow>()
        .Set("Name", "dto-returned").Where("Id", 1)
        .Returning<CrudRow>(item => new ReturningProjectionDto { Id = item.Id, DisplayName = item.Name }).BuildCommand();
    if (dtoCommand.Sql != "Update `package_rows` Set `Name` = @_p_0 Where `Id`=@_p_1 Returning `Id` As `Id`, `Name` As `DisplayName`")
        throw new Exception("DTO returning builder SQL");
    var dtoProjection = (await writer.ExecuteReturningAsync<ReturningProjectionDto>(dtoCommand.WithValidateAffectedRows(true))).Single();
    if (dtoProjection.Id != 1 || dtoProjection.DisplayName != "dto-returned" ||
        dtoProjection.OptionalNote != "initial-note") throw new Exception("DTO returning builder result");
    // 计算列沿用 Returning API；真实 Native 执行验证完整 SQL 和既有 DTO Reader。
    var computedCommand = writer.CreateUpdateBuilder().Update<CrudRow>()
        .Set("Name", "computed-returned").Where("Id", 1)
        .Returning<CrudRow>(item => new ReturningProjectionDto
        {
            Id = (item.Id + item.Id) * item.Id,
            DisplayName = item.Name
        }).BuildCommand();
    if (computedCommand.Sql != "Update `package_rows` Set `Name` = @_p_0 Where `Id`=@_p_1 Returning ((`Id` + `Id`) * `Id`) As `Id`, `Name` As `DisplayName`")
        throw new Exception("computed returning builder SQL");
    var computedProjection = (await writer.ExecuteReturningAsync<ReturningProjectionDto>(computedCommand)).Single();
    if (computedProjection.Id != 2 || computedProjection.DisplayName != "computed-returned" ||
        computedProjection.OptionalNote != "initial-note") throw new Exception("computed returning builder result");
    // 一元负号使用相同的生成布局与 Reader，同一个命令支持同步、异步执行。
    var negatedCommand = writer.CreateUpdateBuilder().Update<CrudRow>()
        .Set("Name", "negated-returned").Where("Id", 1)
        .Returning<CrudRow>(item => new ReturningProjectionDto
        {
            Id = -item.Id,
            DisplayName = item.Name
        }).BuildCommand();
    if (negatedCommand.Sql != "Update `package_rows` Set `Name` = @_p_0 Where `Id`=@_p_1 Returning (-`Id`) As `Id`, `Name` As `DisplayName`")
        throw new Exception("negated returning builder SQL");
    foreach (var negatedResult in new[]
    {
        writer.ExecuteReturning<ReturningProjectionDto>(negatedCommand).Single(),
        (await writer.ExecuteReturningAsync<ReturningProjectionDto>(negatedCommand)).Single()
    })
        if (negatedResult.Id != -1 || negatedResult.DisplayName != "negated-returned" ||
            negatedResult.OptionalNote != "initial-note") throw new Exception("negated returning builder result");
    Console.WriteLine("GENERATED_NEGATED_RETURNING_NATIVE_PASS");
    // 同一生成调用点绑定当前物理列；同步、异步均只解析一次映射。
    writer.ExecuteSql("CREATE TABLE remapped_returning(row_id INTEGER PRIMARY KEY, display_name TEXT)");
    writer.ExecuteSql("INSERT INTO remapped_returning VALUES (3, 'original')");
    var remappedResolver = new RemappedComputedReturningResolver();
    var remappedServices = new SqlBuilderServices(
        metadataOptions: new SqlMetadataOptions { ExecutionMode = SqlExecutionMode.GeneratedOnly },
        entityModelMetadataProvider: new RegisteredEntityModelMetadataProvider(models),
        entityMappingResolver: remappedResolver);
    for (var pass = 0; pass < 2; pass++)
    {
        var remapped = new SqlUpdateBuilder(SqliteSqlProvider.Instance, remappedServices)
            .Update(new SqlTableReference { TableName = "remapped_returning" })
            .Set("display_name", "mapped-returned").Where("row_id", 3)
            .Returning<CrudRow>(item => new ReturningProjectionDto
            {
                Id = (item.Id + item.Id) * item.Id,
                DisplayName = item.Name
            }).BuildCommand();
        if (remapped.Sql != "Update `remapped_returning` Set `display_name` = @_p_0 Where `row_id`=@_p_1 Returning ((`row_id` + `row_id`) * `row_id`) As `Id`, `display_name` As `DisplayName`" ||
            remappedResolver.ResolveCalls != pass + 1)
            throw new Exception("generated computed returning mapping SQL");
        var mappedResult = pass == 0
            ? writer.ExecuteReturning<ReturningProjectionDto>(remapped).Single()
            : (await writer.ExecuteReturningAsync<ReturningProjectionDto>(remapped)).Single();
        if (mappedResult.Id != 18 || mappedResult.DisplayName != "mapped-returned" ||
            mappedResult.OptionalNote != "initial-note") throw new Exception("generated computed returning mapping result");
    }
    Console.WriteLine("GENERATED_COMPUTED_RETURNING_MAPPING_NATIVE_PASS");
    // 固定物理名包含引用符和注释文本时，仍采用生成 SQL，并以真实数据库核验转义。
    writer.ExecuteSql("CREATE TABLE quoted_returning(`row``id--value` INTEGER PRIMARY KEY, Name TEXT)");
    writer.ExecuteSql("INSERT INTO quoted_returning VALUES (3, 'original')");
    var quotedServices = new SqlBuilderServices(
        metadataOptions: new SqlMetadataOptions { ExecutionMode = SqlExecutionMode.GeneratedOnly },
        entityMappingResolver: new RemappedComputedReturningResolver("row`id--value", "Name"));
    var quotedCommand = new SqlUpdateBuilder(SqliteSqlProvider.Instance, quotedServices)
        .Update(new SqlTableReference { TableName = "quoted_returning" })
        .Set("Name", "quoted-returned").Where("row`id--value", 3)
        .Returning<QuotedReturningRow>(item => new ReturningProjectionDto
        {
            Id = -(item.Id + item.Id),
            DisplayName = item.Name
        }).BuildCommand();
    if (quotedCommand.Sql != "Update `quoted_returning` Set `Name` = @_p_0 Where `row``id--value`=@_p_1 Returning (-(`row``id--value` + `row``id--value`)) As `Id`, `Name` As `DisplayName`")
        throw new Exception("quoted returning builder SQL");
    foreach (var quotedResult in new[]
    {
        writer.ExecuteReturning<ReturningProjectionDto>(quotedCommand).Single(),
        (await writer.ExecuteReturningAsync<ReturningProjectionDto>(quotedCommand)).Single()
    })
        if (quotedResult.Id != -6 || quotedResult.DisplayName != "quoted-returned" ||
            quotedResult.OptionalNote != "initial-note") throw new Exception("quoted returning builder result");
    Console.WriteLine("GENERATED_QUOTED_RETURNING_NATIVE_PASS");
    Console.WriteLine("GENERATED_RETURNING_PROJECTION_NATIVE_PASS");
    await CachedReturningProbe.VerifyAsync(writer);
    if (await writer.DeleteAsync(row) != 1) throw new Exception("delete");

    // 使用真实生成器产物核对主命令链复用 SQL 常量，随后通过严格模式 Executor 执行。
    var model = models.Single(item => item.EntityType == typeof(VersionedCrudRow));
    var builder = new DefaultSqlEntityMutationCommandBuilder(SqliteSqlProvider.Instance, new SqlBuilderServices(
        metadataOptions: new SqlMetadataOptions { ExecutionMode = SqlExecutionMode.GeneratedOnly },
        entityModelMetadataProvider: new RegisteredEntityModelMetadataProvider(models)));
    var crudModel = models.Single(item => item.EntityType == typeof(CrudRow));
    // 整行 Returning 直接消费模型程序集生成的完整 SQL、静态参数绑定和实体读取器。
    var returningRow = new CrudRow { Id = 100, Name = "entity-returning" };
    var entityInsert = builder.InsertReturning(returningRow);
    const string entityInsertSql = "Insert Into `package_rows` (`Id`, `Name`) Values (@_p_0, @_p_1) Returning `Id` As `Id`, `Name` As `Name`";
    if (!ReferenceEquals(entityInsert.Sql, crudModel.GeneratedMutationReturningSql?.Invoke("bing.sqlite", entityInsert.OperationKind)) ||
        entityInsert.Sql != entityInsertSql || !entityInsert.IsGeneratedExecution || !entityInsert.HasReturning)
        throw new Exception("generated entity insert returning SQL");
    var insertedRow = writer.ExecuteReturning<CrudRow>(entityInsert).Single();
    if (insertedRow.Id != 100 || insertedRow.Name != "entity-returning") throw new Exception("entity insert returning result");
    insertedRow.Name = "entity-updated";
    var entityUpdate = builder.UpdateReturning(insertedRow);
    const string entityUpdateSql = "Update `package_rows` Set `Name` = @_p_0 Where `Id` = @_p_1 Returning `Id` As `Id`, `Name` As `Name`";
    if (!ReferenceEquals(entityUpdate.Sql, crudModel.GeneratedMutationReturningSql?.Invoke("bing.sqlite", entityUpdate.OperationKind)) ||
        entityUpdate.Sql != entityUpdateSql || !entityUpdate.IsGeneratedExecution || !entityUpdate.HasReturning)
        throw new Exception("generated entity update returning SQL");
    var updatedRow = (await writer.ExecuteReturningAsync<CrudRow>(entityUpdate)).Single();
    if (updatedRow.Id != 100 || updatedRow.Name != "entity-updated") throw new Exception("entity update returning result");
    var entityDelete = builder.DeleteReturning(updatedRow);
    const string entityDeleteSql = "Delete From `package_rows` Where `Id` = @_p_0 Returning `Id` As `Id`, `Name` As `Name`";
    if (!ReferenceEquals(entityDelete.Sql, crudModel.GeneratedMutationReturningSql?.Invoke("bing.sqlite", entityDelete.OperationKind)) ||
        entityDelete.Sql != entityDeleteSql || !entityDelete.IsGeneratedExecution || !entityDelete.HasReturning)
        throw new Exception("generated entity delete returning SQL");
    var deletedRow = writer.ExecuteReturning<CrudRow>(entityDelete).Single();
    if (deletedRow.Id != 100 || deletedRow.Name != "entity-updated") throw new Exception("entity delete returning result");
    Console.WriteLine("GENERATED_ENTITY_RETURNING_SQL_NATIVE_PASS");
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
    // 行数在运行时决定，表列和参数格式来自生成器；完整 SQL 不按批次数缓存。
    var tripleRows = new[]
    {
        new CrudRow { Id = 5, Name = "shape-triple-a" },
        new CrudRow { Id = 6, Name = "shape-triple-b" },
        new CrudRow { Id = 7, Name = "shape-triple-c" }
    };
    var tripleInsert = builder.InsertCombined(tripleRows);
    const string tripleInsertSql = "Insert Into `package_rows` (`Id`, `Name`) Values (@_p_0, @_p_1), (@_p_2, @_p_3), (@_p_4, @_p_5)";
    if (tripleInsert.Sql != tripleInsertSql || !tripleInsert.IsGeneratedExecution ||
        await writer.ExecuteWriteAsync(tripleInsert) != tripleRows.Length)
        throw new Exception("generated variable-row insert shape");
    var singleColumnRows = new[] { new CrudRow { Id = 8 }, new CrudRow { Id = 9 } };
    var singleColumnBatch = builder.InsertCombined(singleColumnRows,
        new SqlInsertOptions { IncludeProperties = new[] { nameof(CrudRow.Id) } });
    const string singleColumnBatchSql = "Insert Into `package_rows` (`Id`) Values (@_p_0), (@_p_1)";
    if (singleColumnBatch.Sql != singleColumnBatchSql || !singleColumnBatch.IsGeneratedExecution ||
        writer.ExecuteWrite(singleColumnBatch) != singleColumnRows.Length)
        throw new Exception("generated single-column batch insert shape");
    Console.WriteLine("GENERATED_MULTIROW_INSERT_NATIVE_PASS");
    // 批量删除沿用生成键读取和静态绑定，运行时只追加 IN 参数槽位。
    var tripleDelete = builder.DeleteCombined(tripleRows);
    const string tripleDeleteSql = "Delete From `package_rows` Where `Id` In (@_p_0,@_p_1,@_p_2)";
    if (tripleDelete.Sql != tripleDeleteSql ||
        tripleDelete.Sql != crudModel.GeneratedMutationShapeSql?.Invoke("bing.sqlite", SqlOperationKind.Delete, null, tripleRows.Length) ||
        !tripleDelete.IsGeneratedExecution || await writer.ExecuteWriteAsync(tripleDelete) != tripleRows.Length)
        throw new Exception("generated variable-row delete shape");
    var pairDelete = builder.DeleteCombined(singleColumnRows);
    const string pairDeleteSql = "Delete From `package_rows` Where `Id` In (@_p_0,@_p_1)";
    if (pairDelete.Sql != pairDeleteSql || !pairDelete.IsGeneratedExecution ||
        writer.ExecuteWrite(pairDelete) != singleColumnRows.Length)
        throw new Exception("generated combined delete shape");
    Console.WriteLine("GENERATED_IN_DELETE_NATIVE_PASS");
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
    conflicted = false;
    try { writer.ExecuteWrite(builder.Update(versioned, original)); }
    catch (Bing.Exceptions.ConcurrencyException) { conflicted = true; }
    if (!conflicted) throw new Exception("generated command concurrency failure");
    // 复合键并发模型保留多个可写列，筛选 Name 后应命中单列静态形状。
    versioned.Name = "filtered-shape";
    var filteredUpdate = builder.Update(versioned,
        new SqlUpdateOptions { IncludeProperties = new[] { nameof(VersionedCrudRow.Name) } });
    const string filteredUpdateSql = "Update `versioned_rows` Set `Name` = @_p_0 Where `TenantId` = @_p_1 And `Id` = @_p_2 And `Version` = @_p_3";
    if (!ReferenceEquals(filteredUpdate.Sql, model.GeneratedMutationShapeSql?.Invoke("bing.sqlite",
            SqlOperationKind.Update, nameof(VersionedCrudRow.Name), 1)) || filteredUpdate.Sql != filteredUpdateSql ||
        !filteredUpdate.IsGeneratedExecution || writer.ExecuteWrite(filteredUpdate) != 1)
        throw new Exception("filtered generated update");
    await SelectedMutationProbe.VerifyAsync(writer, models);
    Console.WriteLine("GENERATED_MUTATION_SHAPES_NATIVE_PASS");
    if (await writer.DeleteAsync(versioned) != 1) throw new Exception("generated delete");

    // 复合键与令牌按实体配对，批次入口按实体数量校验受影响行数。
    for (var batch = 0; batch < 2; batch++)
    {
        var tenant = 10 + batch * 3;
        var selected = new[]
        {
            new VersionedCrudRow { TenantId = tenant, Id = 2, Version = 1 },
            new VersionedCrudRow { TenantId = tenant + 1, Id = 3, Version = 2 },
            new VersionedCrudRow { TenantId = tenant + 2, Id = 4, Version = 3 }
        };
        var crossed = new[]
        {
            new VersionedCrudRow { TenantId = tenant, Id = 3, Version = 1 },
            new VersionedCrudRow { TenantId = tenant + 1, Id = 2, Version = 2 },
            new VersionedCrudRow { TenantId = tenant + 2, Id = 2, Version = 3 }
        };
        if (await writer.InsertBatchAsync(selected.Concat(crossed)) != 6)
            throw new Exception("paired delete seed");
        var paired = builder.DeleteCombined(selected);
        const string pairedSql = "Delete From `versioned_rows` Where (((`TenantId` = @_p_0 And `Id` = @_p_1 And `Version` = @_p_2) Or (`TenantId` = @_p_3 And `Id` = @_p_4 And `Version` = @_p_5)) Or (`TenantId` = @_p_6 And `Id` = @_p_7 And `Version` = @_p_8))";
        if (paired.Sql != pairedSql || paired.Sql != model.GeneratedMutationShapeSql?.Invoke(
                "bing.sqlite", SqlOperationKind.Delete, null, 3) || !paired.IsGeneratedExecution || !paired.ValidateAffectedRows)
            throw new Exception("generated paired delete SQL and binder");
        var affected = batch == 0 ? writer.DeleteBatch(selected) : await writer.DeleteBatchAsync(selected);
        if (affected != 3 || writer.DeleteBatch(crossed, new SqlBatchDeleteOptions
            { Strategy = SqlBatchDeleteStrategy.CompositePredicate }) != 3)
            throw new Exception("generated paired delete crossed keys");
    }
    Console.WriteLine("GENERATED_PAIRED_DELETE_NATIVE_PASS");

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

/// <summary>验证生成式计算投影绑定当前配置，不冻结编译期物理列名。</summary>
internal sealed class RemappedComputedReturningResolver : IEntityMappingResolver
{
    private readonly string _idColumn;
    private readonly string _nameColumn;

    internal RemappedComputedReturningResolver(string idColumn = "row_id", string nameColumn = "display_name")
    {
        _idColumn = idColumn;
        _nameColumn = nameColumn;
    }

    internal int ResolveCalls { get; private set; }
    public EntityDescriptor GetDescriptor(Type entityType) => throw new NotSupportedException();

    public EntityMappingMetadata Resolve(Type entityType, DatabaseContext databaseContext)
    {
        ResolveCalls++;
        return new EntityMappingMetadata
        {
            EntityType = entityType,
            Columns = new Dictionary<string, ColumnMappingMetadata>
            {
                [nameof(CrudRow.Id)] = new() { PropertyName = nameof(CrudRow.Id), ColumnName = _idColumn },
                [nameof(CrudRow.Name)] = new() { PropertyName = nameof(CrudRow.Name), ColumnName = _nameColumn }
            }
        };
    }
}

/// <summary>编译期已知但需要转义的 Returning 物理列。</summary>
public sealed class QuotedReturningRow
{
    /// <summary>列名中的反引号必须按 SQLite 规则转义。</summary>
    [Column("row`id--value")]
    public int Id { get; set; }

    /// <summary>与计算列混合返回的直接列。</summary>
    public string Name { get; set; } = string.Empty;
}
