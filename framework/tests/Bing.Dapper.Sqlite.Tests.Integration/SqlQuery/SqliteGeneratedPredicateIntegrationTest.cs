using Bing.Data.Sql;

namespace Bing.Dapper.Tests.SqlQuery;

public sealed partial class SqliteProjectionIntegrationTest
{
    private static readonly SqlGeneratedColumn PredicateId = new(0, "Id", null, typeof(int), null, false);
    private static readonly SqlGeneratedPredicate GeneratedOrderFilter = SqlGeneratedPredicate.And(
        SqlGeneratedPredicate.Compare(PredicateId, null, Operator.GreaterEqual, 1),
        SqlGeneratedPredicate.Or(
            SqlGeneratedPredicate.Compare(PredicateId, null, Operator.Equal, 1),
            SqlGeneratedPredicate.Compare(PredicateId, null, Operator.Equal, 2)));

    /// <summary>生成绑定器收到冻结的结构化参数，并在 SQLite 中执行当前请求的值。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GeneratedList_WhenFluentPlanHasParameters_ShouldBindFrozenValues(bool asynchronous)
    {
        using var root = _fixture.CreateQuery();
        var query = root.From<Order>("o")
            .Where<Order, int>(2, (order, id) => order.Id == id, "o")
            .Select<Order>(order => new { order.Id });
        var boundValues = new List<object>();

        void Bind(System.Data.Common.DbCommand command, IEnumerable<Bing.Data.Sql.Builders.Params.SqlParam> parameters)
        {
            var snapshot = parameters.ToArray();
            boundValues.AddRange(snapshot.Select(parameter => parameter.Value));
            SqlGeneratedExecution.BindParameters(command, snapshot);
        }

        Order Read(SqlGeneratedRow row) => new() { Id = row.Reader.GetInt32(0) };

        Assert.Equal("Select `o`.`OrderId` As `Id` \r\nFrom `sqp_orders` As `o` \r\nWhere `o`.`OrderId`=@_p_0", query.ToSql());
        var rows = asynchronous
            ? await SqlGeneratedExecution.ToListAsync(query, Read, Bind, null, default)
            : SqlGeneratedExecution.ToList(query, Read, Bind, null);

        Assert.Equal(new object[] { 2 }, boundValues);
        Assert.Equal(2, Assert.Single(rows).Id);
    }

    /// <summary>元组内不同参数应与集合、字符串条件及动态开关一起执行。</summary>
    [Fact]
    public async Task ExplicitTuplePredicate_WhenValuesOrConditionChange_ShouldExecuteWithCurrentValues()
    {
        var enabled = await Read(true, new[] { 1, 3, 4 }, "ull");
        Assert.Equal("Select `o`.`OrderId` As `Id` \r\nFrom `sqp_orders` As `o` \r\n" +
            "Where `o`.`OrderId` In (@_p_0,@_p_1,@_p_2) And `o`.`Name` Like @_p_3 \r\nOrder By `o`.`OrderId`", enabled.Sql);
        Assert.Equal(new[] { 3 }, enabled.Ids);

        var changed = await Read(true, new[] { 2, 4 }, "or");
        Assert.Equal("Select `o`.`OrderId` As `Id` \r\nFrom `sqp_orders` As `o` \r\n" +
            "Where `o`.`OrderId` In (@_p_0,@_p_1) And `o`.`Name` Like @_p_2 \r\nOrder By `o`.`OrderId`", changed.Sql);
        Assert.Equal(new[] { 4 }, changed.Ids);

        var empty = await Read(true, Array.Empty<int>(), "ull");
        Assert.Equal("Select `o`.`OrderId` As `Id` \r\nFrom `sqp_orders` As `o` \r\n" +
            "Where 1 = 0 And `o`.`Name` Like @_p_0 \r\nOrder By `o`.`OrderId`", empty.Sql);
        Assert.Empty(empty.Ids);

        var disabled = await Read(false, new[] { 1 }, "ull");
        Assert.Equal("Select `o`.`OrderId` As `Id` \r\nFrom `sqp_orders` As `o` \r\nOrder By `o`.`OrderId`", disabled.Sql);
        Assert.Equal(new[] { 1, 2, 3, 4 }, disabled.Ids);

        async Task<(string Sql, int[] Ids)> Read(bool condition, int[] ids, string term)
        {
            using var root = _fixture.CreateQuery();
            var query = root.From<Order>("o")
                .WhereIf<Order, (int[] ids, string term)>(condition, (ids, term),
                    (o, value) => value.ids.Contains(o.Id) && o.Name.Contains(value.term), "o")
                .Select<Order>(o => new { o.Id }).OrderBy<Order>(o => o.Id);
            return (query.ToSql(), (await query.ToListAsync<Order>()).Select(item => item.Id).ToArray());
        }
    }

