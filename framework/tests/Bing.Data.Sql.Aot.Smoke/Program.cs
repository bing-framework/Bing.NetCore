using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Diagnostics;
using Bing.Data.Sql.Metadata;
using System.Text;
using Bing;
using Bing.Data;
using Bing.Dapper;
using Bing.Dapper.Sqlite;
using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Generated;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

SQLitePCL.Batteries_V2.Init();
Check(SQLitePCL.raw.sqlite3_libversion_number() >= 3_050_002, "SQLite 3.50.2 or newer");
Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = Encoding.UTF8;
var path = Path.Combine(Path.GetTempPath(), "bing-sql-aot-" + Guid.NewGuid().ToString("N") + ".db");
var connectionString = "Data Source=" + path;
using var evidence = new SmokeEvidence();
using var listener = DiagnosticListener.AllListeners.Subscribe(evidence);
try
{
    using (var connection = new SqliteConnection(connectionString))
    {
        connection.Open();
        using var seed = connection.CreateCommand();
        seed.CommandText = "CREATE TABLE Orders(OrderId INTEGER PRIMARY KEY, ProductId INTEGER, Quantity INTEGER NULL);" +
            "CREATE TABLE Products(ProductNo INTEGER PRIMARY KEY, Name TEXT, Code TEXT, Status INTEGER);" +
            "INSERT INTO Products VALUES(101,'A','secret',1),(102,'B','other',2);" +
            "INSERT INTO Orders VALUES(1,101,5),(2,101,7),(3,102,NULL),(4,999,3);" +
            "CREATE TABLE Binaries(BinaryId INTEGER PRIMARY KEY, ParentId INTEGER, Name TEXT, Payload BLOB);" +
            "INSERT INTO Binaries VALUES(1,2,'first',X'010203'),(2,3,'second',NULL),(3,3,'third',X'04');" +
            "CREATE TABLE FloatingRows(Id INTEGER PRIMARY KEY, GroupId INTEGER, Value REAL NULL, RequiredValue REAL NOT NULL);" +
            "INSERT INTO FloatingRows VALUES(1,1,7,7),(2,1,NULL,0),(3,2,5,5);";
        seed.ExecuteNonQuery();
    }
    var services = new ServiceCollection();
    var queryOptions = new SqlOptions<SqliteSqlQuery>();
    services.AddSingleton(queryOptions);
    services.AddSqlCore();
    services.AddSqlGeneratedModels(LegacySmokeModels.CreateModels());
    services.AddSqlDataSource("default", DatabaseType.Sqlite, connectionString);
    services.AddSqliteProvider();
    using var provider = services.BuildServiceProvider();
    using var scope = provider.CreateScope();
    ISqlQuery Query() => scope.ServiceProvider.GetRequiredService<ISqlQueryFactory>().Create();

    // 动态过滤的局部渲染工作区同时冻结 SQL、参数及 Count/Page，直接复用已生成的实体元数据。
    using (var query = Query())
    {
        var filter = new SmokeProductFilter();
        var builderServices = new SqlBuilderServices(
            entityMappingResolver: scope.ServiceProvider.GetRequiredService<IEntityMappingResolver>(),
            metadataOptions: new SqlMetadataOptions { ExecutionMode = SqlExecutionMode.GeneratedOnly },
            entityModelMetadataProvider: scope.ServiceProvider.GetRequiredService<IEntityModelMetadataProvider>(),
            filters: new ISqlFilter[] { filter });
        var builder = new SqliteBuilder(builderServices);
        builder.Select("OrderId").From<Order>("o").OrderBy("OrderId");
        var plan = SqlQueryPlan.Create(builder);
        var sql = $"Select `OrderId` {Environment.NewLine}From `Orders` As `o` {Environment.NewLine}Where `o`.`ProductId`=@_p_0 {Environment.NewLine}Order By `OrderId`";
        Check(plan.CommandText == sql && filter.Calls == 1, "过滤工作区首次完整SQL与单次取值");
        var count = SqlBuilderRuntimeBridge.CreateCountPlan(plan);
        var page = SqlBuilderRuntimeBridge.CreatePagePlan(plan, new Pager(2, 1));
        Check(count.CommandText == $"Select Count(*) {Environment.NewLine}From `Orders` As `o` {Environment.NewLine}Where `o`.`ProductId`=@_p_0" &&
              page.CommandText == sql + $" {Environment.NewLine}Limit @_p_2 OFFSET @_p_1", "过滤工作区Count与Page完整SQL");
        var executor = (ISqlQueryPlanExecutor)query;
        Check(executor.Scalar<long>(count, null) == 2 &&
              executor.ToList<long>(page, null).SequenceEqual(new long[] { 2 }) && filter.Calls == 1,
            "过滤工作区Count与Page共享参数");
        Check(executor.ToList<long>(SqlQueryPlan.Create(builder), null).SequenceEqual(new long[] { 3 }) && filter.Calls == 2 &&
              builder.GetParams().Count == 0, "过滤工作区重复终结重新取值且不污染草稿");
        Console.WriteLine("FILTER_RENDER_WORKSPACE_NATIVE_PASS");
    }

    // 默认静态查询重复终结复用冻结结构，每次执行和分页仍保持独立。
    using (var query = Query())
    {
        var description = query.Query().Select("OrderId").From("Orders").Where("ProductId", 101).OrderBy("OrderId");
        var sql = $"Select `OrderId` {Environment.NewLine}From `Orders` {Environment.NewLine}Where `ProductId`=@_p_0 {Environment.NewLine}Order By `OrderId`";
        Check(description.ToSql() == sql, "重复终结预览完整SQL");
        Check(description.ToList<long>().SequenceEqual(new long[] { 1, 2 }), "重复终结同步读取");
        Check((await description.ToListAsync<long>()).SequenceEqual(new long[] { 1, 2 }), "重复终结异步读取");
        var page = description.ToPage<long>(new Pager(2, 1));
        var asyncPage = await description.ToPageAsync<long>(new Pager(1, 1));
        Check(page.TotalCount == 2 && asyncPage.TotalCount == 2 && page.Data.SequenceEqual(new long[] { 2 }) &&
            asyncPage.Data.SequenceEqual(new long[] { 1 }), "重复终结同步异步Count与数据页");
        Check(description.ToSql() == sql, "重复终结后完整SQL保持不变");
    }

    // 终结后预览与 Clone 使用同一隔离结构，生成式读取继续使用该结构。
    using (var query = Query())
    {
        var description = query.From<Order>("o").Select<Order>(o => o.Id).OrderBy<Order>(o => o.Id);
        var sql = $"Select `o`.`OrderId` {Environment.NewLine}From `Orders` As `o` {Environment.NewLine}Order By `o`.`OrderId`";
        Check(description.ToList<long>().SequenceEqual(new long[] { 1, 2, 3, 4 }), "冻结生命周期首次读取");
        Check(description.ToSql() == sql, "冻结后预览使用冻结结构");
        var clone = description.Clone().Where<Order, long>(o => o.Id, 3L);
        Check(clone.ToSql() == $"Select `o`.`OrderId` {Environment.NewLine}From `Orders` As `o` {Environment.NewLine}Where `o`.`OrderId`=@_p_0 {Environment.NewLine}Order By `o`.`OrderId`",
            "冻结后克隆可独立追加条件完整SQL");
        Check(clone.ToList<long>().SequenceEqual(new long[] { 3 }), "冻结后克隆使用原结构");
        Check((await description.ToListAsync<long>()).SequenceEqual(new long[] { 1, 2, 3, 4 }) && description.ToSql() == sql,
            "冻结后源查询及生成式重复读取不受克隆影响");
    }

    // 公开计划入口也在创建时冻结，调用方修改 Builder 或公开参数不能影响后续读取与派生。
    using (var query = Query())
    {
        var executor = (ISqlQueryPlanExecutor)query;
        var builder = new SqliteBuilder().Select("OrderId").From("Orders")
            .Where("ProductId", 101).OrderBy("OrderId");
        var source = SqlQueryPlan.Create(builder);
        var sql = $"Select `OrderId` {Environment.NewLine}From `Orders` {Environment.NewLine}Where `ProductId`=@_p_0 {Environment.NewLine}Order By `OrderId`";
        Check(!source.IsBuilderPlan && ReferenceEquals(source, source.Freeze()) && source.CommandText == sql,
            "公开工厂创建时冻结完整SQL");
        builder.Where("OrderId", 4);
        ((SqlParam[])source.Parameters)[0].Value = 102;
        var count = SqlBuilderRuntimeBridge.CreateCountPlan(source);
        var page = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(2, 1, "OrderId"));
        Check(count.CommandText == $"Select Count(*) {Environment.NewLine}From `Orders` {Environment.NewLine}Where `ProductId`=@_p_0" &&
            page.CommandText == sql + $" {Environment.NewLine}Limit @_p_2 OFFSET @_p_1", "公开冻结计划Count/Page完整SQL");
        Check(executor.ToList<long>(source, null).SequenceEqual(new long[] { 1, 2 }) &&
            (await executor.ToListAsync<long>(source, null, default)).SequenceEqual(new long[] { 1, 2 }),
            "公开冻结计划同步异步读取");
        Check(executor.Scalar<long>(count, null) == 2 && executor.ToList<long>(page, null).SequenceEqual(new long[] { 2 }) &&
            Equals(((SqlParam[])source.Parameters)[0].Value, 101), "公开冻结来源隔离与分页执行");
    }

    // Builder 模板进入查询计划后保留 CTE 结构、作用域参数和独立分页槽位。
    using (var query = Query())
    {
        var executor = (ISqlQueryPlanExecutor)query;
        var cte = new SqliteBuilder().Select("OrderId").From("Orders")
            .Where("ProductId", 101).ToTemplate().AsCte("selected");
        var template = new SqliteBuilder().Select("OrderId").From("selected")
            .With(cte.Name, cte.Fragment).OrderBy("OrderId")
            .ToTemplate(new SqlQueryScope("aot"));
        var templateSql = template.Sql;
        var templateParameters = template.Parameters;
        var source = SqlQueryPlan.Create(template.CreateFragment());
        var ctePrefix = $"With Recursive `selected` {Environment.NewLine}" +
            $"As (Select `OrderId` {Environment.NewLine}From `Orders` {Environment.NewLine}" +
            $"Where `ProductId`=@aot_p_0){Environment.NewLine}";
        var sourceSql = ctePrefix + $"Select `OrderId` {Environment.NewLine}From `selected` {Environment.NewLine}Order By `OrderId`";
        var count = SqlBuilderRuntimeBridge.CreateCountPlan(source);
        var page = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(2, 1));
        var firstPage = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(1, 1));
        Check(source.CommandText == sourceSql, "模板计划保留作用域 CTE 完整SQL");
        Check(count.CommandText == ctePrefix + $"Select Count(*) {Environment.NewLine}From `selected`" &&
            page.CommandText == sourceSql + $" {Environment.NewLine}Limit @aot_p_2 OFFSET @aot_p_1",
            "模板计划派生Count与Page完整SQL");
        Check(executor.Scalar<long>(count, null) == 2 && executor.ToList<long>(page, null).SequenceEqual(new long[] { 2 }) &&
            executor.ToList<long>(firstPage, null).SequenceEqual(new long[] { 1 }),
            "模板计划Count与不同分页执行");
        Check((await executor.ToListAsync<long>(page, null, default)).SequenceEqual(new long[] { 2 }),
            "模板计划异步读取");
        Check(templateParameters.Select(parameter => parameter.Name).SequenceEqual(new[] { "aot_p_0" }) &&
            templateParameters.Select(parameter => parameter.Value).SequenceEqual(new object[] { 101 }) &&
            template.Sql == templateSql && template.Parameters.Select(parameter => parameter.Name)
                .SequenceEqual(templateParameters.Select(parameter => parameter.Name)) &&
            template.Parameters.Select(parameter => parameter.Value).SequenceEqual(templateParameters.Select(parameter => parameter.Value)) &&
            source.CommandText == sourceSql, "模板与来源计划在重复派生后保持不变");
    }

    // 第三方 SQL 扩展的回退来源在 Native AOT 中同样支持从页面再分页和自动计数。
    using (var query = Query())
    {
        var executor = (ISqlQueryPlanExecutor)query;
        var source = SqlQueryPlan.Create(new SmokeTailBuilder().Select("OrderId").From("Orders")
            .Where("ProductId", 101).OrderBy("OrderId")).Freeze();
        var page = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(2, 1, "OrderId"));
        var next = SqlBuilderRuntimeBridge.CreatePagePlan(page, new Pager(1, 1, "OrderId"));
        var count = SqlBuilderRuntimeBridge.CreateCountPlan(next);
        var body = $"Select `OrderId` {Environment.NewLine}From `Orders` {Environment.NewLine}Where `ProductId`=@_p_0 {Environment.NewLine}Order By `OrderId`";
        var pageSql = body + $" {Environment.NewLine}Limit @_p_2 OFFSET @_p_1 /* extension */";
        Check(source.CommandText == body + " /* extension */" && page.CommandText == pageSql && next.CommandText == pageSql,
            "回退连续分页完整SQL");
        Check(count.CommandText == $"Select Count(*) {Environment.NewLine}From `Orders` {Environment.NewLine}Where `ProductId`=@_p_0 /* extension */",
            "回退Count移除分页完整SQL");
        Check(executor.Scalar<long>(count, null) == 2, "回退计划Count");
        Check(executor.ToList<long>(page, null).SequenceEqual(new long[] { 2 }), "回退计划第二页");
        Check((await executor.ToListAsync<long>(next, null, default)).SequenceEqual(new long[] { 1 }), "回退计划重新分页");
        var sync = executor.ToPage<long>(page, new Pager(1, 1, "OrderId"), null);
        var asynchronous = await executor.ToPageAsync<long>(page, new Pager(1, 1, "OrderId"), null, default);
        Check(sync.TotalCount == 2 && asynchronous.TotalCount == 2 && sync.Data.SequenceEqual(new long[] { 1 }) &&
            asynchronous.Data.SequenceEqual(sync.Data), "回退页面同步异步自动计数");
        Check(Equals(((SqlParam[])page.Parameters)[1].Value, 1) && Equals(((SqlParam[])source.Parameters)[0].Value, 101),
            "回退源与页面参数隔离");
    }

    // 已分页的冻结计划仅替换分页值，Native AOT 下同样不需要重新解析或反射查询结构。
    using (var query = Query())
    {
        var executor = (ISqlQueryPlanExecutor)query;
        var source = SqlQueryPlan.Create(new SqliteBuilder().Select("OrderId").From("Orders")
            .OrderBy("OrderId").Skip(1).Take(1)).Freeze();
        var page = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(2, 2));
        var sql = $"Select `OrderId` {Environment.NewLine}From `Orders` {Environment.NewLine}Order By `OrderId` {Environment.NewLine}Limit @_p_1 OFFSET @_p_0";
        Check(source.CommandText == sql && page.CommandText == sql, "冻结分页复用完整SQL");
        var parameters = (SqlParam[])page.Parameters;
        Check(Equals(parameters[0].Value, 2) && Equals(parameters[1].Value, 2), "冻结分页参数替换");
        var rows = executor.ToList<long>(page, null);
        var original = await executor.ToListAsync<long>(source, null, default);
        Check(rows.SequenceEqual(new long[] { 3, 4 }) && original.SequenceEqual(new long[] { 2 }),
            "冻结IR分页执行与源计划隔离");
    }

    using (var query = Query())
    {
        var description = query.From<Order>().SelectAll().ExcludeSelect<Product>(p => p.Code)
            .LeftJoin<Order, Product>((o, p) => o.ProductId == p.Id).OrderBy<Order>(o => o.Id);
        Console.WriteLine(description.ToSql());
        var orders = await description.ToListAsync<Order, Product, Order>((o, p) => { o.Product = p; return o; });
        SmokeEvidence.Orders("left_join", orders);
        Check(orders.Count == 4 && orders[0].Id == 1 && orders[0].Product.Id == 101 && orders[0].Product.Name == "A" && orders[0].Product.Status == ProductStatus.Active, "关联对象及重复Id");
        Check(orders[0].Product.Code == null && orders[3].Product == null, "排除字段与LeftJoin空关联");
    }
    using (var query = Query())
    {
        long product = 101;
        async Task<List<Order>> Find() => await query.From<Order>("fixed").SelectAll()
            .Where<Order, long>(renamed => renamed.ProductId, product).ToListAsync<Order>();
        var first = await Find();
        product = 102;
        var second = await Find();
        SmokeEvidence.Orders("parameter_101", first);
        SmokeEvidence.Orders("parameter_102", second);
        Console.WriteLine($"Values: first={string.Join(",", first.Select(row => row.Id))}; second={string.Join(",", second.Select(row => row.Id))}");
        Check(first.Count == 2 && second.Count == 1 && second[0].Id == 3, "参数值变化与显式别名优先");
    }
    using (var query = Query())
    {
        var grouped = query.From<Order>().Select<Order>(o => new { o.ProductId })
            .Select<Order>((o, f) => new { OrderCount = f.Count(x => x.Id), TotalQuantity = f.Sum(x => x.Quantity) })
            .GroupBy<Order>(o => o.ProductId).Having<Order>((o, f) => f.Count(x => x.Id) > 1L)
            .OrderBy<Order>((o, f) => f.Sum(x => x.Quantity), true);
        Console.WriteLine(grouped.ToSql());
        var rows = grouped.ToList<Summary>();
        SmokeEvidence.Summaries("grouped", rows);
        Check(rows.Count == 1 && rows[0].ProductId == 101 && rows[0].OrderCount == 2 && rows[0].TotalQuantity == 12, "Count/Sum分组与Having排序");
        var empty = await query.From<Order>().Where<Order>(o => o.Id < 0)
            .Select<Order>((o, f) => new { OrderCount = f.CountAll(), TotalQuantity = f.Sum(x => x.Quantity) }).ToListAsync<Summary>();
        SmokeEvidence.Summaries("empty_aggregate", empty);
        Check(empty.Count == 1 && empty[0].OrderCount == 0 && empty[0].TotalQuantity == null, "聚合空集与NULL");
        var replaced = query.From<Order>().Select<Order>(o => o.Id).Select<Order>(o => o.Quantity).ReplaceSelect<Order>(o => new { o.Id }).ToList<Order>();
        SmokeEvidence.Orders("replace_select", replaced);
        Check(replaced.Count == 4 && replaced[0].ProductId == 0, "Select累计与ReplaceSelect替换");
    }
    using (var query = Query())
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var cancelled = false;
        try { await query.From<Order>().ToListAsync<Order>(cancellationToken: cancellation.Token); }
        catch (OperationCanceledException) { cancelled = true; }
        Check(cancelled, "取消");
    }
    using (var query = Query())
    {
        var description = query.From<Order>().SelectAll().LeftJoin<Order, Product>((o, p) => o.ProductId == p.Id);
        var failed = false;
        try { description.ToList<Order, Product, Order>((o, p) => throw new InvalidOperationException("map failure")); }
        catch (InvalidOperationException error) when (error.Message == "map failure") { failed = true; }
        Check(failed && description.ToList<Order, Product, Order>((o, p) => o).Count == 4, "map异常后释放连接和执行租约");
    }
    using (var query = Query())
    {
        using var cancellation = new CancellationTokenSource();
        var description = query.From<Order>().SelectAll().LeftJoin<Order, Product>((o, p) => o.ProductId == p.Id);
        var cancelled = false;
        try { await description.ToListAsync<Order, Product, Order>((o, p) => { cancellation.Cancel(); cancellation.Token.ThrowIfCancellationRequested(); return o; }, cancellationToken: cancellation.Token); }
        catch (OperationCanceledException) { cancelled = true; }
        Check(cancelled && description.ToList<Order, Product, Order>((o, p) => o).Count == 4, "映射中协作取消后释放连接和执行租约");
    }
    using (var query = Query())
    {
        var bad = query.From<Order>().SelectAll().ExcludeSelect<Order>(o => o.Id)
            .LeftJoin<Order, Product>((o, p) => o.ProductId == p.Id).ExcludeSelect<Product>(p => p.Id);
        var rejected = false;
        try { bad.ToList<Order, Product, Order>((o, p) => o); }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "不支持的多映射边界提前失败");
    }
    using (var transaction = scope.ServiceProvider.GetRequiredService<ISqlTransactionScopeFactory>().Begin("default"))
    using (var query = transaction.CreateQuery())
    {
        var rows = query.From<Order>().ToList<Order>();
        SmokeEvidence.Orders("transaction", rows);
        Check(rows.Count == 4, "显式事务读取");
        transaction.Rollback();
    }
    using (var query = Query())
    {
        var binary = query.From<BinaryRow>().SelectAll().OrderBy<BinaryRow>(b => b.Id).ToList<BinaryRow>();
        SmokeEvidence.Binaries("inherited_binary", binary);
        Check(binary.Count == 3 && binary[0].Payload.SequenceEqual(new byte[] { 1, 2, 3 }) && binary[1].Payload == null,
            "继承表映射与二进制值和NULL");
        var excluded = query.From<BinaryRow>("b").SelectAll().ExcludeSelect("b.Payload")
            .OrderBy<BinaryRow>(b => b.Id).ToList<BinaryRow>();
        SmokeEvidence.Binaries("string_exclusion", excluded);
        Check(excluded.All(row => row.Payload == null), "字符串排除二进制字段");

        var mapped = query.From<BinaryRow>().SelectAll()
            .Join<BinaryRow, BinaryRow>((b, parent) => b.ParentId == parent.Id)
            .OrderBy<BinaryRow>(b => b.Id)
            .ToList<BinaryRow, BinaryRow, BinaryRow>((b, parent) => { b.Parent = parent; return b; });
        SmokeEvidence.Binaries("binary_two_type", mapped);
        Check(mapped.Count == 3 && mapped[0].Parent.Id == 2 && mapped[0].Parent.Payload == null &&
            mapped[1].Parent.Payload.SequenceEqual(new byte[] { 4 }), "双类型二进制读取与InnerJoin");
        var metadata = scope.ServiceProvider.GetRequiredService<IEntityModelMetadataProvider>()
            .GetMetadata(typeof(BinaryRow));
        var version = metadata.Properties[nameof(BinaryRow.Payload)];
        Check(version.DatabaseGeneratedOption == DatabaseGeneratedOption.Computed && !version.CanInsert && !version.CanUpdate,
            "Timestamp保持只读元数据");
    }
    using (var query = Query())
    {
        var unsupportedSchema = query.From<BinaryRow>("a");
        var schemaRejected = false;
        try { unsupportedSchema.Join<BinaryRow, BinaryRow>((a,b) => a.ParentId == b.Id, new SqlJoinOptions { Schema = "archive" }); }
        catch (NotSupportedException) { schemaRejected = true; }
        Check(schemaRejected, "SQLite拒绝不支持的连接Schema");
        int evaluations = 0;
        SqlJoinOptions Options() { evaluations++; return new SqlJoinOptions { LeftAlias = "a", RightAlias = "chosen" }; }
        var joined = query.From<BinaryRow>("a")
            .Join<BinaryRow, BinaryRow>((a, b) => a.ParentId == b.Id, "b")
            .Join<BinaryRow, BinaryRow>((b, c) => b.ParentId == c.Id, Options())
            .Select<BinaryRow>(chosen => new NameResult { Name = chosen.Name })
            .Where<BinaryRow>(a => a.Id == 1);
        var rows = joined.ToList<NameResult>();
        SmokeEvidence.Names("join_options", rows);
        Check(evaluations == 1 && rows.Count == 1 && rows[0].Name == "second", "动态连接选项与实参求值次数");
    }
    using (var query = Query())
    {
        var inner = query.From<Order>().SelectAll().ExcludeSelect<Product>(p => new { p.Code, p.Status })
            .Join<Order, Product>((o, p) => o.ProductId == p.Id).OrderBy<Order>(o => o.Id);
        var rows = await inner.ToListAsync<Order, Product, Order>((o, p) => { o.Product = p; return o; }, timeout: 2);
        SmokeEvidence.Orders("inner_join_anonymous_exclusion", rows);
        Check(rows.Count == 3 && rows.All(row => row.Product != null && row.Product.Code == null && row.Product.Status == 0),
            "InnerJoin匿名排除与异步超时参数");
        var nulls = query.From<Order>().Where<Order>(o => o.ProductId == 102)
            .Select<Order>((o, f) => new Summary { OrderCount = f.CountAll(), TotalQuantity = f.Sum(x => x.Quantity) })
            .ToList<Summary>();
        SmokeEvidence.Summaries("all_null_dto", nulls);
        Check(nulls.Count == 1 && nulls[0].OrderCount == 1 && nulls[0].TotalQuantity == null, "DTO投影全NULL");
    }
    // 使用真实 SQLite 排他锁触发 command timeout，随后验证连接/租约可恢复。
    using (var blocker = new SqliteConnection(connectionString))
    using (var query = Query())
    {
        blocker.Open();
        using var lockCommand = blocker.CreateCommand();
        lockCommand.CommandText = "BEGIN EXCLUSIVE";
        lockCommand.ExecuteNonQuery();
        var timedOut = false;
        var watch = Stopwatch.StartNew();
        try { query.From<Order>().SelectAll().ToList<Order>(timeout: 1); }
        catch (SqliteException error) when (error.SqliteErrorCode is 5 or 6) { timedOut = true; }
        finally { lockCommand.CommandText = "ROLLBACK"; lockCommand.ExecuteNonQuery(); }
        Check(timedOut && watch.Elapsed < TimeSpan.FromSeconds(15), "真实SQLite命令超时");
        var recovered = query.From<Order>().SelectAll().ToList<Order>(timeout: 2);
        SmokeEvidence.Orders("timeout_recovery", recovered);
        Check(recovered.Count == 4, "超时后连接与执行租约恢复");
    }
    // 同一批生成调用点分别读取两种查询配置，不在编译期固定表别名。
    foreach (var automatic in new[] { true, false })
    {
        queryOptions.AutoGenerateTableAlias = automatic;
        using var query = Query();
        var grouped = query.From<Order>()
            .Join<Order, Product>((o, p) => o.ProductId == p.Id)
            .Select<Order>(o => new Summary {
                ProductId = o.ProductId,
                OrderCount = SqlFn.Count(o.Quantity),
                TotalQuantity = SqlFn.Sum(o.Quantity)
            })
            .GroupBy<Order>(other => other.ProductId)
            .Having<Order>(o => SqlFn.Count(o.Quantity) > 0L)
            .OrderBy<Order>(o => SqlFn.Sum(o.Quantity), true);
        var rows = await grouped.ToListAsync<Summary>();
        SmokeEvidence.Summaries("sqlfn_alias_" + automatic, rows);
        Check(rows.Count == 1 && rows[0].ProductId == 101 && rows[0].OrderCount == 2 && rows[0].TotalQuantity == 12,
            "SqlFn与别名配置" + automatic);
        var nulls = query.From<Order>().Where<Order>(o => o.ProductId == 102)
            .Select<Order>((o, f) => new Summary { OrderCount = f.Count(o.Quantity), TotalQuantity = f.Sum(o.Quantity) })
            .ToList<Summary>();
        SmokeEvidence.Summaries("direct_member_null_" + automatic, nulls);
        Check(nulls.Count == 1 && nulls[0].OrderCount == 0 && nulls[0].TotalQuantity == null,
            "直接成员Count不计NULL" + automatic);
        var empty = query.From<Order>().Where<Order>(o => o.Id < 0)
            .Select<Order>(o => new Summary { OrderCount = SqlFn.CountAll(), TotalQuantity = SqlFn.Sum(o.Quantity) })
            .ToList<Summary>();
        SmokeEvidence.Summaries("static_empty_" + automatic, empty);
        Check(empty.Count == 1 && empty[0].OrderCount == 0 && empty[0].TotalQuantity == null,
            "静态聚合空集" + automatic);
    }
    queryOptions.AutoGenerateTableAlias = true;
    using (var query = Query())
    {
        var names = query.From<BinaryRow>()
            .LeftJoin<BinaryRow, BinaryRow>((row, parent) => row.ParentId == parent.Id)
            .LeftJoin<BinaryRow, BinaryRow>((parent, ancestor) => parent.ParentId == ancestor.Id)
            .Where<BinaryRow>(row => row.Id == 1)
            .Select<BinaryRow>(ancestor => new NameResult { Name = ancestor.Name })
            .ToList<NameResult>();
        SmokeEvidence.Names("inferred_hierarchy", names);
        Check(names.Count == 1 && names[0].Name == "third", "连续自连接不传别名");
        var mapped = query.From<BinaryRow>().SelectAll()
            .LeftJoin<BinaryRow, BinaryRow>((row, parent) => row.ParentId == parent.Id)
            .OrderBy<BinaryRow>(row => row.Id)
            .ToList<BinaryRow, BinaryRow, BinaryRow>((row, parent) => { row.Parent = parent; return row; });
        SmokeEvidence.Binaries("inferred_self_mapping", mapped);
        Check(mapped.Count == 3 && mapped[0].Name == "first" && mapped[0].Parent.Name == "second" &&
            mapped[1].Parent.Name == "third", "自连接双类型映射不传别名");
        var ignored = query.From<Order>().Select<Order>(o => new { OrderCount = SqlFn.CountAll() });
        var rejected = false;
        try { ignored.ToList<IgnoredSummary>(); }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "忽略的聚合结果成员在执行前拒绝");
    }
    queryOptions.AutoGenerateTableAlias = false;
    using (var query = Query())
    {
        var description = query.From<BinaryRow>().SelectAll();
        var previous = description.ToSql();
        var rejected = false;
        try { description.LeftJoin<BinaryRow, BinaryRow>((row, parent) => row.ParentId == parent.Id); }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected && previous == description.ToSql(), "关闭自动别名时歧义自连接回滚");
    }
    queryOptions.AutoGenerateTableAlias = true;
    // Review 回归：object 结果属性必须读取数据库原值，三种聚合写法保持一致。
    for (var syntax = 0; syntax < 3; syntax++)
    {
        using var query = Query();
        var description = query.From<Order>();
        if (syntax == 0) description.Select<Order>(o => new BoxedSummary { OrderCount = SqlFn.Count(o.Id), TotalQuantity = SqlFn.Sum(o.Quantity) });
        if (syntax == 1) description.Select<Order>((o, f) => new BoxedSummary { OrderCount = f.Count(o.Id), TotalQuantity = f.Sum(o.Quantity) });
        if (syntax == 2) description.Select<Order>((o, f) => new BoxedSummary { OrderCount = f.Count(x => x.Id), TotalQuantity = f.Sum(x => x.Quantity) });
        var rows = await description.ToListAsync<BoxedSummary>();
        SmokeEvidence.Boxed("boxed-" + syntax, rows);
        Check(rows.Count == 1 && rows[0].OrderCount is long count && count == 4 &&
            rows[0].TotalQuantity is long total && total == 15, "object聚合结果完整读取" + syntax);
    }
    using (var query = Query())
    {
        var rows = query.From<Order>().Where<Order>(o => o.Id < 0)
            .Select<Order>(o => new BoxedSummary { OrderCount = SqlFn.Count(o.Id), TotalQuantity = SqlFn.Sum(o.Quantity) })
            .ToList<BoxedSummary>();
        SmokeEvidence.Boxed("boxed-empty", rows);
        Check(rows.Count == 1 && rows[0].OrderCount is long count && count == 0 && rows[0].TotalQuantity == null,
            "object聚合空集与DBNull");
    }
    for (var syntax = 0; syntax < 3; syntax++)
    {
        using var query = Query();
        const int minimum = 1;
        var description = query.From<Order>()
            .Select<Order>(o => new Summary { ProductId = o.ProductId, OrderCount = SqlFn.Count(o.Id) })
            .GroupBy<Order>(o => o.ProductId);
        if (syntax == 0) description.Having<Order>(o => SqlFn.Count(o.Id) > minimum);
        if (syntax == 1) description.Having<Order>((o, f) => f.Count(o.Id) > minimum);
        if (syntax == 2) description.Having<Order>((o, f) => f.Count(x => x.Id) > minimum);
        var rows = description.ToList<Summary>();
        SmokeEvidence.Summaries("int-threshold-" + syntax, rows);
        Check(rows.Count == 1 && rows[0].OrderCount == 2 && rows[0].ProductId == 101, "Having int阈值提升" + syntax);
    }
    using (var query = Query())
    {
        var rows = query.From<Order>()
            .Select<Order>(o => new Summary { ProductId = o.ProductId, OrderCount = SqlFn.Count(o.Id) })
            .GroupBy<Order>(o => o.ProductId).Having<Order>(o => SqlFn.Count(o.Id) > (int?)1)
            .ToList<Summary>();
        SmokeEvidence.Summaries("nullable-int-threshold", rows);
        Check(rows.Count == 1 && rows[0].OrderCount == 2 && rows[0].ProductId == 101, "Having nullable int阈值提升");
    }
    using (var query = Query())
    {
        var rows = query.From<Order>()
            .Select<Order>(o => new EscapedSummary { @Count = SqlFn.Count(o.Id, "Count"), @event = SqlFn.CountAll("event") })
            .ToList<EscapedSummary>();
        SmokeEvidence.Escaped("escaped-member", rows);
        Check(rows.Count == 1 && rows[0].Count == 4 && rows[0].@event == 4, "DTO转义成员使用实际名称");
    }
    // Review 回归：不支持的聚合目标必须在打开执行链之前拒绝。
    using (var query = Query())
    {
        var description = query.From<Order>().Select<Order>(o => new { Count = SqlFn.Count(o.Id) });
        var executions = evidence.Executions;
        var rejected = false;
        try { description.ToList<ComparableSummary>(); }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected && evidence.Executions == executions, "接口聚合目标执行前拒绝");
        rejected = false;
        try { await description.ToListAsync<ValueSummary>(); }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected && evidence.Executions == executions, "ValueType聚合目标执行前拒绝");
    }
    for (var syntax = 0; syntax < 3; syntax++)
    {
        using var query = Query();
        var description = query.From<FloatingRow>();
        if (syntax == 0) description.Select<FloatingRow>(r => new FloatingSummary { Count = SqlFn.Count(r.Value), Total = SqlFn.Sum(r.Value) });
        if (syntax == 1) description.Select<FloatingRow>((r, f) => new FloatingSummary { Count = f.Count(r.Value), Total = f.Sum(r.Value) });
        if (syntax == 2) description.Select<FloatingRow>((r, f) => new FloatingSummary { Count = f.Count(x => x.Value), Total = f.Sum(x => x.Value) });
        var rows = await description.ToListAsync<FloatingSummary>();
        SmokeEvidence.Floating("double-nullable-" + syntax, rows);
        Check(rows.Count == 1 && rows[0].Count == 2 && rows[0].Total == 12d, "double可空Count/Sum" + syntax);
    }
    for (var mode = 0; mode < 2; mode++)
    {
        using var query = Query();
        var description = query.From<FloatingRow>();
        if (mode == 0) description.Where<FloatingRow>(r => r.Id < 0);
        if (mode == 1) description.Where<FloatingRow>(r => r.Id == 2);
        var rows = description.Select<FloatingRow>(r => new FloatingSummary { Count = SqlFn.Count(r.Value), Total = SqlFn.Sum(r.Value) })
            .ToList<FloatingSummary>();
        SmokeEvidence.Floating("double-empty-or-null-" + mode, rows);
        Check(rows.Count == 1 && rows[0].Count == 0 && rows[0].Total == null, "double空集或全NULL" + mode);
    }
    using (var query = Query())
    {
        var rows = query.From<FloatingRow>().Select<FloatingRow>(r => new FloatingSummary {
            Count = SqlFn.Count(r.RequiredValue), Total = SqlFn.Sum(r.RequiredValue) }).ToList<FloatingSummary>();
        SmokeEvidence.Floating("double-required", rows);
        Check(rows.Count == 1 && rows[0].Count == 3 && rows[0].Total == 12d, "double非可空输入");
    }
    using (var query = Query())
    {
        const char minimum = '\u0006';
        var rows = query.From<FloatingRow>().Select<FloatingRow>(r => new FloatingSummary {
            GroupId = r.GroupId, Count = SqlFn.Count(r.Value), Total = SqlFn.Sum(r.Value) })
            .GroupBy<FloatingRow>(r => r.GroupId).Having<FloatingRow>(r => SqlFn.Sum(r.Value) > minimum)
            .OrderBy<FloatingRow>(r => r.GroupId).ToList<FloatingSummary>();
        SmokeEvidence.Floating("double-char-threshold", rows);
        Check(rows.Count == 1 && rows[0].GroupId == 1 && rows[0].Count == 1 && rows[0].Total == 7d, "char阈值提升为double");
    }
    // 未分页冻结计划可在 Native AOT 下追加分页，并通过统一终结器执行 Count 和数据查询。
    using (var query = Query())
    {
        var executor = (ISqlQueryPlanExecutor)query;
        var source = SqlQueryPlan.Create(new SqliteBuilder().Select("OrderId").From("Orders")
            .OrderBy("OrderId")).Freeze();
        var originalSql = $"Select `OrderId` {Environment.NewLine}From `Orders` {Environment.NewLine}Order By `OrderId`";
        var page = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(2, 2));
        Check(page.CommandText == originalSql + $" {Environment.NewLine}Limit @_p_1 OFFSET @_p_0",
            "未分页IR追加Provider完整SQL");
        var result = executor.ToPage<long>(source, new Pager(2, 2), null);
        var repeated = await executor.ToPageAsync<long>(source, new Pager(1, 1), null, default);
        Check(result.TotalCount == 4 && result.Data.SequenceEqual(new long[] { 3, 4 }) &&
            repeated.TotalCount == 4 && repeated.Data.SequenceEqual(new long[] { 1 }),
            "未分页IR同步异步Count与数据页");
        Check(source.CommandText == originalSql && ((SqlParam[])source.Parameters).Length == 0,
            "未分页IR重复执行源隔离");
    }
    // 冻结排序支持首次排序与追加排序；三个初始状态都执行同步、异步的 Count/Data。
    for (var state = 0; state < 3; state++)
    {
        using var query = Query();
        var executor = (ISqlQueryPlanExecutor)query;
        var builder = new SqliteBuilder().Select("OrderId").From("Orders");
        if (state > 0) builder.OrderBy("OrderId");
        if (state == 2) builder.Skip(1).Take(1);
        var source = SqlQueryPlan.Create(builder).Freeze();
        var originalSql = source.CommandText;
        var originalValues = ((SqlParam[])source.Parameters).Select(p => p.Value).ToArray();
        const string requestedOrder = "OrderId Desc,ProductId Desc";
        var page = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(2, 2, requestedOrder));
        var expectedSql = $"Select `OrderId` {Environment.NewLine}From `Orders` {Environment.NewLine}Order By `OrderId`" +
            (state == 0 ? " Desc" : "") + $",`ProductId` Desc {Environment.NewLine}Limit @_p_1 OFFSET @_p_0";
        Check(page.CommandText == expectedSql && ((SqlParam[])page.Parameters).Select(p => p.Value)
            .SequenceEqual(new object[] { 2, 2 }), "冻结排序分页完整SQL与参数" + state);
        var result = executor.ToPage<long>(source, new Pager(2, 2, requestedOrder), null);
        var repeated = await executor.ToPageAsync<long>(source, new Pager(1, 1, requestedOrder), null, default);
        Check(result.TotalCount == 4 && result.Data.SequenceEqual(state == 0 ? new long[] { 2, 1 } : new long[] { 3, 4 }) &&
            repeated.TotalCount == 4 && repeated.Data.SequenceEqual(new long[] { state == 0 ? 4 : 1 }),
            "冻结排序同步异步Count与数据页" + state);
        Check(source.CommandText == originalSql && ((SqlParam[])source.Parameters).Select(p => p.Value)
            .SequenceEqual(originalValues), "冻结排序重复执行源隔离" + state);
    }
    // CTE 的四种主查询形态在三个初始状态下直接使用冻结边界，Count 前缀始终在包装外。
    foreach (var shape in new[] { "simple", "group", "distinct", "aggregate" })
    for (var state = 0; state < 3; state++)
    {
        using var query = Query();
        var executor = (ISqlQueryPlanExecutor)query;
        var cte = new SqliteBuilder().Select("ProductId").From("Orders").Where("OrderId", 0, Operator.Greater);
        var builder = new SqliteBuilder();
        if (shape == "aggregate") builder.CountAll("Total");
        else builder.Select("ProductId");
        builder.From("selected").With("selected", cte);
        if (shape == "group") builder.GroupBy("ProductId");
        if (shape == "distinct") builder.Distinct();
        var key = shape == "aggregate" ? "Total" : "ProductId";
        if (state > 0) builder.OrderBy(key);
        if (state == 2) builder.Skip(2).Take(1);
        var source = SqlQueryPlan.Create(builder).Freeze();
        var originalSql = source.CommandText;
        var originalValues = ((SqlParam[])source.Parameters).Select(p => p.Value).ToArray();
        var prefix = $"With Recursive `selected` {Environment.NewLine}As (Select `ProductId` {Environment.NewLine}From `Orders` {Environment.NewLine}Where `OrderId`>@_p_{(state == 2 ? 2 : 0)}){Environment.NewLine}";
        var select = shape == "aggregate" ? "Select Count(*) As `Total`" :
            shape == "distinct" ? "Select Distinct `ProductId`" : "Select `ProductId`";
        var body = select + $" {Environment.NewLine}From `selected`" + (shape == "group" ? $" {Environment.NewLine}Group By `ProductId`" : "");
        var page = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(1, 2, key + " Desc"));
        Check(page.CommandText == prefix + body + $" {Environment.NewLine}Order By `{key}`" + (state == 0 ? " Desc" : "") +
            $" {Environment.NewLine}Limit @_p_{(state == 2 ? 1 : 2)} OFFSET @_p_{(state == 2 ? 0 : 1)}",
            "CTE冻结分页完整SQL-" + shape + state);
        var count = SqlBuilderRuntimeBridge.CreateCountPlan(page);
        Check(count.CommandText == prefix + (shape == "simple" ? $"Select Count(*) {Environment.NewLine}From `selected`" :
            $"Select Count(*) {Environment.NewLine}From ({body}) As `t`") && ((SqlParam[])count.Parameters).Length == 1,
            "CTE外层计数完整SQL-" + shape + state);
        var first = executor.ToPage<long>(source, new Pager(1, 2, key + " Desc"), null);
        var second = await executor.ToPageAsync<long>(source, new Pager(2, 2, key + " Desc"), null, default);
        var values = shape == "aggregate" ? new long[] { 4 } : shape == "simple" ?
            new long[] { 101, 101, 102, 999 } : new long[] { 101, 102, 999 };
        if (state == 0 && shape != "aggregate") Array.Reverse(values);
        Check(first.TotalCount == values.Length && second.TotalCount == values.Length &&
            first.Data.SequenceEqual(values.Take(2)) && second.Data.SequenceEqual(values.Skip(2)),
            "CTE同步异步Count与数据页-" + shape + state);
        Check(source.CommandText == originalSql && ((SqlParam[])source.Parameters).Select(p => p.Value)
            .SequenceEqual(originalValues), "CTE冻结源隔离-" + shape + state);
    }
    // 集合主查询与 CTE 组合直接派生；保留分支参数和重复执行的源状态。
    foreach (var operation in new[] { "Union", "Union All", "Intersect", "Except" })
    foreach (var cte in new[] { false, true })
    foreach (var paged in new[] { false, true })
    {
        using var query = Query();
        var executor = (ISqlQueryPlanExecutor)query;
        var builder = new SqliteBuilder().Select("OrderId").From(cte ? "selected" : "Orders").Where("OrderId", 1, Operator.Greater);
        if (cte) builder.With("selected", new SqliteBuilder().Select("OrderId").From("Orders"));
        var branch = new SqliteBuilder().Select("OrderId").From("Orders").Where("OrderId", 3, Operator.LessEqual);
        switch (operation)
        {
            case "Union": builder.Union(branch); break;
            case "Union All": builder.UnionAll(branch); break;
            case "Intersect": builder.Intersect(branch); break;
            case "Except": builder.Except(branch); break;
        }
        if (paged) builder.OrderBy("OrderId").Skip(5).Take(1);
        var source = SqlQueryPlan.Create(builder).Freeze();
        var originalSql = source.CommandText;
        var originalValues = ((SqlParam[])source.Parameters).Select(p => p.Value).ToArray();
        var prefix = cte ? $"With Recursive `selected` {Environment.NewLine}As (Select `OrderId` {Environment.NewLine}From `Orders`){Environment.NewLine}" : "";
        var body = $"Select `OrderId` {Environment.NewLine}From `{(cte ? "selected" : "Orders")}` {Environment.NewLine}Where `OrderId`>@_p_0 {Environment.NewLine}{operation} {Environment.NewLine}Select `OrderId` {Environment.NewLine}From `Orders` {Environment.NewLine}Where `OrderId`<=@_p_{(paged ? 3 : 1)}";
        var page = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(1, 2, "OrderId"));
        Check(page.CommandText == prefix + body + $" {Environment.NewLine}Order By `OrderId` {Environment.NewLine}Limit @_p_{(paged ? 2 : 3)} OFFSET @_p_{(paged ? 1 : 2)}",
            "集合分页完整SQL-" + operation + cte + paged);
        Check(SqlBuilderRuntimeBridge.CreateCountPlan(page).CommandText == prefix + $"Select Count(*) {Environment.NewLine}From (" + body + ") As `t`",
            "集合计数完整SQL-" + operation + cte + paged);
        var expected = operation switch {
            "Union" => new long[] { 1, 2, 3, 4 },
            "Union All" => new long[] { 1, 2, 2, 3, 3, 4 },
            "Intersect" => new long[] { 2, 3 },
            _ => new long[] { 4 }
        };
        var first = executor.ToPage<long>(source, new Pager(1, 2, "OrderId"), null);
        var second = await executor.ToPageAsync<long>(source, new Pager(2, 2, "OrderId"), null, default);
        Check(first.TotalCount == expected.Length && second.TotalCount == expected.Length &&
            first.Data.SequenceEqual(expected.Take(2)) && second.Data.SequenceEqual(expected.Skip(2).Take(2)), "集合实际Count与页数据");
        Check(source.CommandText == originalSql && ((SqlParam[])source.Parameters).Select(p => p.Value)
            .SequenceEqual(originalValues), "集合冻结源隔离");
    }
    // Skip-only 的 offset 在冻结时已存在；后续分页只补 limit，源值始终不变。
    foreach (var ordered in new[] { false, true })
    {
        using var query = Query();
        var executor = (ISqlQueryPlanExecutor)query;
        var builder = new SqliteBuilder().Select("OrderId").From("Orders").Where("OrderId", 0, Operator.Greater);
        if (ordered) builder.OrderBy("OrderId");
        builder.Skip(5);
        var source = SqlQueryPlan.Create(builder).Freeze();
        var originalSql = $"Select `OrderId` {Environment.NewLine}From `Orders` {Environment.NewLine}Where `OrderId`>@_p_0" +
            (ordered ? $" {Environment.NewLine}Order By `OrderId`" : "");
        var page = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(2, 2, "OrderId"));
        Check(page.CommandText == $"Select `OrderId` {Environment.NewLine}From `Orders` {Environment.NewLine}Where `OrderId`>@_p_0 {Environment.NewLine}Order By `OrderId` {Environment.NewLine}Limit @_p_2 OFFSET @_p_1" &&
            ((SqlParam[])page.Parameters).Select(p => (p.Name, p.Value))
                .SequenceEqual(new[] { ("@_p_0", (object)0), ("@_p_1", (object)2), ("@_p_2", (object)2) }),
            "Skip-only IR完整SQL和参数" + ordered);
        var first = executor.ToPage<long>(source, new Pager(2, 2, "OrderId"), null);
        var second = await executor.ToPageAsync<long>(source, new Pager(1, 1, "OrderId"), null, default);
        Check(first.TotalCount == 4 && first.Data.SequenceEqual(new long[] { 3, 4 }) &&
            second.TotalCount == 4 && second.Data.SequenceEqual(new long[] { 1 }), "Skip-only同步异步Count和数据" + ordered);
        Check(source.CommandText == originalSql && ((SqlParam[])source.Parameters).Select(p => p.Value)
            .SequenceEqual(new object[] { 0, 5 }), "Skip-only冻结源隔离" + ordered);
    }
    // 原有 205 次执行，加上过滤工作区的 Count、Page 和下一次取值查询。
    Check(evidence.Executions == 208, "实际执行SQL与参数快照");
    Console.WriteLine("PASS: generated SQLite query smoke");
}
finally
{
    SqliteConnection.ClearAllPools();
    if (File.Exists(path)) File.Delete(path);
}
static void Check(bool condition, string scenario)
{
    if (!condition) throw new InvalidOperationException("FAIL: " + scenario);
    Console.WriteLine("PASS: " + scenario);
}

