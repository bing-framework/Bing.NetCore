using System.Data;
using Bing.Data.Sql.Builders.Multiple;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Tests.Builders.Multiple;

/// <summary>
/// 多结果集批处理命令 Builder 测试。
/// </summary>
public class SqlMultipleQueryBatchBuilderTest
{
    /// <summary>
    /// 测试目的：多个语句应通过指定分隔符组合，且输入末尾分隔符不应重复输出。
    /// </summary>
    [Fact]
    public void Build_WhenMultipleStatementsAreAppended_ShouldJoinStatementsWithSingleSeparator()
    {
        // Arrange
        var builder = new SqlMultipleQueryBatchBuilder(';');

        // Act
        var command = builder.Append(" Select Id From Orders; ")
            .Append("Select Count(*) From Orders;;")
            .Build();

        // Assert
        Assert.Equal("Select Id From Orders;" + Environment.NewLine + "Select Count(*) From Orders", command.Sql);
        Assert.Empty(command.Parameters);
    }

    /// <summary>
    /// 测试目的：已生成命令必须拥有独立参数快照，调用方修改源参数不应影响命令值或元数据。
    /// </summary>
    [Fact]
    public void Build_WhenSourceParameterChangesAfterBuild_ShouldPreserveParameterSnapshot()
    {
        // Arrange
        var parameter = new SqlParam("id", 7, DbType.Int32)
        {
            OriginalValue = 6,
            Source = SqlParameterSource.Manual,
            MetadataLevel = SqlParameterMetadataLevel.Full,
            ProviderTypeName = "INTEGER"
        };
        var builder = new SqlMultipleQueryBatchBuilder(';');

        // Act
        var command = builder.Append("Select Id From Orders Where Id=@id", new[] { parameter }).Build();
        parameter.Value = 8;
        parameter.OriginalValue = 7;
        parameter.ProviderTypeName = "TEXT";
        var snapshot = Assert.Single(command.Parameters);

        // Assert
        Assert.NotSame(parameter, snapshot);
        Assert.Equal(7, snapshot.Value);
        Assert.Equal(6, snapshot.OriginalValue);
        Assert.Equal("INTEGER", snapshot.ProviderTypeName);
        Assert.Equal(DbType.Int32, snapshot.DbType);
        Assert.Equal(SqlParameterSource.Manual, snapshot.Source);
        Assert.Equal(SqlParameterMetadataLevel.Full, snapshot.MetadataLevel);
    }

    /// <summary>
    /// 测试目的：修改公开参数集合中的项不应改变命令内部快照或后续读取结果。
    /// </summary>
    [Fact]
    public void Parameters_WhenReturnedItemIsModified_ShouldPreserveInternalSnapshot()
    {
        // Arrange
        var command = new SqlMultipleQueryBatchBuilder(';')
            .Append("Select Id From Orders Where Id=@id", new[]
            {
                new SqlParam("id", 7, DbType.Int32) { ProviderTypeName = "INTEGER" }
            })
            .Build();

        // Act
        var exposed = Assert.Single(command.Parameters);
        exposed.Value = 8;
        exposed.ProviderTypeName = "TEXT";
        var snapshot = Assert.Single(command.Parameters);

        // Assert
        Assert.NotSame(exposed, snapshot);
        Assert.Equal(7, snapshot.Value);
        Assert.Equal("INTEGER", snapshot.ProviderTypeName);
        Assert.Equal(DbType.Int32, snapshot.DbType);
    }

    /// <summary>
    /// 测试目的：同名参数应按大小写不敏感规则拒绝，且失败的追加不得污染后续 Builder 状态。
    /// </summary>
    [Fact]
    public void Append_WhenDuplicateParameterValidationFails_ShouldKeepBuilderStateUnchanged()
    {
        // Arrange
        var builder = new SqlMultipleQueryBatchBuilder(';');

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Append("Select @first, @FIRST",
            new[] { new SqlParam("first", 1), new SqlParam("FIRST", 2) }));
        var command = builder.Append("Select @first", new[] { new SqlParam("first", 3) }).Build();

        // Assert
        Assert.Equal("批处理包含重复参数名称 FIRST。", exception.Message);
        Assert.Equal("Select @first", command.Sql);
        var parameter = Assert.Single(command.Parameters);
        Assert.Equal("first", parameter.Name);
        Assert.Equal(3, parameter.Value);
    }

    /// <summary>
    /// 测试目的：空批处理、空 SQL 与空参数名应在命令生成前拒绝。
    /// </summary>
    [Fact]
    public void BuildOrAppend_WhenInputIsInvalid_ShouldThrow()
    {
        // Arrange
        var builder = new SqlMultipleQueryBatchBuilder(';');

        // Act / Assert
        Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Throws<ArgumentException>(() => builder.Append("   "));
        Assert.Throws<ArgumentException>(() => builder.Append("Select 1", new[] { new SqlParam(" ", 1) }));
    }
}