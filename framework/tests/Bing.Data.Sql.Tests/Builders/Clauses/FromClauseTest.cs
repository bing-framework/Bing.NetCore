using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Tests.Samples;
using Bing.Data.Enums;

namespace Bing.Data.Sql.Tests.Builders.Clauses;

/// <summary>
/// From子句测试
/// </summary>
public class FromClauseTest
{
    /// <summary>
    /// From子句
    /// </summary>
    private FromClause _clause;

    /// <summary>
    /// 测试初始化
    /// </summary>
    public FromClauseTest()
    {
        _clause = new FromClause(TestSqlBuilder.CreateTestClauseContext());
    }

    /// <summary>
    /// 获取Sql语句
    /// </summary>
    /// <returns></returns>
    private string GetSql()
    {
        return _clause.ToSql();
    }

    /// <summary>
    /// 测试目的：验证未设置来源时不应输出 From 子句。
    /// </summary>
    [Fact]
    public void ToSql_WhenSourceIsMissing_ShouldReturnNull()
    {
        Assert.Null(GetSql());
    }

    /// <summary>
    /// 测试目的：验证设置表名后应输出带方言引号的 From 子句。
    /// </summary>
    [Fact]
    public void From_WhenTableProvided_ShouldRenderQuotedTable()
    {
        _clause.From("a");
        Assert.Equal("From [a]", GetSql());
    }

    /// <summary>
    /// 测试目的：验证设置表名和别名后应输出格式化的别名。
    /// </summary>
    [Fact]
    public void From_WhenTableAndAliasProvided_ShouldRenderQuotedAlias()
    {
        _clause.From("a", "b");
        Assert.Equal("From [a] As [b]", GetSql());
    }

    /// <summary>
    /// 测试目的：独立 schema 应由结构化表引用格式化。
    /// </summary>
    [Fact]
    public void From_WhenSchemaQualifiedTableProvided_ShouldRenderStructuredReference()
    {
        _clause.From("c.a", "b");
        Assert.Equal("From [c].[a] As [b]", GetSql());
    }

    /// <summary>
    /// 测试目的：字符串中的别名应按既有规则解析。
    /// </summary>
    [Fact]
    public void From_WhenEmbeddedAliasProvided_ShouldParseAlias()
    {
        _clause.From("a.b as t");
        Assert.Equal("From [a].[b] As [t]", GetSql());
    }

