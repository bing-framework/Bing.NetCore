using Bing.Data.Sql;
using Bing.Data.Sql.Aot.PackageModels;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Mutations;
using Bing.Dapper.Sqlite;

/// <summary>严格 Native 模式下验证多列片段、默认值、整行读取和原始并发令牌。</summary>
internal static class SelectedMutationProbe
{
    internal static async Task VerifyAsync(ISqlExecutor writer, EntityModelMetadata[] models)
    {
        var model = models.Single(item => item.EntityType == typeof(SelectedCrudRow));
        if (model.GeneratedMutationColumnsSql == null) throw new Exception("selected columns factory missing");
        var builder = new DefaultSqlEntityMutationCommandBuilder(SqliteSqlProvider.Instance, new SqlBuilderServices(
            metadataOptions: new SqlMetadataOptions { ExecutionMode = SqlExecutionMode.GeneratedOnly },
            entityModelMetadataProvider: new RegisteredEntityModelMetadataProvider(models)));
        writer.ExecuteSql("CREATE TABLE selected_crud_rows(Id INTEGER PRIMARY KEY, Name TEXT, Quantity INTEGER, Version INTEGER DEFAULT 1)");
        var insertOptions = new SqlInsertOptions { IncludeProperties = new[] { "Quantity", "Name" } };
        var row = new SelectedCrudRow { Name = "selected", Quantity = 7, Version = 99 };
        const string insertSql = "Insert Into `selected_crud_rows` (`Name`, `Quantity`) Values (@_p_0, @_p_1)";
        const string returned = " Returning `Id` As `Id`, `Name` As `Name`, `Quantity` As `Quantity`, `Version` As `Version`";
        var insert = builder.InsertReturning(row, insertOptions);
        if (insert.Sql != insertSql + returned || !insert.IsGeneratedExecution || !insert.HasReturning ||
            !insert.Parameters.Select(p => p.Value).SequenceEqual(new object[] { "selected", 7 }))
            throw new Exception("selected returning insert SQL and binder");
        var saved = writer.ExecuteReturning<SelectedCrudRow>(insert).Single();
        if (saved.Id != 1 || saved.Name != "selected" || saved.Quantity != 7 || saved.Version != 1)
            throw new Exception("selected returning insert defaults");
        var ordinaryInsert = builder.Insert(new SelectedCrudRow { Name = "ordinary", Quantity = 3 }, insertOptions);
        if (ordinaryInsert.Sql != insertSql || await writer.ExecuteWriteAsync(ordinaryInsert) != 1)
            throw new Exception("selected ordinary insert");

        // 多行选列沿用同一生成合同，参数槽位按行递增；排除列仍由数据库赋默认值。
        var rows = new[]
        {
            new SelectedCrudRow { Name = "batch-a", Quantity = 11 },
            new SelectedCrudRow { Name = "batch-b", Quantity = 12 },
            new SelectedCrudRow { Name = "batch-c", Quantity = 13 }
        };
        var combinedSql = insertSql + ", (@_p_2, @_p_3), (@_p_4, @_p_5)";
        var generatedCombinedSql = model.GeneratedMutationColumnsSql(SqliteSqlProvider.Instance.Key,
            SqlOperationKind.InsertValues, new[] { "Name", "Quantity" }, rows.Length, false);
        var combined = builder.InsertCombined(rows, new SqlInsertOptions { ExcludeProperties = new[] { "Version" } });
        if (generatedCombinedSql != combinedSql || combined.Sql != combinedSql || !combined.IsGeneratedExecution ||
            combined.GeneratedParameterBinder == null ||
            !combined.Parameters.Select(p => p.Value).SequenceEqual(new object[] { "batch-a", 11, "batch-b", 12, "batch-c", 13 }) ||
            await writer.ExecuteWriteAsync(combined) != rows.Length)
            throw new Exception("selected combined insert SQL and row-major binder");

        saved.Name = "changed";
        saved.Quantity = 8;
        saved.Version = 99;
        var options = new SqlUpdateOptions<SelectedCrudRow> { ExcludeProperties = new[] { "Version" } }
            .Original(item => item.Version, 1);
        const string updateSql = "Update `selected_crud_rows` Set `Name` = @_p_0, `Quantity` = @_p_1 Where `Id` = @_p_2 And `Version` = @_p_3";
        var update = builder.UpdateReturning(saved, options);
        if (update.Sql != updateSql + returned || !update.IsGeneratedExecution || !update.ValidateAffectedRows ||
            !update.Parameters.Select(p => p.Value).SequenceEqual(new object[] { "changed", 8, 1, 1 }))
            throw new Exception("selected returning update SQL and original token");
        var updated = (await writer.ExecuteReturningAsync<SelectedCrudRow>(update)).Single();
        if (updated.Id != 1 || updated.Name != "changed" || updated.Quantity != 8 || updated.Version != 1)
            throw new Exception("selected returning whole row");
        var ordinaryUpdate = builder.Update(saved, options);
        if (ordinaryUpdate.Sql != updateSql || writer.ExecuteWrite(ordinaryUpdate) != 1)
            throw new Exception("selected ordinary update");
        var stale = new SqlUpdateOptions<SelectedCrudRow> { IncludeProperties = new[] { "Quantity", "Name" } }
            .Original(item => item.Version, 0);
        try { writer.ExecuteWrite(builder.Update(saved, stale)); }
        catch (Bing.Exceptions.ConcurrencyException) { return; }
        throw new Exception("selected update must validate stale original token");
    }
}
