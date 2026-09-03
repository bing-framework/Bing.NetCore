using Bing.Data.Sql;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Bing.Data.Sql.Analyzers.Tests;

/// <summary>
/// SQL Operation Fluent API 编译契约测试。
/// </summary>
public class SqlOperationCompileContractTest
{
    /// <summary>
    /// 测试 - 普通查询能力源不应暴露 Insert Fluent API。
    /// </summary>
    [Fact]
    public void InsertExtension_WhenSourceOnlySupportsQuery_ShouldNotCompile()
    {
        // Arrange
        const string source = """
            using Bing.Data.Sql;
            using Bing.Data.Sql.Builders.Operations;
            using Bing.Data.Sql.Metadata;

            sealed class QueryOnly : ISqlQueryOperation { }

            static class Consumer
            {
                static void Use(QueryOnly source) => source.InsertInto(default(SqlTableReference));
            }
            """;

        // Act
        var diagnostics = Compile(source);

        // Assert
        AssertCompileFailedForMissingExtension(diagnostics, "InsertInto");
    }

    /// <summary>
    /// 测试 - Insert Builder 不应暴露 Update Fluent API。
    /// </summary>
    [Fact]
    public void UpdateExtension_WhenSourceIsInsertBuilder_ShouldNotCompile()
    {
        // Arrange
        const string source = """
            using Bing.Data.Sql;
            using Bing.Data.Sql.Builders.Mutations;
            using Bing.Data.Sql.Metadata;

            static class Consumer
            {
                static void Use(ISqlInsertBuilder source) => source.Update(default(SqlTableReference));
            }
            """;

        // Act
        var diagnostics = Compile(source);

        // Assert
        AssertCompileFailedForMissingExtension(diagnostics, "Update");
    }

    /// <summary>
    /// 测试 - Update Builder 不应暴露 Delete Fluent API。
    /// </summary>
    [Fact]
    public void DeleteExtension_WhenSourceIsUpdateBuilder_ShouldNotCompile()
    {
        // Arrange
        const string source = """
            using Bing.Data.Sql;
            using Bing.Data.Sql.Builders.Mutations;
            using Bing.Data.Sql.Metadata;

            static class Consumer
            {
                static void Use(ISqlUpdateBuilder source) => source.DeleteFrom(default(SqlTableReference));
            }
            """;

        // Act
        var diagnostics = Compile(source);

        // Assert
        AssertCompileFailedForMissingExtension(diagnostics, "DeleteFrom");
    }

    /// <summary>
    /// 测试 - 仅实现 Marker 而缺少 Accessor 的类型不应满足 Insert 扩展约束。
    /// </summary>
    [Fact]
    public void InsertExtension_WhenMarkerHasNoAccessor_ShouldNotCompile()
    {
        // Arrange
        const string source = """
            using Bing.Data.Sql;
            using Bing.Data.Sql.Builders.Operations;
            using Bing.Data.Sql.Metadata;

            sealed class InsertMarkerOnly : IInsert { }

            static class Consumer
            {
                static void Use(InsertMarkerOnly source) => source.InsertInto(default(SqlTableReference));
            }
            """;

        // Act
        var diagnostics = Compile(source);

        // Assert
        AssertCompileFailedForMissingExtension(diagnostics, "InsertInto");
    }

