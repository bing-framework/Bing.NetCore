using System.Data;
using Bing.Data;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests;

/// <summary>公开参数管理器直接修改后逻辑参数身份的契约测试。</summary>
public sealed class SqlBuilderDirectParameterManagerLogicalKeysTest
{
    /// <summary>直接添加同名参数后，不能把原逻辑键用于覆盖调用方的新参数。</summary>
    [Theory]
    [InlineData(7)]
    [InlineData(19)]
    public void Add_WhenSameNameIsReplacedDirectly_ShouldDropOldKeyFromReplacement(int replacement)
    {
        var key = new SqlParameterKey("request", "id");
        var builder = CreateBuilder(key);
        _ = builder.ToTemplate();

        builder.ParameterManager.Add("id", replacement);

        var template = builder.ToTemplate();
        var shape = new SqlQueryShapeTemplate(template.CreateFragment());
        var rendered = shape.RenderWithKeys(0, new Dictionary<SqlParameterKey, object> { [key] = 23 });
        const string expectedSql = "With [items] \r\nAs (select @_p_0 as Id)\r\nSelect * \r\nFrom [items] \r\nWhere [Id]=@id";

        Assert.Equal(expectedSql, template.Sql);
        Assert.Equal(new[] { new SqlParameterKey("id"), key }, template.Slots);
        Assert.Equal(new object[] { replacement, 7 }, template.Parameters.Select(parameter => parameter.Value));
        Assert.Equal(expectedSql, rendered.Sql);
        Assert.Equal(new object[] { replacement, 23 }, rendered.Parameters.Select(parameter => parameter.Value));
        Assert.Equal(new[] { new SqlParameterKey("id"), key }, rendered.Slots);
    }

    /// <summary>清除后直接复用同名参数时，新值不能继承清除前的逻辑键。</summary>
    [Fact]
    public void Clear_WhenNameIsReusedDirectly_ShouldNotRestoreOldKey()
    {
        var key = new SqlParameterKey("request", "id");
        var builder = CreateBuilder(key);
        _ = builder.ToTemplate();

        builder.ParameterManager.Clear();
        builder.ParameterManager.Add("id", 19);

        var template = builder.ToTemplate();
        var shape = new SqlQueryShapeTemplate(template.CreateFragment());
        var rendered = shape.RenderWithKeys(0, new Dictionary<SqlParameterKey, object> { [key] = 23 });
        const string expectedSql = "With [items] \r\nAs (select @_p_0 as Id)\r\nSelect * \r\nFrom [items] \r\nWhere [Id]=@id";

        Assert.Equal(expectedSql, template.Sql);
        Assert.Equal(new[] { new SqlParameterKey("id"), key }, template.Slots);
        Assert.Equal(new object[] { 19, 7 }, template.Parameters.Select(parameter => parameter.Value));
        Assert.Equal(expectedSql, rendered.Sql);
        Assert.Equal(new object[] { 19, 23 }, rendered.Parameters.Select(parameter => parameter.Value));
        Assert.Equal(new[] { new SqlParameterKey("id"), key }, rendered.Slots);
    }

    /// <summary>直接 From 片段重渲染时，管理器覆盖的外层参数应失去原逻辑键。</summary>
    [Theory]
    [InlineData(7)]
    [InlineData(19)]
    public void From_WhenSameNameIsReplacedDirectly_ShouldKeepOuterParameterUnkeyed(int replacement)
    {
        var key = new SqlParameterKey("request", "id");
        var builder = CreateFromBuilder(key);
        Assert.Equal("Select * \r\nFrom (select @id as Id) As [items] \r\nWhere [Id]=@id", builder.ToSql());

        builder.ParameterManager.Add("id", replacement);

        var template = builder.ToTemplate();
        var shape = new SqlQueryShapeTemplate(template.CreateFragment());
        var rendered = shape.RenderWithKeys(0, new Dictionary<SqlParameterKey, object> { [key] = 23 });
        const string expectedSql = "Select * \r\nFrom (select @id as Id) As [items] \r\nWhere [Id]=@id";

        Assert.Equal(expectedSql, template.Sql);
        Assert.Equal(new[] { new SqlParameterKey("id") }, template.Slots);
        Assert.Equal(new object[] { replacement }, template.Parameters.Select(parameter => parameter.Value));
        Assert.Equal(expectedSql, rendered.Sql);
        Assert.Equal(new object[] { replacement }, rendered.Parameters.Select(parameter => parameter.Value));
        Assert.Equal(new[] { new SqlParameterKey("id") }, rendered.Slots);
    }

