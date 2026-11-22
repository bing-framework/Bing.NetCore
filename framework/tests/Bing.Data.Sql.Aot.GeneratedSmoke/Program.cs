using System.ComponentModel.DataAnnotations;
using System.Data;
using Bing;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using System.Diagnostics;
using Bing.Uow;
using Bing.Data.Sql.Diagnostics;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bing.Data.Sql.Builders.Params;
using EntityColumn = Bing.Data.Sql.Metadata.ColumnMappingMetadata;
using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.KeyGeneration;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Conditions;
using Bing.Data.Sql.Builders.Mutations.Batching;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Generated;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Mutations;
using Bing.Dapper;
using Bing.Dapper.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

Console.OutputEncoding = Encoding.UTF8;
SQLitePCL.Batteries_V2.Init();
using var diagnostics = new SmokeDiagnostics();
var trace = new SmokeLogger(args.Contains("--trace"));
var path = Path.Combine(Path.GetTempPath(), "bing-generated-" + Guid.NewGuid().ToString("N") + ".db");
try
{
    var services = new ServiceCollection();
    var guidHandler = new Dapper.Handlers.GuidTypeHandler();
    var guidAdapter = new SqlTypeHandlerAdapter<Guid>(guidHandler.Parse, guidHandler.SetValue);
    services.AddSingleton<IFieldValueConverter>(guidAdapter);
    services.AddSingleton<ISqlDbParameterCustomizer>(guidAdapter);
    services.AddSingleton<IFieldValueConverter, PayloadConverter>();
    services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Trace).AddProvider(trace));
    services.AddUnitOfWorkScopes();
    services.ConfigureSqlMetadata(options => options.ExecutionMode = SqlExecutionMode.GeneratedOnly);
    services.AddSqlGeneratedModels(GeneratedModels.CreateModels());
    services.AddSqlDataSource("default", DatabaseType.Sqlite, "Data Source=" + path);
    services.AddSqliteProvider();
    services.AddEntityKeyPolicies(keys => keys.ForGenerated<IdentityRow, long>(nameof(IdentityRow.Id),
        EntityKeyGenerationKind.DatabaseIdentity, static row => row.Id, static (row, id) => row.Id = id));
    using var provider = services.BuildServiceProvider();
    using var scope = provider.CreateScope();
    await IntegerReaderSmoke.Run(path);
    await DeclaredKeyPolicySmoke.Run(path);
    var queries = scope.ServiceProvider.GetRequiredService<ISqlQueryFactory>();
    var executors = scope.ServiceProvider.GetRequiredService<ISqlExecutorFactory>();
    var transactions = scope.ServiceProvider.GetRequiredService<ISqlTransactionScopeFactory>();
    using (var writer = executors.Create())
    {
        writer.ExecuteSql("CREATE TABLE GeneratedRows(Id INTEGER PRIMARY KEY, Name TEXT, Version INTEGER);" +
            "CREATE TABLE AlternateRows(Id INTEGER PRIMARY KEY, Name TEXT, Version INTEGER);" +
            "INSERT INTO AlternateRows VALUES(7, 'alternate', 3);" +
            "CREATE TABLE IdentityRows(Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT);" +
            "CREATE TABLE ConversionRows(Id INTEGER PRIMARY KEY, Token BLOB, Payload TEXT);" +
            "CREATE TABLE BooleanRows(Id INTEGER PRIMARY KEY, Enabled INTEGER);" +
            "INSERT INTO BooleanRows VALUES(1, 1), (2, 0);");
        Check(writer.Insert(new Row { Id = 1, Name = "first", Version = 1 }) == 1, "insert");
        Check(await writer.InsertAsync(new Row { Id = 2, Name = "second", Version = 1 }) == 1, "insert async");
        Check(writer.Update(new Row { Id = 1, Name = "updated", Version = 2 },
            new SqlUpdateOptions<Row>().Original(row => row.Version, 1)) == 1, "original concurrency value");
    }
    var automaticTemplateSql = AutomaticQueryTemplateProbe.ToSql(queries, "r", 1, true, 1, true, new long[] { 1, 2 });
    Check(automaticTemplateSql == $"Select `r`.`Id` {Environment.NewLine}From `GeneratedRows` As `r` {Environment.NewLine}" +
        $"Where `r`.`Id`>=@_p_0 And `r`.`Version`>=@_p_1 And `r`.`Id` In (@_p_2,@_p_3) {Environment.NewLine}" +
        "Order By `r`.`Id`", "automatic query template full SQL");
    var automaticTemplateSameShapeSql = AutomaticQueryTemplateProbe.ToSql(queries, "r", 2, true, 1, true, new long[] { 2, 1 });
    Check(automaticTemplateSql == automaticTemplateSameShapeSql &&
        ReferenceEquals(automaticTemplateSql, automaticTemplateSameShapeSql), "automatic query template reuses same shape SQL");
    var automaticTemplateDisabledSql = AutomaticQueryTemplateProbe.ToSql(queries, "r", 1, false, 2, false, Array.Empty<long>());
    Check(automaticTemplateDisabledSql == $"Select `r`.`Id` {Environment.NewLine}From `GeneratedRows` As `r` {Environment.NewLine}" +
        $"Where `r`.`Id`>=@_p_0 {Environment.NewLine}Order By `r`.`Id`", "automatic query template disabled SQL");
    var automaticTemplateSingleCollectionSql = AutomaticQueryTemplateProbe.ToSql(queries, "r", 1, true, 1, true, new long[] { 1 });
    Check(automaticTemplateSingleCollectionSql == $"Select `r`.`Id` {Environment.NewLine}From `GeneratedRows` As `r` {Environment.NewLine}" +
        $"Where `r`.`Id`>=@_p_0 And `r`.`Version`>=@_p_1 And `r`.`Id` In (@_p_2) {Environment.NewLine}" +
        "Order By `r`.`Id`", "automatic query template single collection SQL");
    var automaticTemplateEmptyCollectionSql = AutomaticQueryTemplateProbe.ToSql(queries, "r", 1, true, 1, true, Array.Empty<long>());
    Check(automaticTemplateEmptyCollectionSql == $"Select `r`.`Id` {Environment.NewLine}From `GeneratedRows` As `r` {Environment.NewLine}" +
        $"Where `r`.`Id`>=@_p_0 And `r`.`Version`>=@_p_1 And 1 = 0 {Environment.NewLine}" +
        "Order By `r`.`Id`", "automatic query template empty collection SQL");
    Check(AutomaticQueryTemplateProbe.ToList(queries, "r", 1, true, 2, true, new long[] { 1, 2 })
        .Select(row => row.Id).SequenceEqual(new long[] { 1 }), "automatic query template sync result");
    Check((await AutomaticQueryTemplateProbe.ToListAsync(queries, "r", 2, false, 0, true, new long[] { 2 }))
        .Select(row => row.Id).SequenceEqual(new long[] { 2 }), "automatic query template async result");
    using (var query = queries.Create())
    {
        var rows = query.Sql("SELECT Name, Id, Version FROM GeneratedRows WHERE Id >= @Minimum ORDER BY Id",
            new Filter { Minimum = 1 }).ToList<Row>();
        Check(rows.Count == 2 && rows[0].Name == "updated" && rows[0].Version == 2, "raw generated reader and named parameters");
        var bindingFilter = new BindingFilter { Value = 7, Offset = null };
        Check(query.Sql("SELECT @Value + COALESCE(@Offset, 0)", bindingFilter).Scalar<int>() == 7,
            "generated parameter binding");
        bindingFilter.Value = 11;
        bindingFilter.Offset = 2;
        Check(query.Sql("SELECT @Value + COALESCE(@Offset, 0)", bindingFilter).Scalar<int>() == 13,
            "generated parameter binding refresh");
        Check(query.Sql("SELECT @Value + @Other", new InheritedBindingFilter { Value = 9, Other = 12 }).Scalar<int>() == 21,
            "generated inherited parameter binding");
        Check(query.Sql("SELECT @Value + COALESCE(@Offset, 0)",
            new Dictionary<string, object> { ["Value"] = 7, ["Offset"] = null }).Scalar<int>() == 7,
            "dictionary parameter binding");
        Check((await query.Sql("SELECT Id, Name FROM GeneratedRows WHERE Id = 2").SingleAsync<Row>()).Name == "second", "single async");
        Check(query.Sql("SELECT COUNT(*) FROM GeneratedRows").Scalar<int>() == 2, "scalar");
        Check(query.Sql("SELECT Id FROM GeneratedRows ORDER BY Id").First<Row>().Id == 1, "first");
        Check(query.Sql("SELECT Id FROM GeneratedRows WHERE Id < 0").FirstOrDefault<Row>() == null, "empty first default");
        var page = await query.Sql("SELECT Id, Name, Version FROM GeneratedRows").ToPageAsync<Row>(new Pager(1, 1, 0, "Id"));
        Check(page.Data.Count == 1 && page.TotalCount == 2, "page count and data");
        Check(query.Sql("SELECT Id FROM GeneratedRows ORDER BY Id").AsEnumerable<Row>().Count() == 2, "stream");
        var count = 0;
        await foreach (var row in query.Sql("SELECT Id FROM GeneratedRows").AsAsyncEnumerable<Row>()) count++;
        Check(count == 2, "async stream");
        Check(query.Sql("SELECT 255").Single<TinyState>() == (TinyState)255, "checked enum keeps unnamed value");
        var collectionBuilder = ((ISqlQueryBuilderSource)query).CreateIndependentSqlBuilder().From<Row>("r").Select<Row>();
        collectionBuilder.Where(new SqlCondition("`r`.`Id` IN @ids AND @ids0=2 AND '@ids'=char(64)||'ids' /* @ids */"));
        collectionBuilder.AddParam("ids", new long[] { 1, 2 });
        collectionBuilder.AddParam("ids0", 2L);
        var collectionQuery = SqlQueryRuntimeFactory.CreateLambdaQuery((ISqlQueryPlanExecutor)query, collectionBuilder);
        Check(collectionQuery.ToSql() == $"Select `r`.`Id`,`r`.`Name`,`r`.`Version` {Environment.NewLine}From `GeneratedRows` As `r` {Environment.NewLine}" +
            "Where `r`.`Id` IN @ids AND @ids0=2 AND '@ids'=char(64)||'ids' /* @ids */", "generated collection source SQL");
        Check(collectionQuery.ToList<Row>().Select(row => row.Id).SequenceEqual(new long[] { 1, 2 }),
            "generated collection preserves SQL literal and distinct scalar parameter");
        var enumOverflow = false;
        try { query.Sql("SELECT 256").Single<TinyState>(); }
        catch (OverflowException) { enumOverflow = true; }
        Check(enumOverflow, "checked enum rejects overflow");
        Check(query.Sql("SELECT 99 AS Computed").Single<ComputedResult>().Computed == 34, "getter-only projection preserves constructor value");
        var template = new SqlQueryShapeTemplate(
            new SqlQueryFragment("SELECT Id FROM GeneratedRows WHERE Version=@version AND @payload=X'010203'",
                new[] { new SqlParam("@version", 0, DbType.Int32), new SqlParam("@payload", Array.Empty<byte>(), DbType.Binary) }),
            new[] { new SqlQueryOptionalFragment(0, "AND Id=@id",
                new[] { new SqlParam("@id", 0L, DbType.Int64) }) });
        var shape = new SqlQueryShapeKey("generated-rows", "sqlite", "v1", "default", 1, string.Empty);
        var payload = new byte[] { 1, 2, 3 };
        var fragment = template.Render(shape,
            new Dictionary<string, object> { ["version"] = 2, ["id"] = 1L, ["payload"] = payload }, null);
        payload[0] = 9;
        Check(query.Sql(fragment.Sql, fragment.Parameters).Single<Row>().Id == 1, "strict generated shape template");
        var orderedTemplate = new SqlQueryShapeTemplate(
            new SqlQueryFragment("SELECT Id FROM GeneratedRows WHERE Version>=@version",
                new[] { new SqlParam("@version", 0, DbType.Int32) }),
            new[] { new SqlQueryOptionalFragment(0, "AND Name=@name",
                new[] { new SqlParam("@name", "", DbType.String) }) },
            " ", new SqlQueryFragment("ORDER BY Id DESC LIMIT @take", new[] { new SqlParam("@take", 0, DbType.Int32) }));
        var ordered = orderedTemplate.Render(1,
            new Dictionary<string, object> { ["version"] = 1, ["name"] = "updated", ["take"] = 2 });
        Check(ordered.Sql == "SELECT Id FROM GeneratedRows WHERE Version>=@version AND Name=@name ORDER BY Id DESC LIMIT @take",
            "shape suffix SQL");
        Check(query.Sql(ordered.Sql, ordered.Parameters).ToList<Row>().Select(row => row.Id).SequenceEqual(new long[] { 1 }),
            "strict generated shape suffix");
        var unorderedCondition = orderedTemplate.Render(0, new Dictionary<string, object> { ["version"] = 1, ["take"] = 1 });
        Check(unorderedCondition.Sql == "SELECT Id FROM GeneratedRows WHERE Version>=@version ORDER BY Id DESC LIMIT @take",
            "shape suffix without optional SQL");
        Check(query.Sql(unorderedCondition.Sql, unorderedCondition.Parameters).ToList<Row>().Select(row => row.Id).SequenceEqual(new long[] { 2 }),
            "strict generated shape suffix without optional");
        var groupedTemplate = SqlQueryShapeTemplate.CreateOrdered(
            new SqlQueryFragment("SELECT Version FROM GeneratedRows WHERE Id>=@minimumId",
                new[] { new SqlParam("@minimumId", 0, DbType.Int32) }),
            new (SqlQueryFragment Fragment, int? OptionalBit)[]
            {
                (new SqlQueryFragment("AND Name=@name", new[] { new SqlParam("@name", "", DbType.String) }), 1),
                (new SqlQueryFragment("GROUP BY Version"), null),
                (new SqlQueryFragment("HAVING COUNT(*)>=@minimumCount", new[] { new SqlParam("@minimumCount", 0, DbType.Int32) }), 0),
                (new SqlQueryFragment("ORDER BY Version"), null)
            });
        var groupedValues = new Dictionary<string, object> { ["minimumId"] = 1, ["name"] = "second", ["minimumCount"] = 1 };
        for (var mask = 0; mask < 4; mask++)
        {
            var grouped = groupedTemplate.Render(mask, groupedValues);
            var expectedSql = "SELECT Version FROM GeneratedRows WHERE Id>=@minimumId" +
                ((mask & 2) != 0 ? " AND Name=@name" : "") + " GROUP BY Version" +
                ((mask & 1) != 0 ? " HAVING COUNT(*)>=@minimumCount" : "") + " ORDER BY Version";
            Check(grouped.Sql == expectedSql, "ordered shape SQL " + mask);
            var expectedVersions = (mask & 2) != 0 ? new[] { 1 } : new[] { 1, 2 };
            Check(query.Sql(grouped.Sql, grouped.Parameters).ToList<int>().SequenceEqual(expectedVersions),
                "strict generated ordered shape " + mask);
        }
        const string scopedSql = "SELECT CASE WHEN '@id' = char(64) || 'id' THEN @id ELSE -1 END /* @id */";
        var scoped = SqlQueryFragment.Compose(" UNION ALL ",
            new SqlQueryFragment(scopedSql, new[] { new SqlParam("@id", 7, DbType.Int32) }),
            new SqlQueryFragment(scopedSql, new[] { new SqlParam("@id", 9, DbType.Int32) }));
        Check(scoped.Sql == "SELECT CASE WHEN '@id' = char(64) || 'id' THEN @fragment_0_id ELSE -1 END /* @id */" +
            " UNION ALL SELECT CASE WHEN '@id' = char(64) || 'id' THEN @fragment_1_id ELSE -1 END /* @id */",
            "scoped fragment SQL");
        Check(query.Sql(scoped.Sql, scoped.Parameters).ToList<int>().SequenceEqual(new[] { 7, 9 }),
            "strict generated scoped fragments");
        var collisionTemplate = new SqlQueryShapeTemplate(new SqlQueryFragment(
            "SELECT CASE WHEN @id=7 AND @_id=9 THEN 1 ELSE 0 END",
            new[] { new SqlParam("@id", 0), new SqlParam("@_id", 0) }));
        var collision = collisionTemplate.Render(0,
            new Dictionary<string, object> { ["id"] = 7, ["_id"] = 9 }, new SqlQueryScope("root"));
        Check(collision.Sql == "SELECT CASE WHEN @root_id=7 AND @root_id_1=9 THEN 1 ELSE 0 END",
            "scoped collision SQL");
        Check(query.Sql(collision.Sql, collision.Parameters).Scalar<int>() == 1,
            "strict generated scoped collision");
        var sharedSlotTemplate = new SqlQueryShapeTemplate(
            new SqlQueryFragment("SELECT @value", new[] { new SqlParam("@value", 1, DbType.Int32) }),
            new[] { new SqlQueryOptionalFragment(0, "WHERE :VALUE > 0",
                new[] { new SqlParam(":VALUE", 2, DbType.Int32) }) });
        var rejectedDefaults = false;
        try { sharedSlotTemplate.Render(1); }
        catch (InvalidOperationException) { rejectedDefaults = true; }
        Check(rejectedDefaults, "shared slot conflicting defaults");
        var sharedSlot = sharedSlotTemplate.Render(1, new Dictionary<string, object> { ["value"] = 7 });
        Check(sharedSlot.Sql == "SELECT @value WHERE @value > 0", "shared slot SQL");
        Check(query.Sql(sharedSlot.Sql, sharedSlot.Parameters).Scalar<int>() == 7,
            "strict generated shared slot");
        var firstKey = new SqlParameterKey("first", "id");
        var secondKey = new SqlParameterKey("second", "id");
        var keyedTemplate = new SqlQueryShapeTemplate(
            new SqlQueryFragment("SELECT @id", new[] { new SqlParam("@id", 0, DbType.Int32) }).WithParameterKey("id", firstKey),
            new[] { new SqlQueryOptionalFragment(0, new SqlQueryFragment("UNION ALL SELECT @id",
                new[] { new SqlParam("@id", 0, DbType.Int32) }).WithParameterKey("id", secondKey)) });
        var keyed = keyedTemplate.RenderWithKeys(1, new Dictionary<SqlParameterKey, object> { [firstKey] = 7, [secondKey] = 9 });
        Check(keyed.Sql == "SELECT @id UNION ALL SELECT @id_1", "logical parameter SQL");
        Check(query.Sql(keyed.Sql, keyed.Parameters).ToList<int>().SequenceEqual(new[] { 7, 9 }),
            "strict generated logical parameters");
        var slotPayload = new byte[] { 1, 2, 3 };
        var slotFragment = template.RenderValues(shape, new object[] { 2, slotPayload, 1L });
        slotPayload[0] = 9;
        Check(slotFragment.Sql == "SELECT Id FROM GeneratedRows WHERE Version=@version AND @payload=X'010203' AND Id=@id",
            "slot binding complete SQL");
        Check(query.Sql(slotFragment.Sql, slotFragment.Parameters).Single<Row>().Id == 1,
            "strict slot binding binary snapshot");
        var slotOrdered = orderedTemplate.RenderValues(1, new object[] { 1, "updated", 2 });
        Check(slotOrdered.Sql == "SELECT Id FROM GeneratedRows WHERE Version>=@version AND Name=@name ORDER BY Id DESC LIMIT @take" &&
            query.Sql(slotOrdered.Sql, slotOrdered.Parameters).ToList<Row>().Select(row => row.Id).SequenceEqual(new long[] { 1 }),
            "strict slot optional suffix");
        var slotDisabled = orderedTemplate.RenderValues(0, new object[] { 1, 1 });
        Check(slotDisabled.Sql == "SELECT Id FROM GeneratedRows WHERE Version>=@version ORDER BY Id DESC LIMIT @take" &&
            query.Sql(slotDisabled.Sql, slotDisabled.Parameters).ToList<Row>().Select(row => row.Id).SequenceEqual(new long[] { 2 }),
            "strict slot disabled optional suffix");
        var slotKeyed = keyedTemplate.RenderValues(1, new object[] { 11, 13 });
        Check(slotKeyed.Sql == "SELECT @id UNION ALL SELECT @id_1" &&
            query.Sql(slotKeyed.Sql, slotKeyed.Parameters).ToList<int>().SequenceEqual(new[] { 11, 13 }),
            "strict slot logical identities");
        var slotShared = sharedSlotTemplate.RenderValues(1, new object[] { 17 });
        Check(slotShared.Sql == "SELECT @value WHERE @value > 0" &&
            query.Sql(slotShared.Sql, slotShared.Parameters).Scalar<int>() == 17,
            "strict slot shared conflicting defaults");
        var directSlotPayload = new byte[] { 1, 2, 3 };
        var directSlotQuery = query.Sql(template, shape, new object[] { 2, directSlotPayload, 1L },
            new SqlQueryScope("direct"));
        directSlotPayload[0] = 9;
        Check(directSlotQuery.CommandText == "SELECT Id FROM GeneratedRows WHERE Version=@direct_version AND @direct_payload=X'010203' AND Id=@direct_id" &&
            directSlotQuery.Single<Row>().Id == 1, "strict direct template scoped binary snapshot");
        var directOrdered = query.Sql(orderedTemplate, 1, new object[] { 1, "updated", 2 });
        Check(directOrdered.CommandText == "SELECT Id FROM GeneratedRows WHERE Version>=@version AND Name=@name ORDER BY Id DESC LIMIT @take" &&
            directOrdered.ToList<Row>().Select(row => row.Id).SequenceEqual(new long[] { 1 }),
            "strict direct template optional suffix");
        Check(query.Sql(keyedTemplate, 1, new object[] { 11, 13 }).ToList<int>().SequenceEqual(new[] { 11, 13 }) &&
            query.Sql(sharedSlotTemplate, 1, new object[] { 17 }).Scalar<int>() == 17,
            "strict direct template logical identities");
        Check(query.Sql(new SqlQueryShapeTemplate(new SqlQueryFragment("SELECT 19")), 0,
            Array.Empty<object>()).Scalar<int>() == 19, "strict direct template empty slots");
        var concurrentSlots = new SqlQueryShapeTemplate(new SqlQueryFragment("SELECT @id",
            new[] { new SqlParam("@id", 0, DbType.Int32) }),
            new[] { new SqlQueryOptionalFragment(0, "WHERE @id>=@minimum",
                new[] { new SqlParam("@minimum", 0, DbType.Int32) }) });
        await Task.WhenAll(Enumerable.Range(1, 16).Select(id => Task.Run(async () =>
        {
            using var independentScope = provider.CreateScope();
            using var independentQuery = independentScope.ServiceProvider.GetRequiredService<ISqlQueryFactory>().Create();
            var enabled = (id & 1) != 0;
            var values = enabled ? new object[] { id, id - 1 } : new object[] { id };
            var request = independentQuery.Sql(concurrentSlots, enabled ? 1 : 0, values);
            Check(request.CommandText == (enabled ? "SELECT @id WHERE @id>=@minimum" : "SELECT @id"),
                "strict direct template concurrent SQL " + id);
            Check(await request.ScalarAsync<int>() == id,
                "strict direct template concurrent request " + id);
        })));
        var builderFragment = new SqlQueryFragment("SELECT @ID AS value UNION ALL SELECT :ID",
            new[] { new SqlParam("@id", 7, DbType.Int32), new SqlParam(":id", 9, DbType.Int32) });
        var fragmentBuilder = new SqliteBuilder();
        fragmentBuilder.AddParam("id", 100);
        fragmentBuilder.Select("*").From(builderFragment, "items");
        var fragmentSql = fragmentBuilder.ToSql();
        Check(fragmentSql == $"Select * {Environment.NewLine}From (SELECT @_p_0 AS value UNION ALL SELECT @_p_1) As `items`",
            "builder fragment SQL");
        Check(query.Sql(fragmentSql, fragmentBuilder.GetSqlParams().Values).ToList<int>().SequenceEqual(new[] { 7, 9 }),
            "strict generated builder fragment");
        var cteFragment = new SqlQueryFragment("SELECT @ID AS value", new[] { new SqlParam("id", 7, DbType.Int32) });
        var unionFragment = new SqlQueryFragment("SELECT :ID AS value", new[] { new SqlParam("id", 9, DbType.Int32) });
        var unionBuilder = new SqliteBuilder().Select("*").From("items").With("items", cteFragment).UnionAll(unionFragment);
        var unionSql = $"With Recursive `items` {Environment.NewLine}As (SELECT @id AS value){Environment.NewLine}Select * {Environment.NewLine}From `items` {Environment.NewLine}Union All {Environment.NewLine}SELECT @_p_0 AS value";
        Check(unionBuilder.ToSql() == unionSql && unionBuilder.ToSql() == unionSql, "builder CTE and union SQL");
        Check(query.Sql(unionSql, unionBuilder.GetSqlParams().Values).ToList<int>().SequenceEqual(new[] { 7, 9 }),
            "strict generated builder CTE and union");
        var leftKey = new SqlParameterKey("left", "id");
        var rightKey = new SqlParameterKey("right", "id");
        var left = new SqlQueryFragment("SELECT @id AS value", new[] { new SqlParam("id", 0, DbType.Int32) })
            .WithParameterKey("id", leftKey);
        var right = new SqlQueryFragment("SELECT @id AS value", new[] { new SqlParam("id", 0, DbType.Int32) })
            .WithParameterKey("id", rightKey);
        var keyedBuilder = new SqliteBuilder().Select("*").From("items").With("items", left).UnionAll(right);
        var keyedShape = new SqlQueryShapeTemplate(keyedBuilder.ToTemplate().CreateFragment());
        var keyedResult = keyedShape.RenderWithKeys(0,
            new Dictionary<SqlParameterKey, object> { [leftKey] = 7, [rightKey] = 9 });
        Check(keyedResult.Sql == unionSql, "builder keyed CTE and union SQL");
        Check(query.Sql(keyedResult.Sql, keyedResult.Parameters).ToList<int>().SequenceEqual(new[] { 7, 9 }),
            "strict generated builder keyed fragments");
        var nestedLeft = new SqliteBuilder().Select("*").From(left, "a");
        var nestedRight = new SqliteBuilder().Select("*").From(right, "b");
        var nestedBuilder = new SqliteBuilder().Select("*").From("items").With("items", nestedLeft).UnionAll(nestedRight);
        var nestedShape = new SqlQueryShapeTemplate(nestedBuilder.ToTemplate().CreateFragment());
        var nestedResult = nestedShape.RenderWithKeys(0,
            new Dictionary<SqlParameterKey, object> { [leftKey] = 7, [rightKey] = 9 });
        Check(nestedResult.Sql == $"With Recursive `items` {Environment.NewLine}As (Select * {Environment.NewLine}From (SELECT @id AS value) As `a`){Environment.NewLine}Select * {Environment.NewLine}From `items` {Environment.NewLine}Union All {Environment.NewLine}Select * {Environment.NewLine}From (SELECT @_p_0 AS value) As `b`",
            "nested builder logical parameter SQL");
        Check(query.Sql(nestedResult.Sql, nestedResult.Parameters).ToList<int>().SequenceEqual(new[] { 7, 9 }),
            "strict generated nested builder logical parameters");
        var directKey = new SqlParameterKey("direct", "id");
        var directFragment = new SqlQueryFragment("SELECT @id AS value", new[] { new SqlParam("id", 7, DbType.Int32) })
            .WithParameterKey("id", directKey);
        var directBuilder = new SqliteBuilder().Select("*").From(directFragment, "items");
        directBuilder.ParameterManager.Add("id", 19);
        var directShape = new SqlQueryShapeTemplate(directBuilder.ToTemplate().CreateFragment());
        var directResult = directShape.RenderWithKeys(0, new Dictionary<SqlParameterKey, object> { [directKey] = 23 });
        Check(directResult.Sql == $"Select * {Environment.NewLine}From (SELECT @id AS value) As `items`", "direct parameter replacement SQL");
        Check(directResult.Slots.SequenceEqual(new[] { new SqlParameterKey("id") }), "direct parameter replacement slot");
        Check(query.Sql(directResult.Sql, directResult.Parameters).ToList<int>().SequenceEqual(new[] { 19 }),
            "strict generated direct parameter replacement");
    }
    using (var query = queries.Create())
    {
        var minimum = 1L;
        var explicitOr = query.From<Row>("r")
            .Where<Row, long>(2L, (row, value) => value < row.Id || row.Id == value, "r")
            .Select<Row>(row => row.Id);
        Check(explicitOr.ToSql() == $"Select `r`.`Id` {Environment.NewLine}From `GeneratedRows` As `r` {Environment.NewLine}Where (`r`.`Id`>@_p_0 Or `r`.`Id`=@_p_1)" &&
            explicitOr.ToList<Row>().Select(row => row.Id).SequenceEqual(new long[] { 2 }),
            "strict generated explicit compound value SQL and result");
        var explicitNull = query.From<Row>("r")
            .Where<Row, string>(null, (row, value) => value == row.Name || value != row.Name, "r")
            .Select<Row>(row => row.Id);
        Check(explicitNull.ToSql() == $"Select `r`.`Id` {Environment.NewLine}From `GeneratedRows` As `r` {Environment.NewLine}Where (`r`.`Name` Is Null Or `r`.`Name` Is Not Null)" &&
            explicitNull.ToList<Row>().Count == 2, "strict generated explicit null SQL and result");
        var compoundEnabled = query.From<Row>("r")
            .WhereIf<Row>(true, row => row.Id >= 1 && (row.Version >= 2 || row.Id == 2))
            .Where<Row, long>(row => row.Id, minimum, Operator.GreaterEqual)
            .Select<Row>(row => row.Id);
        Check(compoundEnabled.ToSql() == $"Select `r`.`Id` {Environment.NewLine}From `GeneratedRows` As `r` {Environment.NewLine}Where `r`.`Id`>=@_p_0 And (`r`.`Version`>=@_p_1 Or `r`.`Id`=@_p_2) And `r`.`Id`>=@_p_3" &&
            compoundEnabled.ToList<Row>().Select(row => row.Id).SequenceEqual(new long[] { 1, 2 }),
            "strict generated compound WhereIf SQL and result");
        var compoundDisabled = query.From<Row>("other")
            .WhereIf<Row>(false, row => row.Id >= 1 && (row.Version >= 2 || row.Id == 2))
            .Where<Row, long>(row => row.Id, 2, Operator.GreaterEqual)
            .Select<Row>(row => row.Id);
        Check(compoundDisabled.ToSql() == $"Select `other`.`Id` {Environment.NewLine}From `GeneratedRows` As `other` {Environment.NewLine}Where `other`.`Id`>=@_p_0" &&
            compoundDisabled.ToList<Row>().Select(row => row.Id).SequenceEqual(new long[] { 2 }),
            "strict generated disabled compound WhereIf SQL and result");
        var constantLeftGreater = query.From<Row>("r")
            .Where<Row>(row => 1 < row.Id)
            .Select<Row>(row => row.Id);
        Check(constantLeftGreater.ToSql() == $"Select `r`.`Id` {Environment.NewLine}From `GeneratedRows` As `r` {Environment.NewLine}Where `r`.`Id`>@_p_0",
            "generated constant-left less-than SQL");
        Check(constantLeftGreater.ToList<Row>().Select(row => row.Id).SequenceEqual(new long[] { 2 }),
            "generated constant-left less-than result");
        var constantLeftLessEqual = query.From<Row>("r")
            .Where<Row>(row => 2 >= row.Id)
            .Select<Row>(row => row.Id);
        Check(constantLeftLessEqual.ToSql() == $"Select `r`.`Id` {Environment.NewLine}From `GeneratedRows` As `r` {Environment.NewLine}Where `r`.`Id`<=@_p_0",
            "generated constant-left greater-equal SQL");
        Check(constantLeftLessEqual.ToList<Row>().Select(row => row.Id).SequenceEqual(new long[] { 1, 2 }),
            "generated constant-left greater-equal result");
        var constantLeftWhereIfDisabled = query.From<Row>("r")
            .WhereIf<Row>(false, row => 1 < row.Id)
            .Select<Row>(row => row.Id);
        Check(constantLeftWhereIfDisabled.ToSql() == $"Select `r`.`Id` {Environment.NewLine}From `GeneratedRows` As `r`",
            "generated constant-left WhereIf disabled SQL");
        Check(constantLeftWhereIfDisabled.ToList<Row>().Select(row => row.Id).SequenceEqual(new long[] { 1, 2 }),
            "generated constant-left WhereIf disabled result");
        var constantLeftWhereIfEnabled = query.From<Row>("r")
            .WhereIf<Row>(true, row => 2 >= row.Id)
            .Select<Row>(row => row.Id);
        Check(constantLeftWhereIfEnabled.ToSql() == $"Select `r`.`Id` {Environment.NewLine}From `GeneratedRows` As `r` {Environment.NewLine}Where `r`.`Id`<=@_p_0",
            "generated constant-left WhereIf enabled SQL");
        Check(constantLeftWhereIfEnabled.ToList<Row>().Select(row => row.Id).SequenceEqual(new long[] { 1, 2 }),
            "generated constant-left WhereIf enabled result");
        var constantLeftString = query.From<Row>("r")
            .Where<Row>(row => "updated" == row.Name)
            .Select<Row>(row => row.Id);
        Check(constantLeftString.ToSql() == $"Select `r`.`Id` {Environment.NewLine}From `GeneratedRows` As `r` {Environment.NewLine}Where `r`.`Name`=@_p_0" &&
            constantLeftString.ToList<Row>().Single().Id == 1, "generated constant-left string SQL and result");
        var constantLeftNull = query.From<Row>("r")
            .Where<Row>(row => null == row.Name)
            .Select<Row>(row => row.Id);
        Check(constantLeftNull.ToSql() == $"Select `r`.`Id` {Environment.NewLine}From `GeneratedRows` As `r` {Environment.NewLine}Where `r`.`Name` Is Null" &&
            constantLeftNull.ToList<Row>().Count == 0, "generated constant-left null equality SQL and result");
        var constantLeftNotNull = query.From<Row>("r")
            .Where<Row>(row => null != row.Name)
            .Select<Row>(row => row.Id);
        Check(constantLeftNotNull.ToSql() == $"Select `r`.`Id` {Environment.NewLine}From `GeneratedRows` As `r` {Environment.NewLine}Where `r`.`Name` Is Not Null" &&
            constantLeftNotNull.ToList<Row>().Count == 2, "generated constant-left null inequality SQL and result");
        var rowsQuery = query.From<Row>("r")
            .Where<Row, long>(row => row.Id, minimum, Operator.GreaterEqual)
            .Where<Row>(row => row.Version >= 1)
            .Select<Row>(row => row.Name)
            .Select<Row>(row => row.Id);
        Check(rowsQuery.ToSql() == $"Select `r`.`Name`,`r`.`Id` {Environment.NewLine}From `GeneratedRows` As `r` {Environment.NewLine}Where `r`.`Id`>=@_p_0 And `r`.`Version`>=@_p_1",
            "generated constant comparison SQL");
        var rows = await rowsQuery.ToListAsync<Row>();
        Check(rows.Count == 2 && rows[0].Name == "updated" && rows.All(row => row.Version == 0),
            "strict lambda explicit value, constant and omitted members");
        var stringQuery = query.From<Row>("r")
            .Where<Row>(row => row.Name == "updated")
            .Select<Row>(row => row.Id);
        Check(stringQuery.ToSql() == $"Select `r`.`Id` {Environment.NewLine}From `GeneratedRows` As `r` {Environment.NewLine}Where `r`.`Name`=@_p_0",
            "generated string comparison SQL");
        Check(stringQuery.ToList<Row>().Single().Id == 1, "generated string comparison parameter");
        var disabledQuery = query.From<Row>("r")
            .WhereIf<Row>(false, row => row.Id, 999L)
            .WhereIf<Row>(false, row => row.Version == -1)
            .Select<Row>(row => row.Id);
        Check(disabledQuery.ToSql() == $"Select `r`.`Id` {Environment.NewLine}From `GeneratedRows` As `r`",
            "generated disabled WhereIf SQL");
        var disabled = disabledQuery.ToList<Row>();
        Check(disabled.Count == 2, "strict generated WhereIf disabled");
        var enabledQuery = query.From<Row>("r")
            .WhereIf<Row>(true, row => row.Id, 2L)
            .WhereIf<Row>(true, row => row.Version >= 1)
            .Select<Row>(row => row.Id);
        Check(enabledQuery.ToSql() == $"Select `r`.`Id` {Environment.NewLine}From `GeneratedRows` As `r` {Environment.NewLine}Where `r`.`Id`=@_p_0 And `r`.`Version`>=@_p_1",
            "generated enabled WhereIf SQL");
        var enabled = enabledQuery.ToList<Row>();
        Check(enabled.Count == 1 && enabled[0].Id == 2, "strict generated WhereIf enabled");
        var inResult = CollectionFilterProbe.Read(queries, true, new long[] { 1, 2 }, Operator.In);
        Check(inResult.Sql == $"Select `r`.`Id` {Environment.NewLine}From `GeneratedRows` As `r` {Environment.NewLine}Where `r`.`Id` In (@_p_0,@_p_1)" &&
            inResult.Ids.SequenceEqual(new long[] { 1, 2 }), "generated WhereIf collection IN");
        var notInResult = CollectionFilterProbe.Read(queries, true, new long[] { 1 }, Operator.NotIn);
        Check(notInResult.Sql == $"Select `r`.`Id` {Environment.NewLine}From `GeneratedRows` As `r` {Environment.NewLine}Where `r`.`Id` Not In (@_p_0)" &&
            notInResult.Ids.SequenceEqual(new long[] { 2 }), "generated WhereIf collection NOT IN");
        var emptyResult = CollectionFilterProbe.Read(queries, true, Array.Empty<long>(), Operator.In);
        Check(emptyResult.Sql == $"Select `r`.`Id` {Environment.NewLine}From `GeneratedRows` As `r` {Environment.NewLine}Where 1 = 0" &&
            emptyResult.Ids.Length == 0, "generated WhereIf empty collection");
        var disabledCollection = CollectionFilterProbe.Read(queries, false, Array.Empty<long>(), Operator.In);
        Check(disabledCollection.Sql == $"Select `r`.`Id` {Environment.NewLine}From `GeneratedRows` As `r`" &&
            disabledCollection.Ids.SequenceEqual(new long[] { 1, 2 }), "generated WhereIf disabled collection");
        var directCollection = query.From<Row>("r")
            .Where<Row, object>(row => row.Id, new long[] { 2 }, Operator.In)
            .Select<Row>(row => row.Id);
        Check(directCollection.ToSql() == $"Select `r`.`Id` {Environment.NewLine}From `GeneratedRows` As `r` {Environment.NewLine}Where `r`.`Id` In (@_p_0)" &&
            directCollection.ToList<Row>().Single().Id == 2, "generated Where collection IN");
        var explicitCollection = query.From<Row>("r")
            .WhereIf<Row, long[]>(true, new long[] { 1, 2 }, (row, values) => values.Contains(row.Id), "r")
            .Select<Row>(row => row.Id);
        Check(explicitCollection.ToSql() == $"Select `r`.`Id` {Environment.NewLine}From `GeneratedRows` As `r` {Environment.NewLine}" +
            "Where `r`.`Id` In (@_p_0,@_p_1)" &&
            explicitCollection.ToList<Row>().Select(row => row.Id).SequenceEqual(new long[] { 1, 2 }),
            "generated explicit collection predicate");
        var enumerableCollection = query.From<Row>("r")
            .WhereIf<Row, long[]>(true, new long[] { 2 },
                (row, values) => Enumerable.Contains(values, row.Id), "r")
            .Select<Row>(row => row.Id);
        Check(enumerableCollection.ToSql() == $"Select `r`.`Id` {Environment.NewLine}From `GeneratedRows` As `r` {Environment.NewLine}" +
            "Where `r`.`Id` In (@_p_0)" && enumerableCollection.ToList<Row>().Single().Id == 2,
            "generated explicit Enumerable.Contains predicate");
        var listCollection = query.From<Row>("r")
            .WhereIf<Row, List<long>>(true, new List<long> { 1 }, (row, values) => values.Contains(row.Id), "r")
            .Select<Row>(row => row.Id);
        Check(listCollection.ToSql() == $"Select `r`.`Id` {Environment.NewLine}From `GeneratedRows` As `r` {Environment.NewLine}" +
            "Where `r`.`Id` In (@_p_0)" && listCollection.ToList<Row>().Single().Id == 1,
            "generated explicit List.Contains predicate");
        var compoundCollection = query.From<Row>("r")
            .WhereIf<Row, long[]>(true, new long[] { 1 },
                (row, values) => values.Contains(row.Id) || row.Id == 2, "r")
            .Select<Row>(row => row.Id);
        Check(compoundCollection.ToSql() == $"Select `r`.`Id` {Environment.NewLine}From `GeneratedRows` As `r` {Environment.NewLine}" +
            "Where (`r`.`Id` In (@_p_0) Or `r`.`Id`=@_p_1)" &&
            compoundCollection.ToList<Row>().Select(row => row.Id).SequenceEqual(new long[] { 1, 2 }),
            "generated compound explicit collection predicate");
        var negatedCollection = query.From<Row>("r")
            .WhereIf<Row, long[]>(true, new long[] { 1 }, (row, values) => !values.Contains(row.Id), "r")
            .Select<Row>(row => row.Id);
        Check(negatedCollection.ToSql() == $"Select `r`.`Id` {Environment.NewLine}From `GeneratedRows` As `r` {Environment.NewLine}" +
            "Where Not (`r`.`Id` In (@_p_0))" &&
            negatedCollection.ToList<Row>().Select(row => row.Id).SequenceEqual(new long[] { 2 }),
            "generated negated explicit collection predicate");
        var nullableCollection = query.From<Row>("r")
            .WhereIf<Row, string[]>(true, new[] { "updated", null },
                (row, values) => values.Contains(row.Name), "r")
            .Select<Row>(row => row.Id);
        Check(nullableCollection.ToSql() == $"Select `r`.`Id` {Environment.NewLine}From `GeneratedRows` As `r` {Environment.NewLine}" +
            "Where `r`.`Name` In (@_p_0,@_p_1)" &&
            nullableCollection.ToList<Row>().Select(row => row.Id).SequenceEqual(new long[] { 1 }),
            "generated explicit collection with null member");
        var negatedNullableCollection = query.From<Row>("r")
            .WhereIf<Row, string[]>(true, new[] { "updated", null },
                (row, values) => !values.Contains(row.Name), "r")
            .Select<Row>(row => row.Id);
        Check(negatedNullableCollection.ToSql() == $"Select `r`.`Id` {Environment.NewLine}From `GeneratedRows` As `r` {Environment.NewLine}" +
            "Where Not (`r`.`Name` In (@_p_0,@_p_1))" &&
            negatedNullableCollection.ToList<Row>().Count == 0,
            "generated negated explicit collection preserves SQL null semantics");
        var containsQuery = query.From<Row>("r")
            .WhereIf<Row>(true, row => row.Name, "dat", Operator.Contains)
            .Select<Row>(row => row.Id);
        Check(containsQuery.ToSql() == $"Select `r`.`Id` {Environment.NewLine}From `GeneratedRows` As `r` {Environment.NewLine}Where `r`.`Name` Like @_p_0" &&
            containsQuery.ToList<Row>().Single().Id == 1, "generated WhereIf string contains");
        var methodContainsQuery = query.From<Row>("r")
            .Where<Row>(row => row.Name.Contains("dat"), "r")
            .Select<Row>(row => row.Id);
        Check(methodContainsQuery.ToSql() == $"Select `r`.`Id` {Environment.NewLine}From `GeneratedRows` As `r` {Environment.NewLine}Where `r`.`Name` Like @_p_0",
            "generated string predicate Contains SQL");
        Check(methodContainsQuery.ToList<Row>().Select(row => row.Id).SequenceEqual(new long[] { 1 }),
            "generated string predicate Contains result");
        var methodStartsWithQuery = query.From<Row>("r")
            .Where<Row>(row => row.Name.StartsWith("sec"), "r")
            .Select<Row>(row => row.Id);
        Check(methodStartsWithQuery.ToSql() == $"Select `r`.`Id` {Environment.NewLine}From `GeneratedRows` As `r` {Environment.NewLine}Where `r`.`Name` Like @_p_0",
            "generated string predicate StartsWith SQL");
        Check(methodStartsWithQuery.ToList<Row>().Select(row => row.Id).SequenceEqual(new long[] { 2 }),
            "generated string predicate StartsWith result");
        var methodEndsWithQuery = query.From<Row>("r")
            .Where<Row>(row => row.Name.EndsWith("ted"), "r")
            .Select<Row>(row => row.Id);
        Check(methodEndsWithQuery.ToSql() == $"Select `r`.`Id` {Environment.NewLine}From `GeneratedRows` As `r` {Environment.NewLine}Where `r`.`Name` Like @_p_0",
            "generated string predicate EndsWith SQL");
        Check(methodEndsWithQuery.ToList<Row>().Select(row => row.Id).SequenceEqual(new long[] { 1 }),
            "generated string predicate EndsWith result");
        var methodContainsWhereIf = query.From<Row>("r")
            .WhereIf<Row>(true, row => row.Name.Contains("cond"), "r")
            .Select<Row>(row => row.Id);
        Check(methodContainsWhereIf.ToSql() == $"Select `r`.`Id` {Environment.NewLine}From `GeneratedRows` As `r` {Environment.NewLine}Where `r`.`Name` Like @_p_0",
            "generated string predicate WhereIf Contains SQL");
        Check(methodContainsWhereIf.ToList<Row>().Select(row => row.Id).SequenceEqual(new long[] { 2 }),
            "generated string predicate WhereIf Contains result");
        var methodStartsWithWhereIf = query.From<Row>("r")
            .WhereIf<Row>(true, row => row.Name.StartsWith("up"), "r")
            .Select<Row>(row => row.Id);
        Check(methodStartsWithWhereIf.ToSql() == $"Select `r`.`Id` {Environment.NewLine}From `GeneratedRows` As `r` {Environment.NewLine}Where `r`.`Name` Like @_p_0",
            "generated string predicate WhereIf StartsWith SQL");
        Check(methodStartsWithWhereIf.ToList<Row>().Select(row => row.Id).SequenceEqual(new long[] { 1 }),
            "generated string predicate WhereIf StartsWith result");
        var methodEndsWithWhereIf = query.From<Row>("r")
            .WhereIf<Row>(true, row => row.Name.EndsWith("ted"), "r")
            .Select<Row>(row => row.Id);
        Check(methodEndsWithWhereIf.ToSql() == $"Select `r`.`Id` {Environment.NewLine}From `GeneratedRows` As `r` {Environment.NewLine}Where `r`.`Name` Like @_p_0",
            "generated string predicate WhereIf EndsWith SQL");
        Check(methodEndsWithWhereIf.ToList<Row>().Select(row => row.Id).SequenceEqual(new long[] { 1 }),
            "generated string predicate WhereIf EndsWith result");
        var disabledStringWhereIf = query.From<Row>("r")
            .WhereIf<Row>(false, row => row.Name.Contains("x"), "r")
            .WhereIf<Row>(false, row => row.Name.StartsWith("x"), "r")
            .WhereIf<Row>(false, row => row.Name.EndsWith("x"), "r")
            .Select<Row>(row => row.Id);
        Check(disabledStringWhereIf.ToSql() == $"Select `r`.`Id` {Environment.NewLine}From `GeneratedRows` As `r`",
            "generated string predicate WhereIf disabled SQL");
        Check(disabledStringWhereIf.ToList<Row>().Select(row => row.Id).SequenceEqual(new long[] { 1, 2 }),
            "generated string predicate WhereIf disabled result");
        var compoundString = query.From<Row>("r")
            .Where<Row>(row => row.Id >= 2 && (row.Name.Contains("dat") ||
                (row.Name.StartsWith("sec") && row.Name.EndsWith("ond"))), "r")
            .Select<Row>(row => row.Id);
        Check(compoundString.ToSql() == $"Select `r`.`Id` {Environment.NewLine}From `GeneratedRows` As `r` {Environment.NewLine}Where `r`.`Id`>=@_p_0 And (`r`.`Name` Like @_p_1 Or `r`.`Name` Like @_p_2 And `r`.`Name` Like @_p_3)" &&
            compoundString.ToList<Row>().Select(row => row.Id).SequenceEqual(new long[] { 2 }),
            "generated compound string predicate SQL and result");
        var compoundStringJoin = query.From<Row>("a")
            .Join<Row, Row>((left, right) => left.Id == right.Id, "b")
            .Where<Row, Row>((left, right) => left.Name.StartsWith("up") || right.Name.EndsWith("ond"), "a", "b")
            .Select<Row>(row => row.Id, "a");
        Check(compoundStringJoin.ToSql() == $"Select `a`.`Id` {Environment.NewLine}From `GeneratedRows` As `a` {Environment.NewLine}Join `GeneratedRows` As `b` On `a`.`Id`=`b`.`Id` {Environment.NewLine}Where (`a`.`Name` Like @_p_0 Or `b`.`Name` Like @_p_1)" &&
            compoundStringJoin.ToList<Row>().Select(row => row.Id).OrderBy(id => id).SequenceEqual(new long[] { 1, 2 }),
            "generated compound string predicate joined aliases");
        var disabledCompoundString = query.From<Row>("r")
            .WhereIf<Row>(false, row => row.Name.Contains("dat") || row.Id == 2, "r")
            .Select<Row>(row => row.Id);
        Check(disabledCompoundString.ToSql() == $"Select `r`.`Id` {Environment.NewLine}From `GeneratedRows` As `r`" &&
            disabledCompoundString.ToList<Row>().Select(row => row.Id).SequenceEqual(new long[] { 1, 2 }),
            "generated compound string WhereIf disabled");
        var negated = query.From<Row>("r")
            .Where<Row>(row => !(row.Id == 2 || row.Name.StartsWith("sec")), "r")
            .Select<Row>(row => row.Id);
        Check(negated.ToSql() == $"Select `r`.`Id` {Environment.NewLine}From `GeneratedRows` As `r` {Environment.NewLine}Where Not ((`r`.`Id`=@_p_0 Or `r`.`Name` Like @_p_1))" &&
            negated.ToList<Row>().Select(row => row.Id).SequenceEqual(new long[] { 1 }),
            "generated negated predicate SQL and result");
        var negatedNested = query.From<Row>("r")
            .Where<Row>(row => row.Id >= 1 && !(row.Name == null || row.Id > 1), "r")
            .Select<Row>(row => row.Id);
        Check(negatedNested.ToSql() == $"Select `r`.`Id` {Environment.NewLine}From `GeneratedRows` As `r` {Environment.NewLine}Where `r`.`Id`>=@_p_0 And Not ((`r`.`Name` Is Null Or `r`.`Id`>@_p_2))" &&
            negatedNested.ToList<Row>().Select(row => row.Id).SequenceEqual(new long[] { 1 }),
            "generated negated nested predicate SQL and result");
        var disabledNegated = query.From<Row>("r")
            .WhereIf<Row>(false, row => !(row.Id == 2 || row.Name.StartsWith("sec")), "r")
            .Select<Row>(row => row.Id);
        Check(disabledNegated.ToSql() == $"Select `r`.`Id` {Environment.NewLine}From `GeneratedRows` As `r`" &&
            disabledNegated.ToList<Row>().Select(row => row.Id).SequenceEqual(new long[] { 1, 2 }),
            "generated negated WhereIf disabled");
        var aggregate = query.From<Row>("g")
            .Select<Row>(row => new { row.Version, Count = SqlFn.Count(row.Id) })
            .GroupBy<Row>(row => row.Version)
            .Having<Row>(row => SqlFn.Count(row.Id) > 0)
            .OrderBy<Row>(row => row.Version);
        Check(aggregate.ToSql() == $"Select `g`.`Version` As `Version`,Count(`g`.`Id`) As `Count` {Environment.NewLine}" +
            $"From `GeneratedRows` As `g` {Environment.NewLine}Group By `g`.`Version` Having Count(`g`.`Id`)>@_p_0 {Environment.NewLine}Order By `g`.`Version`",
            "generated cached aggregate descriptors SQL");
        var grouped = aggregate.ToList<VersionSummary>();
        Check(grouped.Count == 2 && grouped[0].Version == 1 && grouped[0].Count == 1 &&
            grouped[1].Version == 2 && grouped[1].Count == 1, "generated cached aggregate descriptor result");
        var unregisteredAggregate = query.From<Row>("missing")
            .Select<Row>(row => new { Count = SqlFn.Count(row.Id) });
        Check(unregisteredAggregate.ToSql() == $"Select Count(`missing`.`Id`) As `Count` {Environment.NewLine}From `GeneratedRows` As `missing`",
            "unregistered aggregate result SQL");
        var missingModelReported = false;
        try { unregisteredAggregate.ToList<UnregisteredSummary>(); }
        catch (NotSupportedException error) { missingModelReported = error.Message.Contains(nameof(UnregisteredSummary), StringComparison.Ordinal); }
        Check(missingModelReported, "unregistered aggregate result metadata diagnostic");
    }
    var compatibleServices = new ServiceCollection();
    compatibleServices.AddLogging();
    compatibleServices.ConfigureSqlMetadata(options => options.ExecutionMode = SqlExecutionMode.Compatible);
    compatibleServices.AddSqlGeneratedModels(GeneratedModels.CreateModels());
    compatibleServices.AddSqlDataSource("default", DatabaseType.Sqlite, "Data Source=" + path);
    compatibleServices.AddSqliteProvider();
    using (var compatibleProvider = compatibleServices.BuildServiceProvider())
    using (var compatibleScope = compatibleProvider.CreateScope())
    using (var compatibleRoot = compatibleScope.ServiceProvider.GetRequiredService<ISqlQueryFactory>().Create())
    {
        var compatibleQuery = compatibleRoot.From<Row>("legacy");
        var fallbackDisabled = AppContext.TryGetSwitch("Bing.Data.Sql.ReflectionFallbackEnabled", out var enabled) && !enabled;
        var missingMembersRejected = false;
        try { SqlGeneratedQueryBridge.Prototype(compatibleQuery, typeof(RowSummary), new RowSummary(), null); }
        catch (NotSupportedException) { missingMembersRejected = true; }
        Check(missingMembersRejected == fallbackDisabled &&
            (!fallbackDisabled || compatibleQuery.ToSql() == $"Select `legacy`.`Id`,`legacy`.`Name`,`legacy`.`Version` {Environment.NewLine}From `GeneratedRows` As `legacy`"),
            "host reflection switch controls prototype fallback");
        if (fallbackDisabled)
            SqlGeneratedQueryBridge.Prototype(compatibleQuery, typeof(RowSummary), new RowSummary(), new[] { "Id", "Name" });
        Check(compatibleQuery.ToSql() == $"Select `legacy`.`Id` As `Id`,`legacy`.`Name` As `Name` {Environment.NewLine}From `GeneratedRows` As `legacy`" &&
            compatibleQuery.ToList<RowSummary>().Count == 2, "prototype projection executes with current host switch");

        var entityQuery = compatibleRoot.From<Row>("same");
        SqlGeneratedQueryBridge.Prototype(entityQuery, typeof(Row), new Row(), null);
        Check(entityQuery.ToSql() == $"Select `same`.`Id`,`same`.`Name`,`same`.`Version` {Environment.NewLine}From `GeneratedRows` As `same`" &&
            entityQuery.ToList<Row>().Count == 2, "entity prototype uses registered mapping without members");
    }
    using (var query = queries.Create())
    {
        var active = query.From<BooleanRow>("b").Where<BooleanRow>(row => row.Enabled, "b")
            .Select<BooleanRow>(row => row.Id);
        Check(active.ToSql() == $"Select `b`.`Id` {Environment.NewLine}From `BooleanRows` As `b` {Environment.NewLine}Where `b`.`Enabled`=@_p_0" &&
            active.ToList<BooleanRow>().Single().Id == 1, "generated boolean predicate true");
        var inactive = query.From<BooleanRow>("b").Where<BooleanRow>(row => !(row.Enabled))
            .Select<BooleanRow>(row => row.Id);
        Check(inactive.ToSql() == $"Select `b`.`Id` {Environment.NewLine}From `BooleanRows` As `b` {Environment.NewLine}Where `b`.`Enabled`=@_p_0" &&
            inactive.ToList<BooleanRow>().Single().Id == 2, "generated boolean predicate false");
        var optional = query.From<BooleanRow>("b").WhereIf<BooleanRow>(true, row => row.Enabled)
            .Select<BooleanRow>(row => row.Id);
        Check(optional.ToSql() == $"Select `b`.`Id` {Environment.NewLine}From `BooleanRows` As `b` {Environment.NewLine}Where `b`.`Enabled`=@_p_0" &&
            optional.ToList<BooleanRow>().Single().Id == 1, "generated optional boolean predicate");
        var explicitValue = query.From<BooleanRow>("b").WhereIf<BooleanRow>(true, row => row.Enabled, false)
            .Select<BooleanRow>(row => row.Id);
        Check(explicitValue.ToSql() == $"Select `b`.`Id` {Environment.NewLine}From `BooleanRows` As `b` {Environment.NewLine}Where `b`.`Enabled`=@_p_0" &&
            explicitValue.ToList<BooleanRow>().Single().Id == 2, "generated explicit boolean value");
        var joined = query.From<BooleanRow>("l")
            .Join<BooleanRow, BooleanRow>((left, right) => left.Id == right.Id, "r")
            .Where<BooleanRow, BooleanRow>((left, right) => !right.Enabled, "l", "r")
            .Select<BooleanRow>(row => row.Id, "r");
        Check(joined.ToSql() == $"Select `r`.`Id` {Environment.NewLine}From `BooleanRows` As `l` {Environment.NewLine}" +
            $"Join `BooleanRows` As `r` On `l`.`Id`=`r`.`Id` {Environment.NewLine}Where `r`.`Enabled`=@_p_0" &&
            joined.ToList<BooleanRow>().Single().Id == 2, "generated boolean predicate uses joined alias");
    }
    var booleanRequests = await Task.WhenAll(Enumerable.Range(0, 16).Select(index => Task.Run(() =>
    {
        using var requestScope = provider.CreateScope();
        var factory = requestScope.ServiceProvider.GetRequiredService<ISqlQueryFactory>();
        return BooleanFilterProbe.Read(factory, "b" + index, index % 2 == 0);
    })));
    for (var index = 0; index < booleanRequests.Length; index++)
    {
        var alias = "b" + index;
        var enabled = index % 2 == 0;
        var expectedSql = $"Select `{alias}`.`Id` {Environment.NewLine}From `BooleanRows` As `{alias}`" +
            (enabled ? $" {Environment.NewLine}Where `{alias}`.`Enabled`=@_p_0" : string.Empty);
        Check(booleanRequests[index].Sql == expectedSql &&
            booleanRequests[index].Ids.SequenceEqual(enabled ? new long[] { 2 } : new long[] { 1, 2 }),
            "generated optional boolean isolates request " + index);
    }
    var reversedRequests = await Task.WhenAll(Enumerable.Range(0, 16).Select(index => Task.Run(() =>
    {
        using var requestScope = provider.CreateScope();
        var factory = requestScope.ServiceProvider.GetRequiredService<ISqlQueryFactory>();
        return ReversedConstantFilterProbe.Read(factory, "c" + index, index % 2 == 0);
    })));
    for (var index = 0; index < reversedRequests.Length; index++)
    {
        var alias = "c" + index;
        var enabled = index % 2 == 0;
        var expectedSql = $"Select `{alias}`.`Id` {Environment.NewLine}From `GeneratedRows` As `{alias}`" +
            (enabled ? $" {Environment.NewLine}Where `{alias}`.`Id`>@_p_0" : string.Empty);
        Check(reversedRequests[index].Sql == expectedSql &&
            reversedRequests[index].Ids.SequenceEqual(enabled ? new long[] { 2 } : new long[] { 1, 2 }),
            "generated constant-left predicate isolates request " + index);
    }
    var collectionRequests = await Task.WhenAll(Enumerable.Range(0, 16).Select(index => Task.Run(() =>
    {
        using var requestScope = provider.CreateScope();
        var factory = requestScope.ServiceProvider.GetRequiredService<ISqlQueryFactory>();
        var ids = index % 3 == 0 ? Array.Empty<long>() : new long[] { index % 2 + 1 };
        return CollectionFilterProbe.Read(factory, index % 4 != 0, ids, Operator.In);
    })));
    for (var index = 0; index < collectionRequests.Length; index++)
    {
        var enabled = index % 4 != 0;
        var empty = index % 3 == 0;
        var expectedSql = $"Select `r`.`Id` {Environment.NewLine}From `GeneratedRows` As `r`" +
            (!enabled ? string.Empty : empty ? $" {Environment.NewLine}Where 1 = 0" : $" {Environment.NewLine}Where `r`.`Id` In (@_p_0)");
        var expectedIds = !enabled ? new long[] { 1, 2 } : empty ? Array.Empty<long>() : new long[] { index % 2 + 1 };
        Check(collectionRequests[index].Sql == expectedSql && collectionRequests[index].Ids.SequenceEqual(expectedIds),
            "generated collection isolates request " + index);
    }
    var prototypeRequests = await Task.WhenAll(Enumerable.Range(0, 16).Select(index => Task.Run(() =>
    {
        using var requestScope = provider.CreateScope();
        var factory = requestScope.ServiceProvider.GetRequiredService<ISqlQueryFactory>();
        return PrototypeProjectionProbe.Read(factory, "p" + index, index % 2 + 1);
    })));
    for (var index = 0; index < prototypeRequests.Length; index++)
    {
        var alias = "p" + index;
        var expectedSql = $"Select `{alias}`.`Id` As `Id`,`{alias}`.`Name` As `Name` {Environment.NewLine}" +
            $"From `GeneratedRows` As `{alias}` {Environment.NewLine}Where `{alias}`.`Id`=@_p_0";
        Check(prototypeRequests[index].Sql == expectedSql && prototypeRequests[index].Id == index % 2 + 1 &&
            prototypeRequests[index].Name == (index % 2 == 0 ? "updated" : "second"),
            "generated prototype descriptor isolates request " + index);
    }
    var generatedRequests = await Task.WhenAll(Enumerable.Range(0, 32).Select(index => Task.Run(() =>
    {
        using var requestScope = provider.CreateScope();
        var factory = requestScope.ServiceProvider.GetRequiredService<ISqlQueryFactory>();
        return DescriptorReuseProbe.Read(factory, "request" + index, index % 3 != 0, index % 2 + 1);
    })));
    for (var index = 0; index < generatedRequests.Length; index++)
    {
        var alias = "request" + index;
        var enabled = index % 3 != 0;
        var expectedSql = $"Select `{alias}`.`Id` {Environment.NewLine}From `GeneratedRows` As `{alias}`" +
            (enabled ? $" {Environment.NewLine}Where `{alias}`.`Id`=@_p_0" : string.Empty);
        Check(generatedRequests[index].Sql == expectedSql &&
            generatedRequests[index].Ids.SequenceEqual(enabled ? new long[] { index % 2 + 1 } : new long[] { 1, 2 }),
            "generated descriptors isolate alias and parameter " + index);
    }
    var compoundRequests = await Task.WhenAll(Enumerable.Range(0, 16).Select(index => Task.Run(() =>
    {
        using var requestScope = provider.CreateScope();
        var factory = requestScope.ServiceProvider.GetRequiredService<ISqlQueryFactory>();
        return CompoundPredicateReuseProbe.Read(factory, "compound" + index, index % 3 != 0, index % 2 + 1);
    })));
    for (var index = 0; index < compoundRequests.Length; index++)
    {
        var alias = "compound" + index;
        var enabled = index % 3 != 0;
        var minimum = index % 2 + 1;
        var filter = enabled ? $"`{alias}`.`Version`>=@_p_0 And `{alias}`.`Id`<@_p_1 And " : string.Empty;
        var expected = $"Select `{alias}`.`Id` {Environment.NewLine}From `GeneratedRows` As `{alias}` {Environment.NewLine}Where " +
            filter + $"`{alias}`.`Id`>=@_p_{(enabled ? 2 : 0)}";
        var expectedIds = enabled
            ? minimum == 1 ? new long[] { 1 } : Array.Empty<long>()
            : minimum == 1 ? new long[] { 1, 2 } : new long[] { 2 };
        Check(compoundRequests[index].Sql == expected && compoundRequests[index].Ids.SequenceEqual(expectedIds),
            "generated compound descriptor isolates request " + index);
    }
    var explicitRequests = await Task.WhenAll(Enumerable.Range(0, 16).Select(index => Task.Run(() =>
    {
        using var requestScope = provider.CreateScope();
        var factory = requestScope.ServiceProvider.GetRequiredService<ISqlQueryFactory>();
        return ExplicitPredicateReuseProbe.Read(factory, "explicit" + index, index % 3 != 0, index % 2 + 1);
    })));
    for (var index = 0; index < explicitRequests.Length; index++)
    {
        var alias = "explicit" + index;
        var enabled = index % 3 != 0;
        var id = index % 2 + 1;
        var expectedSql = $"Select `{alias}`.`Id` {Environment.NewLine}From `GeneratedRows` As `{alias}`" +
            (enabled ? $" {Environment.NewLine}Where (`{alias}`.`Id`=@_p_0 Or `{alias}`.`Id`>@_p_1)" : string.Empty);
        var expectedIds = enabled && id == 2 ? new long[] { 2 } : new long[] { 1, 2 };
        Check(explicitRequests[index].Sql == expectedSql && explicitRequests[index].Ids.SequenceEqual(expectedIds),
            "generated explicit compound value isolates request " + index);
    }
    var compoundStringRequests = await Task.WhenAll(Enumerable.Range(0, 16).Select(index => Task.Run(() =>
    {
        using var requestScope = provider.CreateScope();
        var factory = requestScope.ServiceProvider.GetRequiredService<ISqlQueryFactory>();
        return CompoundStringPredicateProbe.Read(factory, "mixed" + index, index % 3 != 0, index % 2 + 1);
    })));
    for (var index = 0; index < compoundStringRequests.Length; index++)
    {
        var alias = "mixed" + index;
        var enabled = index % 3 != 0;
        var minimum = index % 2 + 1;
        var filter = enabled
            ? $"`{alias}`.`Version`>=@_p_0 And (`{alias}`.`Name` Like @_p_1 Or `{alias}`.`Name` Like @_p_2 And `{alias}`.`Name` Like @_p_3) And "
            : string.Empty;
        var expectedSql = $"Select `{alias}`.`Id` {Environment.NewLine}From `GeneratedRows` As `{alias}` {Environment.NewLine}Where " +
            filter + $"`{alias}`.`Id`>=@_p_{(enabled ? 4 : 0)}";
        var expectedIds = enabled
            ? minimum == 1 ? new long[] { 1 } : Array.Empty<long>()
            : minimum == 1 ? new long[] { 1, 2 } : new long[] { 2 };
        Check(compoundStringRequests[index].Sql == expectedSql && compoundStringRequests[index].Ids.SequenceEqual(expectedIds),
            "generated compound string predicate isolates request " + index);
    }
    var negatedRequests = await Task.WhenAll(Enumerable.Range(0, 16).Select(index => Task.Run(() =>
    {
        using var requestScope = provider.CreateScope();
        var factory = requestScope.ServiceProvider.GetRequiredService<ISqlQueryFactory>();
        return NegatedPredicateProbe.Read(factory, "neg" + index, index % 3 != 0, index % 2 + 1);
    })));
    for (var index = 0; index < negatedRequests.Length; index++)
    {
        var alias = "neg" + index;
        var enabled = index % 3 != 0;
        var minimum = index % 2 + 1;
        var filter = enabled ? $"Not ((`{alias}`.`Id`=@_p_0 Or `{alias}`.`Name` Like @_p_1)) And " : string.Empty;
        var expectedSql = $"Select `{alias}`.`Id` {Environment.NewLine}From `GeneratedRows` As `{alias}` {Environment.NewLine}Where " +
            filter + $"`{alias}`.`Id`>=@_p_{(enabled ? 2 : 0)}";
        var expectedIds = enabled
            ? minimum == 1 ? new long[] { 1 } : Array.Empty<long>()
            : minimum == 1 ? new long[] { 1, 2 } : new long[] { 2 };
        Check(negatedRequests[index].Sql == expectedSql && negatedRequests[index].Ids.SequenceEqual(expectedIds),
            "generated negated predicate isolates request " + index);
    }
    var stringPredicateRequests = await Task.WhenAll(Enumerable.Range(0, 32).Select(index => Task.Run(() =>
    {
        using var requestScope = provider.CreateScope();
        var factory = requestScope.ServiceProvider.GetRequiredService<ISqlQueryFactory>();
        return StringPredicateReuseProbe.Read(factory, "string" + index, index % 3 != 0,
            index % 2 == 0 ? "dat" : "cond");
    })));
    for (var index = 0; index < stringPredicateRequests.Length; index++)
    {
        var alias = "string" + index;
        var enabled = index % 3 != 0;
        var expectedSql = $"Select `{alias}`.`Id` {Environment.NewLine}From `GeneratedRows` As `{alias}`" +
            (enabled ? $" {Environment.NewLine}Where `{alias}`.`Name` Like @_p_0 And `{alias}`.`Name` Like @_p_1" : string.Empty);
        var expectedIds = !enabled ? new long[] { 1, 2 } : index % 2 == 0 ? new long[] { 1 } : new long[] { 2 };
        Check(stringPredicateRequests[index].Sql == expectedSql &&
            stringPredicateRequests[index].Ids.SequenceEqual(expectedIds),
            "generated string predicate isolates alias, value and switch " + index);
    }
    var explicitStringRequests = await Task.WhenAll(Enumerable.Range(0, 16).Select(index => Task.Run(() =>
    {
        using var requestScope = provider.CreateScope();
        var factory = requestScope.ServiceProvider.GetRequiredService<ISqlQueryFactory>();
        var value = (index % 3) switch { 0 => "dat", 1 => "sec", _ => "ond" };
        return ExplicitStringPredicateReuseProbe.Read(factory, "explicit-string" + index, index % 4 != 0, value);
    })));
    for (var index = 0; index < explicitStringRequests.Length; index++)
    {
        var alias = "explicit-string" + index;
        var enabled = index % 4 != 0;
        var filter = enabled
            ? $"((`{alias}`.`Name` Like @_p_0 Or `{alias}`.`Name` Like @_p_1) Or `{alias}`.`Name` Like @_p_2)"
            : string.Empty;
        var expectedSql = $"Select `{alias}`.`Id` {Environment.NewLine}From `GeneratedRows` As `{alias}`" +
            (enabled ? $" {Environment.NewLine}Where " + filter : string.Empty);
        var expectedIds = !enabled ? new long[] { 1, 2 } : index % 3 == 0 ? new long[] { 1 } : new long[] { 2 };
        Check(explicitStringRequests[index].Sql == expectedSql &&
            explicitStringRequests[index].Ids.SequenceEqual(expectedIds),
            "generated explicit string predicate isolates alias, value and switch " + index);
    }
    var explicitCollectionRequests = await Task.WhenAll(Enumerable.Range(0, 16).Select(index => Task.Run(() =>
    {
        using var requestScope = provider.CreateScope();
        var factory = requestScope.ServiceProvider.GetRequiredService<ISqlQueryFactory>();
        var ids = (index % 5) switch
        {
            0 => new long[] { 1 },
            1 => new long[] { 2, 3 },
            2 => Array.Empty<long>(),
            3 => null,
            _ => new long[] { 1, 2 }
        };
        return ExplicitCollectionPredicateReuseProbe.Read(factory, "explicit-collection" + index,
            index % 4 != 0, ids);
    })));
    for (var index = 0; index < explicitCollectionRequests.Length; index++)
    {
        var alias = "explicit-collection" + index;
        var enabled = index % 4 != 0;
        var ids = (index % 5) switch
        {
            0 => new long[] { 1 },
            1 => new long[] { 2, 3 },
            2 => Array.Empty<long>(),
            3 => null,
            _ => new long[] { 1, 2 }
        };
        var condition = !enabled ? string.Empty : ids == null || ids.Length == 0 ? "Where 1 = 0" :
            "Where `" + alias + "`.`Id` In (" +
            string.Join(",", Enumerable.Range(0, ids.Length).Select(value => "@_p_" + value)) + ")";
        var expectedSql = $"Select `{alias}`.`Id` {Environment.NewLine}From `GeneratedRows` As `{alias}`" +
            (enabled ? $" {Environment.NewLine}" + condition : string.Empty);
        var expectedIds = !enabled ? new long[] { 1, 2 } : ids == null || ids.Length == 0
            ? Array.Empty<long>() : ids.Where(id => id <= 2).Distinct().OrderBy(id => id).ToArray();
        Check(explicitCollectionRequests[index].Sql == expectedSql &&
            explicitCollectionRequests[index].Ids.SequenceEqual(expectedIds),
            "generated explicit collection predicate isolates alias, shape, value and switch " + index);
    }
    var tupleRequests = await Task.WhenAll(Enumerable.Range(0, 16).Select(index => Task.Run(() =>
    {
        using var requestScope = provider.CreateScope();
        var factory = requestScope.ServiceProvider.GetRequiredService<ISqlQueryFactory>();
        var ids = index % 3 == 0 ? Array.Empty<long>() : index % 2 == 0 ? new long[] { 1, 2 } : new long[] { 2 };
        var term = index % 2 == 0 ? "dat" : "sec";
        return ExplicitTuplePredicateReuseProbe.Read(factory, "tuple" + index, index % 4 != 0, ids, term, 1);
    })));
    for (var index = 0; index < tupleRequests.Length; index++)
    {
        var alias = "tuple" + index;
        var enabled = index % 4 != 0;
        var ids = index % 3 == 0 ? Array.Empty<long>() : index % 2 == 0 ? new long[] { 1, 2 } : new long[] { 2 };
        var filter = ids.Length == 0 ? "1 = 0" : "`" + alias + "`.`Id` In (" +
            string.Join(",", Enumerable.Range(0, ids.Length).Select(value => "@_p_" + value)) + ")";
        var expectedSql = $"Select `{alias}`.`Id` {Environment.NewLine}From `GeneratedRows` As `{alias}`" +
            (enabled ? $" {Environment.NewLine}Where {filter} And `{alias}`.`Name` Like @_p_{ids.Length} And `{alias}`.`Id`>=@_p_{ids.Length + 1}" : string.Empty);
        var expectedIds = !enabled ? new long[] { 1, 2 } : ids.Length == 0 ? Array.Empty<long>() :
            index % 2 == 0 ? new long[] { 1 } : new long[] { 2 };
        Check(tupleRequests[index].Sql == expectedSql && tupleRequests[index].Ids.SequenceEqual(expectedIds),
            "generated tuple predicate isolates values, aliases and condition " + index);
    }
    var numericRequests = await Task.WhenAll(Enumerable.Range(0, 16).Select(index => Task.Run(() =>
    {
        using var requestScope = provider.CreateScope();
        var factory = requestScope.ServiceProvider.GetRequiredService<ISqlQueryFactory>();
        return ExplicitNumericPredicateProbe.Read(factory, "numeric" + index, index % 4 != 0, index % 4);
    })));
    for (var index = 0; index < numericRequests.Length; index++)
    {
        var alias = "numeric" + index;
        var enabled = index % 4 != 0;
        var expectedSql = $"Select `{alias}`.`Id` {Environment.NewLine}From `GeneratedRows` As `{alias}`" +
            (enabled ? $" {Environment.NewLine}Where `{alias}`.`Id`>=@_p_0 And (`{alias}`.`Version`=@_p_1 Or `{alias}`.`Id`=@_p_2)" : string.Empty);
        var expectedIds = !enabled || index % 4 == 1 ? new long[] { 1, 2 } :
            index % 4 == 2 ? new long[] { 2 } : Array.Empty<long>();
        Check(numericRequests[index].Sql == expectedSql && numericRequests[index].Ids.SequenceEqual(expectedIds),
            "generated numeric promotion isolates values, aliases and condition " + index);
    }
    var nullableRequests = await Task.WhenAll(Enumerable.Range(0, 16).Select(index => Task.Run(() =>
    {
        using var requestScope = provider.CreateScope();
        var factory = requestScope.ServiceProvider.GetRequiredService<ISqlQueryFactory>();
        int? value = index % 4 == 0 ? null : index % 4;
        return ExplicitNullableNumericPredicateProbe.Read(factory, "nullable" + index, index % 5 != 0, value);
    })));
    for (var index = 0; index < nullableRequests.Length; index++)
    {
        var alias = "nullable" + index;
        var enabled = index % 5 != 0;
        var isNull = index % 4 == 0;
        var expectedSql = $"Select `{alias}`.`Id` {Environment.NewLine}From `GeneratedRows` As `{alias}`" +
            (!enabled ? string.Empty : isNull
                ? $" {Environment.NewLine}Where (`{alias}`.`Id` Is Null Or `{alias}`.`Version` Is Null)"
                : $" {Environment.NewLine}Where (`{alias}`.`Id`=@_p_0 Or `{alias}`.`Version`=@_p_1)");
        var expectedIds = !enabled || index % 4 is 1 or 2 ? new long[] { 1, 2 } : Array.Empty<long>();
        Check(nullableRequests[index].Sql == expectedSql && nullableRequests[index].Ids.SequenceEqual(expectedIds),
            "generated nullable promotion isolates null, values, aliases and condition " + index);
    }
    var nullableTuple = ExplicitNullableNumericPredicateProbe.ReadTuple(queries, "nullable-tuple", 1, 2);
    Check(nullableTuple.Sql == $"Select `nullable-tuple`.`Id` {Environment.NewLine}From `GeneratedRows` As `nullable-tuple` {Environment.NewLine}" +
        "Where `nullable-tuple`.`Id`=@_p_0 And `nullable-tuple`.`Version`=@_p_1" &&
        nullableTuple.Ids.SequenceEqual(new long[] { 1 }), "generated nullable tuple promotion");
    var nullTuple = ExplicitNullableNumericPredicateProbe.ReadTuple(queries, "null-tuple", null, null);
    Check(nullTuple.Sql == $"Select `null-tuple`.`Id` {Environment.NewLine}From `GeneratedRows` As `null-tuple` {Environment.NewLine}" +
        "Where `null-tuple`.`Id` Is Null And `null-tuple`.`Version` Is Null" &&
        nullTuple.Ids.Length == 0, "generated nullable tuple preserves null");
    var injectedString = ExplicitStringPredicateReuseProbe.Read(queries, "explicit-injected", true, "' OR 1=1 --");
    Check(injectedString.Sql == $"Select `explicit-injected`.`Id` {Environment.NewLine}From `GeneratedRows` As `explicit-injected` {Environment.NewLine}" +
        "Where ((`explicit-injected`.`Name` Like @_p_0 Or `explicit-injected`.`Name` Like @_p_1) Or `explicit-injected`.`Name` Like @_p_2)" &&
        injectedString.Ids.Length == 0, "generated explicit string predicate parameterizes SQL text");
    var alternateServices = new ServiceCollection();
    alternateServices.AddLogging();
    alternateServices.ConfigureSqlMetadata(options =>
    {
        options.ExecutionMode = SqlExecutionMode.GeneratedOnly;
        options.EntityMappings.Add(new EntityMappingOptions { EntityType = typeof(Row), TableName = "AlternateRows" });
    });
    alternateServices.AddSqlGeneratedModels(GeneratedModels.CreateModels());
    alternateServices.AddSqlDataSource("default", DatabaseType.Sqlite, "Data Source=" + path);
    alternateServices.AddSqliteProvider();
    using (var alternateProvider = alternateServices.BuildServiceProvider())
    using (var alternateScope = alternateProvider.CreateScope())
    {
        var alternate = DescriptorReuseProbe.Read(alternateScope.ServiceProvider.GetRequiredService<ISqlQueryFactory>(),
            "other", true, 7);
        var original = DescriptorReuseProbe.Read(queries, "original", true, 1);
        Check(alternate.Sql == $"Select `other`.`Id` {Environment.NewLine}From `AlternateRows` As `other` {Environment.NewLine}Where `other`.`Id`=@_p_0" &&
            alternate.Ids.SequenceEqual(new long[] { 7 }) &&
            original.Sql == $"Select `original`.`Id` {Environment.NewLine}From `GeneratedRows` As `original` {Environment.NewLine}Where `original`.`Id`=@_p_0" &&
            original.Ids.SequenceEqual(new long[] { 1 }), "generated descriptors bind current entity mapping");
        var alternateNumeric = ExplicitNumericPredicateProbe.Read(
            alternateScope.ServiceProvider.GetRequiredService<ISqlQueryFactory>(), "other-numeric", true, 7);
        Check(alternateNumeric.Sql == $"Select `other-numeric`.`Id` {Environment.NewLine}From `AlternateRows` As `other-numeric` {Environment.NewLine}" +
            "Where `other-numeric`.`Id`>=@_p_0 And (`other-numeric`.`Version`=@_p_1 Or `other-numeric`.`Id`=@_p_2)" &&
            alternateNumeric.Ids.SequenceEqual(new long[] { 7 }), "numeric promotion uses current entity mapping");
        var alternateNullable = ExplicitNullableNumericPredicateProbe.Read(
            alternateScope.ServiceProvider.GetRequiredService<ISqlQueryFactory>(), "other-nullable", true, 7);
        Check(alternateNullable.Sql == $"Select `other-nullable`.`Id` {Environment.NewLine}From `AlternateRows` As `other-nullable` {Environment.NewLine}" +
            "Where (`other-nullable`.`Id`=@_p_0 Or `other-nullable`.`Version`=@_p_1)" &&
            alternateNullable.Ids.SequenceEqual(new long[] { 7 }), "nullable promotion uses current entity mapping");
        var alternatePrototype = PrototypeProjectionProbe.Read(
            alternateScope.ServiceProvider.GetRequiredService<ISqlQueryFactory>(), "p", 7);
        Check(alternatePrototype.Sql == $"Select `p`.`Id` As `Id`,`p`.`Name` As `Name` {Environment.NewLine}" +
            $"From `AlternateRows` As `p` {Environment.NewLine}Where `p`.`Id`=@_p_0" &&
            alternatePrototype.Id == 7 && alternatePrototype.Name == "alternate", "generated prototype uses current mapping");
    }
    var token = Guid.Parse("8f0ade65-25a0-44aa-8888-112233445566");
    using (var writer = executors.Create())
        Check(writer.Insert(new ConversionRow { Id = 1, Token = token, Payload = new Payload { Label = "json" } }) == 1,
            "static type handler and json insert");
    using (var query = queries.Create())
    {
        var converted = query.Sql("SELECT Payload, Token, Id FROM ConversionRows").Single<ConversionRow>();
        Check(converted.Token == token && converted.Payload.Label == "json", "static type handler and json reader");
        var convertedCondition = query.From<ConversionRow>("c")
            .WhereIf<ConversionRow>(true, row => row.Payload, new Payload { Label = "json" })
            .Select<ConversionRow>(row => row.Id);
        Check(convertedCondition.ToSql() == $"Select `c`.`Id` {Environment.NewLine}From `ConversionRows` As `c` {Environment.NewLine}Where `c`.`Payload`=@_p_0" &&
            convertedCondition.ToList<ConversionRow>().Single().Id == 1, "generated condition uses mapped value converter");
        var compoundConverted = query.From<ConversionRow>("c")
            .Where<ConversionRow, Payload>(new Payload { Label = "json" },
                (row, value) => row.Payload == value || row.Payload == value, "c")
            .Select<ConversionRow>(row => row.Id);
        Check(compoundConverted.ToSql() == $"Select `c`.`Id` {Environment.NewLine}From `ConversionRows` As `c` {Environment.NewLine}Where (`c`.`Payload`=@_p_0 Or `c`.`Payload`=@_p_1)" &&
            compoundConverted.ToList<ConversionRow>().Single().Id == 1, "generated explicit compound value uses mapped converter");
        Check(query.Sql("SELECT Token FROM ConversionRows").Scalar<Guid>() == token, "static scalar type handler");
        Check(query.Sql("SELECT Id FROM ConversionRows WHERE Id=@Id", new Dictionary<string, object> { ["Id"] = 1 }).Scalar<int>() == 1,
            "static dictionary parameters");
    }
    using (var transaction = transactions.Begin())
    {
        using var writer = transaction.CreateExecutor();
        using var query = transaction.CreateQuery();
        Check(writer.Insert(new Row { Id = 3, Name = "shared", Version = 1 }) == 1, "shared write");
        Check(query.Sql("SELECT COUNT(*) FROM GeneratedRows").Scalar<int>() == 3, "shared read sees write");
        transaction.Rollback();
    }
    await using (var transaction = await transactions.BeginAsync())
    {
        using var writer = transaction.CreateExecutor();
        using var query = transaction.CreateQuery();
        Check(await writer.InsertAsync(new Row { Id = 4, Name = "committed", Version = 1 }) == 1, "shared async write");
        Check(await query.Sql("SELECT COUNT(*) FROM GeneratedRows").ScalarAsync<int>() == 3, "shared async read");
        await transaction.CommitAsync();
    }
    using (var writer = executors.Create())
    {
        var command = writer.CreateUpdateBuilder().Update(new SqlTableReference { TableName = "GeneratedRows" }).Set("Name", "returned")
            .Where("Id", 1).Returning("Name", "Id").BuildCommand();
        Console.WriteLine(command.Sql);
        Check((await writer.ExecuteReturningAsync<Row>(command)).Single().Name == "returned", "returning");
        var identity = new IdentityRow { Name = "identity" };
        var keys = await ((ISqlKeyInsertExecutor)writer).InsertWithKeysAsync<IdentityRow, long>(new[] { new InsertRow<IdentityRow>("one", identity) });
        Check(identity.Id > 0 && keys.Keys["one"] == identity.Id, "generated key setter");
        Check(await writer.DeleteAsync(new Row { Id = 2, Version = 1 }) == 1, "delete async");
    }
    using (var query = queries.Create())
    {
        Check(query.Sql("SELECT COUNT(*) FROM GeneratedRows").Scalar<int>() == 2, "rollback and delete persisted state");
        try { query.Sql("SELECT 1 AS Id").ToList<Unknown>(); throw new Exception("unknown model accepted"); }
        catch (NotSupportedException) { Console.WriteLine("PASS unknown model rejected"); }
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        try { await query.Sql("SELECT Id FROM GeneratedRows").ToListAsync<Row>(cancellationToken: cancellation.Token); throw new Exception("cancellation ignored"); }
        catch (OperationCanceledException) { Console.WriteLine("PASS cancellation"); }
    }
    using (var writer = executors.Create())
    {
        var rows = new[]
        {
            new Row { Id = 11, Name = "batch-one", Version = 1 },
            new Row { Id = 12, Name = "batch-two", Version = 1 }
        };
        Check(writer.InsertBatch(rows) == 2, "generated batch insert");
        foreach (var row in rows) row.Name = "batch-updated";
        Check(await writer.UpdateBatchAsync(rows) == 2, "generated batch update async");
        using var query = queries.Create();
        Check(query.Sql("SELECT COUNT(*) FROM GeneratedRows WHERE Id>=11 AND Name='batch-updated'").Scalar<int>() == 2,
            "generated batch persisted values");
        Check(await writer.DeleteBatchAsync(rows) == 2, "generated batch delete async");
    }
    foreach (var useTransaction in new[] { true, false })
    {
        using (var writer = executors.Create())
        {
            var duplicateRows = new[]
            {
                new Row { Id = 21, Name = "first-window", Version = 1 },
                new Row { Id = 21, Name = "duplicate-window", Version = 1 }
            };
            try
            {
                await writer.InsertBatchAsync(duplicateRows, new SqlBatchInsertOptions
                {
                    Strategy = SqlBatchInsertStrategy.PerEntity, BatchSize = 1, UseTransaction = useTransaction
                });
                throw new InvalidOperationException("duplicate batch was accepted");
            }
            catch (SqliteException) { }
        }
        using var query = queries.Create();
        Check(query.Sql("SELECT COUNT(*) FROM GeneratedRows WHERE Id=21").Scalar<int>() == (useTransaction ? 0 : 1),
            useTransaction ? "generated batch owned transaction rollback" : "generated batch without transaction partial success");
        using var cleanup = executors.Create();
        cleanup.ExecuteSql("DELETE FROM GeneratedRows WHERE Id=21");
    }
    await using (var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkScopeManager>().Begin())
    {
        using var writer = executors.Create();
        using var query = queries.Create();
        Check(await writer.InsertAsync(new Row { Id = 8, Name = "uow", Version = 1 }) == 1, "uow write");
        Check(query.Sql("SELECT COUNT(*) FROM GeneratedRows").Scalar<int>() == 3, "uow shared read");
        await uow.RollbackAsync();
    }
    using (var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkScopeManager>()
        .Begin(new UnitOfWorkScopeOptions { IsTransactional = false }))
    {
        using var query = queries.Create();
        Check(query.Sql("SELECT COUNT(*) FROM GeneratedRows").Scalar<int>() == 2, "nontransactional uow read");
        uow.Complete();
    }
    Check(diagnostics.Before > 0 && diagnostics.After > 0, "typed diagnostic payload");
    Check(trace.Enabled ? trace.Count > 0 : trace.Count == 0, "trace " + (trace.Enabled ? "on" : "off"));
    Console.WriteLine("AUTOMATIC_QUERY_TEMPLATE_NATIVE_PASS");
    Console.WriteLine("GENERATED_SQL_SMOKE_PASS");
}
finally { SqliteConnection.ClearAllPools(); if (File.Exists(path)) File.Delete(path); }

