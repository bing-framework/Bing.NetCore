using System.Data;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests;

/// <summary>冻结片段进入 Builder 时的参数标记和绑定契约。</summary>
public sealed class SqlBuilderFragmentParameterTest
{
    /// <summary>无前缀参数遇到外层同名参数时，只改写 SQL 参数标记。</summary>
    [Fact]
    public void From_WhenUnprefixedParameterCollides_ShouldNormalizeTokensWithoutChangingIdentifiers()
    {
        var fragment = new SqlQueryFragment("select id, @ID as value, :id as other, '@id' as literal /* :id */ from source",
            new[] { new SqlParam("id", 7, DbType.Int32) });
        var builder = new TestSqlBuilder();
        builder.AddParam("id", 100);
        builder.Select("*").From(fragment, "items");

        const string expected = "Select * \r\nFrom (select id, @_p_0 as value, @_p_0 as other, '@id' as literal /* :id */ from source) As [items]";
        Assert.Equal(expected, builder.ToSql());
        Assert.Equal(expected, builder.ToSql());
        Assert.Equal(100, builder.GetParam("@id"));
        Assert.Equal(7, builder.GetParam("@_p_0"));
        Assert.Equal(DbType.Int32, builder.GetSqlParams()["@_p_0"].DbType);
    }

    /// <summary>即使没有重名，实际标记也应与规范化后的绑定名称一致。</summary>
    [Fact]
    public void From_WhenTokenCaseAndPrefixDiffer_ShouldUseBoundName()
    {
        var fragment = new SqlQueryFragment("select @ID as value, :id as other, '?id' as literal",
            new[] { new SqlParam("id", 7) });
        var builder = new TestSqlBuilder().Select("*").From(fragment, "items");

        Assert.Equal("Select * \r\nFrom (select @id as value, @id as other, '?id' as literal) As [items]", builder.ToSql());
        Assert.Equal(7, builder.GetParam("@id"));
    }

    /// <summary>相同基础名称、不同前缀的明确声明可分别绑定。</summary>
    [Fact]
    public void From_WhenPrefixedParametersShareBaseName_ShouldKeepDistinctBindings()
    {
        var fragment = new SqlQueryFragment("select @ID as first, :ID as second",
            new[] { new SqlParam("@id", 7), new SqlParam(":id", 9) });
        var builder = new TestSqlBuilder().Select("*").From(fragment, "items");

        Assert.Equal("Select * \r\nFrom (select @id as first, @_p_0 as second) As [items]", builder.ToSql());
        Assert.Equal(7, builder.GetParam("@id"));
        Assert.Equal(9, builder.GetParam("@_p_0"));
    }

    /// <summary>值相同但绑定类型不同时，片段参数不得复用外层弱类型参数。</summary>
    [Fact]
    public void From_WhenEqualValuesHaveDifferentMetadata_ShouldKeepDistinctParameters()
    {
        var fragment = new SqlQueryFragment("select @id as value", new[] { new SqlParam("id", 7, DbType.Int32) });
        var builder = new TestSqlBuilder();
        builder.AddParam("id", 7);
        builder.Select("*").From(fragment, "items");

        Assert.Equal("Select * \r\nFrom (select @_p_0 as value) As [items]", builder.ToSql());
        Assert.Null(builder.GetSqlParams()["@id"].DbType);
        Assert.Equal(DbType.Int32, builder.GetSqlParams()["@_p_0"].DbType);
    }

    /// <summary>同片段中不同前缀的同值参数也应保留各自的绑定类型。</summary>
    [Fact]
    public void From_WhenEqualPrefixedValuesHaveDifferentMetadata_ShouldKeepDistinctParameters()
    {
        var fragment = new SqlQueryFragment("select @id as first, :id as second",
            new[] { new SqlParam("@id", 7, DbType.Int32), new SqlParam(":id", 7, DbType.Int64) });
        var builder = new TestSqlBuilder().Select("*").From(fragment, "items");

        Assert.Equal("Select * \r\nFrom (select @id as first, @_p_0 as second) As [items]", builder.ToSql());
        Assert.Equal(DbType.Int32, builder.GetSqlParams()["@id"].DbType);
        Assert.Equal(DbType.Int64, builder.GetSqlParams()["@_p_0"].DbType);
    }

