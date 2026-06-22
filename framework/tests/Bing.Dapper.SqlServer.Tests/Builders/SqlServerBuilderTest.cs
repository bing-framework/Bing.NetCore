using System.Text;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql;
using Shouldly;
using Xunit;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// <see cref="SqlServerBuilder"/> 单元测试
/// 验证 SQL Server 方言下 SQL 生成行为：方括号标识符、@p_N 参数、分页语法
/// </summary>
public class SqlServerBuilderTest
{
    private SqlServerBuilder NewBuilder() => new SqlServerBuilder();

    // ── Select + From + Where ────────────────────────────────────

    /// <summary>
    /// 测试目的：基础 Select/From/Where 生成 SQL Server 格式（方括号 + @p_N）。
    /// </summary>
    [Fact]
    public void Test_SelectFromWhere_BasicFormat()
    {
        // Arrange
        var result = new StringBuilder();
        result.AppendLine("Select [Name] ");
        result.AppendLine("From [User] ");
        result.Append("Where [Age]=@p_0");

        var builder = NewBuilder();

        // Act
        builder.Select("Name")
               .From("User")
               .Where("Age", 25);

        // Assert
        Assert.Equal(result.ToString(), builder.ToSql());
        Assert.Equal(25, builder.GetParam("p_0"));
    }

    /// <summary>
    /// 测试目的：Select * From 无条件时生成干净 SQL，不含 Where 子句。
    /// </summary>
    [Fact]
    public void Test_SelectAll_NoWhere()
    {
        // Arrange
        var result = new StringBuilder();
        result.AppendLine("Select * ");
        result.Append("From [Product]");

        var builder = NewBuilder();

        // Act
        builder.Select("*").From("Product");

        // Assert
        Assert.Equal(result.ToString(), builder.ToSql());
    }

    /// <summary>
    /// 测试目的：Select 多列时，各列均应被方括号包裹。
    /// </summary>
    [Fact]
    public void Test_Select_MultipleColumns()
    {
        // Arrange
        var result = new StringBuilder();
        result.AppendLine("Select [Id],[Name],[Email] ");
        result.Append("From [User]");

        var builder = NewBuilder();

        // Act
        builder.Select("Id,Name,Email").From("User");

        // Assert
        Assert.Equal(result.ToString(), builder.ToSql());
    }

    /// <summary>
    /// 测试目的：多个 Where 条件应以 And 连接，参数按顺序编号。
    /// </summary>
    [Fact]
    public void Test_Where_MultipleConditions()
    {
        // Arrange
        var result = new StringBuilder();
        result.AppendLine("Select * ");
        result.AppendLine("From [User] ");
        result.Append("Where [Status]=@p_0 And [Age]=@p_1");

        var builder = NewBuilder();

        // Act
        builder.Select("*")
               .From("User")
               .Where("Status", "active")
               .Where("Age", 30);

        // Assert
        Assert.Equal(result.ToString(), builder.ToSql());
        Assert.Equal("active", builder.GetParam("p_0"));
        Assert.Equal(30, builder.GetParam("p_1"));
    }

    // ── Join ─────────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：Join 子句使用方括号标识符，ON 条件参数格式正确。
    /// </summary>
    [Fact]
    public void Test_Join_Basic()
    {
        // Arrange
        var result = new StringBuilder();
        result.AppendLine("Select * ");
        result.AppendLine("From [Order] [o] ");
        result.AppendLine("Join [User] [u] On [o].[UserId]=[u].[Id] ");
        result.Append("Where [o].[Status]=@p_0");

        var builder = NewBuilder();

        // Act
        builder.Select("*")
               .From("Order", "o")
               .Join("User", "u")
               .On("o.UserId", "u.Id")
               .Where("o.Status", "paid");

        // Assert
        Assert.Equal(result.ToString(), builder.ToSql());
    }

