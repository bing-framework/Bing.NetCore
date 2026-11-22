using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests.Builders;

/// <summary>
/// 专用 Insert Builder 的 Insert Select 测试。
/// </summary>
public class InsertSelectBuilderTest
{
    /// <summary>Insert Select 来源中的嵌套 Builder 应保留两个独立物理参数。</summary>
    [Fact]
    public void InsertSelect_WhenNestedSourceKeysDiffer_ShouldKeepBothParameters()
    {
        var first = new SqlQueryFragment("select @id as Id", new[] { new SqlParam("id", 7) })
            .WithParameterKey("id", new SqlParameterKey("first", "id"));
        var second = new SqlQueryFragment("select @id as Id", new[] { new SqlParam("id", 7) })
            .WithParameterKey("id", new SqlParameterKey("second", "id"));
        var child = CreateQueryBuilder().Select("Id").From(second, "leaf");
        var query = CreateQueryBuilder().Select("one.Id").From(first, "one").Join(child, "two")
            .AppendOn("[one].[Id]=[two].[Id]");
        var builder = CreateInsertBuilder().InsertInto("archive_orders").Columns("Id").InsertSelect(query);

        var firstCommand = builder.BuildCommand();
        var secondCommand = builder.BuildCommand();
        const string expected = "Insert Into [archive_orders] ([Id]) \r\nSelect [one].[Id] \r\nFrom (select @id as Id) As [one] \r\nJoin (Select [Id] \r\nFrom (select @_p_0 as Id) As [leaf]) As [two] On [one].[Id]=[two].[Id]";
        Assert.Equal(expected, firstCommand.Sql);
        Assert.Equal(expected, secondCommand.Sql);
        Assert.Equal(new object[] { 7, 7 }, firstCommand.Parameters.Select(parameter => parameter.Value));
        Assert.Equal(new object[] { 7, 7 }, secondCommand.Parameters.Select(parameter => parameter.Value));
    }

    /// <summary>
    /// 测试目的：Insert Select 应按固定 Clause 顺序生成完整 SQL。
    /// </summary>
    [Fact]
    public void InsertSelect_WhenBasicQueryIsConfigured_ShouldGenerateExpectedSql()
    {
        var query = CreateQueryBuilder().Select("Id").AppendSelect("[Code]").From("orders");
        var builder = CreateInsertBuilder().InsertInto("archive_orders").Columns("Id", "Code").InsertSelect(query);

        Assert.Equal("Insert Into [archive_orders] ([Id], [Code]) \r\nSelect [Id],[Code] \r\nFrom [orders]", builder.ToSql());
        Assert.Equal(SqlOperationKind.InsertSelect, builder.BuildCommand().OperationKind);
    }

    /// <summary>
    /// 测试目的：Insert Select 应合并来源查询参数并维持其参数值。
    /// </summary>
    [Fact]
    public void InsertSelect_WhenWhereIsConfigured_ShouldGenerateParameterizedSql()
    {
        var query = CreateQueryBuilder().Select("Id").AppendSelect("[Code]").From("orders").Where("Status", "active");
        var builder = CreateInsertBuilder().InsertInto("archive_orders").Columns("Id", "Code").InsertSelect(query);

        Assert.Equal("Insert Into [archive_orders] ([Id], [Code]) \r\nSelect [Id],[Code] \r\nFrom [orders] \r\nWhere [Status]=@_p_0", builder.ToSql());
        Assert.Equal("active", builder.BuildCommand().Parameters.Single().Value);
    }

    /// <summary>
    /// 测试目的：Insert Select 应保留 Join、Group By、Having 和 Order By 查询语义。
    /// </summary>
    [Fact]
    public void InsertSelect_WhenJoinAndGroupAreConfigured_ShouldGenerateExpectedSql()
    {
        var query = CreateQueryBuilder()
            .Select("o.Status")
            .CountColumn("d.Id", "Total")
            .From("orders", "o")
            .Join("order_details", "d")
            .AppendOn("[o].[Id]=[d].[OrderId]")
            .GroupBy("o.Status")
            .HavingRaw("Count(d.Id) > 0")
            .OrderBy("o.Status");
        var builder = CreateInsertBuilder().InsertInto("archive_orders").Columns("Status", "Total").InsertSelect(query);

        Assert.Equal("Insert Into [archive_orders] ([Status], [Total]) \r\nSelect [o].[Status],Count([d].[Id]) As [Total] \r\nFrom [orders] As [o] \r\nJoin [order_details] As [d] On [o].[Id]=[d].[OrderId] \r\nGroup By [o].[Status] Having Count(d.Id) > 0 \r\nOrder By [o].[Status]", builder.ToSql());
    }