    /// <summary>同值参数的转换状态不同，不能共享绑定槽位。</summary>
    [Fact]
    public void From_WhenEqualValuesHaveDifferentConversionState_ShouldKeepDistinctParameters()
    {
        var manager = new ParameterManager(TestDialect.Instance);
        manager.Add(new SqlParam("id", 7) { OriginalValue = 7 });
        var fragment = new SqlQueryFragment("select @id as value", new[]
        {
            new SqlParam("id", 7) { OriginalValue = "7", IsProviderValue = true }
        });
        var builder = new TestSqlBuilder(parameterManager: manager).Select("*").From(fragment, "items");

        Assert.Equal("Select * \r\nFrom (select @_p_0 as value) As [items]", builder.ToSql());
        Assert.False(builder.GetSqlParams()["@id"].IsProviderValue);
        Assert.True(builder.GetSqlParams()["@_p_0"].IsProviderValue);
        Assert.Equal("7", builder.GetSqlParams()["@_p_0"].OriginalValue);
    }

    /// <summary>同片段中不同前缀参数的转换状态也应分别保留。</summary>
    [Fact]
    public void From_WhenEqualPrefixedValuesHaveDifferentConversionState_ShouldKeepDistinctParameters()
    {
        var fragment = new SqlQueryFragment("select @id as first, :id as second", new[]
        {
            new SqlParam("@id", 7) { OriginalValue = 7 },
            new SqlParam(":id", 7) { OriginalValue = "7", IsProviderValue = true }
        });
        var builder = new TestSqlBuilder().Select("*").From(fragment, "items");

        Assert.Equal("Select * \r\nFrom (select @id as first, @_p_0 as second) As [items]", builder.ToSql());
        Assert.False(builder.GetSqlParams()["@id"].IsProviderValue);
        Assert.True(builder.GetSqlParams()["@_p_0"].IsProviderValue);
        Assert.Equal("7", builder.GetSqlParams()["@_p_0"].OriginalValue);
    }

    /// <summary>CTE 与 Union 的片段参数同样应按实际绑定名称渲染。</summary>
    [Fact]
    public void CteAndUnion_WhenFragmentParameterCaseDiffers_ShouldKeepBindingsStable()
    {
        var cte = new SqlQueryFragment("select @ID as Id", new[] { new SqlParam("id", 7) });
        var union = new SqlQueryFragment("select :ID as Id", new[] { new SqlParam("id", 8) });
        var builder = new TestSqlBuilder().Select("*").From("active_users").With("active_users", cte).UnionAll(union);
        const string expected = "With [active_users] \r\nAs (select @id as Id)\r\n(Select * \r\nFrom [active_users] \r\n) \r\nUnion All \r\n(select @_p_0 as Id \r\n)";

        Assert.Equal(expected, builder.ToSql());
        Assert.Equal(expected, builder.ToSql());
        Assert.Equal(expected, builder.Clone().ToSql());
        Assert.Equal(7, builder.GetParam("@id"));
        Assert.Equal(8, builder.GetParam("@_p_0"));
    }

    /// <summary>SQLite 的 Union 方言应接受冻结片段并重复使用相同参数名。</summary>
    [Fact]
    public void SqliteUnion_WhenBranchIsFrozenFragment_ShouldRenderCompleteSql()
    {
        var cte = new SqlQueryFragment("select @ID as value", new[] { new SqlParam("id", 7) });
        var union = new SqlQueryFragment("select :ID as value", new[] { new SqlParam("id", 8) });
        var builder = new SqliteBuilder().Select("*").From("items").With("items", cte).UnionAll(union);
        const string expected = "With Recursive `items` \r\nAs (select @id as value)\r\nSelect * \r\nFrom `items` \r\nUnion All \r\nselect @_p_0 as value";

        Assert.Equal(expected, builder.ToSql());
        Assert.Equal(expected, builder.ToSql());
        Assert.Equal(expected, builder.Clone().ToSql());
        Assert.Equal(7, builder.GetParam("@id"));
        Assert.Equal(8, builder.GetParam("@_p_0"));
    }