    /// <summary>
    /// 测试 - 正确的专用 Insert Builder 应满足 Insert 扩展约束。
    /// </summary>
    [Fact]
    public void InsertExtension_WhenSourceProvidesMarkerAndAccessor_ShouldCompile()
    {
        // Arrange
        const string source = """
            using Bing.Data.Sql;
            using Bing.Data.Sql.Builders.Mutations;
            using Bing.Data.Sql.Metadata;

            static class Consumer
            {
                static void Use(ISqlInsertBuilder source) => source.InsertInto(default(SqlTableReference));
            }
            """;

        // Act
        var diagnostics = Compile(source);

        // Assert
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    /// <summary>
    /// 测试目的：专用 Update Builder 应公开结构化 UpdateFrom、SetFrom 和 WhereFrom Fluent API。
    /// </summary>
    [Fact]
    public void UpdateFromExtensions_WhenSourceIsUpdateBuilder_ShouldCompile()
    {
        // Arrange
        const string source = """
            using Bing.Data.Sql;
            using Bing.Data.Sql.Builders.Mutations;
            using Bing.Data.Sql.Metadata;

            static class Consumer
            {
                static void Use(ISqlUpdateBuilder source) => source
                    .UpdateFrom(new SqlTableReference { TableName = "updates", Alias = "s" })
                    .SetFrom("Name", "Name")
                    .WhereFrom("Id", "Id");
            }
            """;

        // Act
        var diagnostics = Compile(source);

        // Assert
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    /// <summary>
    /// 测试目的：专用 Delete Builder 应公开结构化 DeleteUsing 和 WhereUsing Fluent API。
    /// </summary>
    [Fact]
    public void DeleteUsingExtensions_WhenSourceIsDeleteBuilder_ShouldCompile()
    {
        // Arrange
        const string source = """
            using Bing.Data.Sql;
            using Bing.Data.Sql.Builders.Mutations;
            using Bing.Data.Sql.Metadata;

            static class Consumer
            {
                static void Use(ISqlDeleteBuilder source) => source
                    .DeleteUsing(new SqlTableReference { TableName = "deletes", Alias = "s" })
                    .WhereUsing("Id", "Id");
            }
            """;

        // Act
        var diagnostics = Compile(source);

        // Assert
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    /// <summary>
    /// 测试目的：统一 Builder 应公开结构化和实体映射 Returning Fluent API。
    /// </summary>
    [Fact]
    public void ReturningExtensions_WhenSourceIsUnifiedBuilder_ShouldCompile()
    {
        // Arrange
        const string source = """
            using Bing.Data.Sql;

            sealed class Item
            {
                public int Id { get; set; }
            }

            static class Consumer
            {
                static void Use(ISqlBuilder source)
                {
                    source.Returning("Id");
                    source.Returning<Item>(item => item.Id);
                }
            }
            """;

        // Act
        var diagnostics = Compile(source);

        // Assert
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    /// <summary>
    /// 测试目的：第三方 Provider 应能实现返回结果子句位置、关键字和限定符方言合同。
    /// </summary>
    [Fact]
    public void ReturningDialect_WhenImplementedByProvider_ShouldCompile()
    {
        // Arrange
        const string source = """
            using Bing.Data.Sql;
            using Bing.Data.Sql.Builders;
            using Bing.Data.Sql.Builders.Mutations;

            sealed class OutputDialect : ISqlReturningDialect
            {
                public SqlReturningClausePosition Position => SqlReturningClausePosition.BeforeSource;
                public string GetKeyword(SqlExecutionKind executionKind) => "Output";
                public string GetQualifier(SqlExecutionKind executionKind, string configuredQualifier) =>
                    executionKind == SqlExecutionKind.Delete ? "Deleted" : "Inserted";
            }
            """;

        // Act
        var diagnostics = Compile(source);

        // Assert
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    /// <summary>
    /// 测试目的：最终查询入口应返回非泛型描述，并由终结方法选择结果类型。
    /// </summary>
    [Fact]
    public void QueryApi_WhenUsingNonGenericDescriptionsAndTerminalResults_ShouldCompile()
    {
        // Arrange
        const string source = """
            using Bing.Data.Sql;

            sealed class Item
            {
                public int Id { get; set; }
            }

            static class Consumer
            {
                static void Use(ISqlQuery query)
                {
                    SqlLambdaQuery lambda = query.From<Item>("i");
                    var lambdaRows = lambda.ToList<Item>();
                    var fluentRows = query.Query().ToList<Item>();
                    var textRows = query.Sql("Select Id From Items").ToList<Item>();
                    var procedureRows = query.Procedure("GetItems").ExecuteList<Item>();
                }
            }
            """;

        // Act
        var diagnostics = Compile(source);

        // Assert
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    /// <summary>
    /// 测试目的：最终 Lambda 描述应支持连续 10 个根来源和 2～10 来源的二元 Join 链。
    /// </summary>
    [Fact]
    public void QueryApi_WhenUsingTenSourcesAndBinaryJoins_ShouldCompile()
    {
        // Arrange
        const string source = """
            using Bing.Data.Sql;

            sealed class Item
            {
                public int Id { get; set; }
                public int NextId { get; set; }
            }

            static class Consumer
            {
                static void Use(ISqlQuery query)
                {
                    var description = query.From<Item>("i1")
                        .From<Item>("i2")
                        .From<Item>("i3")
                        .From<Item>("i4")
                        .From<Item>("i5")
                        .From<Item>("i6")
                        .From<Item>("i7")
                        .From<Item>("i8")
                        .From<Item>("i9")
                        .From<Item>("i10")
                        .Join<Item, Item>((first, second) => first.NextId == second.Id,
                            new SqlJoinOptions { RightAlias = "j2", LeftAlias = "i1" })
                        .Join<Item, Item>((first, second) => first.NextId == second.Id,
                            new SqlJoinOptions { RightAlias = "j3", LeftAlias = "j2" })
                        .Join<Item, Item>((first, second) => first.NextId == second.Id,
                            new SqlJoinOptions { RightAlias = "j4", LeftAlias = "j3" })
                        .Join<Item, Item>((first, second) => first.NextId == second.Id,
                            new SqlJoinOptions { RightAlias = "j5", LeftAlias = "j4" })
                        .Join<Item, Item>((first, second) => first.NextId == second.Id,
                            new SqlJoinOptions { RightAlias = "j6", LeftAlias = "j5" })
                        .Join<Item, Item>((first, second) => first.NextId == second.Id,
                            new SqlJoinOptions { RightAlias = "j7", LeftAlias = "j6" })
                        .Join<Item, Item>((first, second) => first.NextId == second.Id,
                            new SqlJoinOptions { RightAlias = "j8", LeftAlias = "j7" })
                        .Join<Item, Item>((first, second) => first.NextId == second.Id,
                            new SqlJoinOptions { RightAlias = "j9", LeftAlias = "j8" })
                        .Join<Item, Item>((first, second) => first.NextId == second.Id,
                            new SqlJoinOptions { RightAlias = "j10", LeftAlias = "j9" })
                        .Select<Item, Item>((first, second) => new object[] { first.Id, second.Id }, "i1", "j2")
                        .ToList<Item>();
                }
            }
            """;

        // Act
        var diagnostics = Compile(source);

        // Assert
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    /// <summary>
    /// 测试目的：已删除的起始阶段泛型 Query 入口不能被消费者编译使用。
    /// </summary>
    [Fact]
    public void QueryApi_WhenUsingGenericRootQuery_ShouldNotCompile()
    {
        // Arrange
        const string source = """
            using Bing.Data.Sql;

            sealed class Item { }

            static class Consumer
            {
                static void Use(ISqlQuery query) => query.Query<Item>();
            }
            """;

        // Act
        var diagnostics = Compile(source);

        // Assert
        AssertCompileFailedForMissingExtension(diagnostics, "Query");
    }

    /// <summary>
    /// 测试目的：已删除的泛型 Raw、插值 SQL 和 Procedure 起始入口不能被消费者编译使用。
    /// </summary>
    [Fact]
    public void QueryApi_WhenUsingGenericRawAndProcedureDescriptions_ShouldNotCompile()
    {
        // Arrange
        const string source = """
            using Bing.Data.Sql;

            sealed class Item { }

            static class Consumer
            {
                static void Use(ISqlQuery query)
                {
                    query.Sql<Item>("Select 1");
                    query.SqlInterpolated<Item>($"Select 1");
                    query.Procedure<Item>("GetItems");
                }
            }
            """;

        // Act
        var diagnostics = Compile(source);

        // Assert
        Assert.Contains(diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error &&
            diagnostic.GetMessage().Contains("Sql", StringComparison.Ordinal));
        Assert.Contains(diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error &&
            diagnostic.GetMessage().Contains("Procedure", StringComparison.Ordinal));
    }

    /// <summary>
    /// 测试目的：已删除的泛型 Lambda 描述类型不能重新成为公共消费者入口。
    /// </summary>
    [Fact]
    public void QueryApi_WhenUsingGenericLambdaDescription_ShouldNotCompile()
    {
        // Arrange
        const string source = """
            using Bing.Data.Sql;

            sealed class Item { }

            static class Consumer
            {
                static SqlLambdaQuery<Item> Use(ISqlQuery query) => query.From<Item>();
            }
            """;

        // Act
        var diagnostics = Compile(source);

        // Assert
        Assert.Contains(diagnostics, diagnostic =>
            diagnostic.Severity == DiagnosticSeverity.Error &&
            diagnostic.GetMessage().Contains("SqlLambdaQuery", StringComparison.Ordinal));
    }

    /// <summary>
    /// 测试目的：非泛型 Fluent 和 Raw SQL 描述应支持 Dapper 2、3、7 段多映射，并由终结方法选择结果类型。
    /// </summary>
    [Fact]
    public void QueryApi_WhenUsingTwoThreeAndSevenWayMappings_ShouldCompile()
    {
        // Arrange
        const string source = """
            using Bing.Data.Sql;

            sealed class Item { public int Id { get; set; } }

            static class Consumer
            {
                static Item Map2(Item first, Item second) => new Item { Id = first.Id + second.Id };
                static Item Map3(Item first, Item second, Item third) => new Item { Id = first.Id + second.Id + third.Id };
                static Item Map7(Item first, Item second, Item third, Item fourth, Item fifth, Item sixth, Item seventh) =>
                    new Item { Id = first.Id + second.Id + third.Id + fourth.Id + fifth.Id + sixth.Id + seventh.Id };

                static void Use(ISqlQuery query)
                {
                    query.Query().ToList<Item, Item, Item>(Map2);
                    query.Query().ToList<Item, Item, Item, Item>(Map3);
                    query.Query().ToList<Item, Item, Item, Item, Item, Item, Item, Item>(Map7);
                    query.Query().ToListAsync<Item, Item, Item>(Map2);
                    query.Query().ToListAsync<Item, Item, Item, Item>(Map3);
                    query.Query().ToListAsync<Item, Item, Item, Item, Item, Item, Item, Item>(Map7);

                    query.Sql("Select 1").ToList<Item, Item, Item>(Map2);
                    query.Sql("Select 1").ToList<Item, Item, Item, Item>(Map3);
                    query.Sql("Select 1").ToList<Item, Item, Item, Item, Item, Item, Item, Item>(Map7);
                    query.Sql("Select 1").ToListAsync<Item, Item, Item>(Map2);
                    query.Sql("Select 1").ToListAsync<Item, Item, Item, Item>(Map3);
                    query.Sql("Select 1").ToListAsync<Item, Item, Item, Item, Item, Item, Item, Item>(Map7);
                }
            }
            """;

        // Act
        var diagnostics = Compile(source);

        // Assert
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    /// <summary>
    /// 测试目的：第三方代码仅依赖 ISqlQuery 时，应能使用非泛型 Root 描述和终结泛型，不需要强转 SqlQueryBase。
    /// </summary>
    [Fact]
    public void QueryApi_WhenThirdPartyConsumerUsesISqlQuery_ShouldCompile()
    {
        // Arrange
        const string source = """
            using Bing.Data.Sql;

            sealed class Item { public int Id { get; set; } }

            static class ThirdPartyConsumer
            {
                static void Use(ISqlQuery query)
                {
                    var rows = query.Query().ToList<Item>();
                    var value = query.Sql("Select Id From Items").Scalar<int>();
                    var procedure = query.Procedure("GetItems").ExecuteList<Item>();
                }
            }
            """;

        // Act
        var diagnostics = Compile(source);

        // Assert
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    /// <summary>
    /// 测试目的：Lambda 组合公共 API 只允许一元或二元表达式，不重新暴露三元来源入口。
    /// </summary>
    [Fact]
    public void QueryApi_WhenUsingThreeParameterLambda_ShouldNotCompile()
    {
        // Arrange
        const string source = """
            using Bing.Data.Sql;

            sealed class Item { public int Id { get; set; } }

            static class Consumer
            {
                static void Use(ISqlQuery query)
                {
                    query.From<Item>("a")
                        .From<Item>("b")
                        .From<Item>("c")
                        .Where<Item, Item, Item>((a, b, c) => a.Id == b.Id && b.Id == c.Id);
                }
            }
            """;

        // Act
        var diagnostics = Compile(source);

        // Assert
        Assert.Contains(diagnostics, diagnostic =>
            diagnostic.Severity == DiagnosticSeverity.Error &&
            diagnostic.GetMessage().Contains("Where", StringComparison.Ordinal));
    }

    /// <summary>
    /// 测试目的：第三方 Provider 消费者应能在不引用具体 Clause 实现的情况下编译使用五个 Lambda 多源 optional SPI。
    /// </summary>
    [Fact]
    public void LambdaMultiSourceSpi_WhenImplementedByThirdPartyProvider_ShouldCompile()
    {
        // Arrange
        const string source = """
            using System;
            using System.Collections.Generic;
            using System.Linq.Expressions;
            using System.Text;
            using Bing;
            using Bing.Data.Enums;
            using Bing.Data.Queries;
            using Bing.Data.Sql;
            using Bing.Data.Sql.Builders;
            using Bing.Data.Sql.Builders.Core;
            using Bing.Data.Sql.Builders.Clauses;
            using Bing.Data.Sql.Builders.Params;
            using Bing.Data.Sql.Metadata;

            sealed class Item
            {
                public int Id { get; set; }
            }

            abstract class FromContract : IFromClause
            {
                public abstract void From(string table, string alias = null);
                public abstract void From(SqlTableReference reference);
                public abstract void From<TEntity>(string alias = null, string schema = null)
                    where TEntity : class;
                public abstract void From(ISqlBuilder builder, string alias);
                public abstract void From(Action<ISqlBuilder> action, string alias);
                public abstract void AppendSql(string sql);
                public abstract void Validate();
                public abstract string ToSql();
                public abstract void AppendTo(StringBuilder builder);
                public abstract void Clear();
                public abstract IFromClause Clone(SqlClauseContext context);
            }

            sealed class ThirdPartyFrom : FromContract, ISqlMultiSourceFromClause
            {
                public IReadOnlyList<TableSource> Sources => Array.Empty<TableSource>();
                public void AppendRoot(Type entityType, string alias = null, string schema = null) { }
                public void From<TProjection>(SqlSubquery<TProjection> subquery) where TProjection : class { }
                public ICondition ResolveMultiSourcePredicate(LambdaExpression expression,
                    IReadOnlyList<TableSource> sources) => null;
                public ICondition ResolveMultiSourcePredicate(LambdaExpression expression,
                    IReadOnlyList<TableSource> sources, IParameterManager parameters) => null;
                public IReadOnlyList<string> ResolveMultiSourceColumns(LambdaExpression expression,
                    IReadOnlyList<TableSource> sources) => Array.Empty<string>();
                public IReadOnlyList<string> ResolveMultiSourceDtoColumns(LambdaExpression expression,
                    IReadOnlyList<TableSource> sources, out IReadOnlyCollection<string> projectedMembers)
                {
                    projectedMembers = Array.Empty<string>();
                    return Array.Empty<string>();
                }
                public ICondition ResolveMultiSourceValueCondition(LambdaExpression expression,
                    TableSource source, object value, Operator @operator) => null;
                public void MergeNewParameters(IParameterManager parameters) { }
                public override void From(string table, string alias = null) { }
                public override void From(SqlTableReference reference) { }
                public override void From<TEntity>(string alias = null, string schema = null) { }
                public override void From(ISqlBuilder builder, string alias) { }
                public override void From(Action<ISqlBuilder> action, string alias) { }
                public override void AppendSql(string sql) { }
                public override void Validate() { }
                public override string ToSql() => string.Empty;
                public override void AppendTo(StringBuilder builder) { }
                public override void Clear() { }
                public override IFromClause Clone(SqlClauseContext context) => this;
            }

            abstract class SelectContract : ISelectClause
            {
                public abstract bool IsDistinct { get; }
                public abstract int? ProjectionCount { get; }
                public abstract void Distinct();
                public abstract void CountAll(string alias = null);
                public abstract void CountColumn(string column, string alias = null, bool distinct = false);
                public abstract void Count<TEntity>(Expression<Func<TEntity, object>> expression,
                    string alias = null, bool distinct = false) where TEntity : class;
                public abstract void Aggregate(SqlAggregateFunction function, string column,
                    string columnAlias = null, bool distinct = false);
                public abstract void Aggregate<TEntity>(SqlAggregateFunction function,
                    Expression<Func<TEntity, object>> expression, string columnAlias = null, bool distinct = false)
                    where TEntity : class;
                public abstract void AggregateRaw(SqlAggregateFunction function, string argumentSql,
                    string columnAlias = null, bool distinct = false);
                public abstract void AggregateExpression(SqlAggregateFunction function, string expressionSql,
                    string columnAlias = null, bool distinct = false);
                public abstract void Sum(string column, string columnAlias = null, bool distinct = false);
                public abstract void Sum<TEntity>(Expression<Func<TEntity, object>> expression,
                    string columnAlias = null, bool distinct = false) where TEntity : class;
                public abstract void Avg(string column, string columnAlias = null, bool distinct = false);
                public abstract void Avg<TEntity>(Expression<Func<TEntity, object>> expression,
                    string columnAlias = null, bool distinct = false) where TEntity : class;
                public abstract void Max(string column, string columnAlias = null, bool distinct = false);
                public abstract void Max<TEntity>(Expression<Func<TEntity, object>> expression,
                    string columnAlias = null, bool distinct = false) where TEntity : class;
                public abstract void Min(string column, string columnAlias = null, bool distinct = false);
                public abstract void Min<TEntity>(Expression<Func<TEntity, object>> expression,
                    string columnAlias = null, bool distinct = false) where TEntity : class;
                public abstract void Select(string columns, string tableAlias = null);
                public abstract void Select<TEntity>(bool propertyAsAlias = false);
                public abstract void Select<TEntity>(Expression<Func<TEntity, object[]>> expression,
                    bool propertyAsAlias = false) where TEntity : class;
                public abstract void Select<TEntity>(Expression<Func<TEntity, object>> expression,
                    string columnAlias = null) where TEntity : class;
                public abstract void Select(ISqlBuilder builder, string columnAlias);
                public abstract void Select(Action<ISqlBuilder> action, string columnAlias);
                public abstract void AppendSql(string sql, string columnAlias = null);
                public abstract void RemoveSelect(string columns, string tableAlias = null);
                public abstract void RemoveSelect<TEntity>(Expression<Func<TEntity, object[]>> expression)
                    where TEntity : class;
                public abstract void RemoveSelect<TEntity>(Expression<Func<TEntity, object>> expression)
                    where TEntity : class;
                public abstract string ToSql();
                public abstract void AppendTo(StringBuilder builder);
                public abstract void Clear();
                public abstract ISelectClause Clone(SqlClauseContext context);
            }

            sealed class ThirdPartySelect : SelectContract, ISqlMultiSourceSelectClause
            {
                public void AppendBoundColumns(string columns) { }
                public void Aggregate<TEntity>(SqlAggregateFunction function,
                    Expression<Func<TEntity, object>> expression, string tableAlias, string columnAlias,
                    bool distinct = false) where TEntity : class { }
                public override bool IsDistinct => false;
                public override int? ProjectionCount => 0;
                public override void Distinct() { }
                public override void CountAll(string alias = null) { }
                public override void CountColumn(string column, string alias = null, bool distinct = false) { }
                public override void Count<TEntity>(Expression<Func<TEntity, object>> expression,
                    string alias = null, bool distinct = false) { }
                public override void Aggregate(SqlAggregateFunction function, string column,
                    string columnAlias = null, bool distinct = false) { }
                public override void Aggregate<TEntity>(SqlAggregateFunction function,
                    Expression<Func<TEntity, object>> expression, string columnAlias = null,
                    bool distinct = false) { }
                public override void AggregateRaw(SqlAggregateFunction function, string argumentSql,
                    string columnAlias = null, bool distinct = false) { }
                public override void AggregateExpression(SqlAggregateFunction function, string expressionSql,
                    string columnAlias = null, bool distinct = false) { }
                public override void Sum(string column, string columnAlias = null, bool distinct = false) { }
                public override void Sum<TEntity>(Expression<Func<TEntity, object>> expression,
                    string columnAlias = null, bool distinct = false) { }
                public override void Avg(string column, string columnAlias = null, bool distinct = false) { }
                public override void Avg<TEntity>(Expression<Func<TEntity, object>> expression,
                    string columnAlias = null, bool distinct = false) { }
                public override void Max(string column, string columnAlias = null, bool distinct = false) { }
                public override void Max<TEntity>(Expression<Func<TEntity, object>> expression,
                    string columnAlias = null, bool distinct = false) { }
                public override void Min(string column, string columnAlias = null, bool distinct = false) { }
                public override void Min<TEntity>(Expression<Func<TEntity, object>> expression,
                    string columnAlias = null, bool distinct = false) { }
                public override void Select(string columns, string tableAlias = null) { }
                public override void Select<TEntity>(bool propertyAsAlias = false) { }
                public override void Select<TEntity>(Expression<Func<TEntity, object[]>> expression,
                    bool propertyAsAlias = false) { }
                public override void Select<TEntity>(Expression<Func<TEntity, object>> expression,
                    string columnAlias = null) { }
                public override void Select(ISqlBuilder builder, string columnAlias) { }
                public override void Select(Action<ISqlBuilder> action, string columnAlias) { }
                public override void AppendSql(string sql, string columnAlias = null) { }
                public override void RemoveSelect(string columns, string tableAlias = null) { }
                public override void RemoveSelect<TEntity>(Expression<Func<TEntity, object[]>> expression)
                    { }
                public override void RemoveSelect<TEntity>(Expression<Func<TEntity, object>> expression)
                    { }
                public override string ToSql() => string.Empty;
                public override void AppendTo(StringBuilder builder) { }
                public override void Clear() { }
                public override ISelectClause Clone(SqlClauseContext context) => this;
            }

            abstract class GroupByContract : IGroupByClause
            {
                public abstract bool IsGroup { get; }
                public abstract string GroupColumns { get; }
                public abstract void GroupBy(string groupBy);
                public abstract void GroupBy<TEntity>(params Expression<Func<TEntity, object>>[] columns);
                public abstract void GroupBy<TEntity>(Expression<Func<TEntity, object>> column)
                    ;
                public abstract void Having(string sql);
                public abstract void HavingRaw(string sql);
                public abstract void AppendSql(string sql);
                public abstract string ToSql();
                public abstract void AppendTo(StringBuilder builder);
                public abstract void Clear();
                public abstract IGroupByClause Clone(SqlClauseContext context);
            }

            sealed class ThirdPartyGroupBy : GroupByContract, ISqlMultiSourceGroupByClause
            {
                public void AppendBoundColumns(IEnumerable<string> columns) { }
                public void SetBoundHaving(ICondition condition) { }
                public override bool IsGroup => false;
                public override string GroupColumns => string.Empty;
                public override void GroupBy(string groupBy) { }
                public override void GroupBy<TEntity>(params Expression<Func<TEntity, object>>[] columns) { }
                public override void GroupBy<TEntity>(Expression<Func<TEntity, object>> column) { }
                public override void Having(string sql) { }
                public override void HavingRaw(string sql) { }
                public override void AppendSql(string sql) { }
                public override string ToSql() => string.Empty;
                public override void AppendTo(StringBuilder builder) { }
                public override void Clear() { }
                public override IGroupByClause Clone(SqlClauseContext context) => this;
            }

            abstract class OrderByContract : IOrderByClause
            {
                public abstract void OrderBy(string order, string tableAlias = null);
                public abstract void OrderBy<TEntity>(Expression<Func<TEntity, object>> column, bool desc = false);
                public abstract void AppendSql(string order);
                public abstract void Validate(bool isPage);
                public abstract string ToSql();
                public abstract void AppendTo(StringBuilder builder);
                public abstract void Clear();
                public abstract IOrderByClause Clone(SqlClauseContext context);
            }

            sealed class ThirdPartyOrderBy : OrderByContract, ISqlMultiSourceOrderByClause
            {
                public void AppendBoundColumns(IEnumerable<string> columns, bool desc) { }
                public override void OrderBy(string order, string tableAlias = null) { }
                public override void OrderBy<TEntity>(Expression<Func<TEntity, object>> column, bool desc = false) { }
                public override void AppendSql(string order) { }
                public override void Validate(bool isPage) { }
                public override string ToSql() => string.Empty;
                public override void AppendTo(StringBuilder builder) { }
                public override void Clear() { }
                public override IOrderByClause Clone(SqlClauseContext context) => this;
            }

            abstract class JoinContract : IJoinClause
            {
                public abstract IJoinOn Find(Type type);
                public abstract void Join(string table, string alias = null);
                public abstract void Join(SqlTableReference reference);
                public abstract void Join<TEntity>(string alias = null, string schema = null)
                    where TEntity : class;
                public abstract void Join(ISqlBuilder builder, string alias);
                public abstract void Join(Action<ISqlBuilder> action, string alias);
                public abstract void AppendJoin(string sql);
                public abstract void LeftJoin(string table, string alias = null);
                public abstract void LeftJoin(SqlTableReference reference);
                public abstract void LeftJoin<TEntity>(string alias = null, string schema = null)
                    where TEntity : class;
                public abstract void LeftJoin(ISqlBuilder builder, string alias);
                public abstract void LeftJoin(Action<ISqlBuilder> action, string alias);
                public abstract void AppendLeftJoin(string sql);
                public abstract void RightJoin(string table, string alias = null);
                public abstract void RightJoin(SqlTableReference reference);
                public abstract void RightJoin<TEntity>(string alias = null, string schema = null)
                    where TEntity : class;
                public abstract void RightJoin(ISqlBuilder builder, string alias);
                public abstract void RightJoin(Action<ISqlBuilder> action, string alias);
                public abstract void AppendRightJoin(string sql);
                public abstract void FullJoin(string table, string alias = null);
                public abstract void FullJoin(SqlTableReference reference);
                public abstract void FullJoin<TEntity>(string alias = null, string schema = null)
                    where TEntity : class;
                public abstract void AppendFullJoin(string sql);
                public abstract void CrossJoin(string table, string alias = null);
                public abstract void CrossJoin(SqlTableReference reference);
                public abstract void CrossJoin<TEntity>(string alias = null, string schema = null)
                    where TEntity : class;
                public abstract void AppendCrossJoin(string sql);
                public abstract void On(ICondition condition);
                public abstract void On(string column, object value, Operator @operator = Operator.Equal);
                public abstract void On<TLeft, TRight>(Expression<Func<TLeft, object>> left,
                    Expression<Func<TRight, object>> right, Operator @operator = Operator.Equal)
                    where TLeft : class where TRight : class;
                public abstract void On<TLeft, TRight>(Expression<Func<TLeft, TRight, bool>> expression)
                    where TLeft : class where TRight : class;
                public abstract void AppendOn(string sql);
                public abstract string ToSql();
                public abstract void AppendTo(StringBuilder builder);
                public abstract void Clear();
                public abstract IJoinClause Clone(SqlClauseContext context);
            }

            sealed class ThirdPartyJoin : JoinContract, ISqlMultiSourceJoinClause
            {
                public IReadOnlyList<TableSource> TypedSources => Array.Empty<TableSource>();
                public void Join<TEntity>(IFromClause fromClause, LambdaExpression predicate,
                    string alias = null, string schema = null) where TEntity : class { }
                public void LeftJoin<TEntity>(IFromClause fromClause, LambdaExpression predicate,
                    string alias = null, string schema = null) where TEntity : class { }
                public void RightJoin<TEntity>(IFromClause fromClause, LambdaExpression predicate,
                    string alias = null, string schema = null) where TEntity : class { }
                public void FullJoin<TEntity>(IFromClause fromClause, LambdaExpression predicate,
                    string alias = null, string schema = null) where TEntity : class { }
                public void Join<TProjection>(IFromClause fromClause, SqlSubquery<TProjection> subquery,
                    LambdaExpression predicate) where TProjection : class { }
                public void LeftJoin<TProjection>(IFromClause fromClause, SqlSubquery<TProjection> subquery,
                    LambdaExpression predicate) where TProjection : class { }
                public void RightJoin<TProjection>(IFromClause fromClause, SqlSubquery<TProjection> subquery,
                    LambdaExpression predicate) where TProjection : class { }
                public void FullJoin<TProjection>(IFromClause fromClause, SqlSubquery<TProjection> subquery,
                    LambdaExpression predicate) where TProjection : class { }
                public void CrossJoin<TProjection>(SqlSubquery<TProjection> subquery)
                    where TProjection : class { }
                public override IJoinOn Find(Type type) => null;
                public override void Join(string table, string alias = null) { }
                public override void Join(SqlTableReference reference) { }
                public override void Join<TEntity>(string alias = null, string schema = null)
                    { }
                public override void Join(ISqlBuilder builder, string alias) { }
                public override void Join(Action<ISqlBuilder> action, string alias) { }
                public override void AppendJoin(string sql) { }
                public override void LeftJoin(string table, string alias = null) { }
                public override void LeftJoin(SqlTableReference reference) { }
                public override void LeftJoin<TEntity>(string alias = null, string schema = null)
                    { }
                public override void LeftJoin(ISqlBuilder builder, string alias) { }
                public override void LeftJoin(Action<ISqlBuilder> action, string alias) { }
                public override void AppendLeftJoin(string sql) { }
                public override void RightJoin(string table, string alias = null) { }
                public override void RightJoin(SqlTableReference reference) { }
                public override void RightJoin<TEntity>(string alias = null, string schema = null)
                    { }
                public override void RightJoin(ISqlBuilder builder, string alias) { }
                public override void RightJoin(Action<ISqlBuilder> action, string alias) { }
                public override void AppendRightJoin(string sql) { }
                public override void FullJoin(string table, string alias = null) { }
                public override void FullJoin(SqlTableReference reference) { }
                public override void FullJoin<TEntity>(string alias = null, string schema = null)
                    { }
                public override void AppendFullJoin(string sql) { }
                public override void CrossJoin(string table, string alias = null) { }
                public override void CrossJoin(SqlTableReference reference) { }
                public override void CrossJoin<TEntity>(string alias = null, string schema = null)
                    { }
                public override void AppendCrossJoin(string sql) { }
                public override void On(ICondition condition) { }
                public override void On(string column, object value, Operator @operator = Operator.Equal) { }
                public override void On<TLeft, TRight>(Expression<Func<TLeft, object>> left,
                    Expression<Func<TRight, object>> right, Operator @operator = Operator.Equal)
                    { }
                public override void On<TLeft, TRight>(Expression<Func<TLeft, TRight, bool>> expression)
                    { }
                public override void AppendOn(string sql) { }
                public override string ToSql() => string.Empty;
                public override void AppendTo(StringBuilder builder) { }
                public override void Clear() { }
                public override IJoinClause Clone(SqlClauseContext context) => this;
            }

            static class ThirdPartyProvider
            {
                static void Use(ThirdPartyFrom from, ThirdPartySelect select,
                    ThirdPartyGroupBy groupBy, ThirdPartyOrderBy orderBy, ThirdPartyJoin join,
                    IReadOnlyList<TableSource> sources, IParameterManager parameters, ICondition condition)
                {
                    from.AppendRoot(typeof(Item), "i");
                    Expression<Func<Item, object>> column = item => item.Id;
                    from.ResolveMultiSourceColumns(column, sources);
                    from.MergeNewParameters(parameters);
                    select.AppendBoundColumns("[i].[Id]");
                    select.Aggregate(SqlAggregateFunction.Count, Expression.Lambda<Func<Item, object>>(
                        Expression.Convert(Expression.Property(Expression.Parameter(typeof(Item), "item"), "Id"),
                            typeof(object)), Expression.Parameter(typeof(Item), "item")), "i", "count", false);
                    groupBy.AppendBoundColumns(new[] { "[i].[Id]" });
                    groupBy.SetBoundHaving(condition);
                    orderBy.AppendBoundColumns(new[] { "[i].[Id]" }, false);
                    _ = join.TypedSources;
                    LambdaExpression joinPredicate = (Expression<Func<Item, Item, bool>>)(
                        (left, right) => left.Id == right.Id);
                    join.Join<Item>(from, joinPredicate, "i");
                }
            }
            """;

        // Act
        var diagnostics = Compile(source);

        // Assert
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    /// <summary>
    /// 测试目的：实体 Join 使用普通右别名时，显式传入 null 应无歧义地编译为普通入口。
    /// </summary>
    [Fact]
    public void QueryApi_WhenJoinUsesNullRightAlias_ShouldCompile()
    {
        // Arrange
        const string source = """
            using Bing.Data.Sql;

            sealed class Item { public int Id { get; set; } }

            static class Consumer
            {
                static void Use(ISqlQuery query)
                {
                    query.From<Item>("left")
                        .Join<Item, Item>((left, right) => left.Id == right.Id, null)
                        .LeftJoin<Item, Item>((left, right) => left.Id == right.Id, null)
                        .RightJoin<Item, Item>((left, right) => left.Id == right.Id, null)
                        .FullJoin<Item, Item>((left, right) => left.Id == right.Id, null);
                }
            }
            """;

        // Act
        var diagnostics = Compile(source);

        // Assert
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    /// <summary>
    /// 测试目的：四类实体 Join 均应支持普通右别名和 SqlJoinOptions，并允许通过 options 指定 schema。
    /// </summary>
    [Fact]
    public void QueryApi_WhenJoinUsesAliasesAndSchemaOptions_ShouldCompile()
    {
        // Arrange
        const string source = """
            using Bing.Data.Sql;

            sealed class Item { public int Id { get; set; } }

            static class Consumer
            {
                static void Use(ISqlQuery query)
                {
                    query.From<Item>("left")
                        .Join<Item, Item>((left, right) => left.Id == right.Id, "inner")
                        .LeftJoin<Item, Item>((left, right) => left.Id == right.Id, "left_join")
                        .RightJoin<Item, Item>((left, right) => left.Id == right.Id, "right_join")
                        .FullJoin<Item, Item>((left, right) => left.Id == right.Id, "full_join")
                        .Join<Item, Item>((left, right) => left.Id == right.Id,
                            new SqlJoinOptions { RightAlias = "inner_options", LeftAlias = "left", Schema = "reporting" })
                        .LeftJoin<Item, Item>((left, right) => left.Id == right.Id,
                            new SqlJoinOptions { RightAlias = "left_options", LeftAlias = "left", Schema = "reporting" })
                        .RightJoin<Item, Item>((left, right) => left.Id == right.Id,
                            new SqlJoinOptions { RightAlias = "right_options", LeftAlias = "left", Schema = "reporting" })
                        .FullJoin<Item, Item>((left, right) => left.Id == right.Id,
                            new SqlJoinOptions { RightAlias = "full_options", LeftAlias = "left", Schema = "reporting" });
                }
            }
            """;

        // Act
        var diagnostics = Compile(source);

        // Assert
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    /// <summary>
    /// 测试目的：删除的高层 FromTable、ClearSelect 和旧三字符串 Join 入口不得被第三方代码编译使用。
    /// </summary>
    [Fact]
    public void QueryApi_WhenUsingRemovedLegacyMembers_ShouldNotCompile()
    {
        // Arrange
        const string source = """
            using Bing.Data.Sql;

            sealed class Item { public int Id { get; set; } }

            static class Consumer
            {
                static void Use(ISqlQuery query)
                {
                    query.FromTable("Items", "i");
                    query.From<Item>("i").ClearSelect();
                    query.From<Item>("i").Join<Item, Item>((left, right) => left.Id == right.Id, "r", "l");
                }
            }
            """;

        // Act
        var diagnostics = Compile(source);

        // Assert
        Assert.Contains(diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error &&
            diagnostic.GetMessage().Contains("FromTable", StringComparison.Ordinal));
        Assert.Contains(diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error &&
            diagnostic.GetMessage().Contains("ClearSelect", StringComparison.Ordinal));
        Assert.True(diagnostics.Count(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error) >= 3);
    }

    /// <summary>
    /// 测试目的：第三方消费者不得依赖内部 Helper 或 JoinItem 的内部注入和克隆协作。
    /// </summary>
    [Fact]
    public void BuilderInternals_WhenUsedByThirdPartyConsumer_ShouldNotCompile()
    {
        // Arrange
        const string source = """
            using Bing.Data.Sql.Builders.Core;
            using Bing.Data.Sql.Builders.Internal;

            static class Consumer
            {
                static void Use()
                {
                    var helper = new Helper(null);
                    JoinItem.CreateTable("Join", "Orders").SetDependency(helper);
                }
            }
            """;

        // Act
        var diagnostics = Compile(source);

        // Assert
        Assert.Contains(diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error &&
            diagnostic.GetMessage().Contains("Helper", StringComparison.Ordinal));
        Assert.Contains(diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error &&
            diagnostic.GetMessage().Contains("SetDependency", StringComparison.Ordinal));
    }

    /// <summary>
    /// 编译动态 C# 源码并收集诊断信息。
    /// </summary>
    /// <param name="source">待验证公开 SQL 操作契约的 C# 源码。</param>
    /// <returns>编译过程产生的诊断集合。</returns>
    private static IReadOnlyList<Diagnostic> Compile(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest));
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path))
            .Cast<MetadataReference>()
            .ToList();
        references.Add(MetadataReference.CreateFromFile(typeof(ISqlBuilder).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(SqlQueryBase).Assembly.Location));
        var compilation = CSharpCompilation.Create(
            "SqlOperationCompileContract",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        return compilation.GetDiagnostics();
    }

    /// <summary>
    /// 断言因缺少指定扩展方法而产生编译错误。
    /// </summary>
    /// <param name="diagnostics">动态编译产生的诊断集合。</param>
    /// <param name="method">预期出现在错误消息中的缺失方法名称。</param>
    private static void AssertCompileFailedForMissingExtension(IReadOnlyList<Diagnostic> diagnostics, string method)
    {
        Assert.Contains(diagnostics, diagnostic =>
            diagnostic.Severity == DiagnosticSeverity.Error && diagnostic.GetMessage().Contains(method));
    }
}