/// <summary>
/// 表示 AOT smoke 测试映射的订单实体。
/// </summary>
[Table("Orders")]
public class Order
{
    /// <summary>
    /// 获取或设置订单记录标识。
    /// </summary>
    [Key, Column("OrderId")] public long Id { get; set; }
    /// <summary>
    /// 获取或设置订单关联的商品标识。
    /// </summary>
    public long ProductId { get; set; }
    /// <summary>
    /// 获取或设置订单商品数量。
    /// </summary>
    public long? Quantity { get; set; }
    /// <summary>
    /// 获取或设置未映射的商品导航对象。
    /// </summary>
    [NotMapped] public Product Product { get; set; }
}
/// <summary>
/// 表示 AOT smoke 测试映射的商品实体。
/// </summary>
[Table("Products")]
public class Product
{
    /// <summary>
    /// 获取或设置商品记录标识。
    /// </summary>
    [Key, Column("ProductNo")] public long Id { get; set; }
    /// <summary>
    /// 获取或设置商品名称。
    /// </summary>
    public string Name { get; set; }
    /// <summary>
    /// 获取或设置商品编码。
    /// </summary>
    public string Code { get; set; }
    /// <summary>
    /// 获取或设置商品状态值。
    /// </summary>
    public ProductStatus Status { get; set; }
}
/// <summary>
/// 承载按商品汇总的订单数量和总数量。
/// </summary>
public class Summary
{
    /// <summary>
    /// 获取或设置聚合结果对应的商品标识。
    /// </summary>
    public long ProductId { get; set; }
    /// <summary>
    /// 获取或设置该商品关联的订单数量。
    /// </summary>
    public long OrderCount { get; set; }
    /// <summary>
    /// 获取或设置该商品订单的数量合计。
    /// </summary>
    public long? TotalQuantity { get; set; }
}

