using System.Data;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests;

/// <summary>
/// 查询模板、片段和作用域的职责级测试。
/// </summary>
public class SqlQueryTemplateTest
{
    /// <summary>
    /// 测试目的：作用域渲染应复制参数并只改变目标片段，不修改源片段。
    /// </summary>
    [Fact]
    public void Fragment_Render_ShouldUseScopedParameterAndKeepSourceUnchanged()
    {
        // Arrange
        var values = new[] { 1, 2 };
        var source = new SqlQueryFragment(
            "select * from users where id=@id and status=@status",
            new[]
            {
                new SqlParam("@id", values, DbType.Int32),
                new SqlParam("@status", "active", DbType.String)
            },
            SqlQueryFragmentKind.Select);
        values[0] = 99;

        // Act
        var rendered = source.Render(new SqlQueryScope("root"));

        // Assert
        Assert.Equal("select * from users where id=@root_id and status=@root_status", rendered.Sql);
        Assert.Equal(SqlQueryFragmentKind.Select, rendered.Kind);
        Assert.Equal(SqlQueryCorrelationKind.Independent, rendered.Correlation);
        Assert.Equal(new[] { "root_id", "root_status" }, rendered.Parameters.Select(item => item.Name));
        Assert.Equal(new[] { 1, 2 }, Assert.IsType<int[]>(source.Parameters[0].Value));
        Assert.Equal("select * from users where id=@id and status=@status", source.Sql);
    }

    /// <summary>作用域改名只处理实际参数，保留字面量、标识符、注释与方言语法。</summary>
    [Fact]
    public void Fragment_Render_WhenSqlContainsParameterLikeText_ShouldOnlyRenameParameterTokens()
    {
        const string sql = "select '@id', 'it''s @id', \"@id\", [@id], `@id`, $$@id$$, $tag$@id$tag$, " +
            "@ID, :id, ?id, @id2, @@id, name@id, value::id -- @id\r\nwhere value=@id /* @id */";
        const string expected = "select '@id', 'it''s @id', \"@id\", [@id], `@id`, $$@id$$, $tag$@id$tag$, " +
            "@root_id, :root_id, ?root_id, @root_id2, @@id, name@id, value::id -- @id\r\nwhere value=@root_id /* @id */";
        var fragment = new SqlQueryFragment(sql, new[] { new SqlParam("@id", 7), new SqlParam("@id2", 8) });

        var rendered = fragment.Render(new SqlQueryScope("root"));

        Assert.Equal(expected, rendered.Sql);
        Assert.Equal(new[] { "root_id", "root_id2" }, rendered.Parameters.Select(parameter => parameter.Name));
        Assert.Equal(new object[] { 7, 8 }, rendered.Parameters.Select(parameter => parameter.Value));
        Assert.Equal(sql, fragment.Sql);
    }

    /// <summary>作用域渲染接管参数快照后，源片段与返回给调用方的数组仍相互隔离。</summary>
    [Fact]
    public void Fragment_Render_WhenParameterIsMutable_ShouldKeepSnapshotsIsolated()
    {
        var source = new SqlQueryFragment("select @data", new[] { new SqlParam("@data", new byte[] { 1, 2 }) });

        var rendered = source.Render(new SqlQueryScope("root"));
        var exposed = Assert.IsType<byte[]>(Assert.Single(rendered.Parameters).Value);
        exposed[0] = 9;

        Assert.Equal("select @root_data", rendered.Sql);
        Assert.Equal(new byte[] { 1, 2 }, Assert.IsType<byte[]>(Assert.Single(source.Parameters).Value));
        Assert.Equal(new byte[] { 1, 2 }, Assert.IsType<byte[]>(Assert.Single(rendered.Parameters).Value));
    }

    /// <summary>没有参数的片段仍返回独立快照，并保留 SQL 和片段用途。</summary>
    [Fact]
    public void Fragment_Render_WithoutParameters_ShouldKeepSqlAndKind()
    {
        var source = new SqlQueryFragment("select 1", kind: SqlQueryFragmentKind.Select);

        var rendered = source.Render(new SqlQueryScope("root"));

        Assert.NotSame(source, rendered);
        Assert.Equal("select 1", rendered.Sql);
        Assert.Equal(SqlQueryFragmentKind.Select, rendered.Kind);
        Assert.Empty(rendered.Parameters);
    }

