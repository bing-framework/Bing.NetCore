using System.Data;
using System.Runtime.CompilerServices;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Operations;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;
using Moq;

namespace Bing.Data.Sql.Tests;

/// <summary>冻结定义只持有查询数据，编辑分支留在草稿生命周期。</summary>
public sealed class SqlQueryTemplateLifecycleTest
{
    /// <summary>保留模板、片段或 CTE 时均不得间接保留 Builder 的服务图。</summary>
    [Theory]
    [InlineData("template")]
    [InlineData("fragment")]
    [InlineData("cte")]
    public void FrozenDefinition_ShouldReleaseSourceAndBuilderServices(string kind)
    {
        var captured = Capture(kind);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(captured.Source.IsAlive);
        Assert.False(captured.Services.IsAlive);
        var fragment = captured.Definition is SqlCteDefinition cte
            ? cte.Fragment : (ISqlQueryFragment)captured.Definition;
        Assert.Equal("Select [Id] \r\nFrom [Users] \r\nWhere [Id]=@_p_0", fragment.Freeze().Sql);
        Assert.Equal(7, Assert.Single(fragment.Freeze().Parameters).Value);
        GC.KeepAlive(captured.Definition);
    }

    /// <summary>模板的接口与默认实现不提供恢复可变草稿的第二种生命周期。</summary>
    [Fact]
    public void PublicContract_ShouldExposeCompositionWithoutBuilderRestoration()
    {
        Assert.Null(typeof(ISqlQueryTemplate).GetMethod("CreateBuilder"));
        Assert.Null(typeof(SqlQueryTemplate).GetMethod("CreateBuilder"));
    }

    /// <summary>第三方 Builder 只在冻结期间克隆和读取一次，后续模板消费不再访问它。</summary>
    [Fact]
    public void ToTemplate_WhenBuilderIsThirdParty_ShouldCaptureSqlAndParametersOnce()
    {
        var parameter = new SqlParam("id", new byte[] { 1, 2 }, DbType.Binary);
        var clone = new Mock<ISqlBuilder>(MockBehavior.Strict);
        clone.Setup(builder => builder.ToSql()).Returns("select @id as payload");
        var manager = new Mock<IAdvancedParameterManager>(MockBehavior.Strict);
        manager.Setup(value => value.GetSqlParams())
            .Returns(new Dictionary<string, SqlParam> { ["id"] = parameter });
        clone.As<ISqlCommonPartAccessor>().SetupGet(builder => builder.ParameterManager).Returns(manager.Object);
        var source = new Mock<ISqlBuilder>(MockBehavior.Strict);
        source.Setup(builder => builder.Clone()).Returns(clone.Object);

        var template = source.Object.ToTemplate(new SqlQueryScope("shared"));
        ((byte[])parameter.Value)[0] = 9;
        clone.Setup(builder => builder.ToSql()).Throws(new InvalidOperationException("Already frozen"));

        foreach (var fragment in new[] { template.CreateFragment(), template.Render() })
        {
            Assert.Equal("select @shared_id as payload", fragment.Sql);
            var actual = Assert.Single(fragment.Parameters);
            Assert.Equal(new byte[] { 1, 2 }, Assert.IsType<byte[]>(actual.Value));
            Assert.Equal(DbType.Binary, actual.DbType);
        }
        source.Verify(builder => builder.Clone(), Times.Once);
        clone.Verify(builder => builder.ToSql(), Times.Once);
        manager.Verify(value => value.GetSqlParams(), Times.Once);
    }

    /// <summary>冻结失败不修改草稿，修正后可正常重试；不发布半成品定义。</summary>
    [Fact]
    public void ToTemplate_WhenRenderingFails_ShouldLeaveSourceEditable()
    {
        var builder = new TestSqlBuilder().Select("Id");
        Assert.Throws<InvalidOperationException>(() => builder.ToTemplate());
        builder.From("Users").Where("Id", 7);

        var template = builder.ToTemplate();

        Assert.Equal("Select [Id] \r\nFrom [Users] \r\nWhere [Id]=@_p_0", template.Sql);
        Assert.Equal(template.Sql, builder.ToSql());
        Assert.Equal(7, Assert.Single(template.Parameters).Value);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (object Definition, WeakReference Source, WeakReference Services) Capture(string kind)
    {
        var builder = new TestSqlBuilder();
        builder.Select("Id").From("Users").Where("Id", 7);
        var source = new WeakReference(builder);
        var services = new WeakReference(builder.SharedServices);
        var template = builder.ToTemplate();
        object definition = kind == "fragment" ? template.CreateFragment()
            : kind == "cte" ? template.AsCte("users") : template;
        return (definition, source, services);
    }
}