    /// <summary>同一个显式值在复合条件中应经 SQLite 参数绑定，并支持关闭条件。</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExplicitPredicate_WhenConditionChanges_ShouldExecuteWithRequestValue(bool enabled)
    {
        using var root = _fixture.CreateQuery();
        var query = root.From<Order>("o")
            .WhereIf<Order, int>(enabled, 101, (o, id) => o.Id == id || o.ProductNo == id, "o")
            .Select<Order>(o => new { o.Id }).OrderBy<Order>(o => o.Id);
        var expectedSql = "Select `o`.`OrderId` As `Id` \r\nFrom `sqp_orders` As `o`" +
            (enabled ? " \r\nWhere (`o`.`OrderId`=@_p_0 Or `o`.`ProductNo`=@_p_1)" : string.Empty) +
            " \r\nOrder By `o`.`OrderId`";

        Assert.Equal(expectedSql, query.ToSql());
        var results = await query.ToListAsync<Order>();
        Assert.Equal(enabled ? new[] { 1, 2 } : new[] { 1, 2, 3, 4 }, results.Select(item => item.Id));
    }

    /// <summary>字符串值作为参数传递，不改变 SQL 结构。</summary>
    [Fact]
    public async Task ExplicitPredicate_WhenStringContainsSqlText_ShouldRemainParameterized()
    {
        using var root = _fixture.CreateQuery();
        var query = root.From<Order>("o")
            .Where<Order, string>("' OR 1=1 --", (o, name) => o.Name == name || o.Name == name, "o")
            .Select<Order>(o => new { o.Id });

        Assert.Equal("Select `o`.`OrderId` As `Id` \r\nFrom `sqp_orders` As `o` \r\nWhere (`o`.`Name`=@_p_0 Or `o`.`Name`=@_p_1)", query.ToSql());
        Assert.Empty(await query.ToListAsync<Order>());
    }

    /// <summary>显式值应绑定到 Contains、StartsWith 和 EndsWith，并由 WhereIf 控制条件启停。</summary>
    [Fact]
    public async Task ExplicitStringPredicate_WhenConditionChanges_ShouldBindEachMatchAndExecute()
    {
        using var root = _fixture.CreateQuery();
        var contains = root.From<Order>("o")
            .WhereIf<Order, string>(true, "ull", (o, value) => o.Name.Contains(value), "o")
            .Select<Order>(o => new { o.Id });
        Assert.Equal("Select `o`.`OrderId` As `Id` \r\nFrom `sqp_orders` As `o` \r\nWhere `o`.`Name` Like @_p_0", contains.ToSql());
        Assert.Equal(new[] { 3 }, (await contains.ToListAsync<Order>()).Select(item => item.Id));

        var startsWith = root.From<Order>("o")
            .WhereIf<Order, string>(true, "null", (o, value) => o.Name.StartsWith(value), "o")
            .Select<Order>(o => new { o.Id });
        Assert.Equal("Select `o`.`OrderId` As `Id` \r\nFrom `sqp_orders` As `o` \r\nWhere `o`.`Name` Like @_p_0", startsWith.ToSql());
        Assert.Equal(new[] { 3 }, (await startsWith.ToListAsync<Order>()).Select(item => item.Id));

        var startsWithOnly = root.From<Order>("o")
            .WhereIf<Order, string>(true, "quantity", (o, value) => o.Name.StartsWith(value), "o")
            .Select<Order>(o => new { o.Id });
        Assert.Equal("Select `o`.`OrderId` As `Id` \r\nFrom `sqp_orders` As `o` \r\nWhere `o`.`Name` Like @_p_0", startsWithOnly.ToSql());
        Assert.Empty(await startsWithOnly.ToListAsync<Order>());

        var endsWith = root.From<Order>("o")
            .WhereIf<Order, string>(true, "quantity", (o, value) => o.Name.EndsWith(value), "o")
            .Select<Order>(o => new { o.Id });
        Assert.Equal("Select `o`.`OrderId` As `Id` \r\nFrom `sqp_orders` As `o` \r\nWhere `o`.`Name` Like @_p_0", endsWith.ToSql());
        Assert.Equal(new[] { 3 }, (await endsWith.ToListAsync<Order>()).Select(item => item.Id));

        var endsWithOnly = root.From<Order>("o")
            .WhereIf<Order, string>(true, "null", (o, value) => o.Name.EndsWith(value), "o")
            .Select<Order>(o => new { o.Id });
        Assert.Equal("Select `o`.`OrderId` As `Id` \r\nFrom `sqp_orders` As `o` \r\nWhere `o`.`Name` Like @_p_0", endsWithOnly.ToSql());
        Assert.Empty(await endsWithOnly.ToListAsync<Order>());

        var disabled = root.From<Order>("o")
            .WhereIf<Order, string>(false, "ull", (o, value) =>
                o.Name.Contains(value) || o.Name.StartsWith(value) || o.Name.EndsWith(value), "o")
            .Select<Order>(o => new { o.Id }).OrderBy<Order>(o => o.Id);
        Assert.Equal("Select `o`.`OrderId` As `Id` \r\nFrom `sqp_orders` As `o` \r\nOrder By `o`.`OrderId`", disabled.ToSql());
        Assert.Equal(new[] { 1, 2, 3, 4 }, (await disabled.ToListAsync<Order>()).Select(item => item.Id));
    }

