using Bing.Data.Enums;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations.Batching;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Mutations;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests.Builders.Mutations.Batching;

/// <summary>
/// Mutation 批处理选项测试。
/// </summary>
public sealed class SqlMutationBatchOptionsTest
{
    /// <summary>
    /// 测试目的：实体 Mutation 选项不得暴露全表写入绕过成员，避免上层实体 API 生成无条件 Update 或 Delete。
    /// </summary>
    [Fact]
    public void EntityMutationOptions_ShouldNotExposeAllowAllRows()
    {
        // Arrange and Act
        var updateProperty = typeof(SqlUpdateOptions).GetProperty("AllowAllRows");
        var deleteProperty = typeof(SqlDeleteOptions).GetProperty("AllowAllRows");

        // Assert
        Assert.Null(updateProperty);
        Assert.Null(deleteProperty);
    }

    /// <summary>
    /// 测试目的：实体 Mutation 选项不得暴露弱类型 OriginalValues，且并发冲突默认必须抛出异常。
    /// </summary>
    [Fact]
    public void EntityMutationOptions_ShouldRemoveWeakOriginalValuesAndDefaultToThrow()
    {
        // Arrange and Act
        var updateProperty = typeof(SqlUpdateOptions).GetProperty("OriginalValues");
        var deleteProperty = typeof(SqlDeleteOptions).GetProperty("OriginalValues");
        var updateOptions = new SqlUpdateOptions();
        var deleteOptions = new SqlDeleteOptions();

        // Assert
        Assert.Null(updateProperty);
        Assert.Null(deleteProperty);
        Assert.Equal(SqlConcurrencyConflictBehavior.Throw, updateOptions.ConcurrencyConflictBehavior);
        Assert.Equal(SqlConcurrencyConflictBehavior.Throw, deleteOptions.ConcurrencyConflictBehavior);
    }

    /// <summary>
    /// 测试目的：调用方只能收紧 Provider 参数上限，不能通过批处理选项放宽已声明的硬性限制。
    /// </summary>
    [Fact]
    public void GetEffectiveMaxParameterCount_WhenProviderLimitExists_ShouldNotAllowWidening()
    {
        // Arrange
        var limitedProvider = new ParameterLimitedProvider(5);

        // Act
        var providerDefault = new SqlMutationBatchOptions().GetEffectiveMaxParameterCount(limitedProvider);
        var callerTightens = new SqlMutationBatchOptions { MaxParameterCount = 3 }
            .GetEffectiveMaxParameterCount(limitedProvider);
        var callerWidens = new SqlMutationBatchOptions { MaxParameterCount = 7 }
            .GetEffectiveMaxParameterCount(limitedProvider);
        var noLimit = new SqlMutationBatchOptions().GetEffectiveMaxParameterCount(TestMutationSqlProvider.Instance);

        // Assert
        Assert.Equal(5, providerDefault);
        Assert.Equal(3, callerTightens);
        Assert.Equal(5, callerWidens);
        Assert.Null(noLimit);
    }

    /// <summary>
    /// 声明固定参数上限的测试 Provider。
    /// </summary>
    private sealed class ParameterLimitedProvider : ISqlProvider, ISqlParameterLimitProvider
    {
        /// <summary>
        /// 复用常规测试 Provider 的不可变方言与工厂依赖。
        /// </summary>
        private readonly ISqlProvider _inner = TestMutationSqlProvider.Instance;

        /// <summary>
        /// 初始化一个 <see cref="ParameterLimitedProvider"/> 类型的实例。
        /// </summary>
        /// <param name="maxParameterCount">允许的最大参数数量。</param>
        public ParameterLimitedProvider(int maxParameterCount) => MaxParameterCount = maxParameterCount;

        /// <inheritdoc />
        public string Key => "test.mutation.limited";

        /// <inheritdoc />
        public DatabaseType DatabaseType => _inner.DatabaseType;

        /// <inheritdoc />
        public IDialect Dialect => _inner.Dialect;

        /// <inheritdoc />
        public ISqlClauseFactory ClauseFactory => _inner.ClauseFactory;

        /// <inheritdoc />
        public ISqlTableReferenceParser TableReferenceParser => _inner.TableReferenceParser;

        /// <inheritdoc />
        public ISqlPaginationRenderer PaginationRenderer => _inner.PaginationRenderer;

        /// <inheritdoc />
        public IParameterManagerFactory ParameterManagerFactory => _inner.ParameterManagerFactory;

        /// <inheritdoc />
        public IParamLiteralsResolver ParamLiteralsResolver => _inner.ParamLiteralsResolver;

        /// <inheritdoc />
        public int? MaxParameterCount { get; }
    }
}