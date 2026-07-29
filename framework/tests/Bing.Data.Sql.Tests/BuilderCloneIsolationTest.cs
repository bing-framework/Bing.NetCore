using Bing.Data.Sql.Tests.Samples;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Tests;

/// <summary>
/// SQL Builder 克隆隔离测试。
/// </summary>
public class BuilderCloneIsolationTest
{
    /// <summary>
    /// 测试 - 修改克隆 Builder 的分页不应影响原 Builder。
    /// </summary>
    [Fact]
    public void Clone_WhenChangingPagination_ShouldNotChangeSourceBuilder()
    {
        // Arrange
        var source = new TestSqlBuilder();
        source.Take(10);
        var clone = (TestSqlBuilder)source.Clone();

        // Act
        clone.Take(20);

        // Assert
        Assert.Equal(10, source.Pager.PageSize);
        Assert.Equal(20, clone.Pager.PageSize);
        Assert.Equal(10, source.GetParam("@_p_0"));
        Assert.Equal(20, clone.GetParam("@_p_0"));
    }

    /// <summary>
    /// 测试 - 修改克隆 Builder 的 Join 不应影响原 Builder。
    /// </summary>
    [Fact]
    public void Clone_WhenAddingJoin_ShouldNotChangeSourceBuilder()
    {
        // Arrange
        var source = new TestSqlBuilder();
        source.From<Sample>("s");
        var clone = (TestSqlBuilder)source.Clone();

        // Act
        clone.LeftJoin<Sample2>("s2");

        // Assert
        Assert.Equal(string.Empty, source.JoinClause.ToSql());
        Assert.Equal("Left Join [Sample2] As [s2]", clone.JoinClause.ToSql());
    }

    /// <summary>
    /// 测试 - 修改克隆 Builder 的参数不应影响原 Builder。
    /// </summary>
    [Fact]
    public void Clone_WhenAddingParameter_ShouldNotChangeSourceBuilder()
    {
        // Arrange
        var source = new TestSqlBuilder();
        source.Where("Name", "source");
        var clone = (TestSqlBuilder)source.Clone();

        // Act
        clone.Where("Age", 18);

        // Assert
        Assert.Single(source.GetParams());
        Assert.Equal("source", source.GetParam("@_p_0"));
        Assert.Equal(2, clone.GetParams().Count);
        Assert.Equal(18, clone.GetParam("@_p_1"));
    }

    /// <summary>
    /// 测试 - Clear 应清理原始 From、Join、Where、参数、分页和别名状态。
    /// </summary>
    [Fact]
    public void Clear_WhenBuilderContainsRawSqlAndPagination_ShouldRemoveAllPreviousState()
    {
        // Arrange
        var builder = new TestSqlBuilder();
        builder.AppendFrom("Orders o")
            .AppendJoin("Items i")
            .AppendOn("i.OrderId=o.Id")
            .Where("o.Status", 2)
            .Take(5);

        // Act
        var sql = builder.Clear()
            .Select("n.Id")
            .AppendFrom("NewOrders n")
            .ToSql();

        // Assert
        Assert.Equal("Select [n].[Id] \r\nFrom NewOrders n", sql);
        Assert.Empty(builder.GetParams());
        Assert.Equal(20, builder.Pager.PageSize);
        Assert.Equal(string.Empty, builder.JoinClause.ToSql());
    }

    /// <summary>
    /// 测试 - 不同并发操作使用独立 Clone 实例时，条件和参数不应污染来源或其他副本。
    /// </summary>
    [Fact]
    public async Task Clone_WhenIndependentBuildersRunConcurrently_ShouldKeepSourceAndClonesIsolated()
    {
        // Arrange
        var source = new TestSqlBuilder();
        source.Select("*").From("Orders");
        var first = source.Clone();
        var second = source.Clone();
        var start = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Act
        var firstTask = Task.Run(async () =>
        {
            await start.Task;
            return first.Where("OrderId", 1).ToSql();
        });
        var secondTask = Task.Run(async () =>
        {
            await start.Task;
            return second.Where("OrderId", 2).ToSql();
        });
        start.SetResult(true);
        var sql = await Task.WhenAll(firstTask, secondTask);

        // Assert
        Assert.Equal("Select * \r\nFrom [Orders] \r\nWhere [OrderId]=@_p_0", sql[0]);
        Assert.Equal(sql[0], sql[1]);
        Assert.Empty(source.GetParams());
        Assert.Equal(1, first.GetParam("@_p_0"));
        Assert.Equal(2, second.GetParam("@_p_0"));
    }

    /// <summary>
    /// 测试目的：参数管理器 Clone 返回 null 时，Builder 必须拒绝生成不可用副本。
    /// </summary>
    [Fact]
    public void Clone_WhenParameterManagerCloneReturnsNull_ShouldThrow()
    {
        // Arrange
        var source = new TestSqlBuilder(parameterManager: new InvalidCloneParameterManager(true));

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => source.Clone());

        // Assert
        Assert.Equal("参数管理器克隆时返回了 null。", exception.Message);
    }

    /// <summary>
    /// 测试目的：参数管理器 Clone 返回自身时，Builder 必须拒绝共享可变参数状态。
    /// </summary>
    [Fact]
    public void Clone_WhenParameterManagerCloneReturnsSource_ShouldThrow()
    {
        // Arrange
        var source = new TestSqlBuilder(parameterManager: new InvalidCloneParameterManager(false));

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => source.Clone());

        // Assert
        Assert.Equal("参数管理器克隆时不能返回当前实例。", exception.Message);
    }

    /// <summary>
    /// 测试目的：延迟初始化参数管理器的 Builder 清理参数时不应发生空引用。
    /// </summary>
    [Fact]
    public void ClearSqlParams_WhenParameterManagerIsNotInitialized_ShouldCreateAndClearManager()
    {
        // Arrange
        var builder = new TestSqlBuilder();

        // Act
        builder.ClearSqlParams();

        // Assert
        Assert.Empty(builder.GetParams());
    }

    /// <summary>
    /// 返回无效 Clone 结果的参数管理器测试替身。
    /// </summary>
    private sealed class InvalidCloneParameterManager : ParameterManager
    {
        /// <summary>
        /// 是否返回 null。
        /// </summary>
        private readonly bool _returnNull;

        /// <summary>
        /// 初始化测试替身。
        /// </summary>
        /// <param name="returnNull">是否返回 null。</param>
        public InvalidCloneParameterManager(bool returnNull) : base(TestDialect.Instance) => _returnNull = returnNull;

        /// <inheritdoc />
        public override IParameterManager Clone() => _returnNull ? null : this;
    }
}