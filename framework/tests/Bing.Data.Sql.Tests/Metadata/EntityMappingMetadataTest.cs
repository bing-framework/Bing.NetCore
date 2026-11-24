using System.Collections.ObjectModel;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Tests.Metadata;

/// <summary>最终映射的列查找合同，不为自定义映射新增索引或改变首项语义。</summary>
public sealed class EntityMappingMetadataTest
{
    /// <summary>默认解析器创建的映射可复用属性索引，查找仍区分属性大小写。</summary>
    [Fact]
    public void FindColumn_WhenDefaultMappingIsUsed_ShouldUsePropertyIndexAndExactCase()
    {
        var mapping = new DefaultEntityMappingResolver().Resolve(typeof(Row), new DatabaseContext());

        Assert.True(mapping.HasPropertyNameIndex);
        Assert.Same(mapping.Columns["Name"], mapping.FindColumn("Name"));
        Assert.Null(mapping.FindColumn("name"));
        Assert.Null(mapping.FindColumn("Missing"));
    }

    /// <summary>没有映射或没有匹配属性时返回空，调用方决定错误语义。</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Missing")]
    public void FindColumn_WhenNoColumnMatches_ShouldReturnNull(string propertyName)
    {
        Assert.Null(new EntityMappingMetadata().FindColumn(propertyName));
        Assert.Null(new EntityMappingMetadata { Columns = new ReadOnlyDictionary<string, ColumnMappingMetadata>(
            new Dictionary<string, ColumnMappingMetadata>()) }.FindColumn(propertyName));
    }

    /// <summary>自定义字典键与属性无关，重复属性保留原始顺序。</summary>
    [Fact]
    public void FindColumn_WhenCustomMappingUsesOtherKeys_ShouldKeepFirstExactProperty()
    {
        var first = new ColumnMappingMetadata { PropertyName = "Name", ColumnName = "first" };
        var mapping = new EntityMappingMetadata
        {
            Columns = new Dictionary<string, ColumnMappingMetadata>
            {
                ["legacy"] = first,
                ["Name"] = new() { PropertyName = "Name", ColumnName = "second" }
            }
        };

        Assert.False(mapping.HasPropertyNameIndex);
        Assert.Same(first, mapping.FindColumn("Name"));
        Assert.Null(mapping.FindColumn("name"));
    }

    private sealed class Row
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}
