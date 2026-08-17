using System.Text;
using Bing.Data.Enums;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Conditions;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Mutations.Clauses;
using Bing.Data.Sql.Builders.Mutations.Contexts;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Tests.Samples;
using MutationSqlExecutionKind = Bing.Data.Sql.SqlExecutionKind;

namespace Bing.Data.Sql.Tests.Builders.Mutations;

/// <summary>
/// Mutation 子句直接状态测试。
/// </summary>
public sealed class SqlMutationClauseTest
{
    /// <summary>
    /// 测试目的：Insert、Update 与 Delete 目标表子句应按 Provider 方言完整渲染，并在克隆后隔离表引用状态。
    /// </summary>
    [Fact]
    public void TableClauses_WhenTargetsConfigured_ShouldRenderAndCloneIndependentTables()
    {
        // Arrange
        var context = CreateContext();
        var table = new SqlTableReference { TableName = "orders" };
        var insert = new InsertClause(context);
        var update = new UpdateClause(context);
        var delete = new DeleteClause(context);
        insert.Into(table);
        update.UpdateTable(table);
        delete.From(table);

        // Act
        var insertClone = insert.Clone(CreateContext());
        insert.Into(new SqlTableReference { TableName = "changed_orders" });

        // Assert
        Assert.Equal("Insert Into [orders]", ToSql(insertClone));
        Assert.Equal("Update [orders]", ToSql(update));
        Assert.Equal("Delete From [orders]", ToSql(delete));
        Assert.Equal("Insert Into [changed_orders]", ToSql(insert));
    }

    /// <summary>
    /// 测试目的：目标表子句未配置表时，验证应按操作类型返回明确的错误，避免在后续 SQL 渲染时失败。
    /// </summary>
    [Theory]
    [InlineData(MutationSqlExecutionKind.Insert, "Insert 未指定目标表。")]
    [InlineData(MutationSqlExecutionKind.Update, "Update 未指定目标表。")]
    [InlineData(MutationSqlExecutionKind.Delete, "Delete 未指定目标表。")]
    public void TableClauses_WhenTargetMissing_ShouldRejectValidation(MutationSqlExecutionKind executionKind, string message)
    {
        // Arrange
        var context = CreateContext();
        ISqlValidatable clause = executionKind switch
        {
            MutationSqlExecutionKind.Insert => new InsertClause(context),
            MutationSqlExecutionKind.Update => new UpdateClause(context),
            MutationSqlExecutionKind.Delete => new DeleteClause(context),
            _ => throw new ArgumentOutOfRangeException(nameof(executionKind))
        };
        var validationContext = new SqlValidationContext(TestMutationSqlProvider.Instance, 0, false, executionKind);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => clause.Validate(validationContext));