/// <summary>
/// 标识 Smoke 测试实体的产品状态。
/// </summary>
public enum ProductStatus
{
    /// <summary>
    /// 产品处于可用状态。
    /// </summary>
    Active = 1,
    /// <summary>
    /// 产品已归档。
    /// </summary>
    Archived = 2
}

/// <summary>
/// 定义二进制列映射测试使用的基础实体。
/// </summary>
[Table("Binaries")]
public class BinaryBase
{
    /// <summary>
    /// 获取或设置记录标识。
    /// </summary>
    [Key, Column("BinaryId")] public long Id { get; set; }
}
/// <summary>
/// 表示包含二进制载荷和父级关联的测试记录。
/// </summary>
public class BinaryRow : BinaryBase
{
    /// <summary>
    /// 获取或设置父记录标识。
    /// </summary>
    public long ParentId { get; set; }
    /// <summary>
    /// 获取或设置实体或结果名称。
    /// </summary>
    public string Name { get; set; }
    /// <summary>
    /// 获取或设置记录的二进制载荷。
    /// </summary>
    [Timestamp] public byte[] Payload { get; set; }
    /// <summary>
    /// 获取或设置关联的父实体。
    /// </summary>
    [NotMapped] public BinaryRow Parent { get; set; }
}
/// <summary>
/// 承载名称列投影的查询结果。
/// </summary>
public class NameResult
{
    /// <summary>
    /// 获取或设置实体或结果名称。
    /// </summary>
    public string Name { get; set; }
}