    /// <summary>
    /// 测试目的：LeftJoin 正确生成 "Left Join" 关键字。
    /// </summary>
    [Fact]
    public void Test_LeftJoin()
    {
        // Arrange
        var result = new StringBuilder();
        result.AppendLine("Select * ");
        result.AppendLine("From [Order] [o] ");
        result.Append("Left Join [User] [u] On [o].[UserId]=[u].[Id]");

        var builder = NewBuilder();

        // Act
        builder.Select("*")
               .From("Order", "o")
               .LeftJoin("User", "u")
               .On("o.UserId", "u.Id");

        // Assert
        Assert.Equal(result.ToString(), builder.ToSql());
    }

    // ── OrderBy ──────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：OrderBy 指定列名，升序生成 Asc 关键字。
    /// </summary>
    [Fact]
    public void Test_OrderBy_Asc()
    {
        // Arrange
        var result = new StringBuilder();
        result.AppendLine("Select * ");
        result.AppendLine("From [User] ");
        result.Append("Order By [Name] Asc");

        var builder = NewBuilder();

        // Act
        builder.Select("*").From("User").OrderBy("Name");

        // Assert
        Assert.Equal(result.ToString(), builder.ToSql());
    }

    /// <summary>
    /// 测试目的：OrderByDesc 生成 Desc 关键字。
    /// </summary>
    [Fact]
    public void Test_OrderBy_Desc()
    {
        // Arrange
        var result = new StringBuilder();
        result.AppendLine("Select * ");
        result.AppendLine("From [User] ");
        result.Append("Order By [CreatedTime] Desc");

        var builder = NewBuilder();

        // Act
        builder.Select("*").From("User").OrderByDesc("CreatedTime");

        // Assert
        Assert.Equal(result.ToString(), builder.ToSql());
    }

    // ── Paging ───────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：SQL Server 分页使用 OFFSET/FETCH NEXT 语法，不使用 LIMIT/TOP。
    /// </summary>
    [Fact]
    public void Test_Paging_OffsetFetch()
    {
        // Arrange
        var result = new StringBuilder();
        result.AppendLine("Select * ");
        result.AppendLine("From [User] ");
        result.AppendLine("Order By [Id] Asc ");
        result.Append("Offset @_p_0 Rows Fetch Next @_p_1 Rows Only");

        var builder = NewBuilder();

        // Act
        builder.Select("*").From("User").OrderBy("Id").Page(1, 10);

        // Assert
        Assert.Equal(result.ToString(), builder.ToSql());
    }

    // ── Clone / New ──────────────────────────────────────────────

    /// <summary>
    /// 测试目的：Clone 后返回独立副本，修改原始 Builder 不影响克隆体。
    /// </summary>
    [Fact]
    public void Test_Clone_ShouldBeIndependent()
    {
        // Arrange
        var original = NewBuilder();
        original.Select("*").From("User");

        // Act
        var cloned = (SqlServerBuilder)original.Clone();
        original.Where("Id", 1);

        // Assert — clone 不含 Where 条件
        cloned.ToSql().ShouldNotContain("Where");
    }

    /// <summary>
    /// 测试目的：New() 返回相同方言的新 Builder，不含任何已有状态。
    /// </summary>
    [Fact]
    public void Test_New_ShouldReturnFreshBuilder()
    {
        // Arrange
        var original = NewBuilder();
        original.Select("*").From("User").Where("Id", 1);

        // Act
        var fresh = original.New();

        // Assert
        fresh.ToSql().ShouldBeNullOrEmpty();
    }

    // ── GetParams ────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：无条件时 GetParams 返回空字典，不为 null。
    /// </summary>
    [Fact]
    public void Test_GetParams_NoConditions_ShouldBeEmpty()
    {
        // Arrange
        var builder = NewBuilder();
        builder.Select("*").From("User");

        // Act
        var parms = builder.GetParams();

        // Assert
        parms.ShouldNotBeNull();
        parms.ShouldBeEmpty();
    }

    /// <summary>
    /// 测试目的：有条件时 GetParams 返回所有参数键值对，数量与条件数量一致。
    /// </summary>
    [Fact]
    public void Test_GetParams_WithConditions_ShouldHaveCorrectCount()
    {
        // Arrange
        var builder = NewBuilder();
        builder.Select("*").From("User").Where("Id", 1).Where("Name", "Alice");

        // Act
        var parms = builder.GetParams();

        // Assert
        parms.Count.ShouldBe(2);
    }
}