    /// <summary>复合字符串匹配中的 SQL 特殊字符仍应只作为参数执行。</summary>
    [Fact]
    public async Task ExplicitStringPredicate_WhenValueContainsSqlText_ShouldRemainParameterized()
    {
        using var root = _fixture.CreateQuery();
        const string value = "' OR 1=1 --";
        var query = root.From<Order>("o")
            .WhereIf<Order, string>(true, value, (o, search) =>
                o.Name.Contains(search) || o.Name.StartsWith(search) || o.Name.EndsWith(search), "o")
            .Select<Order>(o => new { o.Id });

        Assert.Equal("Select `o`.`OrderId` As `Id` \r\nFrom `sqp_orders` As `o` \r\n" +
            "Where ((`o`.`Name` Like @_p_0 Or `o`.`Name` Like @_p_1) Or `o`.`Name` Like @_p_2)", query.ToSql());
        Assert.Empty(await query.ToListAsync<Order>());
    }

    /// <summary>显式集合在开关、空集合和集合长度变化时应生成匹配 SQL 并返回对应订单。</summary>
    [Fact]
    public async Task ExplicitCollectionPredicate_WhenShapeChanges_ShouldExecuteWithRequestValues()
    {
        var one = await Read(true, new[] { 1 });
        Assert.Equal("Select `o`.`OrderId` As `Id` \r\nFrom `sqp_orders` As `o` \r\n" +
            "Where `o`.`OrderId` In (@_p_0) \r\nOrder By `o`.`OrderId`", one.Sql);
        Assert.Equal(new[] { 1 }, one.Ids);

        var two = await Read(true, new[] { 2, 4 });
        Assert.Equal("Select `o`.`OrderId` As `Id` \r\nFrom `sqp_orders` As `o` \r\n" +
            "Where `o`.`OrderId` In (@_p_0,@_p_1) \r\nOrder By `o`.`OrderId`", two.Sql);
        Assert.Equal(new[] { 2, 4 }, two.Ids);

        var empty = await Read(true, Array.Empty<int>());
        Assert.Equal("Select `o`.`OrderId` As `Id` \r\nFrom `sqp_orders` As `o` \r\n" +
            "Where 1 = 0 \r\nOrder By `o`.`OrderId`", empty.Sql);
        Assert.Empty(empty.Ids);

        var nullCollection = await Read(true, null);
        Assert.Equal(empty.Sql, nullCollection.Sql);
        Assert.Empty(nullCollection.Ids);

        var disabled = await Read(false, new[] { 1 });
        Assert.Equal("Select `o`.`OrderId` As `Id` \r\nFrom `sqp_orders` As `o` \r\n" +
            "Order By `o`.`OrderId`", disabled.Sql);
        Assert.Equal(new[] { 1, 2, 3, 4 }, disabled.Ids);

        async Task<(string Sql, int[] Ids)> Read(bool enabled, int[] ids)
        {
            using var root = _fixture.CreateQuery();
            var query = root.From<Order>("o")
                .WhereIf<Order, int[]>(enabled, ids, (o, values) => values.Contains(o.Id), "o")
                .Select<Order>(o => new { o.Id }).OrderBy<Order>(o => o.Id);
            var sql = query.ToSql();
            var results = await query.ToListAsync<Order>();
            return (sql, results.Select(item => item.Id).ToArray());
        }
    }