static void Check(bool value, string name)
{
    if (!value) throw new InvalidOperationException(name);
    Console.WriteLine("PASS " + name);
}

internal static class AutomaticQueryTemplateProbe
{
    internal static string ToSql(ISqlQueryFactory factory, string alias, long minimumId,
        bool filterVersion, int minimumVersion, bool filterIds, long[] ids)
    {
        using var root = factory.Create();
        return root.From<Row>(alias)
            .Where<Row, long>(row => row.Id, minimumId, Operator.GreaterEqual)
            .WhereIf<Row>(filterVersion, row => row.Version, minimumVersion, Operator.GreaterEqual)
            .WhereIf<Row>(filterIds, row => row.Id, ids, alias, Operator.In)
            .Select<Row>(row => row.Id)
            .OrderBy<Row>(row => row.Id)
            .ToSql();
    }

    internal static List<Row> ToList(ISqlQueryFactory factory, string alias, long minimumId,
        bool filterVersion, int minimumVersion, bool filterIds, long[] ids)
    {
        using var root = factory.Create();
        return root.From<Row>(alias)
            .Where<Row, long>(row => row.Id, minimumId, Operator.GreaterEqual)
            .WhereIf<Row>(filterVersion, row => row.Version, minimumVersion, Operator.GreaterEqual)
            .WhereIf<Row>(filterIds, row => row.Id, ids, alias, Operator.In)
            .Select<Row>(row => row.Id)
            .OrderBy<Row>(row => row.Id)
            .ToList<Row>();
    }

