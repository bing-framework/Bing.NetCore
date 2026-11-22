using System.Data;
using Bing.Data.Enums;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;
using Xunit.Abstractions;

namespace Bing.Data.Sql.Tests.Builders.Params;

/// <summary>参数管理器克隆分配与快照隔离测试。</summary>
public sealed class ParameterManagerCloneAllocationTest
{
    private const int ParameterCount = 128;
    private const int WarmupIterations = 32;
    private const int MeasurementIterations = 200;
    private const int MaxBytesPerClone = 16 * 1024;

    private readonly ITestOutputHelper _output;

    public ParameterManagerCloneAllocationTest(ITestOutputHelper output) => _output = output;

    /// <summary>128 个参数的克隆应保持在每次 16 KiB 的分配上限内。</summary>
    [Fact]
    public void Clone_With128Parameters_ShouldStayWithinAllocationBudget()
    {
        var source = CreateManagerWithParameters(ParameterCount);
        for (var index = 0; index < WarmupIterations; index++)
            GC.KeepAlive(source.Clone());

        var count = 0;
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < MeasurementIterations; index++)
        {
            var clone = (ParameterManager)source.Clone();
            count += clone.Count;
            GC.KeepAlive(clone);
        }
        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        var bytesPerClone = (double)allocatedBytes / MeasurementIterations;