    /// <summary>直接 From 片段的参数被清除并以同名参数复用时，新值不继承旧键。</summary>
    [Fact]
    public void From_WhenClearedNameIsReusedDirectly_ShouldKeepOuterParameterUnkeyed()
    {
        var key = new SqlParameterKey("request", "id");
        var builder = CreateFromBuilder(key);
        Assert.Equal("Select * \r\nFrom (select @id as Id) As [items] \r\nWhere [Id]=@id", builder.ToSql());

        builder.ParameterManager.Clear();
        builder.ParameterManager.Add("id", 19);

        var template = builder.ToTemplate();
        var rendered = new SqlQueryShapeTemplate(template.CreateFragment()).RenderWithKeys(0,
            new Dictionary<SqlParameterKey, object> { [key] = 23 });
        const string expectedSql = "Select * \r\nFrom (select @id as Id) As [items] \r\nWhere [Id]=@id";

        Assert.Equal(expectedSql, template.Sql);
        Assert.Equal(new[] { new SqlParameterKey("id") }, template.Slots);
        Assert.Equal(new object[] { 19 }, template.Parameters.Select(parameter => parameter.Value));
        Assert.Equal(expectedSql, rendered.Sql);
        Assert.Equal(new object[] { 19 }, rendered.Parameters.Select(parameter => parameter.Value));
        Assert.Equal(new[] { new SqlParameterKey("id") }, rendered.Slots);
    }

    /// <summary>高级参数管理器以相同值和类型替换 From 参数后，旧逻辑键失效。</summary>
    [Fact]
    public void From_WhenSameMetadataParameterIsReplacedDirectly_ShouldKeepReplacementUnkeyed()
    {
        var key = new SqlParameterKey("request", "id");
        var builder = CreateFromBuilder(key);
        const string expectedSql = "Select * \r\nFrom (select @id as Id) As [items] \r\nWhere [Id]=@id";
        Assert.Equal(expectedSql, builder.ToSql());

        ((IAdvancedParameterManager)builder.ParameterManager).Add(new SqlParam("id", 7, DbType.Int32));

        var template = builder.ToTemplate();
        var rendered = new SqlQueryShapeTemplate(template.CreateFragment()).RenderWithKeys(0,
            new Dictionary<SqlParameterKey, object> { [key] = 23 });

        Assert.Equal(expectedSql, template.Sql);
        Assert.Equal(new[] { new SqlParameterKey("id") }, template.Slots);
        Assert.Equal(7, Assert.Single(template.Parameters).Value);
        Assert.Equal(DbType.Int32, Assert.Single(template.Parameters).DbType);
        Assert.Equal(expectedSql, rendered.Sql);
        Assert.Equal(new[] { new SqlParameterKey("id") }, rendered.Slots);
        Assert.Equal(7, Assert.Single(rendered.Parameters).Value);
    }

    /// <summary>CTE 首次渲染后替换外层参数时，CTE 键与外层普通参数保持隔离。</summary>
    [Fact]
    public void Cte_WhenSameMetadataParameterIsReplacedAfterToSql_ShouldKeepCteKeyIsolated()
    {
        var key = new SqlParameterKey("request", "id");
        var builder = CreateBuilder(key);
        Assert.Equal("With [items] \r\nAs (select @id as Id)\r\nSelect * \r\nFrom [items] \r\nWhere [Id]=@id",
            builder.ToSql());

        ((IAdvancedParameterManager)builder.ParameterManager).Add(new SqlParam("id", 7, DbType.Int32));

        var template = builder.ToTemplate();
        var rendered = new SqlQueryShapeTemplate(template.CreateFragment()).RenderWithKeys(0,
            new Dictionary<SqlParameterKey, object> { [key] = 23 });
        const string expectedSql = "With [items] \r\nAs (select @_p_0 as Id)\r\nSelect * \r\nFrom [items] \r\nWhere [Id]=@id";
        var expectedSlots = new[] { new SqlParameterKey("id"), key };

        Assert.Equal(expectedSql, template.Sql);
        Assert.Equal(expectedSlots, template.Slots);
        Assert.Equal(new object[] { 7, 7 }, template.Parameters.Select(parameter => parameter.Value));
        Assert.All(template.Parameters, parameter => Assert.Equal(DbType.Int32, parameter.DbType));
        Assert.Equal(expectedSql, rendered.Sql);
        Assert.Equal(expectedSlots, rendered.Slots);
        Assert.Equal(new object[] { 7, 23 }, rendered.Parameters.Select(parameter => parameter.Value));
    }