    internal static async Task<List<Row>> ToListAsync(ISqlQueryFactory factory, string alias, long minimumId,
        bool filterVersion, int minimumVersion, bool filterIds, long[] ids)
    {
        using var root = factory.Create();
        return await root.From<Row>(alias)
            .Where<Row, long>(row => row.Id, minimumId, Operator.GreaterEqual)
            .WhereIf<Row>(filterVersion, row => row.Version, minimumVersion, Operator.GreaterEqual)
            .WhereIf<Row>(filterIds, row => row.Id, ids, alias, Operator.In)
            .Select<Row>(row => row.Id)
            .OrderBy<Row>(row => row.Id)
            .ToListAsync<Row>();
    }
}

[SqlModel(typeof(Row))]
[SqlModel(typeof(BooleanRow))]
[SqlModel(typeof(IdentityRow))]
[SqlParameters(typeof(Filter))]
[SqlParameters(typeof(BindingFilter))]
[SqlParameters(typeof(InheritedBindingFilter))]
[SqlModel(typeof(ConversionRow))]
[SqlResult(typeof(ComputedResult))]
[SqlResult(typeof(VersionSummary))]
[SqlResult(typeof(RowSummary))]
[SqlResult(typeof(IntegerResult))]
[SqlModel(typeof(MutationBaseRow))]
[SqlModel(typeof(ClientKeyBaseRow))]
[SqlModel(typeof(IdentityKeyBaseRow))]
internal static partial class GeneratedModels { }