    /// <summary>复合集合条件及其取反应保留分组和 SQL 的 NULL 语义。</summary>
    [Fact]
    public async Task ExplicitCollectionPredicate_WhenCombinedOrNegated_ShouldPreserveGrouping()
    {
        using var root = _fixture.CreateQuery();
        var combined = root.From<Order>("o")
            .WhereIf<Order, int[]>(true, new[] { 1, 2 }, (o, values) => values.Contains(o.Id) || o.Id == 4, "o")
            .Select<Order>(o => new { o.Id }).OrderBy<Order>(o => o.Id);
        Assert.Equal("Select `o`.`OrderId` As `Id` \r\nFrom `sqp_orders` As `o` \r\n" +
            "Where (`o`.`OrderId` In (@_p_0,@_p_1) Or `o`.`OrderId`=@_p_2) \r\nOrder By `o`.`OrderId`", combined.ToSql());
        Assert.Equal(new[] { 1, 2, 4 }, (await combined.ToListAsync<Order>()).Select(item => item.Id));

        using var negatedRoot = _fixture.CreateQuery();
        var negated = negatedRoot.From<Order>("o")
            .WhereIf<Order, int[]>(true, new[] { 1 }, (o, values) => !(values.Contains(o.Id) || o.Id == 3), "o")
            .Select<Order>(o => new { o.Id }).OrderBy<Order>(o => o.Id);
        Assert.Equal("Select `o`.`OrderId` As `Id` \r\nFrom `sqp_orders` As `o` \r\n" +
            "Where Not ((`o`.`OrderId` In (@_p_0) Or `o`.`OrderId`=@_p_1)) \r\nOrder By `o`.`OrderId`", negated.ToSql());
        Assert.Equal(new[] { 2, 4 }, (await negated.ToListAsync<Order>()).Select(item => item.Id));
    }

    /// <summary>集合含空值时，IN 和 NOT IN 应遵循 SQL 的三值逻辑。</summary>
    [Fact]
    public async Task ExplicitCollectionPredicate_WhenValuesContainNull_ShouldUseSqlInNullSemantics()
    {
        using var root = _fixture.CreateQuery();
        var positive = root.From<Order>("o")
            .WhereIf<Order, int?[]>(true, new int?[] { 5, null },
                (o, values) => values.Contains(o.Quantity), "o")
            .Select<Order>(o => new { o.Id }).OrderBy<Order>(o => o.Id);
        Assert.Equal("Select `o`.`OrderId` As `Id` \r\nFrom `sqp_orders` As `o` \r\n" +
            "Where `o`.`Quantity` In (@_p_0,@_p_1) \r\nOrder By `o`.`OrderId`", positive.ToSql());
        Assert.Equal(new[] { 1 }, (await positive.ToListAsync<Order>()).Select(item => item.Id));

        var negated = root.From<Order>("o")
            .WhereIf<Order, int?[]>(true, new int?[] { 5, null },
                (o, values) => !values.Contains(o.Quantity), "o")
            .Select<Order>(o => new { o.Id }).OrderBy<Order>(o => o.Id);
        Assert.Equal("Select `o`.`OrderId` As `Id` \r\nFrom `sqp_orders` As `o` \r\n" +
            "Where Not (`o`.`Quantity` In (@_p_0,@_p_1)) \r\nOrder By `o`.`OrderId`", negated.ToSql());
        Assert.Empty(await negated.ToListAsync<Order>());
    }

