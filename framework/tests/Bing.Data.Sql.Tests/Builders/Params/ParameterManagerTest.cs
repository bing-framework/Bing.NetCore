using System.Data;
using Bing.Data.Enums;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests.Builders.Params;

/// <summary>
/// Sql参数管理器测试
/// </summary>
public class ParameterManagerTest
{
    #region 测试初始化

    /// <summary>
    /// Sql参数管理器
    /// </summary>
    private readonly ParameterManager _manager;

    /// <summary>
    /// 测试初始化
    /// </summary>
    public ParameterManagerTest()
    {
        _manager = new ParameterManager(TestDialect.Instance);
    }

    #endregion

    /// <summary>
    /// 测试 - 创建参数名
    /// </summary>
    [Fact]
    public void Test_GenerateName()
    {
        Assert.Equal("@_p_0", _manager.GenerateName());
        Assert.Equal("@_p_1", _manager.GenerateName());
    }

    /// <summary>
    /// 测试 - 自动参数名应跳过已由子查询合并或调用方显式添加的名称。
    /// </summary>
    [Fact]
    public void GenerateName_WhenGeneratedNameAlreadyExists_ShouldSkipExistingName()
    {
        // Arrange
        _manager.Add("@_p_0", 1);

        // Act
        var name = _manager.GenerateName();

        // Assert
        Assert.Equal("@_p_1", name);
    }

    /// <summary>
    /// 测试 - 是否包含参数
    /// </summary>
    [Fact]
    public void Test_Contains_1()
    {
        _manager.Add("a", 1);
        Assert.True(_manager.Contains("a"));
        Assert.True(_manager.Contains("@a"));
        Assert.False(_manager.Contains("b"));
    }

    /// <summary>
    /// 测试 - 是否包含参数
    /// </summary>
    [Fact]
    public void Test_Contains_2()
    {
        _manager.Add("@a", 1);
        Assert.True(_manager.Contains("a"));
        Assert.True(_manager.Contains("@a"));
        Assert.False(_manager.Contains("b"));
    }

    /// <summary>
    /// 测试 - 获取参数列表
    /// </summary>
    [Fact]
    public void Test_GetParams()
    {
        var parameters = _manager.GetParams();
        Assert.Empty(parameters);
    }

    /// <summary>
    /// 测试 - 添加1个参数
    /// </summary>
    [Fact]
    public void Test_Add_1()
    {
        _manager.Add("a", 1);
        var parameters = _manager.GetParams();
        Assert.Single(parameters);
        Assert.Equal(1, _manager.GetValue("a"));
    }

    /// <summary>
    /// 测试 - 添加2个参数
    /// </summary>
    [Fact]
    public void Test_Add_2()
    {
        _manager.Add("a", 1);
        _manager.Add("b", 2);
        var parameters = _manager.GetParams();
        Assert.Equal(2, parameters.Count);
        Assert.Equal(1, _manager.GetValue("a"));
        Assert.Equal(2, _manager.GetValue("b"));
    }

    /// <summary>
    /// 测试 - 覆盖参数
    /// </summary>
    [Fact]
    public void Test_Add_3()
    {
        _manager.Add("a", 1);
        _manager.Add("a", 2);
        var parameters = _manager.GetParams();
        Assert.Single(parameters);
        Assert.Equal(2, _manager.GetValue("a"));
    }

    /// <summary>
    /// 测试 - 添加参数 - 参数名为空
    /// </summary>
    [Fact]
    public void Test_Add_4()
    {
        _manager.Add("", 1);
        var parameters = _manager.GetParams();
        Assert.Empty(parameters);
    }

    /// <summary>
    /// 测试 - 清空参数
    /// </summary>
    [Fact]
    public void Test_Clear()
    {
        var paramName = _manager.GenerateName();
        _manager.Add(paramName, 1);
        _manager.Clear();
        var parameters = _manager.GetParams();
        Assert.Empty(parameters);
        Assert.Equal("@_p_0", _manager.GenerateName());
    }

    /// <summary>
    /// 测试 - 复制Sql参数管理器副本 - 参数
    /// </summary>
    [Fact]
    public void Test_Clone_Param()
    {
        _manager.Add("name", "a");
        var clone = _manager.Clone();
        Assert.Equal("a", clone.GetValue("name"));
    }

    /// <summary>
    /// 测试 - 已知前缀和大小写不同的同名参数应合并为当前方言的标准名称。
    /// </summary>
    [Fact]
    public void Add_WhenNamesUseKnownPrefixesOrDifferentCasing_ShouldReplaceSingleNormalizedParameter()
    {
        // Arrange
        _manager.Add("id", 1);

        // Act
        _manager.Add(":ID", 2);
        _manager.Add("?Id", 3);

        // Assert
        Assert.Single(_manager.GetParams());
        Assert.Equal(3, _manager.GetValue("@id"));
        Assert.True(_manager.Contains("id"));
        Assert.True(_manager.Contains(":ID"));
        Assert.True(_manager.Contains("?Id"));
        Assert.Equal("@id", _manager.NormalizeName(":id"));
    }