    /// <summary>嵌套注释中的引号不能影响后续实际参数的扫描。</summary>
    [Fact]
    public void Fragment_Render_WhenBlockCommentsAreNested_ShouldContinueAfterOuterComment()
    {
        const string sql = "select @id /* outer /* inner @id */ ' @id */ where value=@id";
        var fragment = new SqlQueryFragment(sql, new[] { new SqlParam("@id", 7) });

        var rendered = fragment.Render(new SqlQueryScope("root"));

        Assert.Equal("select @root_id /* outer /* inner @id */ ' @id */ where value=@root_id", rendered.Sql);
    }

    /// <summary>参数作用域内的新名称与其他旧名称相同时，不能重复改写。</summary>
    [Fact]
    public void Fragment_Render_WhenScopedNameMatchesAnotherParameter_ShouldRenameEachTokenOnce()
    {
        var fragment = new SqlQueryFragment("select @id, @root_id, @ID",
            new[] { new SqlParam("@id", 1), new SqlParam("@root_id", 2) });

        var rendered = fragment.Render(new SqlQueryScope("root"));

        Assert.Equal("select @root_id, @root_root_id, @root_id", rendered.Sql);
        Assert.Equal(new[] { "root_id", "root_root_id" }, rendered.Parameters.Select(parameter => parameter.Name));
    }

    /// <summary>不同参数规范化到相同名称时应分配独立名称，同一逻辑参数仍共享槽位。</summary>
    [Fact]
    public void Fragment_Render_WhenNormalizedNamesCollide_ShouldKeepDistinctParameterValues()
    {
        var fragment = new SqlQueryFragment("select @id, @_id, @id_1, @__id, :id",
            new[] { new SqlParam("@id", 1), new SqlParam("@_id", 2),
                new SqlParam("@id_1", 3), new SqlParam("@__id", 4) });

        var rendered = fragment.Render(new SqlQueryScope("root"));

        Assert.Equal("select @root_id, @root_id_1, @root_id_1_1, @root_id_2, :root_id", rendered.Sql);
        Assert.Equal(new[] { "root_id", "root_id_1", "root_id_1_1", "root_id_2" },
            rendered.Parameters.Select(parameter => parameter.Name));
        Assert.Equal(new object[] { 1, 2, 3, 4 }, rendered.Parameters.Select(parameter => parameter.Value));
    }

    /// <summary>参数名含补充平面字符时仍应保持原有的 OrdinalIgnoreCase 匹配语义。</summary>
    [Fact]
    public void Fragment_Render_WhenParameterUsesSupplementaryUnicode_ShouldIgnoreCase()
    {
        var upper = char.ConvertFromUtf32(0x10400);
        var lower = char.ConvertFromUtf32(0x10428);
        var fragment = new SqlQueryFragment("select @" + upper, new[] { new SqlParam("@" + lower, 7) });

        var rendered = fragment.Render(new SqlQueryScope("root"));

        Assert.Equal("select @root_p", rendered.Sql);
        Assert.Equal("root_p", Assert.Single(rendered.Parameters).Name);
    }

    /// <summary>冒号参数紧邻 PostgreSQL 类型转换时仍应改名，转换类型不应被改写。</summary>
    [Fact]
    public void Fragment_Render_WhenColonParameterPrecedesCast_ShouldKeepCastAndRenameParameter()
    {
        var fragment = new SqlQueryFragment("select :id::integer, value::id", new[] { new SqlParam(":id", 7) });

        var rendered = fragment.Render(new SqlQueryScope("root"));

        Assert.Equal("select :root_id::integer, value::id", rendered.Sql);
    }

    /// <summary>Builder 共用的参数扫描同样应保留 PostgreSQL 类型转换。</summary>
    [Fact]
    public void ReplaceParameterTokens_WhenColonParameterPrecedesCast_ShouldKeepCastAndRenameParameter()
    {
        var names = new Dictionary<string, string> { [":id"] = ":child_id" };

        var sql = SqlBuilderBase.ReplaceParameterTokens("select :id::integer, value::id", names);

        Assert.Equal("select :child_id::integer, value::id", sql);
    }

    /// <summary>大小写匹配选项不应依赖调用方字典使用的比较器。</summary>
    [Fact]
    public void ReplaceParameterTokens_WhenIgnoreCaseIsEnabled_ShouldMatchRegardlessOfDictionaryComparer()
    {
        var names = new Dictionary<string, string> { ["@id"] = "@child_id" };

        var sql = SqlBuilderBase.ReplaceParameterTokens("select @ID", names, ignoreCase: true);

        Assert.Equal("select @child_id", sql);
    }