    /// <summary>同一生成调用点并发处理不同集合形状时应隔离参数值和关闭条件。</summary>
    [Fact]
    public async Task ExplicitCollectionPredicate_WhenRequestsRunConcurrently_ShouldIsolateValuesAndShapes()
    {
        var requests = await Task.WhenAll(Enumerable.Range(0, 16).Select(index => Task.Run(async () =>
        {
            var enabled = index % 4 != 0;
            var ids = (index % 4) switch
            {
                0 => new[] { 1 },
                1 => new[] { 2 },
                2 => new[] { 1, 4 },
                _ => Array.Empty<int>()
            };
            using var root = _fixture.CreateQuery();
            var query = root.From<Order>("o")
                .WhereIf<Order, int[]>(enabled, ids, (o, values) => values.Contains(o.Id), "o")
                .Select<Order>(o => new { o.Id }).OrderBy<Order>(o => o.Id);
            var sql = query.ToSql();
            var results = await query.ToListAsync<Order>();
            return (Sql: sql, Ids: results.Select(item => item.Id).ToArray(), Enabled: enabled, Values: ids);
        })));

        for (var index = 0; index < requests.Length; index++)
        {
            var request = requests[index];
            var expectedSql = "Select `o`.`OrderId` As `Id` \r\nFrom `sqp_orders` As `o`";
            if (request.Enabled)
            {
                var condition = request.Values.Length == 0 ? "1 = 0" : "`o`.`OrderId` In (" +
                    string.Join(",", Enumerable.Range(0, request.Values.Length).Select(value => "@_p_" + value)) + ")";
                expectedSql += " \r\nWhere " + condition;
            }
            expectedSql += " \r\nOrder By `o`.`OrderId`";
            Assert.Equal(expectedSql, request.Sql);
            Assert.Equal(request.Enabled ? request.Values : new[] { 1, 2, 3, 4 }, request.Ids);
        }
    }

    /// <summary>左侧显式空值应生成 IS NULL 并在 SQLite 返回匹配行。</summary>
    [Fact]
    public async Task ExplicitPredicate_WhenNullIsOnLeft_ShouldExecuteNullComparison()
    {
        using var root = _fixture.CreateQuery();
        var query = root.From<Order>("o")
            .Where<Order, int?>(null, (o, value) => value == o.Quantity, "o")
            .Select<Order>(o => new { o.Id });

        Assert.Equal("Select `o`.`OrderId` As `Id` \r\nFrom `sqp_orders` As `o` \r\nWhere `o`.`Quantity` Is Null", query.ToSql());
        Assert.Equal(new[] { 3 }, (await query.ToListAsync<Order>()).Select(item => item.Id));
    }

    /// <summary>取反复合条件应在 SQLite 保留空值条件、参数和实际结果。</summary>
    [Fact]
    public async Task GeneratedPredicate_WhenNegated_ShouldExecuteWithNullAndGrouping()
    {
        using var root = _fixture.CreateQuery();
        var query = root.From<Order>("o").Select<Order>(o => new { o.Id });
        var quantity = new SqlGeneratedColumn(0, "Quantity", null, typeof(int?), null, false);
        var predicate = SqlGeneratedPredicate.Not(SqlGeneratedPredicate.Or(
            SqlGeneratedPredicate.Compare(quantity, null, Operator.Equal, null),
            SqlGeneratedPredicate.Compare(PredicateId, null, Operator.GreaterEqual, 3)));
        SqlGeneratedQueryBridge.WherePredicate(query, new[] { typeof(Order) }, new[] { "o" }, null, predicate);
        query.OrderBy<Order>(o => o.Id);

        Assert.Equal("Select `o`.`OrderId` As `Id` \r\nFrom `sqp_orders` As `o` \r\nWhere Not ((`o`.`Quantity` Is Null Or `o`.`OrderId`>=@_p_1)) \r\nOrder By `o`.`OrderId`", query.ToSql());
        var results = await query.ToListAsync<Order>();
        Assert.Equal(new[] { 1, 2 }, results.Select(item => item.Id));
    }

    /// <summary>SQL 对空值取反仍为未知；显式空值分支可将空值包含在结果中。</summary>
    [Fact]
    public async Task GeneratedPredicate_WhenNegatedComparisonHasNulls_ShouldUseSqlNullSemantics()
    {
        var quantity = new SqlGeneratedColumn(0, "Quantity", null, typeof(int?), null, false);
        var greaterOrEqual = SqlGeneratedPredicate.Compare(quantity, null, Operator.GreaterEqual, 5);
        using var root = _fixture.CreateQuery();
        var withoutNulls = root.From<Order>("o").Select<Order>(o => new { o.Id });
        SqlGeneratedQueryBridge.WherePredicate(withoutNulls, new[] { typeof(Order) }, new[] { "o" }, null,
            SqlGeneratedPredicate.Not(greaterOrEqual));
        withoutNulls.OrderBy<Order>(o => o.Id);
        Assert.Equal("Select `o`.`OrderId` As `Id` \r\nFrom `sqp_orders` As `o` \r\nWhere Not (`o`.`Quantity`>=@_p_0) \r\nOrder By `o`.`OrderId`", withoutNulls.ToSql());
        Assert.Equal(new[] { 4 }, (await withoutNulls.ToListAsync<Order>()).Select(item => item.Id));

        var withNulls = root.From<Order>("o").Select<Order>(o => new { o.Id });
        SqlGeneratedQueryBridge.WherePredicate(withNulls, new[] { typeof(Order) }, new[] { "o" }, null,
            SqlGeneratedPredicate.Or(SqlGeneratedPredicate.Compare(quantity, null, Operator.Equal, null),
                SqlGeneratedPredicate.Not(greaterOrEqual)));
        withNulls.OrderBy<Order>(o => o.Id);
        Assert.Equal("Select `o`.`OrderId` As `Id` \r\nFrom `sqp_orders` As `o` \r\nWhere (`o`.`Quantity` Is Null Or Not (`o`.`Quantity`>=@_p_1)) \r\nOrder By `o`.`OrderId`", withNulls.ToSql());
        Assert.Equal(new[] { 3, 4 }, (await withNulls.ToListAsync<Order>()).Select(item => item.Id));
    }