/// <summary>
/// 承载验证忽略映射行为的聚合结果。
/// </summary>
public class IgnoredSummary
{
    /// <summary>
    /// 获取或设置汇总范围内的订单数量。
    /// </summary>
    [NotMapped] public long OrderCount { get; set; }
}

/// <summary>
/// 承载装箱聚合查询的计数和数量结果。
/// </summary>
public class BoxedSummary {
    /// <summary>
    /// 获取或设置汇总范围内的订单数量。
    /// </summary>
    public object OrderCount { get; set; }
    /// <summary>
    /// 获取或设置订单总数量。
    /// </summary>
    public object TotalQuantity { get; set; } }
/// <summary>
/// 承载转义标识符查询的聚合结果。
/// </summary>
public class EscapedSummary {
    /// <summary>
    /// 获取或设置以转义标识符读取的聚合计数。
    /// </summary>
    public long @Count { get; set; }
    /// <summary>
    /// 获取或设置以转义标识符读取的事件字段值。
    /// </summary>
    public long @event { get; set; } }

/// <summary>
/// 承载可比较值聚合查询的计数结果。
/// </summary>
public class ComparableSummary {
    /// <summary>
    /// 获取或设置聚合计数。
    /// </summary>
    public IComparable Count { get; set; } }
/// <summary>
/// 承载值类型聚合查询的计数结果。
/// </summary>
public class ValueSummary {
    /// <summary>
    /// 获取或设置聚合计数。
    /// </summary>
    public ValueType Count { get; set; } }