public enum TinyState : byte { One = 1 }
public sealed class ComputedResult
{
    public int Id { get; set; } = 17;
    public int Computed => Id * 2;
}

[Table("GeneratedRows")]
public sealed class Row
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None)] public long Id { get; set; }
    public string Name { get; set; } = "initial";
    [ConcurrencyCheck] public int Version { get; set; }
}

[Table("BooleanRows")]
public sealed class BooleanRow
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None)] public long Id { get; set; }
    public bool Enabled { get; set; }
}

[Table("IdentityRows")]
public sealed class IdentityRow
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)] public long Id { get; set; }
    public string Name { get; set; }
}
public sealed class Filter { public long Minimum { get; set; } }
public sealed class BindingFilter { public int Value { get; set; } public int? Offset { get; set; } }
public class BindingFilterBase { public int Value { get; set; } }
public sealed class InheritedBindingFilter : BindingFilterBase { public int Other { get; set; } }
public sealed class Unknown { public int Id { get; set; } }
public sealed class VersionSummary { public int Version { get; set; } public long Count { get; set; } }
public sealed class RowSummary { public long Id { get; set; } public string Name { get; set; } }

public sealed class UnregisteredSummary { public long Count { get; set; } }

internal static class BooleanFilterProbe
{
    internal static (string Sql, long[] Ids) Read(ISqlQueryFactory factory, string alias, bool enabled)
    {
        using var root = factory.Create();
        var query = root.From<BooleanRow>(alias).WhereIf<BooleanRow>(enabled, row => !row.Enabled, alias)
            .Select<BooleanRow>(row => row.Id);
        return (query.ToSql(), query.ToList<BooleanRow>().Select(row => row.Id).OrderBy(value => value).ToArray());
    }
}

