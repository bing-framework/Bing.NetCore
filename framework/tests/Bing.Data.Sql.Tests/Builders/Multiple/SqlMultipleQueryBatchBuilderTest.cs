using System.Data;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Multiple;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Mutations;

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
    /// 测试 - 已发布的写入命令公开参数数组被修改后，后续读取仍应返回原始冻结值。
    /// </summary>
    [Fact]
    public void SqlWriteCommand_WhenExposedArrayValueIsMutated_ShouldPreserveInternalSnapshot()
    {
        // Arrange
        var command = new SqlWriteCommand("Update Orders Set Id=@id", new[]
        {
            new SqlParam("id", new[] { 1, 2 })
        });

        // Act
        var exposed = Assert.Single(command.Parameters);
        ((int[])exposed.Value)[0] = 9;
        var snapshot = Assert.Single(command.Parameters);

        // Assert
        Assert.Equal(new[] { 1, 2 }, Assert.IsType<int[]>(snapshot.Value));
    }

    /// <summary>
    /// 测试目的：数组参数在追加、构建和公开读取的各阶段均应隔离，避免可变值污染命令快照。
    /// </summary>
    [Fact]
    public void Build_WhenParameterValuesAreArrays_ShouldPreserveIndependentArraySnapshots()
    {
        // Arrange
        var values = new[] { 1, 2 };
        var originalValues = new[] { 3, 4 };
        var parameter = new SqlParam("ids", values) { OriginalValue = originalValues };
        var builder = new SqlMultipleQueryBatchBuilder(';');

        // Act
        var command = builder.Append("Select @ids", new[] { parameter }).Build();
        values[0] = 9;
        originalValues[0] = 8;
        var exposed = Assert.Single(command.Parameters);
        ((int[])exposed.Value)[1] = 7;
        ((int[])exposed.OriginalValue)[1] = 6;
        var snapshot = Assert.Single(command.Parameters);

        // Assert
        Assert.Equal(new[] { 1, 2 }, (int[])snapshot.Value);
        Assert.Equal(new[] { 3, 4 }, (int[])snapshot.OriginalValue);
    }

    /// <summary>
    /// 测试目的：嵌套字典、集合和字节数组在追加、构建和公开读取后均必须保持独立，避免可变参数容器污染命令快照。
    /// </summary>
    [Fact]
    public void Build_WhenNestedParameterContainersAreMutated_ShouldPreserveCommandSnapshot()
    {
        // Arrange
        var bytes = new byte[] { 1, 2 };
        var values = new List<object> { bytes, "source" };
        var value = new Dictionary<string, object> { ["values"] = values };
        var originalBytes = new byte[] { 3, 4 };
        var originalValues = new List<object> { originalBytes, "original" };
        var originalValue = new Dictionary<string, object> { ["values"] = originalValues };
        var parameter = new SqlParam("payload", value) { OriginalValue = originalValue };

        // Act
        var command = new SqlMultipleQueryBatchBuilder(';').Append("Select @payload", new[] { parameter }).Build();
        bytes[0] = 9;
        values[1] = "changed";
        value["extra"] = true;
        originalBytes[0] = 8;
        originalValues[1] = "changed-original";
        var exposed = Assert.Single(command.Parameters);
        var exposedValue = Assert.IsType<Dictionary<string, object>>(exposed.Value);
        ((byte[])((object[])exposedValue["values"])[0])[1] = 7;
        exposedValue["extra"] = true;
        var snapshot = Assert.Single(command.Parameters);
        var snapshotValue = Assert.IsType<Dictionary<string, object>>(snapshot.Value);
        var snapshotOriginalValue = Assert.IsType<Dictionary<string, object>>(snapshot.OriginalValue);

        // Assert
        Assert.False(snapshotValue.ContainsKey("extra"));
        Assert.Equal(new byte[] { 1, 2 }, (byte[])((object[])snapshotValue["values"])[0]);
        Assert.Equal("source", ((object[])snapshotValue["values"])[1]);
        Assert.Equal(new byte[] { 3, 4 }, (byte[])((object[])snapshotOriginalValue["values"])[0]);
        Assert.Equal("original", ((object[])snapshotOriginalValue["values"])[1]);
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
    /// 测试目的：参数嵌套集合快照失败时，不得保留尚未完整提交的 SQL 语句或参数名称。
    /// </summary>
    [Fact]
    public void Append_WhenNestedParameterSnapshotFails_ShouldKeepBuilderStateUnchanged()
    {
        // Arrange
        var builder = new SqlMultipleQueryBatchBuilder(';');

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Append("Select @payload",
            new[] { new SqlParam("payload", ThrowAfterFirstValue()) }));
        var command = builder.Append("Select 1").Build();

        // Assert
        Assert.Equal("Parameter snapshot failed.", exception.Message);
        Assert.Equal("Select 1", command.Sql);
        Assert.Empty(command.Parameters);
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

    /// <summary>
    /// 在返回首个嵌套参数值后抛出异常的枚举器。
    /// </summary>
    private static IEnumerable<object> ThrowAfterFirstValue()
    {
        yield return 1;
        throw new InvalidOperationException("Parameter snapshot failed.");
    }
}