    /// <summary>外部重新占用已缓存名称后，片段应重新选择名称，不能使用外部值。</summary>
    [Fact]
    public void CteAndUnion_WhenCachedParameterIsReplaced_ShouldAllocateAnotherName()
    {
        var cte = new SqlQueryFragment("select @id as Id", new[] { new SqlParam("id", 7) });
        var union = new SqlQueryFragment("select @id as Id", new[] { new SqlParam("id", 8) });
        var builder = new TestSqlBuilder().Select("*").From("active_users").With("active_users", cte).UnionAll(union);
        Assert.Equal("With [active_users] \r\nAs (select @id as Id)\r\n(Select * \r\nFrom [active_users] \r\n) \r\nUnion All \r\n(select @_p_0 as Id \r\n)", builder.ToSql());

        builder.AddParam("_p_0", 99);

        const string expected = "With [active_users] \r\nAs (select @id as Id)\r\n(Select * \r\nFrom [active_users] \r\n) \r\nUnion All \r\n(select @_p_1 as Id \r\n)";
        Assert.Equal(expected, builder.ToSql());
        Assert.Equal(expected, builder.ToSql());
        Assert.Equal(99, builder.GetParam("@_p_0"));
        Assert.Equal(8, builder.GetParam("@_p_1"));
    }

    /// <summary>数组参数快照内容相同时，重复渲染不应持续分配新参数名。</summary>
    [Fact]
    public void CteAndUnion_WhenCachedValueIsAnArray_ShouldKeepParameterNameStable()
    {
        var cte = new SqlQueryFragment("select @payload as Data", new[] { new SqlParam("payload", new byte[] { 1 }) });
        var union = new SqlQueryFragment("select @payload as Data", new[] { new SqlParam("payload", new byte[] { 2 }) });
        var builder = new TestSqlBuilder().Select("*").From("items").With("items", cte).UnionAll(union);
        const string expected = "With [items] \r\nAs (select @payload as Data)\r\n(Select * \r\nFrom [items] \r\n) \r\nUnion All \r\n(select @_p_0 as Data \r\n)";

        Assert.Equal(expected, builder.ToSql());
        Assert.Equal(expected, builder.ToSql());
        Assert.Equal(2, builder.GetParams().Count);
    }

    /// <summary>不确定引号转义方式且需要改写参数时，失败不得污染已有参数与来源。</summary>
    [Fact]
    public void From_WhenRewriteHasAmbiguousEscaping_ShouldLeaveBuilderUnchanged()
    {
        var fragment = new SqlQueryFragment("select 'it\\'s @ID' as text, @ID as value",
            new[] { new SqlParam("id", 7) });
        var builder = new TestSqlBuilder().Select("*").From("before");
        builder.AddParam("id", 100);

        Assert.Throws<InvalidOperationException>(() => builder.From(fragment, "items"));

        Assert.Equal("Select * \r\nFrom [before]", builder.ToSql());
        Assert.Single(builder.GetParams());
        Assert.Equal(100, builder.GetParam("@id"));
    }

    /// <summary>多个独立 Builder 并发使用同一冻结片段时，参数与名称互不干扰。</summary>
    [Fact]
    public void From_WhenFragmentIsUsedConcurrently_ShouldKeepBuilderParametersIsolated()
    {
        var fragment = new SqlQueryFragment("select @ID as value", new[] { new SqlParam("id", 7) });
        var results = new (string Sql, object Outer, object Inner)[128];

        Parallel.For(0, results.Length, index =>
        {
            var builder = new TestSqlBuilder();
            builder.AddParam("id", index + 100);
            builder.Select("*").From(fragment, "items");
            results[index] = (builder.ToSql(), builder.GetParam("id"), builder.GetParam("_p_0"));
        });

        for (var index = 0; index < results.Length; index++)
        {
            Assert.Equal("Select * \r\nFrom (select @_p_0 as value) As [items]", results[index].Sql);
            Assert.Equal(index + 100, results[index].Outer);
            Assert.Equal(7, results[index].Inner);
        }
        Assert.Equal("select @ID as value", fragment.Sql);
        Assert.Equal(7, Assert.Single(fragment.Parameters).Value);
    }
}
