using System.Data;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests.Builders.Params;

/// <summary>
/// 增强参数数量上限管理器测试。
/// </summary>
public class AdvancedParameterLimitManagerTest
{
    /// <summary>
    /// 测试 - 增强参数应保留元数据，同名替换不增加数量。
    /// </summary>
    [Fact]
    public void AddSqlParam_WhenReplacingExisting_ShouldPreserveMetadataAndCount()
    {
        // Arrange
        var manager = new AdvancedParameterLimitManager(new ParameterManager(TestDialect.Instance), 2, "TestProvider");

        // Act
        manager.Add(new SqlParam("id", 1, DbType.Int32));
        manager.Add(new SqlParam("id", 2, DbType.Int64));
        manager.Add("name", "Bing");

        // Assert
        Assert.Equal(2, manager.GetParams().Count);
        Assert.Equal(2, manager.GetSqlParams()["@id"].Value);
        Assert.Equal(DbType.Int64, manager.GetSqlParams()["@id"].DbType);
        Assert.Equal("Bing", manager.ExportValues()["@name"]);
    }

    /// <summary>
    /// 测试 - 增强参数超过上限时应报告完整诊断且不写入新增参数。
    /// </summary>
    [Fact]
    public void AddSqlParam_WhenLimitExceeded_ShouldThrowWithoutMutatingMetadata()
    {
        // Arrange
        var manager = new AdvancedParameterLimitManager(new ParameterManager(TestDialect.Instance), 1, "AdvancedProvider");
        manager.Add(new SqlParam("first", 1));

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => manager.Add(new SqlParam("second", 2)));

        // Assert
        Assert.Contains("AdvancedProvider", exception.Message);
        Assert.Contains("当前参数数量: 1", exception.Message);
        Assert.Contains("尝试添加后数量: 2", exception.Message);
        Assert.Single(manager.GetSqlParams());
        Assert.False(manager.Contains("second"));
    }

    /// <summary>
    /// 测试 - Clone 和 CreateEmpty 应保留增强参数能力、限制和隔离状态。
    /// </summary>
    [Fact]
    public void CloneAndCreateEmpty_ShouldKeepMetadataContractAndIsolation()
    {
        // Arrange
        var manager = new AdvancedParameterLimitManager(new ParameterManager(TestDialect.Instance), 2, "AdvancedProvider");
        manager.Add(new SqlParam("first", 1, DbType.Int32));

        // Act
        var clone = Assert.IsAssignableFrom<IAdvancedParameterManager>(manager.Clone());
        clone.Add(new SqlParam("second", 2, DbType.Int32));
        var empty = Assert.IsAssignableFrom<IAdvancedParameterManager>(((IParameterManagerLifecycle)manager).CreateEmpty());

        // Assert
        Assert.Single(manager.GetSqlParams());
        Assert.Equal(2, clone.GetSqlParams().Count);
        Assert.Empty(empty.GetSqlParams());
        Assert.Throws<InvalidOperationException>(() => clone.Add(new SqlParam("third", 3)));
    }
}