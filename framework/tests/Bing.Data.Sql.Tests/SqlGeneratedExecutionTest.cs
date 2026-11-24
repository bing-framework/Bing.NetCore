using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.Common;
using Bing.Data;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;
using Moq;
using Xunit;

namespace Bing.Data.Sql.Tests;

/// <summary>
/// 验证生成式查询列映射与布局校验的测试。
/// </summary>
/// <remarks>
/// 生成式执行必须按真实结果列映射，并在执行前拒绝未知布局。
/// </remarks>
public class SqlGeneratedExecutionTest
{
    /// <summary>绑定阶段只调用一次工厂，逐行直接读取；旧包装回调仍保留。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ToBoundList_ShouldBindOrdinalsOnceForAllRows(bool asynchronous)
    {
        var executor = new Mock<ISqlQueryPlanExecutor>();
        var generated = executor.As<ISqlGeneratedPlanExecutor>();
        List<Row> Read(SqlQueryPlan plan, Func<DbDataReader, Row> read, int? timeout)
        {
            using var table = new DataTable(); table.Columns.Add("Id", typeof(int));
            table.Rows.Add(1); table.Rows.Add(2);
            using var reader = table.CreateDataReader();
            var rows = new List<Row>(); while (reader.Read()) rows.Add(read(reader)); return rows;
        }
        generated.Setup(item => item.ToGeneratedList(It.IsAny<SqlQueryPlan>(), It.IsAny<Func<DbDataReader, Row>>(), null))
            .Returns((SqlQueryPlan plan, Func<DbDataReader, Row> read, int? timeout) => Read(plan, read, timeout));
        generated.Setup(item => item.ToGeneratedListAsync(It.IsAny<SqlQueryPlan>(), It.IsAny<Func<DbDataReader, Row>>(), null, default))
            .Returns((SqlQueryPlan plan, Func<DbDataReader, Row> read, int? timeout, CancellationToken _) => Task.FromResult(Read(plan, read, timeout)));
        var builder = new TestSqlBuilder(); builder.From<Row>("r");
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(executor.Object, builder).Select<Row>(r => new { r.Id });
        var binds = 0;
        Func<IReadOnlyDictionary<string, int>, Func<DbDataReader, Row>> bind = ordinals =>
        { binds++; var ordinal = ordinals["Id"]; return reader => new Row { Id = reader.GetInt32(ordinal) }; };
        var rows = asynchronous ? await SqlGeneratedExecution.ToBoundListAsync(query, bind, Binder, null, default)
            : SqlGeneratedExecution.ToBoundList(query, bind, Binder, null);
        Assert.Equal(1, binds); Assert.Equal(new[] { 1, 2 }, rows.Select(row => row.Id));
    }

    /// <summary>生成式 DTO 单行终结优先使用专用读取器，避免先物化完整列表。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ToBoundSinglePrototype_ShouldUseSingleExecutor(bool asynchronous)
    {
        var executor = new Mock<ISqlQueryPlanExecutor>();
        var generated = executor.As<ISqlGeneratedPlanExecutor>();
        var single = executor.As<ISqlGeneratedSinglePlanExecutor>();
        var listCalls = 0;
        generated.Setup(item => item.ToGeneratedList(It.IsAny<SqlQueryPlan>(), It.IsAny<Func<DbDataReader, Row>>(), null))
            .Callback(() => listCalls++).Returns(new List<Row>());
        generated.Setup(item => item.ToGeneratedListAsync(It.IsAny<SqlQueryPlan>(), It.IsAny<Func<DbDataReader, Row>>(), null, It.IsAny<CancellationToken>()))
            .Callback(() => listCalls++).Returns(Task.FromResult(new List<Row>()));
        Row Execute(Func<DbDataReader, Row> read)
        {
            using var table = new DataTable();
            table.Columns.Add("Id", typeof(int));
            table.Rows.Add(7);
            using var reader = table.CreateDataReader();
            reader.Read();
            return read(reader);
        }
        single.Setup(item => item.ToGeneratedSingle(It.IsAny<SqlQueryPlan>(), It.IsAny<Func<DbDataReader, Row>>(), null))
            .Returns((SqlQueryPlan _, Func<DbDataReader, Row> read, int? _) => Execute(read));
        single.Setup(item => item.ToGeneratedSingleAsync(It.IsAny<SqlQueryPlan>(), It.IsAny<Func<DbDataReader, Row>>(), null, It.IsAny<CancellationToken>()))
            .Returns((SqlQueryPlan _, Func<DbDataReader, Row> read, int? _, CancellationToken _) => Task.FromResult(Execute(read)));

        var builder = new TestSqlBuilder();
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(executor.Object, builder)
            .From<Row>("r");
        Func<IReadOnlyDictionary<string, int>, Func<DbDataReader, Row>> bind = ordinals =>
        {
            var ordinal = ordinals["Id"];
            return reader => new Row { Id = reader.GetInt32(ordinal) };
        };
        var result = asynchronous
            ? await SqlGeneratedExecution.ToBoundSinglePrototypeAsync(query, new Row(), new[] { "Id" }, bind, Binder, null, default)
            : SqlGeneratedExecution.ToBoundSinglePrototype(query, new Row(), new[] { "Id" }, bind, Binder, null);

        Assert.Equal(7, result.Id);
        Assert.Equal(0, listCalls);
        if (asynchronous)
            single.Verify(item => item.ToGeneratedSingleAsync(It.IsAny<SqlQueryPlan>(), It.IsAny<Func<DbDataReader, Row>>(), null,
                It.IsAny<CancellationToken>()), Times.Once);
        else
            single.Verify(item => item.ToGeneratedSingle(It.IsAny<SqlQueryPlan>(), It.IsAny<Func<DbDataReader, Row>>(), null), Times.Once);
    }

