using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Mutations;
using Bing.Data.Sql.Tests.Samples;
using Moq;

namespace Bing.Data.Sql.Tests.Builders.Mutations;

/// <summary>
/// Insert Mutation Builder 测试。
/// </summary>
public sealed class SqlInsertBuilderTest
{
    /// <summary>
    /// 测试目的：Insert Fluent API 应返回原 Builder，并按子句顺序输出参数化多行 Values SQL。
    /// </summary>
    [Fact]
    public void InsertInto_WhenColumnsAndValuesConfigured_ShouldRenderExpectedSql()
    {
        // Arrange
        var builder = new SqlInsertBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices());

        // Act
        var result = builder.InsertInto<SqlInsertBuilder, MutationSample>()
            .Columns(nameof(MutationSample.Name), nameof(MutationSample.Age))
            .Values("Bing", 18)
            .Values(new[] { (object)"Framework", 20 });

        // Assert
        Assert.Same(builder, result);
        Assert.Equal("Insert Into [samples] ([Name], [Age]) Values (@_p_0, @_p_1), (@_p_2, @_p_3)", builder.ToSql());
        Assert.Equal(4, builder.GetParameters().Count);
    }

    /// <summary>
    /// 测试目的：强类型 Columns 应通过实体映射输出物理列名，且保持后续 Values 参数化行为。
    /// </summary>
    [Fact]
    public void Columns_WhenTypedMappedPropertiesAreProvided_ShouldRenderMappedColumns()
    {
        // Arrange
        var builder = new SqlInsertBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices());

        // Act
        builder.InsertInto<MutationSample>()
            .Columns<MutationSample>(item => new object[] { item.Name, item.Age })
            .Values("Bing", 18);

        // Assert
        Assert.Equal("Insert Into [samples] ([Name], [Age]) Values (@_p_0, @_p_1)", builder.ToSql());
        Assert.Equal(new object[] { "Bing", 18 }, builder.BuildCommand().Parameters.Select(item => item.Value));
    }

    /// <summary>
    /// 测试目的：SQL 渲染不得导出参数快照，BuildCommand 应仅导出一次带元数据的可执行参数快照。
    /// </summary>
    [Fact]
    public void BuildCommand_WhenParametersConfigured_ShouldExportSingleSnapshotAfterRendering()
    {
        // Arrange
        var parameterManager = new CountingParameterManager(TestMutationSqlProvider.Instance.Dialect);
        var builder = new SqlInsertBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices(), parameterManager)
            .InsertInto<SqlInsertBuilder, MutationSample>()
            .Columns(nameof(MutationSample.Name))
            .Values("Bing");

        // Act
        var sql = builder.ToSql();
        var command = builder.BuildCommand();

        // Assert
        Assert.Equal("Insert Into [samples] ([Name]) Values (@_p_0)", sql);
        Assert.Equal(sql, command.Sql);
        Assert.Single(command.Parameters);
        Assert.Equal(1, parameterManager.GetSqlParamsCallCount);
        Assert.Equal(1, parameterManager.Count);
    }

    /// <summary>
    /// 测试目的：Mutation 描述创建后必须冻结 SQL 和参数，后续 Builder 写入或数组值变更不能影响已创建描述。
    /// </summary>
    [Fact]
    public void ToMutationDescription_WhenBuilderChangesAfterCreation_ShouldKeepSqlAndParametersIndependent()
    {
        // Arrange
        var payload = new byte[] { 1, 2 };
        var builder = new TestSqlBuilder()
            .InsertInto("samples")
            .Columns(nameof(MutationSample.Name))
            .Values(payload);

        // Act
        var description = ((ISqlBuilder)builder).ToMutationDescription();
        builder.Values("later");
        payload[0] = 9;

        // Assert
        Assert.Equal("Insert Into [samples] ([Name]) Values (@_p_0)", description.Sql);
        Assert.Equal("test.sqlserver", description.ProviderKey);
        Assert.Single(description.Parameters);
        Assert.Equal(new byte[] { 1, 2 }, Assert.IsType<byte[]>(description.Parameters[0].Value));
    }

    /// <summary>
    /// 测试目的：Mutation 描述应递归冻结数组、集合和字典参数，避免嵌套容器在 Builder 创建后改变本次执行输入。
    /// </summary>
    [Fact]
    public void ToMutationDescription_WhenNestedParameterContainersChange_ShouldKeepIndependentSnapshots()
    {
        // Arrange
        var payload = new byte[] { 1, 2 };
        var values = new List<int> { 3, 4 };
        var parameter = new Dictionary<string, object>
        {
            ["Payload"] = payload,
            ["Values"] = values
        };
        var builder = new TestSqlBuilder()
            .InsertInto("samples")
            .Columns(nameof(MutationSample.Name))
            .Values(parameter);

        // Act
        var description = builder.ToMutationDescription();
        payload[0] = 9;
        values[0] = 8;
        parameter["Payload"] = new byte[] { 7 };
        var snapshot = Assert.IsType<Dictionary<string, object>>(description.Parameters[0].Value);

        // Assert
        Assert.Equal(new byte[] { 1, 2 }, Assert.IsType<byte[]>(snapshot["Payload"]));
        Assert.Equal(new object[] { 3, 4 }, Assert.IsType<object[]>(snapshot["Values"]));
    }

    /// <summary>
    /// 测试 - 调用方修改公开参数容器后，Mutation 描述应保留内部执行快照。
    /// </summary>
    [Fact]
    public void ToMutationDescription_WhenExposedParameterContainerIsMutated_ShouldPreserveExecutionSnapshot()
    {
        // Arrange
        var builder = new TestSqlBuilder()
            .InsertInto("samples")
            .Columns(nameof(MutationSample.Name))
            .Values(new Dictionary<string, object> { ["Payload"] = new byte[] { 1, 2 } });
        var description = builder.ToMutationDescription();

        // Act
        var exposed = Assert.IsType<Dictionary<string, object>>(description.Parameters[0].Value);
        ((byte[])exposed["Payload"])[0] = 9;
        exposed["Payload"] = new byte[] { 8 };
        var executionParameter = Assert.Single(description.CreateParameters());
        var executionValue = Assert.IsType<Dictionary<string, object>>(executionParameter.Value);

        // Assert
        Assert.Equal(new byte[] { 1, 2 }, Assert.IsType<byte[]>(executionValue["Payload"]));
    }

    /// <summary>
    /// 测试目的：Mutation 描述必须在 SQL 渲染完成后只导出一次参数快照，确保渲染期间合并的参数与 SQL 对应。
    /// </summary>
    [Fact]
    public void ToMutationDescription_WhenParametersConfigured_ShouldRenderBeforeExportingSnapshot()
    {
        // Arrange
        var parameterManager = new CountingParameterManager(TestMutationSqlProvider.Instance.Dialect);
        var builder = new TestSqlBuilder(parameterManager: parameterManager)
            .InsertInto("samples")
            .Columns(nameof(MutationSample.Name))
            .Values("Bing");

        // Act
        var description = builder.ToMutationDescription();

        // Assert
        Assert.Equal("Insert Into [samples] ([Name]) Values (@_p_0)", description.Sql);
        Assert.Equal("test.sqlserver", description.ProviderKey);
        Assert.Single(description.Parameters);
        Assert.Equal(1, parameterManager.GetSqlParamsCallCount);
    }

    /// <summary>
    /// 测试目的：第三方 Builder 未提供 Provider 时必须在渲染 SQL 前明确拒绝，避免生成无法验证执行身份的 Mutation 描述。
    /// </summary>
    [Fact]
    public void ToMutationDescription_WhenBuilderDoesNotProvideProvider_ShouldRejectBeforeRendering()
    {
        // Arrange
        var builder = new Mock<ISqlBuilder>();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Object.ToMutationDescription());

        // Assert
        Assert.Equal("Mutation Builder 必须提供 SQL Provider。", exception.Message);
        builder.Verify(item => item.ToSql(), Times.Never);
    }

    /// <summary>
    /// 测试目的：同一 Mutation 描述的每次执行准备都必须返回独立参数，避免 Provider 绑定污染后续执行。
    /// </summary>
    [Fact]
    public void MutationDescription_WhenParametersArePreparedRepeatedly_ShouldCreateIndependentParameterInstances()
    {
        // Arrange
        var description = new TestSqlBuilder()
            .InsertInto("samples")
            .Columns(nameof(MutationSample.Name))
            .Values(new byte[] { 1, 2 })
            .ToMutationDescription();

        // Act
        var first = Assert.Single(description.CreateParameters());
        var second = Assert.Single(description.CreateParameters());
        ((byte[])first.Value)[0] = 9;

        // Assert
        Assert.NotSame(first, second);
        Assert.NotSame(first.Value, second.Value);
        Assert.Equal(new byte[] { 1, 2 }, second.Value);
    }

    /// <summary>
    /// 测试目的：Values 行列数量不一致时应在状态写入前抛出明确异常。
    /// </summary>
    [Fact]
    public void Values_WhenRowColumnCountDiffers_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var builder = new SqlInsertBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices())
            .InsertInto<SqlInsertBuilder, MutationSample>()
            .Columns(nameof(MutationSample.Name), nameof(MutationSample.Age))
            .Values("Bing", 18);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Values("Framework"));

        // Assert
        Assert.Equal("Insert Values 行列数量不一致。", exception.Message);
    }

    /// <summary>
    /// 测试目的：Clone 应复制 Insert Clause 状态和参数，同时保持后续写入隔离。
    /// </summary>
    [Fact]
    public void Clone_WhenSourceContainsValues_ShouldKeepInstancesIndependent()
    {
        // Arrange
        var source = new SqlInsertBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices())
            .InsertInto<SqlInsertBuilder, MutationSample>()
            .Columns(nameof(MutationSample.Name))
            .Values("source");

        // Act
        var clone = (SqlInsertBuilder)source.Clone();
        clone.Values("clone");

        // Assert
        Assert.Equal("Insert Into [samples] ([Name]) Values (@_p_0)", source.ToSql());
        Assert.Equal("Insert Into [samples] ([Name]) Values (@_p_0), (@_p_1)", clone.ToSql());
    }

    /// <summary>
    /// 映射到测试表的 Insert 实体。
    /// </summary>
    [Table("samples")]
    private sealed class MutationSample
    {
        /// <summary>
        /// 名称。
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 年龄。
        /// </summary>
        public int Age { get; set; }
    }

    /// <summary>
    /// 用于验证参数快照导出次数的增强参数管理器。
    /// </summary>
    private sealed class CountingParameterManager : IAdvancedParameterManager
    {
        /// <summary>
        /// 实际参数管理器。
        /// </summary>
        private readonly ParameterManager _inner;

        /// <summary>
        /// 初始化一个 <see cref="CountingParameterManager"/> 类型的实例。
        /// </summary>
        /// <param name="dialect">SQL 方言。</param>
        public CountingParameterManager(IDialect dialect) => _inner = new ParameterManager(dialect);

        /// <summary>
        /// 获取增强参数快照的调用次数。
        /// </summary>
        public int GetSqlParamsCallCount { get; private set; }

        /// <inheritdoc />
        public int Count => _inner.Count;

        /// <inheritdoc />
        public string GenerateName() => _inner.GenerateName();

        /// <inheritdoc />
        public string NormalizeName(string name) => _inner.NormalizeName(name);

        /// <inheritdoc />
        public void Add(string name, object value, Operator? @operator = null) => _inner.Add(name, value, @operator);

        /// <inheritdoc />
        public void Add(SqlParam parameter) => _inner.Add(parameter);

        /// <inheritdoc />
        public IReadOnlyDictionary<string, object> GetParams() => _inner.GetParams();

        /// <inheritdoc />
        public IReadOnlyDictionary<string, SqlParam> GetSqlParams()
        {
            GetSqlParamsCallCount++;
            return _inner.GetSqlParams();
        }

        /// <inheritdoc />
        public IReadOnlyDictionary<string, object> ExportValues() => _inner.ExportValues();

        /// <inheritdoc />
        public bool Contains(string name) => _inner.Contains(name);

        /// <inheritdoc />
        public object GetValue(string name) => _inner.GetValue(name);

        /// <inheritdoc />
        public IParameterManager Clone() => _inner.Clone();

        /// <inheritdoc />
        public IParameterManager CreateEmpty() => _inner.CreateEmpty();

        /// <inheritdoc />
        public void Clear() => _inner.Clear();
    }
}