    /// <summary>
    /// 测试目的：片段组合应为同名参数分配独立作用域，并保留相关性。
    /// </summary>
    [Fact]
    public void Compose_ShouldDisambiguateParametersAndPreserveCorrelation()
    {
        // Arrange
        var first = new SqlQueryFragment("select @id as id", new[] { new SqlParam("@id", 1) });
        var second = new SqlQueryFragment("select @id as id", new[] { new SqlParam("@id", 2) },
            SqlQueryFragmentKind.Union, SqlQueryCorrelationKind.Correlated);

        // Act
        var composed = SqlQueryFragment.Compose(" UNION ALL ", first, second);

        // Assert
        Assert.Equal("select @fragment_0_id as id UNION ALL select @fragment_1_id as id", composed.Sql);
        Assert.Equal(new[] { "fragment_0_id", "fragment_1_id" }, composed.Parameters.Select(item => item.Name));
        Assert.Equal(SqlQueryCorrelationKind.Correlated, composed.Correlation);
    }

    /// <summary>
    /// 测试目的：作用域添加 CTE 应保持不可变并拒绝重复名称。
    /// </summary>
    [Fact]
    public void Scope_AddCte_ShouldReturnIndependentScopeAndRejectDuplicates()
    {
        // Arrange
        var fragment = new SqlQueryFragment("select id from users where id=@id",
            new[] { new SqlParam("@id", 7, DbType.Int32) }, SqlQueryFragmentKind.Select);
        var root = new SqlQueryScope("root");

        // Act
        var withCte = root.Add(new SqlCteDefinition("active_users", fragment));
        var child = withCte.CreateChild("consumer");
        var renderedCte = withCte.Ctes[0].Render(withCte);

        // Assert
        Assert.Empty(root.Ctes);
        Assert.Single(withCte.Ctes);
        Assert.Single(child.Ctes);
        Assert.Equal("active_users AS (select id from users where id=@root_id)", renderedCte.Sql);
        Assert.Throws<InvalidOperationException>(() => withCte.Add(new SqlCteDefinition("ACTIVE_USERS", fragment)));
    }

    /// <summary>
    /// 测试目的：Builder 冻结应使用独立副本，模板和后续 Builder 修改互不污染。
    /// </summary>
    [Fact]
    public void Builder_ToTemplate_ShouldFreezeSqlAndKeepDraftBranchesIndependent()
    {
        // Arrange
        var builder = new TestSqlBuilder().Select("Id").From("Users").Where("Id", 7);
        const string expectedSql = "Select [Id] \r\nFrom [Users] \r\nWhere [Id]=@root_p_0";

        // Act
        var clone = builder.Clone();
        var template = builder.ToTemplate(new SqlQueryScope("root"));
        clone.Where("Status", 1);
        builder.Where("Kind", 2);

        // Assert
        Assert.Equal(expectedSql, template.Sql);
        Assert.Equal(expectedSql, template.CreateFragment().Sql);
        Assert.Equal("Select [Id] \r\nFrom [Users] \r\nWhere [Id]=@_p_0 And [Status]=@_p_1", clone.ToSql());
        Assert.Equal("Select [Id] \r\nFrom [Users] \r\nWhere [Id]=@_p_0 And [Kind]=@_p_1", builder.ToSql());
        Assert.Equal(new object[] { 7, 1 }, clone.GetParams().Values);
        Assert.Equal(new object[] { 7, 2 }, builder.GetParams().Values);
        Assert.Equal("root_p_0", Assert.Single(template.Parameters).Name);
    }