    /// <summary>普通单类型终结直接使用专用生成读取器，不依赖投影原型。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ToBoundSingle_ShouldUseSingleExecutor(bool asynchronous)
    {
        var executor = new Mock<ISqlQueryPlanExecutor>();
        var generated = executor.As<ISqlGeneratedPlanExecutor>();
        var single = executor.As<ISqlGeneratedSinglePlanExecutor>();
        var listCalls = 0;
        generated.Setup(item => item.ToGeneratedList(It.IsAny<SqlQueryPlan>(), It.IsAny<Func<DbDataReader, Row>>(), null))
            .Callback(() => listCalls++).Returns(new List<Row>());
        generated.Setup(item => item.ToGeneratedListAsync(It.IsAny<SqlQueryPlan>(), It.IsAny<Func<DbDataReader, Row>>(), null, It.IsAny<CancellationToken>()))
            .Callback(() => listCalls++).Returns(Task.FromResult(new List<Row>()));
        Row Execute(Func<DbDataReader, Row> read)
        {
            using var table = new DataTable();
            table.Columns.Add("Id", typeof(int));
            table.Rows.Add(11);
            using var reader = table.CreateDataReader();
            reader.Read();
            return read(reader);
        }
        single.Setup(item => item.ToGeneratedSingle(It.IsAny<SqlQueryPlan>(), It.IsAny<Func<DbDataReader, Row>>(), null))
            .Returns((SqlQueryPlan _, Func<DbDataReader, Row> read, int? _) => Execute(read));
        single.Setup(item => item.ToGeneratedSingleAsync(It.IsAny<SqlQueryPlan>(), It.IsAny<Func<DbDataReader, Row>>(), null, It.IsAny<CancellationToken>()))
            .Returns((SqlQueryPlan _, Func<DbDataReader, Row> read, int? _, CancellationToken _) => Task.FromResult(Execute(read)));

        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(executor.Object, new TestSqlBuilder())
            .From<Row>("r");
        Func<IReadOnlyDictionary<string, int>, Func<DbDataReader, Row>> bind = ordinals =>
        {
            var ordinal = ordinals.TryGetValue("Id", out var propertyOrdinal)
                ? propertyOrdinal
                : ordinals["physical_id"];
            return reader => new Row { Id = reader.GetInt32(ordinal) };
        };
        var result = asynchronous
            ? await SqlGeneratedExecution.ToBoundSingleAsync(query, bind, Binder, null, default)
            : SqlGeneratedExecution.ToBoundSingle(query, bind, Binder, null);

        Assert.Equal(11, result.Id);
        Assert.Equal(0, listCalls);
        if (asynchronous)
            single.Verify(item => item.ToGeneratedSingleAsync(It.IsAny<SqlQueryPlan>(), It.IsAny<Func<DbDataReader, Row>>(), null,
                It.IsAny<CancellationToken>()), Times.Once);
        else
            single.Verify(item => item.ToGeneratedSingle(It.IsAny<SqlQueryPlan>(), It.IsAny<Func<DbDataReader, Row>>(), null), Times.Once);
    }

