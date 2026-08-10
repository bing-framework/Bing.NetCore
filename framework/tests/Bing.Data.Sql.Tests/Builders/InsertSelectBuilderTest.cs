using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests.Builders;

/// <summary>
/// Insert Select 统一 Builder 测试。
/// </summary>
public class InsertSelectBuilderTest
{
    /// <summary>
    /// 测试 - 基本 Insert Select 应按固定 Clause 顺序生成完整 SQL。
    /// </summary>
    [Fact]
    public void InsertSelect_WhenBasicQueryIsConfigured_ShouldGenerateExpectedSql()
    {
        // Arrange
        var builder = new TestSqlBuilder()
            .InsertInto("archive_orders")
            .Columns("Id", "Code")
            .Select("Id", "Code")
            .From("orders");

        // Act
        var sql = builder.ToSql();

        // Assert
        Assert.Equal("Insert Into [archive_orders] ([Id], [Code]) \r\nSelect [Id],[Code] \r\nFrom [orders]", sql);
        Assert.Equal(SqlOperationKind.InsertSelect, builder.OperationKind);
    }

    /// <summary>
    /// 测试 - Insert Select 查询条件应复用同一个参数管理器。
    /// </summary>
    [Fact]
    public void InsertSelect_WhenWhereIsConfigured_ShouldGenerateParameterizedSql()
    {
        // Arrange
        var builder = new TestSqlBuilder()
            .InsertInto("archive_orders")
            .Columns("Id", "Code")
            .Select("Id", "Code")
            .From("orders")
            .Where("Status", "active");

        // Act
        var sql = builder.ToSql();

        // Assert
        Assert.Equal("Insert Into [archive_orders] ([Id], [Code]) \r\nSelect [Id],[Code] \r\nFrom [orders] \r\nWhere [Status]=@_p_0", sql);
        Assert.Equal("active", ((ISqlCommonPartAccessor)builder).ParameterManager.GetValue("_p_0"));
    }

    /// <summary>
    /// 测试 - Insert Select 应复用 Join、Group By、Having 和 Order By Clause。
    /// </summary>
    [Fact]
    public void InsertSelect_WhenJoinAndGroupAreConfigured_ShouldGenerateExpectedSql()
    {
        // Arrange
        var builder = new TestSqlBuilder()
            .InsertInto("archive_orders")
            .Columns("Status", "Total")
            .Select("o.Status")
            .CountColumn("d.Id", "Total")
            .From("orders", "o")
            .Join("order_details", "d")
            .AppendOn("[o].[Id]=[d].[OrderId]")
            .GroupBy("o.Status")
            .HavingRaw("Count(d.Id) > 0")
            .OrderBy("o.Status");

        // Act
        var sql = builder.ToSql();

        // Assert
        Assert.Equal("Insert Into [archive_orders] ([Status], [Total]) \r\nSelect [o].[Status],Count([d].[Id]) As [Total] \r\nFrom [orders] As [o] \r\nJoin [order_details] As [d] On [o].[Id]=[d].[OrderId] \r\nGroup By [o].[Status] Having Count(d.Id) > 0 \r\nOrder By [o].[Status]", sql);
    }

    /// <summary>
    /// 测试 - 目标列与已知查询投影数量不一致时应拒绝渲染。
    /// </summary>
    [Fact]
    public void InsertSelect_WhenProjectionCountDoesNotMatchTarget_ShouldThrow()
    {
        // Arrange
        var builder = new TestSqlBuilder()
            .InsertInto("archive_orders")
            .Columns("Id", "Code")
            .Select("Id")
            .From("orders");

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.ToSql());