internal static class ReversedConstantFilterProbe
{
    internal static (string Sql, long[] Ids) Read(ISqlQueryFactory factory, string alias, bool enabled)
    {
        using var root = factory.Create();
        var query = root.From<Row>(alias).WhereIf<Row>(enabled, row => 1 < row.Id, alias)
            .Select<Row>(row => row.Id, alias);
        return (query.ToSql(), query.ToList<Row>().Select(row => row.Id).OrderBy(value => value).ToArray());
    }
}

internal static class DescriptorReuseProbe
{
    internal static (string Sql, long[] Ids) Read(ISqlQueryFactory factory, string alias, bool enabled, long id)
    {
        using var root = factory.Create();
        var query = root.From<Row>(alias)
            .WhereIf<Row>(enabled, row => row.Id, id, alias)
            .Select<Row>(row => row.Id, alias);
        return (query.ToSql(), query.ToList<Row>().Select(row => row.Id).OrderBy(value => value).ToArray());
    }
}

internal static class StringPredicateReuseProbe
{
    internal static (string Sql, long[] Ids) Read(ISqlQueryFactory factory, string alias, bool enabled, string value)
    {
        using var root = factory.Create();
        var query = root.From<Row>(alias)
            .WhereIf<Row>(enabled, row => row.Name.Contains("d"), alias)
            .WhereIf<Row>(enabled, row => row.Name, value, alias, Operator.Contains)
            .Select<Row>(row => row.Id, alias);
        return (query.ToSql(), query.ToList<Row>().Select(row => row.Id).OrderBy(id => id).ToArray());
    }
}