    /// <summary>双分段各绑定一次，外连接空分段不能触发第二读取器。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ToBoundList_MultipleSegments_ShouldKeepNullBoundaryAndBindOnce(bool asynchronous)
    {
        var executor = new Mock<ISqlQueryPlanExecutor>();
        var generated = executor.As<ISqlGeneratedPlanExecutor>();
        List<string> Execute(Func<DbDataReader, string> read)
        {
            using var table = new DataTable();
            table.Columns.Add("first", typeof(int)); table.Columns.Add("second", typeof(int));
            table.Rows.Add(1, DBNull.Value); table.Rows.Add(2, 20);
            using var reader = table.CreateDataReader();
            var result = new List<string>(); while (reader.Read()) result.Add(read(reader)); return result;
        }
        generated.Setup(item => item.ToGeneratedList(It.IsAny<SqlQueryPlan>(), It.IsAny<Func<DbDataReader, string>>(), null))
            .Returns((SqlQueryPlan _, Func<DbDataReader, string> read, int? _) => Execute(read));
        generated.Setup(item => item.ToGeneratedListAsync(It.IsAny<SqlQueryPlan>(), It.IsAny<Func<DbDataReader, string>>(), null, default))
            .Returns((SqlQueryPlan _, Func<DbDataReader, string> read, int? _, CancellationToken _) => Task.FromResult(Execute(read)));
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(executor.Object, new TestSqlBuilder())
            .From<Row>().SelectAll().LeftJoin<Row, Row>((first, second) => first.Id == second.Id);
        var firstBinds = 0; var secondBinds = 0; var secondReads = 0;
        Func<IReadOnlyDictionary<string, int>, Func<DbDataReader, Row>> first = ordinals =>
        { firstBinds++; var index = ordinals["Id"]; return reader => new Row { Id = reader.GetInt32(index) }; };
        Func<IReadOnlyDictionary<string, int>, Func<DbDataReader, Row>> second = ordinals =>
        { secondBinds++; var index = ordinals["Id"]; return reader => { secondReads++; return new Row { Id = reader.GetInt32(index) }; }; };
        Func<Row, Row, string> map = (left, right) => left.Id + ":" + (right?.Id.ToString() ?? "null");
        var rows = asynchronous
            ? await SqlGeneratedExecution.ToBoundListAsync(query, first, second, map, Binder, null, default)
            : SqlGeneratedExecution.ToBoundList(query, first, second, map, Binder, null);
        Assert.Equal(new[] { "1:null", "2:20" }, rows);
        Assert.Equal(1, firstBinds); Assert.Equal(1, secondBinds); Assert.Equal(1, secondReads);
    }