    /// <summary>
    /// 测试目的：目标列与已知查询投影数量不一致时应拒绝设置来源。
    /// </summary>
    [Fact]
    public void InsertSelect_WhenProjectionCountDoesNotMatchTarget_ShouldThrow()
    {
        var query = CreateQueryBuilder().Select("Id").From("orders");
        var builder = CreateInsertBuilder().InsertInto("archive_orders").Columns("Id", "Code");

        var exception = Assert.Throws<InvalidOperationException>(() => builder.InsertSelect(query));

        Assert.Equal("Insert 目标列数量与 Select 投影数量不一致。", exception.Message);
        Assert.Equal("Insert Into [archive_orders] ([Id], [Code]) Values (@_p_0, @_p_1)",
            builder.Values(1, "A").ToSql());
    }

    /// <summary>
    /// 测试目的：未指定目标列时允许数据库按目标表字段规则处理。
    /// </summary>
    [Fact]
    public void InsertSelect_WhenTargetColumnsAreOmitted_ShouldGenerateSql()
    {
        var query = CreateQueryBuilder().Select("Id").AppendSelect("[Code]").From("orders");
        var builder = CreateInsertBuilder().InsertInto("archive_orders").InsertSelect(query);

        Assert.Equal("Insert Into [archive_orders] \r\nSelect [Id],[Code] \r\nFrom [orders]", builder.ToSql());
    }

    /// <summary>
    /// 测试目的：无法可靠分析的原始投影不应通过字符串逗号误判列数。
    /// </summary>
    [Fact]
    public void InsertSelect_WhenProjectionCountIsUnknown_ShouldDeferValidationToDatabase()
    {
        var query = CreateQueryBuilder().AppendSelect("Concat([FirstName], [LastName])").From("orders");
        var builder = CreateInsertBuilder().InsertInto("archive_orders").Columns("DisplayName").InsertSelect(query);

        Assert.Equal("Insert Into [archive_orders] ([DisplayName]) \r\nSelect Concat([FirstName], [LastName]) \r\nFrom [orders]", builder.ToSql());
        Assert.Null(query.SelectClause.ProjectionCount);
    }

    /// <summary>
    /// 测试目的：Insert Select 包含 CTE 时应明确拒绝。
    /// </summary>
    [Fact]
    public void InsertSelect_WhenCteIsConfigured_ShouldRejectRendering()
    {
        var cte = CreateQueryBuilder().Select("Id").From("orders");
        var query = CreateQueryBuilder().With("selected", cte).Select("Id").From("selected");
        var builder = CreateInsertBuilder().InsertInto("archive_orders").Columns("Id");

        var exception = Assert.Throws<NotSupportedException>(() => builder.InsertSelect(query));

        Assert.Equal("Insert Select 当前不支持 CTE 或 Union 来源查询。", exception.Message);
    }

    /// <summary>
    /// 测试目的：Insert Select 包含集合操作时应明确拒绝。
    /// </summary>
    [Fact]
    public void InsertSelect_WhenUnionIsConfigured_ShouldRejectRendering()
    {
        var union = CreateQueryBuilder().Select("Id").From("archived_orders");
        var query = CreateQueryBuilder().Select("Id").From("orders").Union(union);
        var builder = CreateInsertBuilder().InsertInto("archive_orders").Columns("Id");

        var exception = Assert.Throws<NotSupportedException>(() => builder.InsertSelect(query));

        Assert.Equal("Insert Select 当前不支持 CTE 或 Union 来源查询。", exception.Message);
    }