internal static class CompoundPredicateReuseProbe
{
    internal static (string Sql, long[] Ids) Read(ISqlQueryFactory factory, string alias, bool enabled, long minimum)
    {
        using var root = factory.Create();
        var query = root.From<Row>(alias)
            .WhereIf<Row>(enabled, row => row.Version >= 2 && row.Id < 2, alias)
            .Where<Row, long>(row => row.Id, minimum, alias, Operator.GreaterEqual)
            .Select<Row>(row => row.Id, alias);
        return (query.ToSql(), query.ToList<Row>().Select(row => row.Id).OrderBy(id => id).ToArray());
    }
}

internal static class ExplicitPredicateReuseProbe
{
    internal static (string Sql, long[] Ids) Read(ISqlQueryFactory factory, string alias, bool enabled, int id)
    {
        using var root = factory.Create();
        var query = root.From<Row>(alias)
            .WhereIf<Row, int>(enabled, id, (row, value) => row.Id == value || row.Id > value, alias)
            .Select<Row>(row => row.Id, alias);
        return (query.ToSql(), query.ToList<Row>().Select(row => row.Id).OrderBy(value => value).ToArray());
    }
}

internal static class ExplicitStringPredicateReuseProbe
{
    internal static (string Sql, long[] Ids) Read(ISqlQueryFactory factory, string alias, bool enabled, string value)
    {
        using var root = factory.Create();
        var query = root.From<Row>(alias)
            .WhereIf<Row, string>(enabled, value, (row, term) =>
                row.Name.Contains(term) || row.Name.StartsWith(term) || row.Name.EndsWith(term), alias)
            .Select<Row>(row => row.Id, alias);
        return (query.ToSql(), query.ToList<Row>().Select(row => row.Id).OrderBy(id => id).ToArray());
    }
}