    /// <summary>并发查询复用逻辑条件，同时按各自别名和请求值绑定 SQL 参数。</summary>
    [Fact]
    public async Task GeneratedPredicate_WhenRequestsRunConcurrently_ShouldIsolateAliasesAndValues()
    {
        var requests = Enumerable.Range(0, 16).Select(index => Task.Run(() =>
        {
            var alias = index % 2 == 0 ? "a" : "b";
            var enabled = index % 4 < 2;
            var minimum = index % 2 + 1;
            using var root = _fixture.CreateQuery();
            var query = root.From<Order>(alias).Select<Order>(o => new { o.Id });
            if (enabled)
                SqlGeneratedQueryBridge.WherePredicate(query, new[] { typeof(Order) }, new[] { "o" },
                    new[] { alias }, GeneratedOrderFilter);
            query.Where<Order, int>(o => o.Id, minimum, Operator.GreaterEqual).OrderBy<Order>(o => o.Id);

            var prefix = $"Select `{alias}`.`OrderId` As `Id` \r\nFrom `sqp_orders` As `{alias}` \r\nWhere ";
            var filter = enabled
                ? $"`{alias}`.`OrderId`>=@_p_0 And (`{alias}`.`OrderId`=@_p_1 Or `{alias}`.`OrderId`=@_p_2) And "
                : string.Empty;
            var expected = prefix + filter + $"`{alias}`.`OrderId`>=@_p_{(enabled ? 3 : 0)} \r\nOrder By `{alias}`.`OrderId`";
            Assert.Equal(expected, query.ToSql());
            var ids = query.ToList<Order>().Select(item => item.Id).ToArray();
            Assert.Equal(Enumerable.Range(minimum, (enabled ? 2 : 4) - minimum + 1), ids);
        }));

        await Task.WhenAll(requests);
    }

    /// <summary>字符串匹配与数值条件组合后应在 SQLite 保留 SQL 优先级及结果。</summary>
    [Fact]
    public async Task GeneratedPredicate_WhenStringMatchesAreCombined_ShouldExecuteWithCorrectGrouping()
    {
        using var root = _fixture.CreateQuery();
        var query = root.From<Order>("o").Select<Order>(o => new { o.Id });
        var name = new SqlGeneratedColumn(0, "Name", null, typeof(string), null, false);
        var predicate = SqlGeneratedPredicate.And(
            SqlGeneratedPredicate.Compare(PredicateId, null, Operator.GreaterEqual, 1),
            SqlGeneratedPredicate.Or(
                SqlGeneratedPredicate.Compare(name, null, Operator.Contains, "bulk"),
                SqlGeneratedPredicate.And(
                    SqlGeneratedPredicate.Compare(name, null, Operator.Starts, "orp"),
                    SqlGeneratedPredicate.Compare(name, null, Operator.Ends, "han"))));
        SqlGeneratedQueryBridge.WherePredicate(query, new[] { typeof(Order) }, new[] { "o" }, null, predicate);
        query.OrderBy<Order>(o => o.Id);

        Assert.Equal("Select `o`.`OrderId` As `Id` \r\nFrom `sqp_orders` As `o` \r\nWhere `o`.`OrderId`>=@_p_0 And (`o`.`Name` Like @_p_1 Or `o`.`Name` Like @_p_2 And `o`.`Name` Like @_p_3) \r\nOrder By `o`.`OrderId`", query.ToSql());
        var results = await query.ToListAsync<Order>();
        Assert.Equal(new[] { 1, 2, 4 }, results.Select(item => item.Id));
    }
}