    /// <summary>
    /// 测试 - 标准化名称应去除已知前缀后应用当前方言前缀，无效名称不应写入或抛出异常。
    /// </summary>
    [Fact]
    public void NormalizeName_WhenDialectOrInputNameChanges_ShouldUseDialectPrefixAndIgnoreInvalidNames()
    {
        // Arrange
        var manager = new ParameterManager(TestDialect2.Instance);

        // Act
        manager.Add("@item", 1);
        manager.Add(null, 2);
        manager.Add("   ", 3);
        manager.Add("@", 4);

        // Assert
        Assert.Equal("*item", manager.NormalizeName(":item"));
        Assert.Equal(string.Empty, manager.NormalizeName("@"));
        Assert.Equal(string.Empty, manager.NormalizeName(null));
        Assert.Single(manager.GetParams());
        Assert.Equal(1, manager.GetValue("?ITEM"));
        Assert.False(manager.Contains(null));
        Assert.Null(manager.GetValue("@"));
    }

    /// <summary>
    /// 测试 - 参数值导出应返回调用时刻的独立集合快照。
    /// </summary>
    [Fact]
    public void GetParamsAndExportValues_WhenManagerChangesLater_ShouldKeepOriginalSnapshots()
    {
        // Arrange
        _manager.Add("first", 1);
        var parameters = _manager.GetParams();
        var exportedValues = _manager.ExportValues();

        // Act
        _manager.Add("FIRST", 2);
        _manager.Add("second", 3);
        _manager.Clear();

        // Assert
        Assert.Single(parameters);
        Assert.Single(exportedValues);
        Assert.Equal(1, parameters["@first"]);
        Assert.Equal(1, exportedValues["@first"]);
        Assert.Empty(_manager.GetParams());
    }

    /// <summary>
    /// 测试 - 增强参数快照和克隆应复制全部 SqlParam 容器元数据，不与来源共享可变容器状态。
    /// </summary>
    [Fact]
    public void GetSqlParamsAndClone_WhenParameterContainsMetadata_ShouldPreserveMetadataWithoutSharingContainer()
    {
        // Arrange
        var source = new SqlParam(":item", "value", DbType.String, ParameterDirection.InputOutput, 64, 12, 4)
        {
            OriginalValue = "original",
            EntityType = typeof(Sample),
            PropertyName = nameof(Sample.StringValue),
            ColumnName = "string_value",
            DatabaseType = DatabaseType.MySql,
            ProviderTypeName = "varchar",
            Source = SqlParameterSource.Manual,
            MetadataLevel = SqlParameterMetadataLevel.Full,
            StorageKind = ColumnStorageKind.Json,
            ConverterKind = FieldValueConverterKind.Custom,
            CustomConverterName = "TestConverter"
        };
        _manager.Add(source);
        source.Value = "source-changed";
        source.OriginalValue = "source-original-changed";
        var snapshot = _manager.GetSqlParams();
        var clone = Assert.IsAssignableFrom<IAdvancedParameterManager>(_manager.Clone());

        // Act
        snapshot["@item"].Value = "snapshot-changed";
        snapshot["@item"].OriginalValue = "snapshot-original-changed";
        snapshot["@item"].EntityType = typeof(string);
        snapshot["@item"].PropertyName = "Changed";

        // Assert
        var parameter = _manager.GetSqlParams()["@item"];
        Assert.Equal("value", parameter.Value);
        Assert.Equal("original", parameter.OriginalValue);
        Assert.Equal(typeof(Sample), parameter.EntityType);
        Assert.Equal(nameof(Sample.StringValue), parameter.PropertyName);
        Assert.Equal("string_value", parameter.ColumnName);
        Assert.Equal(DatabaseType.MySql, parameter.DatabaseType);
        Assert.Equal("varchar", parameter.ProviderTypeName);
        Assert.Equal(SqlParameterSource.Manual, parameter.Source);
        Assert.Equal(SqlParameterMetadataLevel.Full, parameter.MetadataLevel);
        Assert.Equal(ColumnStorageKind.Json, parameter.StorageKind);
        Assert.Equal(FieldValueConverterKind.Custom, parameter.ConverterKind);
        Assert.Equal("TestConverter", parameter.CustomConverterName);
        Assert.Equal(DbType.String, parameter.DbType);
        Assert.Equal(ParameterDirection.InputOutput, parameter.Direction);
        Assert.Equal(64, parameter.Size);
        Assert.Equal((byte)12, parameter.Precision);
        Assert.Equal((byte)4, parameter.Scale);
        Assert.NotSame(parameter, clone.GetSqlParams()["@item"]);
        Assert.Equal("original", clone.GetSqlParams()["@item"].OriginalValue);
    }
}