        // Assert
        Assert.Equal(message, exception.Message);
    }

    /// <summary>
    /// 测试目的：独立 Mutation 表子句渲染失败时，不得向调用方缓冲区遗留关键字前缀。
    /// </summary>
    [Theory]
    [InlineData("Insert", "未指定写操作目标表。")]
    [InlineData("Update", "未指定写操作目标表。")]
    [InlineData("Delete", "未指定写操作目标表。")]
    [InlineData("UpdateFrom", "未指定写操作目标表。")]
    [InlineData("DeleteUsing", "未指定写操作目标表。")]
    public void TableClauses_WhenTargetMissing_ShouldKeepCallerBufferUnchanged(
        string clauseType, string message)
    {
        // Arrange
        var context = CreateContext();
        ISqlContent clause = clauseType switch
        {
            "Insert" => new InsertClause(context),
            "Update" => new UpdateClause(context),
            "Delete" => new DeleteClause(context),
            "UpdateFrom" => new UpdateFromClause(context),
            "DeleteUsing" => new DeleteUsingClause(context),
            _ => throw new ArgumentOutOfRangeException(nameof(clauseType))
        };
        var result = new StringBuilder("Prefix:");

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => clause.AppendTo(result));

        // Assert
        Assert.Equal(message, exception.Message);
        Assert.Equal("Prefix:", result.ToString());
    }

    /// <summary>
    /// 测试目的：Set 子句克隆时必须保留已有参数名称和元数据，后续追加赋值不能影响来源子句。
    /// </summary>
    [Fact]
    public void SetClause_WhenCloned_ShouldPreserveExistingAssignmentsAndIsolateLaterChanges()
    {
        // Arrange
        var sourceContext = CreateContext();
        var source = new SetClause(sourceContext);
        source.Set("Name", new SqlParam("@name", "Bing") { MetadataLevel = SqlParameterMetadataLevel.Full });
        var cloneContext = CreateContext(parameterManager: sourceContext.ParameterManager.Clone());

        // Act
        var clone = source.Clone(cloneContext);
        clone.Set("Age", 18);

        // Assert
        Assert.Equal(" Set [Name] = @name", ToSql(source));
        Assert.Equal(" Set [Name] = @name, [Age] = @_p_0", ToSql(clone));
        var sourceParameters = ((IAdvancedParameterManager)sourceContext.ParameterManager).GetSqlParams();
        var cloneParameters = ((IAdvancedParameterManager)cloneContext.ParameterManager).GetSqlParams();
        Assert.Single(sourceParameters);
        Assert.Equal(2, cloneParameters.Count);
        Assert.Equal(SqlParameterMetadataLevel.Full, cloneParameters["@name"].MetadataLevel);
    }

    /// <summary>
    /// 测试目的：Values 子句应拒绝不支持多行 Values 的 Provider，同时保持来源子句行状态不被验证改变。
    /// </summary>
    [Fact]
    public void ValuesClause_WhenProviderDoesNotSupportMultipleRows_ShouldRejectValidation()
    {
        // Arrange
        var context = CreateContext(SingleRowValuesProvider.Instance);
        var values = new ValuesClause(context);
        values.AddRow(new object[] { "Bing" });
        values.AddRow(new object[] { "Framework" });
        var validationContext = new SqlValidationContext(SingleRowValuesProvider.Instance,
            context.ParameterManager.Count, false, MutationSqlExecutionKind.Insert);

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => values.Validate(validationContext));

        // Assert
        Assert.Equal("Provider test.mutation.single-row-values 不支持多行 Values。", exception.Message);
        Assert.Equal(2, values.RowCount);
        Assert.Equal(1, values.ColumnCount);
    }

    /// <summary>
    /// 测试目的：Values 子句的后续参数格式化失败时，不得向调用方缓冲区遗留关键字、括号或前序参数。
    /// </summary>
    [Fact]
    public void ValuesClause_WhenLaterParameterFormattingFails_ShouldKeepCallerBufferUnchanged()
    {
        // Arrange
        var values = new ValuesClause(CreateContext(FailingValuesRenderProvider.Instance));
        values.AddRow(new object[] { "Bing", "Framework" });
        var result = new StringBuilder("Prefix:");

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => values.AppendTo(result));

        // Assert
        Assert.Equal("Parameter rendering failed.", exception.Message);
        Assert.Equal("Prefix:", result.ToString());
    }

    /// <summary>
    /// 测试目的：Where 子句清空来源后，克隆副本应仍保留已配置条件，并按全表许可规则执行验证。
    /// </summary>
    [Fact]
    public void MutationWhereClause_WhenClonedAndCleared_ShouldPreserveCloneAndValidateAllRowsPolicy()
    {
        // Arrange
        var source = new MutationWhereClause(CreateContext());
        source.And(new EqualCondition("[Id]", "@_p_0"));
        var clone = source.Clone(CreateContext());
        var validationContext = new SqlValidationContext(TestMutationSqlProvider.Instance, 0, false,
            MutationSqlExecutionKind.Delete);

        // Act
        source.Clear();
        var exception = Assert.Throws<InvalidOperationException>(() => source.Validate(validationContext));
        clone.Validate(new SqlValidationContext(TestMutationSqlProvider.Instance, 0, true,
            MutationSqlExecutionKind.Delete));

        // Assert
        Assert.True(source.IsEmpty);
        Assert.Equal("拒绝执行无条件 Delete 操作。", exception.Message);
        Assert.Equal(" Where [Id]=@_p_0", ToSql(clone));
    }

    /// <summary>
    /// 测试目的：Returning 子句应按结构化限定列和结果别名渲染，并在 Clone/Clear 后保持隔离。
    /// </summary>
    [Fact]
    public void ReturningClause_WhenColumnsConfigured_ShouldRenderAndCloneIndependentProjection()
    {
        // Arrange
        var source = new ReturningClause(CreateContext());
        source.AddRange(new[]
        {
            new SqlReturningColumn("id", "t", "Id"),
            new SqlReturningColumn("occurred_at", alias: "OccurredAt")
        });
        var validationContext = new SqlValidationContext(TestMutationSqlProvider.Instance, 0, false,
            MutationSqlExecutionKind.Update);

        // Act
        source.Validate(validationContext);
        var clone = source.Clone(CreateContext());
        source.Clear();

        // Assert
        Assert.True(source.IsEmpty);
        Assert.Equal(" Returning [t].[id] As [Id], [occurred_at] As [OccurredAt]", ToSql(clone));
    }

    /// <summary>
    /// 测试目的：Returning 子句的后续列格式化失败时，不得向调用方缓冲区遗留已渲染的关键字或列。
    /// </summary>
    [Fact]
    public void ReturningClause_WhenLaterColumnIsInvalid_ShouldKeepCallerBufferUnchanged()
    {
        // Arrange
        var clause = new ReturningClause(CreateContext());
        clause.AddRange(new[]
        {
            new SqlReturningColumn("Id"),
            new SqlReturningColumn("invalid;")
        });
        var result = new StringBuilder("Prefix:");

        // Act
        var exception = Assert.Throws<ArgumentException>(() => clause.AppendTo(result));

        // Assert
        Assert.Equal("name", exception.ParamName);
        Assert.Equal("Prefix:", result.ToString());
    }

    /// <summary>
    /// 测试目的：Set 子句的后续赋值列格式化失败时，不得向调用方缓冲区遗留已渲染的 Set 文本。
    /// </summary>
    [Fact]
    public void SetClause_WhenLaterColumnIsInvalid_ShouldKeepCallerBufferUnchanged()
    {
        // Arrange
        var clause = new SetClause(CreateContext());
        clause.Set("Name", "Bing");
        clause.Set("invalid;", "Framework");
        var result = new StringBuilder("Prefix:");

        // Act
        var exception = Assert.Throws<ArgumentException>(() => clause.AppendTo(result));

        // Assert
        Assert.Equal("name", exception.ParamName);
        Assert.Equal("Prefix:", result.ToString());
    }

    /// <summary>
    /// 测试目的：Insert 列子句的后续列格式化失败时，不得向调用方缓冲区遗留左括号或已渲染列。
    /// </summary>
    [Fact]
    public void InsertColumnsClause_WhenLaterColumnIsInvalid_ShouldKeepCallerBufferUnchanged()
    {
        // Arrange
        var clause = new InsertColumnsClause(CreateContext());
        clause.Add("Name");
        clause.Add("invalid;");
        var result = new StringBuilder("Prefix:");

        // Act
        var exception = Assert.Throws<ArgumentException>(() => clause.AppendTo(result));

        // Assert
        Assert.Equal("name", exception.ParamName);
        Assert.Equal("Prefix:", result.ToString());
    }

    /// <summary>
    /// 创建使用指定 Provider 和参数管理器的 Mutation 子句上下文。
    /// </summary>
    /// <param name="provider">SQL Provider；为空时使用默认 Mutation 测试 Provider。</param>
    /// <param name="parameterManager">参数管理器；为空时按 Provider 方言创建。</param>
    /// <returns>独立的 Mutation 子句上下文。</returns>
    private static SqlMutationContext CreateContext(ISqlProvider provider = null, IParameterManager parameterManager = null)
    {
        provider ??= TestMutationSqlProvider.Instance;
        parameterManager ??= provider.ParameterManagerFactory.Create(provider.Dialect);
        return new SqlMutationContext(provider, parameterManager, new SqlBuilderServices(),
            new SqlBuilderExecutionContext(null));
    }

    /// <summary>
    /// 渲染单个 Mutation 子句。
    /// </summary>
    /// <param name="clause">待渲染子句。</param>
    /// <returns>完整子句 SQL 文本。</returns>
    private static string ToSql(ISqlContent clause)
    {
        var builder = new StringBuilder();
        clause.AppendTo(builder);
        return builder.ToString();
    }

    /// <summary>
    /// 只支持单行 Values 的测试 Provider。
    /// </summary>
    private sealed class SingleRowValuesProvider : ISqlProvider, ISqlProviderProfileProvider
    {
        /// <summary>
        /// 测试 Provider 单例。
        /// </summary>
        public static SingleRowValuesProvider Instance { get; } = new();

        /// <inheritdoc />
        public string Key => "test.mutation.single-row-values";

        /// <inheritdoc />
        public DatabaseType DatabaseType => TestMutationSqlProvider.Instance.DatabaseType;

        /// <inheritdoc />
        public IDialect Dialect => TestMutationSqlProvider.Instance.Dialect;

        /// <inheritdoc />
        public ISqlClauseFactory ClauseFactory => TestMutationSqlProvider.Instance.ClauseFactory;

        /// <inheritdoc />
        public ISqlTableReferenceParser TableReferenceParser => TestMutationSqlProvider.Instance.TableReferenceParser;

        /// <inheritdoc />
        public ISqlPaginationRenderer PaginationRenderer => TestMutationSqlProvider.Instance.PaginationRenderer;

        /// <inheritdoc />
        public IParameterManagerFactory ParameterManagerFactory => TestMutationSqlProvider.Instance.ParameterManagerFactory;

        /// <inheritdoc />
        public IParamLiteralsResolver ParamLiteralsResolver => TestMutationSqlProvider.Instance.ParamLiteralsResolver;

        /// <inheritdoc />
        public SqlProviderProfile Profile { get; } = new()
        {
            Mutation = new SqlProviderMutationCapabilities { SupportsMultiRowValues = false }
        };
    }

    /// <summary>
    /// 在第二个参数名格式化时失败的测试 Provider。
    /// </summary>
    private sealed class FailingValuesRenderProvider : ISqlProvider, ISqlProviderProfileProvider
    {
        /// <summary>
        /// 测试 Provider 单例。
        /// </summary>
        public static FailingValuesRenderProvider Instance { get; } = new();

        /// <inheritdoc />
        public string Key => "test.mutation.failing-values-render";

        /// <inheritdoc />
        public DatabaseType DatabaseType => TestMutationSqlProvider.Instance.DatabaseType;

        /// <inheritdoc />
        public IDialect Dialect { get; } = new FailingParameterDialect();

        /// <inheritdoc />
        public ISqlClauseFactory ClauseFactory => TestMutationSqlProvider.Instance.ClauseFactory;

        /// <inheritdoc />
        public ISqlTableReferenceParser TableReferenceParser => TestMutationSqlProvider.Instance.TableReferenceParser;

        /// <inheritdoc />
        public ISqlPaginationRenderer PaginationRenderer => TestMutationSqlProvider.Instance.PaginationRenderer;

        /// <inheritdoc />
        public IParameterManagerFactory ParameterManagerFactory => TestMutationSqlProvider.Instance.ParameterManagerFactory;

        /// <inheritdoc />
        public IParamLiteralsResolver ParamLiteralsResolver => TestMutationSqlProvider.Instance.ParamLiteralsResolver;

        /// <inheritdoc />
        public SqlProviderProfile Profile { get; } = new();
    }

    /// <summary>
    /// 在指定参数名格式化时失败的测试方言。
    /// </summary>
    private sealed class FailingParameterDialect : DialectBase
    {
        /// <inheritdoc />
        public override string GetParamName(string paramName)
        {
            if (paramName == "@_p_1")
                throw new InvalidOperationException("Parameter rendering failed.");
            return base.GetParamName(paramName);
        }
    }
}