    /// <summary>
    /// 测试目的：字符串表名包含 SQL 语句分隔符时应被拒绝。
    /// </summary>
    [Fact]
    public void From_WhenTableContainsStatementSeparator_ShouldThrowArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _clause.From("a;DropTable"));
    }

    /// <summary>
    /// 测试目的：SQL Server 多段字符串表名应按既有规则拆分。
    /// </summary>
    [Fact]
    public void From_WhenTableContainsMultipleDots_ShouldFormatAsAtomicIdentifier()
    {
        _clause.From("profile.api.Event");
        Assert.Equal("From [profile].[api].[Event]", GetSql());
    }

    /// <summary>
    /// 测试目的：字符串表名包含函数结构时应被拒绝。
    /// </summary>
    [Fact]
    public void From_WhenTableContainsFunctionStructure_ShouldThrowArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _clause.From("a(b)"));
    }

    /// <summary>
    /// 测试目的：复合预加引号限定名必须被拒绝，调用方应改用独立 schema 参数。
    /// </summary>
    [Fact]
    public void From_WhenTableContainsQualifiedQuotedIdentifier_ShouldThrowArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _clause.From("[archive].[Order.Log2025]"));
    }

    /// <summary>
    /// 测试目的：字符串内的别名与显式别名冲突时应被拒绝，避免隐式覆盖。
    /// </summary>
    [Fact]
    public void From_WhenEmbeddedAliasConflictsWithExplicitAlias_ShouldThrowInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() => _clause.From("Order.Log2025 as log", "other"));
    }

    /// <summary>
    /// 测试目的：AppendFrom 原始 SQL 表达式应绕过结构化表名解析。
    /// </summary>
    [Fact]
    public void AppendFrom_ShouldPreserveSqlExpression()
    {
        _clause.AppendSql("(Select 1) As source");

        Assert.Equal("From (Select 1) As source", GetSql());
    }

    /// <summary>
    /// 测试目的：验证泛型实体应解析为对应表名。
    /// </summary>
    [Fact]
    public void From_WhenGenericEntityProvided_ShouldRenderEntityTable()
    {
        _clause.From<Sample>();
        Assert.Equal("From [Sample]", GetSql());
    }

    /// <summary>
    /// 测试目的：验证泛型实体和别名应输出格式化 From 子句。
    /// </summary>
    [Fact]
    public void From_WhenGenericEntityAndAliasProvided_ShouldRenderAlias()
    {
        _clause.From<Sample>("a");
        Assert.Equal("From [Sample] As [a]", GetSql());
    }

    /// <summary>
    /// 测试目的：验证泛型实体、别名和架构应按约定顺序输出。
    /// </summary>
    [Fact]
    public void From_WhenGenericEntityAliasAndSchemaProvided_ShouldRenderStructuredReference()
    {
        _clause.From<Sample>("a", "b");
        Assert.Equal("From [b].[Sample] As [a]", GetSql());
    }

    /// <summary>
    /// 测试目的：验证重复设置实体来源时最后一次设置应覆盖前一次。
    /// </summary>
    [Fact]
    public void From_WhenGenericEntitySetRepeatedly_ShouldUseLatestSource()
    {
        _clause.From<Sample>("a");
        _clause.From<Sample>("b");
        Assert.Equal("From [Sample] As [b]", GetSql());
    }

    /// <summary>
    /// 测试目的：验证原始 SQL 表表达式应保持调用方提供的内容。
    /// </summary>
    [Fact]
    public void AppendSql_WhenRawExpressionProvided_ShouldPreserveExpression()
    {
        _clause.AppendSql("a.b as c");
        Assert.Equal("From a.b as c", GetSql());
    }

    /// <summary>
    /// 测试目的：验证来源与原始表达式混合设置时最后一次设置应生效。
    /// </summary>
    [Fact]
    public void From_WhenSourceAndRawExpressionSetRepeatedly_ShouldUseLatestExpression()
    {
        _clause.From<Sample>("a");
        _clause.AppendSql("b");
        _clause.From<Sample>("c");
        _clause.AppendSql("d");
        Assert.Equal("From d", GetSql());
    }

    /// <summary>
    /// 测试目的：验证连续追加原始表表达式应保留追加顺序。
    /// </summary>
    [Fact]
    public void AppendSql_WhenCalledRepeatedly_ShouldAppendExpressions()
    {
        _clause.AppendSql("a");
        _clause.AppendSql("b");
        Assert.Equal("From ab", GetSql());
    }

    /// <summary>
    /// 测试目的：验证自定义实体解析器应决定实体的架构和表名。
    /// </summary>
    [Fact]
    public void From_WhenCustomEntityResolverProvided_ShouldUseResolvedTable()
    {
        _clause = new FromClause(TestSqlBuilder.CreateTestClauseContext(
            entityResolver: new TestEntityResolver()));
        _clause.From<Sample>();
        var result = _clause.ToSql();
        Assert.Equal("From [s].[t_Sample]", result);
    }

    /// <summary>
    /// 测试目的：验证自定义实体解析器与别名应共同输出格式化来源。
    /// </summary>
    [Fact]
    public void From_WhenCustomEntityResolverAndAliasProvided_ShouldUseResolvedTableAndAlias()
    {
        _clause = new FromClause(TestSqlBuilder.CreateTestClauseContext(
            entityResolver: new TestEntityResolver()));
        _clause.From<Sample>("a");
        var result = _clause.ToSql();
        Assert.Equal("From [s].[t_Sample] As [a]", result);
    }

    /// <summary>
    /// 测试目的：验证克隆后应保留来源且与原子句保持独立。
    /// </summary>
    [Fact]
    public void Clone_WhenSourceConfigured_ShouldPreserveSourceAndRemainIndependent()
    {
        _clause.From("a", "b");
        var copy = _clause.Clone(TestSqlBuilder.CreateTestClauseContext());
        Assert.Equal("From [a] As [b]", GetSql());
        Assert.Equal("From [a] As [b]", copy.ToSql());

        copy.From("c", "d");
        Assert.Equal("From [a] As [b]", GetSql());
        Assert.Equal("From [c] As [d]", copy.ToSql());
    }

    /// <summary>
    /// 测试目的：内部根来源追加应按来源注册顺序使用逗号渲染，且保留每个来源的稳定标识。
    /// </summary>
    [Fact]
    public void AppendRoot_WhenMultipleStructuredSourcesProvided_ShouldRenderCommaSeparatedSources()
    {
        // Arrange
        _clause.From<Sample>("s");

        // Act
        _clause.AppendRoot<Sample2>("s2");

        // Assert
        Assert.Equal("From [Sample] As [s], [Sample2] As [s2]", GetSql());
        Assert.Equal(new[] { "source_0", "source_1" }, _clause.Sources.Select(source => source.SourceId));
        Assert.Equal(new[] { typeof(Sample), typeof(Sample2) }, _clause.Sources.Select(source => source.EntityType));
    }

    /// <summary>
    /// 测试目的：同一实体的多个根来源必须保存不同表源实例，不能因 CLR 类型相同而合并。
    /// </summary>
    [Fact]
    public void AppendRoot_WhenSameEntityIsAddedTwice_ShouldKeepDistinctSourceInstances()
    {
        // Arrange
        _clause.From<Sample>("owner");

        // Act
        _clause.AppendRoot<Sample>("reviewer");

        // Assert
        Assert.Equal("From [Sample] As [owner], [Sample] As [reviewer]", GetSql());
        Assert.Equal(2, _clause.Sources.Count);
        Assert.NotSame(_clause.Sources[0], _clause.Sources[1]);
        Assert.All(_clause.Sources, source => Assert.Equal(typeof(Sample), source.EntityType));
    }

    /// <summary>
    /// 测试目的：克隆多根来源后，向克隆追加来源不得改变原查询图或来源标识序列。
    /// </summary>
    [Fact]
    public void Clone_WhenMultipleRootSourcesConfigured_ShouldRemainDeeplyIsolated()
    {
        // Arrange
        _clause.From<Sample>("s");
        _clause.AppendRoot<Sample2>("s2");
        var copy = (FromClause)_clause.Clone(TestSqlBuilder.CreateTestClauseContext());

        // Act
        copy.AppendRoot<Sample3>("s3");

        // Assert
        Assert.Equal("From [Sample] As [s], [Sample2] As [s2]", GetSql());
        Assert.Equal("From [Sample] As [s], [Sample2] As [s2], [Sample3] As [s3]", copy.ToSql());
        Assert.Equal(new[] { "source_0", "source_1" }, _clause.Sources.Select(source => source.SourceId));
        Assert.Equal(new[] { "source_0", "source_1", "source_2" }, copy.Sources.Select(source => source.SourceId));
    }

    /// <summary>
    /// 测试目的：1～10 个类型化根来源都应按参数顺序渲染完整 From SQL，并为重复实体生成稳定别名。
    /// </summary>
    [Theory]
    [MemberData(nameof(SetRootsCases))]
    public void SetRoots_WhenOneThroughTenSourcesProvided_ShouldRenderCompleteSql(Type[] entityTypes, string expectedSql)
    {
        // Arrange
        var clause = new FromClause(TestSqlBuilder.CreateTestClauseContext());

        // Act
        clause.SetRoots(entityTypes);

        // Assert
        Assert.Equal(expectedSql, clause.ToSql());
        Assert.Equal(entityTypes.Length, clause.Sources.Count);
    }

    /// <summary>
    /// 测试目的：根来源配置失败时应保留原有来源和别名注册，避免部分提交破坏后续查询。
    /// </summary>
    [Fact]
    public void SetRoots_WhenSourceValidationFails_ShouldKeepExistingSources()
    {
        // Arrange
        var clause = new FromClause(TestSqlBuilder.CreateTestClauseContext());
        clause.From<Sample>("original");

        // Act
        Assert.Throws<ArgumentException>(() => clause.SetRoots(new Type[] { typeof(Sample2), null }));

        // Assert
        Assert.Equal("From [Sample] As [original]", clause.ToSql());
        Assert.Single(clause.Sources);
        Assert.Equal(typeof(Sample), clause.Sources[0].EntityType);
        Assert.Equal("original", clause.Sources[0].Alias);
    }

    /// <summary>
    /// 测试目的：类型化根来源超过十个时应在写入 Builder 状态前拒绝，避免内部入口绕过公开元数限制。
    /// </summary>
    [Fact]
    public void SetRoots_WhenMoreThanTenSourcesProvided_ShouldRejectWithoutChangingExistingSources()
    {
        // Arrange
        var clause = new FromClause(TestSqlBuilder.CreateTestClauseContext());
        clause.From<Sample>("original");

        // Act
        var exception = Assert.Throws<ArgumentException>(() => clause.SetRoots(Enumerable.Repeat(typeof(Sample2), 11).ToArray()));

        // Assert
        Assert.StartsWith("查询根表源最多支持十个来源。", exception.Message, StringComparison.Ordinal);
        Assert.Equal("From [Sample] As [original]", clause.ToSql());
        Assert.Single(clause.Sources);
        Assert.Equal("original", clause.Sources[0].Alias);
    }

    /// <summary>
    /// 生成 1～10 个根来源的完整 SQL 用例。
    /// </summary>
    public static IEnumerable<object[]> SetRootsCases()
    {
        var types = new[]
        {
            typeof(Sample), typeof(Sample2), typeof(Sample3), typeof(Sample5), typeof(Sample6),
            typeof(Sample7), typeof(Sample8), typeof(Sample), typeof(Sample2), typeof(Sample3)
        };
        for (var count = 1; count <= types.Length; count++)
        {
            var names = new Dictionary<Type, int>();
            var sources = new List<string>();
            foreach (var type in types.Take(count))
            {
                names.TryGetValue(type, out var occurrence);
                names[type] = ++occurrence;
                var table = type.Name;
                sources.Add(occurrence == 1 ? $"[{table}]" : $"[{table}] As [{table}_{occurrence}]");
            }
            yield return new object[] { types.Take(count).ToArray(), $"From {string.Join(", ", sources)}" };
        }
    }
}
