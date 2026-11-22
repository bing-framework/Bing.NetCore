using System.Text;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests;

/// <summary>验证排序文本和列身份冻结后可独立派生。</summary>
public sealed class SqlQueryOrderTest
{
    /// <summary>空排序可追加首列，空请求不创建新状态。</summary>
    [Fact]
    public void Append_WhenOrderIsEmpty_ShouldAddFirstColumnAndIgnoreEmptyRequest()
    {
        var clause = CreateClause();
        var buffer = new StringBuilder("Prefix:");

        var frozen = clause.AppendFrozenTo(buffer);
        var unchanged = frozen.Append(" , ", TestDialect.Instance);
        var appended = frozen.Append("Name Desc", TestDialect.Instance);

        Assert.Same(frozen, unchanged);
        Assert.False(frozen.HasOrder);
        Assert.Equal("Order By [Name] Desc", appended.ToSql());
        Assert.Equal("Prefix:", buffer.ToString());
    }

    /// <summary>重复列沿用已有方向，无前缀请求匹配已有的限定列。</summary>
    [Fact]
    public void Append_WhenColumnAlreadyExists_ShouldKeepFirstDirection()
    {
        var clause = CreateClause();
        clause.OrderBy("Name Desc,a.Name");
        var frozen = Freeze(clause);

        var appended = frozen.Append("name asc,a.Name Desc,b.Name,b.Name Desc", TestDialect.Instance);

        Assert.Equal("Order By [Name] Desc,[a].[Name],[b].[Name]", appended.ToSql());
        Assert.Equal("Order By [Name] Desc,[a].[Name]", frozen.ToSql());
    }

    /// <summary>带前缀的同列各自保留，重复的限定列只添加一次。</summary>
    [Fact]
    public void Append_WhenPrefixesDiffer_ShouldKeepQualifiedColumnsSeparate()
    {
        var clause = CreateClause();
        clause.OrderBy("a.Name Desc");
        var frozen = Freeze(clause);

        var appended = frozen.Append("a.Name,b.Name Desc,b.Name Asc", TestDialect.Instance);

        Assert.Equal("Order By [a].[Name] Desc,[b].[Name] Desc", appended.ToSql());
        Assert.Equal("Order By [a].[Name] Desc", frozen.ToSql());
    }

    /// <summary>原始排序表达式维持原文，普通列按子句既有次序追加。</summary>
    [Fact]
    public void Append_WhenFrozenOrderContainsRawExpression_ShouldKeepIt()
    {
        var clause = CreateClause();
        clause.AppendSql("case when [Name] is null then 1 else 0 end");
        var frozen = Freeze(clause);

        var appended = frozen.Append("Id Desc", TestDialect.Instance);

        Assert.Equal("Order By case when [Name] is null then 1 else 0 end,[Id] Desc", appended.ToSql());
        Assert.Equal("Order By case when [Name] is null then 1 else 0 end", frozen.ToSql());
    }

    /// <summary>冻结的 Lambda 排序已保留列映射和注册别名文本。</summary>
    [Fact]
    public void Append_WhenTypedOrderAliasChanges_ShouldKeepFrozenMappingAndAlias()
    {
        var register = new EntityAliasRegister();
        register.Register(typeof(Sample), "before");
        var clause = new OrderByClause(TestSqlBuilder.CreateTestClauseContext(aliasRegister: register));
        clause.OrderBy<Sample>(sample => sample.Email, true);
        var frozen = Freeze(clause);
        register.Replace(typeof(Sample), "after");

        var appended = frozen.Append("Id", TestDialect.Instance);

        Assert.Equal("Order By [before].[Email] Desc,[Id]", appended.ToSql());
        Assert.Equal("Order By [before].[Email] Desc", frozen.ToSql());
    }

    /// <summary>冻结后清空并重用 Clause 不会改变已有排序描述。</summary>
    [Fact]
    public void Append_WhenClauseChangesAfterFreeze_ShouldKeepSnapshotUnchanged()
    {
        var clause = CreateClause();
        clause.OrderBy("Name");
        var buffer = new StringBuilder();
        var frozen = clause.AppendFrozenTo(buffer);
        clause.Clear();
        clause.OrderBy("Id Desc");

        var appended = frozen.Append("Status", TestDialect.Instance);

        Assert.Equal("Order By [Name],[Status]", appended.ToSql());
        Assert.Equal("Order By [Name]", frozen.ToSql());
        Assert.Equal("Order By [Name]", buffer.ToString());
    }

    /// <summary>自定义别名注册器继续使用原 Clause 渲染路径，不被探测空类型。</summary>
    [Fact]
    public void AppendFrozenTo_WhenAliasRegisterIsCustom_ShouldKeepOriginalRenderPath()
    {
        var clause = new OrderByClause(TestSqlBuilder.CreateTestClauseContext(
            entityResolver: new TestEntityResolver(), aliasRegister: new TestEntityAliasRegister()));
        clause.OrderBy<Sample>(sample => sample.Email, true);
        var buffer = new StringBuilder();

        var frozen = clause.AppendFrozenTo(buffer);

        Assert.Null(frozen);
        Assert.Equal("Order By [as_Sample].[t_Email] Desc", buffer.ToString());
    }

    /// <summary>新排序格式化失败时，调用方缓冲区保留原内容。</summary>
    [Fact]
    public void AppendFrozenTo_WhenColumnIsInvalid_ShouldKeepCallerBufferUnchanged()
    {
        var clause = CreateClause();
        clause.OrderBy("Name,invalid;");
        var buffer = new StringBuilder("Prefix:");

        var exception = Assert.Throws<ArgumentException>(() => clause.AppendFrozenTo(buffer));

        Assert.Equal("name", exception.ParamName);
        Assert.Equal("Prefix:", buffer.ToString());
    }

    /// <summary>追加排序格式化失败后，已有冻结排序仍可重复使用。</summary>
    [Fact]
    public void Append_WhenRequestedColumnIsInvalid_ShouldKeepFrozenOrderUnchanged()
    {
        var clause = CreateClause();
        clause.OrderBy("Name");
        var frozen = Freeze(clause);

        Assert.Throws<ArgumentException>(() => frozen.Append("Id,invalid;", TestDialect.Instance));

        Assert.Equal("Order By [Name]", frozen.ToSql());
    }

    private static OrderByClause CreateClause() =>
        new(TestSqlBuilder.CreateTestClauseContext());

    private static SqlQueryOrder Freeze(OrderByClause clause) =>
        clause.AppendFrozenTo(new StringBuilder());
}