    /// <summary>
    /// 测试目的：First/Single 终结应优先使用专用基数执行器，避免列表物化回退。
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Terminal_ShouldPreferSpecializedExecutor(bool asynchronous)
    {
        var executor = new Mock<ISqlQueryPlanExecutor>();
        var list = executor.As<ISqlGeneratedPlanExecutor>();
        var terminal = executor.As<ISqlGeneratedTerminalPlanExecutor>();
        var listCalls = 0;
        list.Setup(item => item.ToGeneratedList(It.IsAny<SqlQueryPlan>(), It.IsAny<Func<DbDataReader, Row>>(), null))
            .Callback(() => listCalls++).Returns(new List<Row>());
        list.Setup(item => item.ToGeneratedListAsync(It.IsAny<SqlQueryPlan>(), It.IsAny<Func<DbDataReader, Row>>(), null,
                It.IsAny<CancellationToken>()))
            .Callback(() => listCalls++).Returns(Task.FromResult(new List<Row>()));

        Row Execute(Func<DbDataReader, Row> read)
        {
            using var table = new DataTable();
            table.Columns.Add("Id", typeof(int));
            table.Rows.Add(17);
            using var reader = table.CreateDataReader();
            reader.Read();
            return read(reader);
        }

        terminal.Setup(item => item.ToGeneratedTerminal(It.IsAny<SqlQueryPlan>(), It.IsAny<Func<DbDataReader, Row>>(),
                SqlGeneratedTerminal.First, null))
            .Returns((SqlQueryPlan _, Func<DbDataReader, Row> read, SqlGeneratedTerminal _, int? _) => Execute(read));
        terminal.Setup(item => item.ToGeneratedTerminalAsync(It.IsAny<SqlQueryPlan>(), It.IsAny<Func<DbDataReader, Row>>(),
                SqlGeneratedTerminal.Single, null, It.IsAny<CancellationToken>()))
            .Returns((SqlQueryPlan _, Func<DbDataReader, Row> read, SqlGeneratedTerminal _, int? _, CancellationToken _) =>
                Task.FromResult(Execute(read)));

        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(executor.Object, new TestSqlBuilder())
            .From<Row>("r").Select<Row>(row => new { row.Id });
        Func<IReadOnlyDictionary<string, int>, Func<DbDataReader, Row>> bind = ordinals =>
        {
            var ordinal = ordinals["Id"];
            return reader => new Row { Id = reader.GetInt32(ordinal) };
        };

        var result = asynchronous
            ? await SqlGeneratedExecution.SingleAsync(query, bind, Binder, null, default)
            : SqlGeneratedExecution.First(query, bind, Binder, null);

        Assert.Equal(17, result.Id);
        Assert.Equal(0, listCalls);
        if (asynchronous)
            terminal.Verify(item => item.ToGeneratedTerminalAsync(It.IsAny<SqlQueryPlan>(), It.IsAny<Func<DbDataReader, Row>>(),
                SqlGeneratedTerminal.Single, null, It.IsAny<CancellationToken>()), Times.Once);
        else
            terminal.Verify(item => item.ToGeneratedTerminal(It.IsAny<SqlQueryPlan>(), It.IsAny<Func<DbDataReader, Row>>(),
                SqlGeneratedTerminal.First, null), Times.Once);
    }

    /// <summary>
    /// 测试目的：没有专用执行器时，基数终结回退仍保持 First/Single 的异常语义。
    /// </summary>
    [Fact]
    public void TerminalFallback_ShouldPreserveCardinalitySemantics()
    {
        var executor = new Mock<ISqlQueryPlanExecutor>();
        var generated = executor.As<ISqlGeneratedPlanExecutor>();
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(executor.Object, new TestSqlBuilder())
            .From<Row>("r").Select<Row>(row => new { row.Id });
        Func<IReadOnlyDictionary<string, int>, Func<DbDataReader, Row>> bind = _ => _ => new Row();

        generated.Setup(item => item.ToGeneratedList(It.IsAny<SqlQueryPlan>(), It.IsAny<Func<DbDataReader, Row>>(), null))
            .Returns(new List<Row>());
        Assert.Throws<InvalidOperationException>(() => SqlGeneratedExecution.First(query, bind, Binder, null));
        Assert.Null(SqlGeneratedExecution.FirstOrDefault(query, bind, Binder, null));

        generated.Setup(item => item.ToGeneratedList(It.IsAny<SqlQueryPlan>(), It.IsAny<Func<DbDataReader, Row>>(), null))
            .Returns(new List<Row> { new(), new() });
        Assert.Throws<InvalidOperationException>(() => SqlGeneratedExecution.Single(query, bind, Binder, null));
        Assert.Throws<InvalidOperationException>(() => SqlGeneratedExecution.SingleOrDefault(query, bind, Binder, null));
    }

    /// <summary>
    /// 测试目的：标量终结应复用冻结计划和生成参数绑定器，不要求逐行生成执行器。
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Scalar_ShouldUseGeneratedPlanAndPreserveAsyncCancellation(bool asynchronous)
    {
        var executor = new Mock<ISqlQueryPlanExecutor>();
        SqlQueryPlan captured = null;
        executor.Setup(item => item.Scalar<int>(It.IsAny<SqlQueryPlan>(), 7))
            .Returns((SqlQueryPlan plan, int? _) => { captured = plan; return 42; });
        executor.Setup(item => item.ScalarAsync<int>(It.IsAny<SqlQueryPlan>(), 7, It.IsAny<CancellationToken>()))
            .Returns((SqlQueryPlan plan, int? _, CancellationToken _) => { captured = plan; return Task.FromResult(42); });
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(executor.Object, new TestSqlBuilder())
            .From<Row>("r").Select<Row>(row => row.Id);

        var result = asynchronous
            ? await SqlGeneratedExecution.ScalarAsync<int>(query, Binder, 7, default)
            : SqlGeneratedExecution.Scalar<int>(query, Binder, 7);

        Assert.Equal(42, result);
        Assert.NotNull(captured);
        Assert.True(captured.IsGeneratedExecution);
        Assert.Same(Binder, captured.GeneratedParameterBinder);
    }