    /// <summary>
    /// 测试目的：模板、片段和冻结快照返回的参数副本不得修改内部参数状态。
    /// </summary>
    [Fact]
    public void Parameters_ShouldReturnDeepClones_AndKeepTemplateReusable()
    {
        // Arrange
        var values = new[] { 1, 2 };
        var originalValues = new[] { 3, 4 };
        var parameter = new SqlParam("@ids", values, DbType.Int32)
        {
            OriginalValue = originalValues,
            IsProviderValue = true,
            EntityType = typeof(SqlQueryTemplateTest),
            PropertyName = "Ids",
            ColumnName = "ids"
        };
        var fragment = new SqlQueryFragment("select * from users where id in (@ids)",
            new[] { parameter }, SqlQueryFragmentKind.Select);
        var snapshot = fragment.Freeze();
        var template = new SqlQueryTemplate(fragment.Sql, fragment.Parameters, fragment.Kind,
            fragment.Correlation);

        // Act
        var exposedFragmentParameter = fragment.Parameters[0];
        var exposedSnapshotParameter = snapshot.Parameters[0];
        var exposedTemplateParameter = template.Parameters[0];
        exposedFragmentParameter.Value = new[] { 9, 9 };
        exposedFragmentParameter.OriginalValue = new[] { 8, 8 };
        exposedFragmentParameter.IsProviderValue = false;
        ((int[])exposedSnapshotParameter.Value)[0] = 7;
        exposedSnapshotParameter.IsProviderValue = false;
        ((int[])exposedTemplateParameter.OriginalValue)[0] = 6;
        exposedTemplateParameter.ColumnName = "changed";

        // Assert
        Assert.NotSame(exposedFragmentParameter, fragment.Parameters[0]);
        Assert.NotSame(exposedSnapshotParameter, snapshot.Parameters[0]);
        Assert.NotSame(exposedTemplateParameter, template.Parameters[0]);
        Assert.Equal(new[] { 1, 2 }, Assert.IsType<int[]>(fragment.Parameters[0].Value));
        Assert.Equal(new[] { 3, 4 }, Assert.IsType<int[]>(fragment.Parameters[0].OriginalValue));
        Assert.True(fragment.Parameters[0].IsProviderValue);
        Assert.Equal("ids", fragment.Parameters[0].ColumnName);
        Assert.Equal(new[] { 1, 2 }, Assert.IsType<int[]>(snapshot.Parameters[0].Value));
        Assert.Equal(new[] { 3, 4 }, Assert.IsType<int[]>(snapshot.Parameters[0].OriginalValue));
        Assert.True(snapshot.Parameters[0].IsProviderValue);
        Assert.Equal(new[] { 1, 2 }, Assert.IsType<int[]>(template.Parameters[0].Value));
        Assert.Equal(new[] { 3, 4 }, Assert.IsType<int[]>(template.Parameters[0].OriginalValue));
        Assert.True(template.Parameters[0].IsProviderValue);
        Assert.Equal("select * from users where id in (@ids)", template.Render().Sql);
        Assert.Equal("select * from users where id in (@ids)", template.CreateFragment().Sql);
    }

    /// <summary>
    /// 测试目的：冻结片段作为 From 来源时应合并参数，并允许模板跨查询复用。
    /// </summary>
    [Fact]
    public void From_WhenUsingFrozenFragment_ShouldRenderAndMergeParameters()
    {
        // Arrange
        var template = new SqlQueryTemplate("select @id as Id",
            new[] { new SqlParam("@id", 7, DbType.Int32) });
        var first = new TestSqlBuilder().Select("*").From(template, "items");
        var second = new TestSqlBuilder().Select("*").From(template, "items");

        // Act
        var firstSql = first.ToSql();
        var secondSql = second.ToSql();

        // Assert
        const string expected = "Select * \r\nFrom (select @id as Id) As [items]";
        Assert.Equal(expected, firstSql);
        Assert.Equal(expected, secondSql);
        Assert.Equal(7, first.GetParam("@id"));
        Assert.Equal(7, second.GetParam("@id"));
        Assert.Equal("select @id as Id", template.Sql);
    }

    /// <summary>
    /// 测试目的：冻结片段应可作为 CTE 和 Union 分支参与 Builder 渲染。
    /// </summary>
    [Fact]
    public void CteAndUnion_WhenUsingFrozenFragments_ShouldRenderCompleteSql()
    {
        // Arrange
        var cte = new SqlQueryFragment("select @id as Id",
            new[] { new SqlParam("@id", 7, DbType.Int32) });
        var union = new SqlQueryTemplate("select @id as Id",
            new[] { new SqlParam("@id", 8, DbType.Int32) });
        var builder = new TestSqlBuilder().Select("*").From("active_users");

        // Act
        builder.With("active_users", cte).UnionAll(union);
        var sql = builder.ToSql();

        // Assert
        const string expected = "With [active_users] \r\nAs (select @id as Id)\r\n(Select * \r\nFrom [active_users] \r\n) \r\nUnion All \r\n(select @_p_0 as Id \r\n)";
        Assert.Equal(expected, sql);
        Assert.Equal(7, builder.GetParam("@id"));
        Assert.Equal(8, builder.GetParam("@_p_0"));
    }
}