    /// <summary>
    /// 测试目的：Clone 应保留独立的 Insert Select 查询快照和参数。
    /// </summary>
    [Fact]
    public void InsertSelect_WhenCloned_ShouldPreserveIndependentState()
    {
        var query = CreateQueryBuilder().Select("Id").From("orders").Where("Status", "active");
        var builder = CreateInsertBuilder().InsertInto("archive_orders").Columns("Id").InsertSelect(query);

        var clone = builder.Clone();
        query.Clear().Select("Status").From("disabled_orders");

        Assert.Equal("Insert Into [archive_orders] ([Id]) \r\nSelect [Id] \r\nFrom [orders] \r\nWhere [Status]=@_p_0", clone.ToSql());
        Assert.Equal("active", clone.BuildCommand().Parameters.Single().Value);
    }

    /// <summary>
    /// 测试目的：Clear 后专用 Insert Builder 可配置 Values 数据源。
    /// </summary>
    [Fact]
    public void InsertSelect_WhenCleared_ShouldAllowInsertValuesReuse()
    {
        var query = CreateQueryBuilder().Select("Id").From("orders");
        var builder = CreateInsertBuilder().InsertInto("archive_orders").Columns("Id").InsertSelect(query);

        builder.Clear();

        Assert.Equal("Insert Into [archive_orders] ([Id]) Values (@_p_0)",
            builder.InsertInto("archive_orders").Columns("Id").Values(1).ToSql());
    }

    /// <summary>
    /// 测试目的：强类型目标和来源投影应使用实体表与列映射。
    /// </summary>
    [Fact]
    public void InsertSelect_WhenMappedAnonymousProjectionIsConfigured_ShouldUseMappedNames()
    {
        var query = CreateQueryBuilder().Select<MappedOrder>(item => new object[] { item.Id, item.Code }).From<MappedOrder>();
        var builder = CreateInsertBuilder().InsertInto<MappedArchive>(item => new { item.OrderId, item.DisplayCode }).InsertSelect(query);

        Assert.Equal("Insert Into [archive_orders] ([order_id], [display_code]) \r\nSelect [orders].[order_id],[orders].[order_code] \r\nFrom [orders]", builder.ToSql());
    }

    /// <summary>
    /// 测试目的：目标与来源参数同名时应重新分配唯一参数名，重复渲染不得累积参数。
    /// </summary>
    [Fact]
    public void InsertSelect_WhenExistingParameterNameConflicts_ShouldGenerateDistinctParameter()
    {
        var query = CreateQueryBuilder().Select("Id").From("orders").AppendWhere("[Status]=@_p_0").AddParam("_p_0", "active");
        var builder = CreateInsertBuilder().InsertInto("archive_orders").Columns("Id").InsertSelect(query);

        var firstSql = builder.ToSql();
        var secondSql = builder.ToSql();

        Assert.Equal("Insert Into [archive_orders] ([Id]) \r\nSelect [Id] \r\nFrom [orders] \r\nWhere [Status]=@_p_0", firstSql);
        Assert.Equal(firstSql, secondSql);
        Assert.Equal("active", builder.BuildCommand().Parameters.Single().Value);
    }

    private static SqlInsertBuilder CreateInsertBuilder() =>
        new(SqlServerSqlProvider.Instance, new SqlBuilderServices());

    private static ProviderQueryBuilder CreateQueryBuilder() =>
        new(SqlServerSqlProvider.Instance, new SqlBuilderServices());

    /// <summary>
    /// 使用 Mutation 测试 Provider 的查询来源 Builder。
    /// </summary>
    private sealed class ProviderQueryBuilder : SqlBuilderBase
    {
        public ProviderQueryBuilder(ISqlProvider provider, SqlBuilderServices services,
            IParameterManager parameterManager = null)
            : base(provider, services, parameterManager)
        {
        }

        protected override SqlBuilderBase CreateBuilder(IParameterManager parameterManager) =>
            new ProviderQueryBuilder(Provider, Services, parameterManager);
    }

    /// <summary>
    /// Insert Select 目标映射实体。
    /// </summary>
    [Table("archive_orders")]
    private sealed class MappedArchive
    {
        [Column("order_id")]
        public int OrderId { get; set; }

        [Column("display_code")]
        public string DisplayCode { get; set; }
    }

    /// <summary>
    /// Insert Select 来源映射实体。
    /// </summary>
    [Table("orders")]
    private sealed class MappedOrder
    {
        [Column("order_id")]
        public int Id { get; set; }

        [Column("order_code")]
        public string Code { get; set; }
    }
}