    /// <summary>From Builder 克隆后只修改克隆的参数时，两者 SQL 与逻辑键状态互不影响。</summary>
    [Fact]
    public void From_WhenCloneParameterIsReplaced_ShouldKeepTemplatesAndKeysIndependent()
    {
        var key = new SqlParameterKey("request", "id");
        var builder = CreateFromBuilder(key);
        const string expectedSql = "Select * \r\nFrom (select @id as Id) As [items] \r\nWhere [Id]=@id";
        Assert.Equal(expectedSql, builder.ToSql());

        var clone = (TestSqlBuilder)builder.Clone();
        clone.ParameterManager.Add("id", 19);

        var originalTemplate = builder.ToTemplate();
        var cloneTemplate = clone.ToTemplate();
        var originalRendered = new SqlQueryShapeTemplate(originalTemplate.CreateFragment()).RenderWithKeys(0,
            new Dictionary<SqlParameterKey, object> { [key] = 23 });
        var cloneRendered = new SqlQueryShapeTemplate(cloneTemplate.CreateFragment()).RenderWithKeys(0,
            new Dictionary<SqlParameterKey, object> { [key] = 23 });

        Assert.Equal(expectedSql, originalTemplate.Sql);
        Assert.Equal(key, Assert.Single(originalTemplate.Slots));
        Assert.Equal(7, Assert.Single(originalTemplate.Parameters).Value);
        Assert.Equal(expectedSql, cloneTemplate.Sql);
        Assert.Equal(new SqlParameterKey("id"), Assert.Single(cloneTemplate.Slots));
        Assert.Equal(19, Assert.Single(cloneTemplate.Parameters).Value);
        Assert.Equal(23, Assert.Single(originalRendered.Parameters).Value);
        Assert.Equal(19, Assert.Single(cloneRendered.Parameters).Value);
    }

    /// <summary>清除分页参数只移除分页绑定，其他逻辑参数仍可按键覆盖。</summary>
    [Fact]
    public void ClearPageParams_ShouldKeepUnrelatedLogicalParameterKeys()
    {
        var key = new SqlParameterKey("request", "id");
        var builder = CreateFromBuilder(key).OrderBy("Id").Page(new Pager(2, 10, "Id"));

        builder.ClearPageParams();

        var template = builder.ToTemplate();
        var rendered = new SqlQueryShapeTemplate(template.CreateFragment()).RenderWithKeys(0,
            new Dictionary<SqlParameterKey, object> { [key] = 23 });
        const string expectedSql = "Select * \r\nFrom (select @id as Id) As [items] \r\nWhere [Id]=@id \r\nOrder By [Id]";

        Assert.Equal(expectedSql, template.Sql);
        Assert.Equal(key, Assert.Single(template.Slots));
        Assert.Equal(7, Assert.Single(template.Parameters).Value);
        Assert.Equal(expectedSql, rendered.Sql);
        Assert.Equal(key, Assert.Single(rendered.Slots));
        Assert.Equal(23, Assert.Single(rendered.Parameters).Value);
    }

    private static TestSqlBuilder CreateBuilder(SqlParameterKey key)
    {
        var fragment = new SqlQueryFragment("select @id as Id", new[]
            { new SqlParam("id", 7, DbType.Int32) }).WithParameterKey("id", key);
        return new TestSqlBuilder().Select("*").From("items").AppendWhere("[Id]=@id").With("items", fragment);
    }

    private static TestSqlBuilder CreateFromBuilder(SqlParameterKey key)
    {
        var fragment = new SqlQueryFragment("select @id as Id", new[]
            { new SqlParam("id", 7, DbType.Int32) }).WithParameterKey("id", key);
        return new TestSqlBuilder().Select("*").From(fragment, "items").AppendWhere("[Id]=@id");
    }
}