internal static class ExplicitCollectionPredicateReuseProbe
{
    internal static (string Sql, long[] Ids) Read(ISqlQueryFactory factory, string alias, bool enabled, long[] ids)
    {
        using var root = factory.Create();
        var query = root.From<Row>(alias)
            .WhereIf<Row, long[]>(enabled, ids, (row, values) => values.Contains(row.Id), alias)
            .Select<Row>(row => row.Id, alias);
        return (query.ToSql(), query.ToList<Row>().Select(row => row.Id).OrderBy(id => id).ToArray());
    }
}

internal static class ExplicitTuplePredicateReuseProbe
{
    internal static (string Sql, long[] Ids) Read(ISqlQueryFactory factory, string alias, bool enabled,
        long[] ids, string term, int minimum)
    {
        using var root = factory.Create();
        var query = root.From<Row>(alias)
            .WhereIf<Row, (long[] ids, string term, int minimum)>(enabled, (ids, term, minimum),
                (row, value) => value.ids.Contains(row.Id) && row.Name.Contains(value.term) && row.Id >= value.minimum, alias)
            .Select<Row>(row => row.Id, alias);
        return (query.ToSql(), query.ToList<Row>().Select(row => row.Id).OrderBy(id => id).ToArray());
    }
}

internal static class ExplicitNumericPredicateProbe
{
    internal static (string Sql, long[] Ids) Read(ISqlQueryFactory factory, string alias, bool enabled, int minimum)
    {
        using var root = factory.Create();
        var query = root.From<Row>(alias)
            .WhereIf<Row, int>(enabled, minimum, (row, value) =>
                row.Id >= value && (row.Version == value || row.Id == value), alias)
            .Select<Row>(row => row.Id, alias);
        return (query.ToSql(), query.ToList<Row>().Select(row => row.Id).OrderBy(id => id).ToArray());
    }
}