    /// <summary>
    /// 测试目的：分页终结应复用生成计划并把 Pager 交给 Provider 分页执行器。
    /// </summary>
    [Fact]
    public void ToPage_ShouldUseGeneratedPlanAndProviderPagerExecutor()
    {
        var executor = new Mock<ISqlQueryPlanExecutor>();
        SqlQueryPlan captured = null;
        var expected = new PagerList<Row>(1, 10, 1, new[] { new Row { Id = 9 } });
        executor.Setup(item => item.ToPage<Row>(It.IsAny<SqlQueryPlan>(), It.IsAny<IPager>(), null))
            .Returns((SqlQueryPlan plan, IPager _, int? _) => { captured = plan; return expected; });
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(executor.Object, new TestSqlBuilder()).From<Row>("r");
        var pager = new Pager(1, 10, "Id");

        var result = SqlGeneratedExecution.ToPage<Row>(query, pager, Binder, null);

        Assert.Same(expected, result);
        Assert.NotNull(captured);
        Assert.True(captured.IsGeneratedExecution);
        Assert.Same(Binder, captured.GeneratedParameterBinder);
    }

    /// <summary>生成式分页也应在原查询作用域排序，并保留静态绑定器。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ToPage_ShouldPreserveSourceScopeForGeneratedPlan(bool asynchronous)
    {
        var executor = new Mock<ISqlQueryPlanExecutor>();
        SqlQueryPlan captured = null;
        var expected = new PagerList<Row>(1, 1, 0, Array.Empty<Row>());
        executor.Setup(item => item.ToPage<Row>(It.IsAny<SqlQueryPlan>(), It.IsAny<IPager>(), null))
            .Returns((SqlQueryPlan plan, IPager _, int? _) => { captured = plan; return expected; });
        executor.Setup(item => item.ToPageAsync<Row>(It.IsAny<SqlQueryPlan>(), It.IsAny<IPager>(), null,
                It.IsAny<CancellationToken>()))
            .Returns((SqlQueryPlan plan, IPager _, int? _, CancellationToken _) =>
            { captured = plan; return Task.FromResult(expected); });
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(executor.Object, new TestSqlBuilder())
            .From<Row>("r").Select<Row>(row => new { row.Id });
        var pager = new Pager(1, 1, "physical_id");

        if (asynchronous)
            await SqlGeneratedExecution.ToPageAsync<Row>(query, pager, Binder, null, default);
        else
            SqlGeneratedExecution.ToPage<Row>(query, pager, Binder, null);

        Assert.True(captured.IsGeneratedExecution);
        Assert.Same(Binder, captured.GeneratedParameterBinder);
        var page = SqlBuilderRuntimeBridge.CreatePagePlan(captured, pager, new TestSqlBuilder());
        Assert.Equal("Select [r].[physical_id] As [Id] \r\nFrom [rows] As [r] \r\nOrder By [physical_id] \r\nOffset @_p_0 Rows Fetch Next @_p_1 Rows Only", page.CommandText);
    }

    /// <summary>
    /// 保存生成执行测试使用的空参数绑定器。
    /// </summary>
    private static readonly Action<DbCommand, IEnumerable<SqlParam>> Binder = (_, _) => { };