        // Assert
        Assert.Equal("Insert Select 的目标列数量与查询输出列数量不一致。", exception.Message);
    }

    /// <summary>
    /// 测试 - 未指定目标列时应允许数据库按目标表字段规则处理。
    /// </summary>
    [Fact]
    public void InsertSelect_WhenTargetColumnsAreOmitted_ShouldGenerateSql()
    {
        // Arrange
        var builder = new TestSqlBuilder()
            .InsertInto("archive_orders")
            .Select("Id", "Code")
            .From("orders");

        // Act
        var sql = builder.ToSql();

        // Assert
        Assert.Equal("Insert Into [archive_orders] \r\nSelect [Id],[Code] \r\nFrom [orders]", sql);
    }

    /// <summary>
    /// 测试 - 无法可靠分析的原始投影不应通过字符串逗号误判列数。
    /// </summary>
    [Fact]
    public void InsertSelect_WhenProjectionCountIsUnknown_ShouldDeferValidationToDatabase()
    {
        // Arrange
        var builder = new TestSqlBuilder()
            .InsertInto("archive_orders")
            .Columns("DisplayName")
            .AppendSelect("Concat([FirstName], [LastName])")
            .From("orders");

        // Act
        var sql = builder.ToSql();

        // Assert
        Assert.Equal("Insert Into [archive_orders] ([DisplayName]) \r\nSelect Concat([FirstName], [LastName]) \r\nFrom [orders]", sql);
        Assert.Null(builder.SelectClause.ProjectionCount);
    }

    /// <summary>
    /// 测试目的：Insert Select 包含 CTE 时必须在渲染前明确拒绝，避免输出方言不一致的写入 SQL。
    /// </summary>
    [Fact]
    public void InsertSelect_WhenCteIsConfigured_ShouldRejectRendering()
    {
        // Arrange
        var source = new TestSqlBuilder().Select("Id").From("orders");
        var builder = new TestSqlBuilder()
            .InsertInto("archive_orders")
            .Columns("Id")
            .With("selected", source)
            .Select("Id")
            .From("selected");

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => builder.ToSql());

        // Assert
        Assert.Equal("Insert Select 当前不支持 Union 或 CTE。", exception.Message);
    }

    /// <summary>
    /// 测试目的：Insert Select 包含集合操作时必须在渲染前明确拒绝，避免生成未定义的写入语义。
    /// </summary>
    [Fact]
    public void InsertSelect_WhenUnionIsConfigured_ShouldRejectRendering()
    {
        // Arrange
        var union = new TestSqlBuilder().Select("Id").From("archived_orders");
        var builder = new TestSqlBuilder()
            .InsertInto("archive_orders")
            .Columns("Id")
            .Select("Id")
            .From("orders")
            .Union(union);

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => builder.ToSql());

        // Assert
        Assert.Equal("Insert Select 当前不支持 Union 或 CTE。", exception.Message);
    }

    /// <summary>
    /// 测试 - Clone 应保留 Insert Select Clause，并与来源参数状态隔离。
    /// </summary>
    [Fact]
    public void InsertSelect_WhenCloned_ShouldPreserveIndependentState()
    {
        // Arrange
        var source = new TestSqlBuilder()
            .InsertInto("archive_orders")
            .Columns("Id")
            .Select("Id")
            .From("orders")
            .Where("Status", "active");

        // Act
        var clone = source.Clone();
        source.Clear().Update(new Bing.Data.Sql.Metadata.SqlTableReference { TableName = "orders" })
            .Set("Status", "disabled")
            .AllowAllRows();

        // Assert
        Assert.Equal(SqlOperationKind.InsertSelect, clone.OperationKind);
        Assert.Equal("Insert Into [archive_orders] ([Id]) \r\nSelect [Id] \r\nFrom [orders] \r\nWhere [Status]=@_p_0", clone.ToSql());
        Assert.Equal(SqlOperationKind.Update, source.OperationKind);
    }

    /// <summary>
    /// 测试 - Clear 后应可将 Insert Select Builder 重新用于 Insert Values。
    /// </summary>
    [Fact]
    public void InsertSelect_WhenCleared_ShouldAllowInsertValuesReuse()
    {
        // Arrange
        var builder = new TestSqlBuilder()
            .InsertInto("archive_orders")
            .Columns("Id")
            .Select("Id")
            .From("orders");

        // Act
        builder.Clear().InsertInto("archive_orders").Columns("Id").Values(1);

        // Assert
        Assert.Equal(SqlOperationKind.InsertValues, builder.OperationKind);
        Assert.Equal("Insert Into [archive_orders] ([Id]) Values (@_p_0)", builder.ToSql());
    }

    /// <summary>
    /// 测试 - 强类型匿名目标投影应使用实体表和列映射生成 Insert Select。
    /// </summary>
    [Fact]
    public void InsertSelect_WhenMappedAnonymousProjectionIsConfigured_ShouldUseMappedNames()
    {
        // Arrange
        var builder = new TestSqlBuilder()
            .InsertInto<MappedArchive>(item => new { item.OrderId, item.DisplayCode })
            .Select<MappedOrder>(item => new object[] { item.Id, item.Code })
            .From<MappedOrder>();

        // Act
        var sql = builder.ToSql();

        // Assert
        Assert.Equal("Insert Into [archive_orders] ([order_id], [display_code]) \r\nSelect [orders].[order_id],[orders].[order_code] \r\nFrom [orders]", sql);
    }

    /// <summary>
    /// 测试 - Insert Select 的原始参数与自动参数同名时应生成隔离的新参数名。
    /// </summary>
    [Fact]
    public void InsertSelect_WhenExistingParameterNameConflicts_ShouldGenerateDistinctParameter()
    {
        // Arrange
        var builder = new TestSqlBuilder()
            .InsertInto("archive_orders")
            .Columns("Id")
            .Select("Id")
            .From("orders")
            .AddParam("_p_0", "active")
            .AppendWhere("[Status]=@_p_0")
            .Where("Code", "A");

        // Act
        var sql = builder.ToSql();

        // Assert
        Assert.Equal("Insert Into [archive_orders] ([Id]) \r\nSelect [Id] \r\nFrom [orders] \r\nWhere [Status]=@_p_0 And [Code]=@_p_1", sql);
        Assert.Equal(new[] { "@_p_0", "@_p_1" }, builder.GetParams().Keys);
    }

    /// <summary>
    /// Insert Select 目标映射实体。
    /// </summary>
    [Table("archive_orders")]
    private sealed class MappedArchive
    {
        /// <summary>
        /// 订单标识。
        /// </summary>
        [Column("order_id")]
        public int OrderId { get; set; }

        /// <summary>
        /// 展示编码。
        /// </summary>
        [Column("display_code")]
        public string DisplayCode { get; set; }
    }

    /// <summary>
    /// Insert Select 来源映射实体。
    /// </summary>
    [Table("orders")]
    private sealed class MappedOrder
    {
        /// <summary>
        /// 订单标识。
        /// </summary>
        [Column("order_id")]
        public int Id { get; set; }

        /// <summary>
        /// 订单编码。
        /// </summary>
        [Column("order_code")]
        public string Code { get; set; }
    }
}