/// <summary>同一生成调用点按请求绑定可空值及当前实体映射。</summary>
internal static class ExplicitNullableNumericPredicateProbe
{
    internal static (string Sql, long[] Ids) Read(ISqlQueryFactory factory, string alias, bool enabled, int? input)
    {
        using var root = factory.Create();
        var query = root.From<Row>(alias)
            .WhereIf<Row, int?>(enabled, input, (row, value) => row.Id == value || row.Version == value, alias)
            .Select<Row>(row => row.Id, alias);
        return (query.ToSql(), query.ToList<Row>().Select(row => row.Id).OrderBy(id => id).ToArray());
    }

    internal static (string Sql, long[] Ids) ReadTuple(ISqlQueryFactory factory, string alias, int? id, short? version)
    {
        using var root = factory.Create();
        var query = root.From<Row>(alias)
            .Where<Row, (int? id, short? version)>((id, version),
                (row, value) => row.Id == value.id && row.Version == value.version, alias)
            .Select<Row>(row => row.Id, alias);
        return (query.ToSql(), query.ToList<Row>().Select(row => row.Id).OrderBy(id => id).ToArray());
    }
}

internal static class CompoundStringPredicateProbe
{
    internal static (string Sql, long[] Ids) Read(ISqlQueryFactory factory, string alias, bool enabled, long minimum)
    {
        using var root = factory.Create();
        var query = root.From<Row>(alias)
            .WhereIf<Row>(enabled, row => row.Version >= 2 && (row.Name.Contains("dat") ||
                (row.Name.StartsWith("sec") && row.Name.EndsWith("ond"))), alias)
            .Where<Row, long>(row => row.Id, minimum, alias, Operator.GreaterEqual)
            .Select<Row>(row => row.Id, alias);
        return (query.ToSql(), query.ToList<Row>().Select(row => row.Id).OrderBy(id => id).ToArray());
    }
}

internal static class NegatedPredicateProbe
{
    internal static (string Sql, long[] Ids) Read(ISqlQueryFactory factory, string alias, bool enabled, long minimum)
    {
        using var root = factory.Create();
        var query = root.From<Row>(alias)
            .WhereIf<Row>(enabled, row => !(row.Id == 2 || row.Name.StartsWith("sec")), alias)
            .Where<Row, long>(row => row.Id, minimum, alias, Operator.GreaterEqual)
            .Select<Row>(row => row.Id, alias);
        return (query.ToSql(), query.ToList<Row>().Select(row => row.Id).OrderBy(id => id).ToArray());
    }
}

internal static class CollectionFilterProbe
{
    internal static (string Sql, long[] Ids) Read(ISqlQueryFactory factory, bool enabled, long[] ids, Operator comparison)
    {
        using var root = factory.Create();
        var query = root.From<Row>("r")
            .WhereIf<Row>(enabled, row => row.Id, ids, comparison)
            .Select<Row>(row => row.Id);
        return (query.ToSql(), query.ToList<Row>().Select(row => row.Id).OrderBy(value => value).ToArray());
    }
}

internal static class PrototypeProjectionProbe
{
    internal static (string Sql, long Id, string Name) Read(ISqlQueryFactory factory, string alias, long id)
    {
        using var root = factory.Create();
        var query = root.From<Row>(alias).Where<Row, long>(row => row.Id, id, alias);
        var result = query.ToList(new RowSummary()).Single();
        return (query.ToSql(), result.Id, result.Name);
    }
}

[Table("ConversionRows")]
public sealed class ConversionRow
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None)] public long Id { get; set; }
    public Guid Token { get; set; }
    public Payload Payload { get; set; }
}
public sealed class Payload { public string Label { get; set; } }

[JsonSerializable(typeof(Payload))]
internal partial class PayloadJsonContext : JsonSerializerContext { }

internal sealed class PayloadConverter : IFieldValueConverter
{
    public bool CanConvert(EntityColumn column, DatabaseContext context) => column?.ClrType == typeof(Payload);
    public object ConvertToProvider(object value, EntityColumn column, DatabaseContext context) =>
        value == null ? null : JsonSerializer.Serialize((Payload)value, PayloadJsonContext.Default.Payload);
    public object ConvertFromProvider(object value, EntityColumn column, DatabaseContext context) =>
        value == null ? null : JsonSerializer.Deserialize((string)value, PayloadJsonContext.Default.Payload);
}

internal sealed class SmokeLogger(bool enabled) : ILoggerProvider, ILogger
{
    internal bool Enabled => enabled;
    internal int Count { get; private set; }
    public ILogger CreateLogger(string categoryName) => this;
    public bool IsEnabled(LogLevel level) => enabled;
    public IDisposable BeginScope<TState>(TState state) where TState : notnull => new EmptyScope();
    public void Log<TState>(LogLevel level, EventId id, TState state, Exception exception, Func<TState, Exception, string> formatter)
    { if (enabled) { _ = formatter(state, exception); Count++; } }
    public void Dispose() { }
    private sealed class EmptyScope : IDisposable { public void Dispose() { } }
}

internal sealed class SmokeDiagnostics : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object>>, IDisposable
{
    private readonly IDisposable _all;
    private readonly List<IDisposable> _subscriptions = new();
    internal int Before { get; private set; }
    internal int After { get; private set; }
    public SmokeDiagnostics() => _all = DiagnosticListener.AllListeners.Subscribe(this);
    public void OnNext(DiagnosticListener value)
    { if (value.Name == SqlQueryDiagnosticListenerNames.DiagnosticListenerName) _subscriptions.Add(value.Subscribe(this)); }
    public void OnNext(KeyValuePair<string, object> value)
    {
        if (value.Value is not DiagnosticsMessage message) throw new Exception("unexpected payload");
        if (message.Operation == SqlQueryDiagnosticListenerNames.BeforeExecute) Before++;
        if (message.Operation == SqlQueryDiagnosticListenerNames.AfterExecute) After++;
    }
    public void OnError(Exception error) => throw error;
    public void OnCompleted() { }
    public void Dispose() { foreach (var item in _subscriptions) item.Dispose(); _all.Dispose(); }
}