/// <summary>
/// 提供浮点聚合测试所需的分组和数值字段。
/// </summary>
[Table("FloatingRows")]
public class FloatingRow
{
    /// <summary>
    /// 获取或设置记录标识。
    /// </summary>
    [Key] public long Id { get; set; }
    /// <summary>
    /// 获取或设置分组标识。
    /// </summary>
    public long GroupId { get; set; }
    /// <summary>
    /// 获取或设置参与查询或聚合的测试值。
    /// </summary>
    public double? Value { get; set; }
    /// <summary>
    /// 获取或设置参与聚合的必需数值。
    /// </summary>
    public double RequiredValue { get; set; }
}
/// <summary>
/// 承载浮点聚合查询的分组、计数和总值。
/// </summary>
public class FloatingSummary
{
    /// <summary>
    /// 获取或设置聚合结果的分组标识。
    /// </summary>
    public long GroupId { get; set; }
    /// <summary>
    /// 获取或设置聚合计数。
    /// </summary>
    public long Count { get; set; }
    /// <summary>
    /// 获取或设置聚合总值。
    /// </summary>
    public double? Total { get; set; }
}

/// <summary>验证不依赖反射的第三方 SQL 尾部扩展回退。</summary>
internal sealed class SmokeTailBuilder : SqliteBuilder
{
    internal SmokeTailBuilder(IParameterManager parameters = null) : base(parameterManager: parameters) { }
    public override string ToSql() => base.ToSql() + " /* extension */";
    protected override SqlBuilderBase CreateBuilder(IParameterManager parameterManager) => new SmokeTailBuilder(parameterManager);
}

/// <summary>为 AOT 过滤工作区验证提供交替的商品参数。</summary>
internal sealed class SmokeProductFilter : ISqlFilter
{
    internal int Calls { get; private set; }

    public void Filter(SqlFilterContext context)
    {
        var source = context.Sources.Single(item => item.EntityType == typeof(Order));
        context.AddPredicate(source, context.GetColumn(source, nameof(Order.ProductId)),
            ++Calls % 2 == 1 ? 101L : 102L);
    }
}