    /// <summary>
    /// 验证无结果别名的映射列按实际 SQL 列名读取。
    /// </summary>
    [Fact]
    public void ToList_WhenMappedColumnHasNoResultAlias_ShouldUseActualSqlResultName()
    {
        var executor = new Mock<ISqlQueryPlanExecutor>();
        var generated = executor.As<ISqlGeneratedPlanExecutor>();
        SqlQueryPlan captured = null;
        generated.Setup(item => item.ToGeneratedList(It.IsAny<SqlQueryPlan>(),
                It.IsAny<Func<DbDataReader, Row>>(), 13))
            .Returns((SqlQueryPlan plan, Func<DbDataReader, Row> read, int? timeout) =>
            {
                captured = plan;
                using var table = new DataTable();
                table.Columns.Add("physical_id", typeof(int));
                table.Rows.Add(42);
                using var reader = table.CreateDataReader();
                reader.Read();
                return new List<Row> { read(reader) };
            });
        var builder = new TestSqlBuilder();
        builder.From<Row>("r");
        builder.Select<Row>();
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(executor.Object, builder);
        var values = SqlGeneratedExecution.ToList(query, row =>
            new Row { Id = row.TryGetOrdinal("Id", out var index) ? row.Reader.GetInt32(index) : 0 }, Binder, 13);
        Assert.Equal(0, values[0].Id); // 与普通 Dapper 对未起别名的物理列保持一致。
        Assert.False(captured.IsBuilderPlan);
        Assert.NotNull(captured.QueryIr);
        Assert.Equal("Select [r].[physical_id] \r\nFrom [rows] As [r]", captured.CommandText);
        Assert.True(captured.IsGeneratedExecution);
        Assert.Same(Binder, captured.GeneratedParameterBinder);
    }

    /// <summary>
    /// 验证旧列与追加投影混用在执行前被拒绝且允许修正。
    /// </summary>
    [Fact]
    public void ToList_WhenLegacyColumnsAreMixedWithAppend_ShouldRejectBeforeExecutorAndAllowCorrection()
    {
        var executor = new Mock<ISqlQueryPlanExecutor>();
        executor.As<ISqlGeneratedPlanExecutor>();
        var builder = new TestSqlBuilder();
        builder.From<Row>("r");
        builder.Select<Row>();
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(executor.Object, builder)
            .AppendSelect<Row>(r => new object[] { r.Id });
        Assert.Throws<NotSupportedException>(() => SqlGeneratedExecution.ToList(query, _ => new Row(), Binder, null));
        executor.VerifyNoOtherCalls();
        query.ReplaceSelect<Row>(r => new { r.Id });
        Assert.Equal("Select [r].[physical_id] As [Id] \r\nFrom [rows] As [r]", query.ToSql());
    }

    /// <summary>
    /// 验证生成桥接与运行时在相同输入下产生相同 SQL 和参数。
    /// </summary>
    [Fact]
    public void Compare_WhenGeneratedBridgeAndRuntimeUseSameInput_ShouldMatchSqlAndParameters()
    {
        var executor = new Mock<ISqlQueryPlanExecutor>();
        SqlLambdaQuery Create() => SqlQueryRuntimeFactory.CreateLambdaQuery(executor.Object, new TestSqlBuilder()).From<Row>();
        var runtime = Create().Where<Row>(r => r.Id > 42).Select<Row>(r => new { r.Id });
        var generated = Create();
        var column = new SqlGeneratedColumn(0, "Id", null, typeof(int), null, false);
        SqlGeneratedQueryBridge.Compare(generated, false, new[] { typeof(Row) }, new[] { "r" }, null, column, null, Operator.Greater, 42);
        SqlGeneratedQueryBridge.Project(generated, "Select", new[] { typeof(Row) }, new[] { "r" }, null,
            new[] { new SqlGeneratedColumn(0, "Id", "Id", typeof(int), null, false) });
        const string expected = "Select [r].[physical_id] As [Id] \r\nFrom [rows] As [r] \r\nWhere [r].[physical_id]>@_p_0";
        Assert.Equal(expected, runtime.ToSql());
        Assert.Equal(expected, generated.ToSql());
        var a = ((ISqlCommonPartAccessor)runtime.GeneratedCore.ReadBuilder).ParameterManager.GetParams();
        var b = ((ISqlCommonPartAccessor)generated.GeneratedCore.ReadBuilder).ParameterManager.GetParams();
        Assert.Equal(a, b);
    }

    /// <summary>
    /// 提供生成查询执行测试使用的查询数据模型。
    /// </summary>
    [Table("rows")]
    public class Row {
        /// <summary>
        /// 获取或设置记录标识。
        /// </summary>
        [Column("physical_id")] public int Id { get; set; } }
}
