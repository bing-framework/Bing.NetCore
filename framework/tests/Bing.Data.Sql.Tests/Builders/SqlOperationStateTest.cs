using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests.Builders;

/// <summary>
/// 统一 SQL Builder 操作状态测试。
/// </summary>
public class SqlOperationStateTest
{
    /// <summary>
    /// 用于状态迁移测试的默认目标表引用。
    /// </summary>
    private static SqlTableReference Table => new() { TableName = "samples" };

    /// <summary>
    /// 测试 - Select 后调用 Update 应立即失败。
    /// </summary>
    [Fact]
    public void Update_WhenBuilderIsSelect_ShouldThrowImmediately()
    {
        // Arrange
        var builder = new TestSqlBuilder().Select("Id");

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Update(Table));

        // Assert
        Assert.Equal("当前 Builder 已处于 Select 状态，不能调用 Update。", exception.Message);
        Assert.Equal(SqlOperationKind.Select, builder.OperationKind);
    }

    /// <summary>
    /// 测试 - Update 后调用 InsertInto 应立即失败。
    /// </summary>
    [Fact]
    public void InsertInto_WhenBuilderIsUpdate_ShouldThrowImmediately()
    {
        // Arrange
        var builder = new TestSqlBuilder().Update(Table);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.InsertInto(Table));

        // Assert
        Assert.Equal("当前 Builder 已处于 Update 状态，不能调用 InsertInto。", exception.Message);
        Assert.Equal(SqlOperationKind.Update, builder.OperationKind);
    }

    /// <summary>
    /// 测试目的：UpdateFrom 只能追加到 Update 状态，不能隐式创建缺少目标表的 Update。
    /// </summary>
    [Fact]
    public void UpdateFrom_WhenBuilderHasNoUpdate_ShouldThrowWithoutChangingClause()
    {
        // Arrange
        var builder = new TestSqlBuilder();
        var source = new SqlTableReference { TableName = "sample_updates", Alias = "s" };

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.UpdateFrom(source));

        // Assert
        Assert.Equal("当前 Builder 已处于 None 状态，不能调用 UpdateFrom。", exception.Message);
        Assert.Null(builder.UpdateFromClause.Table);
        Assert.Equal(SqlOperationKind.None, builder.OperationKind);
    }

    /// <summary>
    /// 测试目的：DeleteUsing 只能追加到 Delete 状态，不能隐式创建缺少目标表的 Delete。
    /// </summary>
    [Fact]
    public void DeleteUsing_WhenBuilderHasNoDelete_ShouldThrowWithoutChangingClause()
    {
        // Arrange
        var builder = new TestSqlBuilder();
        var source = new SqlTableReference { TableName = "sample_deletes", Alias = "s" };

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.DeleteUsing(source));

        // Assert
        Assert.Equal("当前 Builder 已处于 None 状态，不能调用 DeleteUsing。", exception.Message);
        Assert.Null(builder.DeleteUsingClause.Table);
        Assert.Equal(SqlOperationKind.None, builder.OperationKind);
    }

    /// <summary>
    /// 测试目的：Returning 只能追加到已完成类型判定的 Mutation 状态。
    /// </summary>
    [Fact]
    public void Returning_WhenBuilderHasNoMutation_ShouldThrowWithoutChangingClause()
    {
        // Arrange
        var builder = new TestSqlBuilder();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Returning("Id"));

        // Assert
        Assert.Equal("当前 Builder 已处于 None 状态，不能调用 Returning。", exception.Message);
        Assert.True(builder.ReturningClause.IsEmpty);
        Assert.Equal(SqlOperationKind.None, builder.OperationKind);
    }

    /// <summary>
    /// 测试 - Delete 后调用 Set 应立即失败且不新增参数。
    /// </summary>
    [Fact]
    public void Set_WhenBuilderIsDelete_ShouldThrowWithoutAddingParameter()
    {
        // Arrange
        var builder = new TestSqlBuilder().DeleteFrom(Table);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Set("Name", "Bing"));

        // Assert
        Assert.Equal("当前 Builder 已处于 Delete 状态，不能调用 Set。", exception.Message);
        Assert.Equal(0, builder.ParameterManager.Count);
    }

    /// <summary>
    /// 测试 - Insert Values 后调用 Select 应立即失败。
    /// </summary>
    [Fact]
    public void Select_WhenBuilderIsInsertValues_ShouldThrowImmediately()
    {
        // Arrange
        var builder = new TestSqlBuilder().InsertInto(Table).Columns("Id").Values(1);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Select("Id"));

        // Assert
        Assert.Equal("当前 Builder 已处于 InsertValues 状态，不能调用 Select。", exception.Message);
        Assert.Equal(SqlOperationKind.InsertValues, builder.OperationKind);
    }

    /// <summary>
    /// 测试 - Insert Select 后调用 Values 应立即失败且不新增参数。
    /// </summary>
    [Fact]
    public void Values_WhenBuilderIsInsertSelect_ShouldThrowWithoutAddingParameter()
    {
        // Arrange
        var builder = new TestSqlBuilder().InsertInto(Table).Columns("Id").Select("Id").From("source_samples");

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Values(1));

        // Assert
        Assert.Equal("当前 Builder 已处于 InsertSelect 状态，不能调用 Values。", exception.Message);
        Assert.Equal(0, builder.ParameterManager.Count);
    }

    /// <summary>
    /// 测试 - InsertInto 后公开状态应保持 None，直到 Values 或 Select 确定操作类型。
    /// </summary>
    [Fact]
    public void InsertInto_WhenSourceIsPending_ShouldExposeNoneUntilSourceIsSelected()
    {
        // Arrange
        var builder = new TestSqlBuilder().InsertInto(Table).Columns("Id");

        // Act
        var pending = builder.OperationKind;
        builder.Select("Id");

        // Assert
        Assert.Equal(SqlOperationKind.None, pending);
        Assert.Equal(SqlOperationKind.InsertSelect, builder.OperationKind);
    }

    /// <summary>
    /// 测试 - Clear 后应清空状态并允许切换到其他操作。
    /// </summary>
    [Fact]
    public void Clear_WhenBuilderHasOperation_ShouldResetStateAndAllowSwitch()
    {
        // Arrange
        var builder = new TestSqlBuilder().Update(Table).Set("Name", "Bing");

        // Act
        builder.Clear().DeleteFrom(Table).AllowAllRows();

        // Assert
        Assert.Equal(SqlOperationKind.Delete, builder.OperationKind);
        Assert.Equal("Delete From [samples]", builder.ToSql());
    }

    /// <summary>
    /// 测试 - Clone 应保留操作状态且后续修改相互隔离。
    /// </summary>
    [Fact]
    public void Clone_WhenBuilderIsUpdate_ShouldPreserveIndependentState()
    {
        // Arrange
        var source = new TestSqlBuilder().Update(Table).Set("Name", "Bing").AllowAllRows();

        // Act
        var clone = source.Clone();
        source.Clear().DeleteFrom(Table).AllowAllRows();

        // Assert
        Assert.Equal(SqlOperationKind.Update, clone.OperationKind);
        Assert.Equal(SqlOperationKind.Delete, source.OperationKind);
        Assert.Equal("Update [samples] Set [Name] = @_p_0", clone.ToSql());
    }

    /// <summary>
    /// 测试 - New 应共享配置但返回空状态 Builder。
    /// </summary>
    [Fact]
    public void New_WhenSourceHasOperation_ShouldReturnNoneState()
    {
        // Arrange
        var source = new TestSqlBuilder().DeleteFrom(Table).AllowAllRows();

        // Act
        var result = source.New();

        // Assert
        Assert.Equal(SqlOperationKind.None, result.OperationKind);
        Assert.Equal(0, ((ISqlCommonPartAccessor)result).ParameterManager.Count);
    }
}