        _output.WriteLine("Clone with {0} parameters allocated {1:F1} B/op.", ParameterCount, bytesPerClone);
        Assert.Equal(ParameterCount * MeasurementIterations, count);
        Assert.True(bytesPerClone <= MaxBytesPerClone,
            $"Expected at most {MaxBytesPerClone} B/op, measured {bytesPerClone:F1} B/op.");
    }

    /// <summary>克隆后任一管理器替换参数都不应污染另一份参数和元数据。</summary>
    [Fact]
    public void Clone_WhenSourceOrCloneReplacesParameter_ShouldKeepStatesIndependent()
    {
        var source = new ParameterManager(TestDialect.Instance);
        source.Add(CreateMetadataParameter("@item", "original", "source-original"));
        var clone = (ParameterManager)source.Clone();

        source.Add(CreateMetadataParameter("@item", "source-replacement", "source-replacement-original"));

        Assert.Equal("source-replacement", source.GetValue("@item"));
        AssertFullMetadata(source.GetSqlParams()["@item"], "@item", "source-replacement", "source-replacement-original");
        Assert.Equal("original", clone.GetValue("@item"));
        AssertFullMetadata(clone.GetSqlParams()["@item"], "@item", "original", "source-original");

        clone.Add(CreateMetadataParameter("@item", "clone-replacement", "clone-replacement-original"));

        Assert.Equal("source-replacement", source.GetValue("@item"));
        AssertFullMetadata(source.GetSqlParams()["@item"], "@item", "source-replacement", "source-replacement-original");
        Assert.Equal("clone-replacement", clone.GetValue("@item"));
        AssertFullMetadata(clone.GetSqlParams()["@item"], "@item", "clone-replacement", "clone-replacement-original");
    }

    /// <summary>修改 GetSqlParams 返回的可变描述符不应影响源管理器或克隆。</summary>
    [Fact]
    public void GetSqlParams_WhenSnapshotDescriptorIsModified_ShouldKeepBothManagersUnchanged()
    {
        var source = new ParameterManager(TestDialect.Instance);
        source.Add(CreateMetadataParameter("@item", "provider-value", "original-value"));
        var clone = (ParameterManager)source.Clone();
        var sourceSnapshot = source.GetSqlParams()["@item"];
        var cloneSnapshot = clone.GetSqlParams()["@item"];

        sourceSnapshot.Value = "changed-source-snapshot";
        sourceSnapshot.OriginalValue = "changed-source-original";
        sourceSnapshot.EntityType = typeof(string);
        sourceSnapshot.PropertyName = "ChangedSource";
        sourceSnapshot.IsProviderValue = true;
        cloneSnapshot.Value = "changed-clone-snapshot";
        cloneSnapshot.OriginalValue = "changed-clone-original";
        cloneSnapshot.ColumnName = "changed_clone_column";
        cloneSnapshot.CustomConverterName = "ChangedCloneConverter";

        Assert.NotSame(sourceSnapshot, cloneSnapshot);
        AssertFullMetadata(source.GetSqlParams()["@item"], "@item", "provider-value", "original-value");
        AssertFullMetadata(clone.GetSqlParams()["@item"], "@item", "provider-value", "original-value");
        Assert.Equal("@item", sourceSnapshot.Name);
        Assert.Equal(64, sourceSnapshot.Size);
        Assert.Equal("@item", cloneSnapshot.Name);
        Assert.Equal(64, cloneSnapshot.Size);
    }

    /// <summary>克隆应保留复杂参数元数据、Provider 转换标记和后续名称序列。</summary>
    [Fact]
    public void Clone_WhenMetadataAndNameIndexExist_ShouldPreserveMetadataAndNextName()
    {
        var source = new ParameterManager(TestDialect.Instance);
        source.Add(CreateMetadataParameter("@_p_0", "provider-value", "original-value"));
        source.Add("@_p_1", 2);
        Assert.Equal("@_p_2", source.GenerateName());

        var clone = (ParameterManager)source.Clone();

        AssertFullMetadata(source.GetSqlParams()["@_p_0"], "@_p_0", "provider-value", "original-value");
        AssertFullMetadata(clone.GetSqlParams()["@_p_0"], "@_p_0", "provider-value", "original-value");
        Assert.Equal("@_p_3", source.GenerateName());
        Assert.Equal("@_p_3", clone.GenerateName());
    }

    private static ParameterManager CreateManagerWithParameters(int count)
    {
        var manager = new ParameterManager(TestDialect.Instance);
        for (var index = 0; index < count; index++)
            manager.Add($"item{index:D3}", index);
        return manager;
    }

    private static SqlParam CreateMetadataParameter(string name, string value, string originalValue) =>
        new(name, value, DbType.String, ParameterDirection.InputOutput, 64, 12, 4)
        {
            OriginalValue = originalValue,
            IsProviderValue = true,
            EntityType = typeof(Sample),
            PropertyName = nameof(Sample.StringValue),
            ColumnName = "string_value",
            DatabaseType = DatabaseType.MySql,
            ProviderTypeName = "varchar",
            Source = SqlParameterSource.Manual,
            MetadataLevel = SqlParameterMetadataLevel.Full,
            StorageKind = ColumnStorageKind.Json,
            ConverterKind = FieldValueConverterKind.Custom,
            CustomConverterName = "SampleConverter"
        };

    private static void AssertFullMetadata(SqlParam parameter, string expectedName, string value, string originalValue)
    {
        Assert.Equal(expectedName, parameter.Name);
        Assert.Equal(value, parameter.Value);
        Assert.Equal(originalValue, parameter.OriginalValue);
        Assert.True(parameter.IsProviderValue);
        Assert.Equal(typeof(Sample), parameter.EntityType);
        Assert.Equal(nameof(Sample.StringValue), parameter.PropertyName);
        Assert.Equal("string_value", parameter.ColumnName);
        Assert.Equal(DatabaseType.MySql, parameter.DatabaseType);
        Assert.Equal("varchar", parameter.ProviderTypeName);
        Assert.Equal(SqlParameterSource.Manual, parameter.Source);
        Assert.Equal(SqlParameterMetadataLevel.Full, parameter.MetadataLevel);
        Assert.Equal(ColumnStorageKind.Json, parameter.StorageKind);
        Assert.Equal(FieldValueConverterKind.Custom, parameter.ConverterKind);
        Assert.Equal("SampleConverter", parameter.CustomConverterName);
        Assert.Equal(DbType.String, parameter.DbType);
        Assert.Equal(ParameterDirection.InputOutput, parameter.Direction);
        Assert.Equal(64, parameter.Size);
        Assert.Equal((byte)12, parameter.Precision);
        Assert.Equal((byte)4, parameter.Scale);
    }
}
