using System.Collections;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Mutations.Builders;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Tests.Builders.Mutations;

/// <summary>Returning 优先复用已有属性索引，同时保持自定义映射的解析语义。</summary>
public sealed class SqlReturningMappingLookupTest
{
    private const string ExpectedSql = "Update `samples` Set `physical_name` = @_p_0 Where `id`=@_p_1 " +
                                       "Returning `physical_name` As `Display`, `physical_id` As `Key`, " +
                                       "`physical_name` As `Again`";

    /// <summary>默认映射按属性索引命中，重复投影不应逐列枚举完整映射。</summary>
    [Fact]
    public void Returning_WhenPropertyIndexMatches_ShouldAvoidMappingEnumeration()
    {
        var columns = CreateColumns(propertyKeys: true);
        var builder = CreateBuilder(columns, propertyIndex: true);

        builder.Returning<Row>(row => new { Display = row.Name, Key = row.Id, Again = row.Name });

        Assert.Equal(ExpectedSql, builder.ToSql());
        Assert.Equal(3, columns.LookupCount);
        Assert.Equal(0, columns.ValuesReadCount);
    }

    /// <summary>自定义字典可以使用其他键；同名字典键指向另一属性时也必须解析真实属性。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Returning_WhenCustomKeysDiffer_ShouldPreservePropertyMatching(bool misleadingIndex)
    {
        var columns = CreateColumns(propertyKeys: false);
        if (misleadingIndex)
            columns.Items["Name"] = new ColumnMappingMetadata { PropertyName = "Other", ColumnName = "wrong_column" };
        var builder = CreateBuilder(columns);

        builder.Returning<Row>(row => new { Display = row.Name, Key = row.Id, Again = row.Name });

        Assert.Equal(ExpectedSql, builder.ToSql());
        Assert.Equal(3, columns.ValuesReadCount);
    }

    /// <summary>自定义映射的重复属性继续按 Values 中首个匹配项解析。</summary>
    [Fact]
    public void Returning_WhenCustomMappingRepeatsProperty_ShouldKeepFirstMatch()
    {
        var columns = new TrackedColumns(new Dictionary<string, ColumnMappingMetadata>
        {
            ["first"] = new() { PropertyName = "Name", ColumnName = "first_name" },
            ["Name"] = new() { PropertyName = "Name", ColumnName = "second_name" }
        });
        var builder = CreateBuilder(columns).Returning<Row>(row => row.Name);

        Assert.Equal("Update `samples` Set `physical_name` = @_p_0 Where `id`=@_p_1 Returning `first_name` As `Name`",
            builder.ToSql());
    }

    /// <summary>大小写不敏感字典不能放宽原本区分大小写的属性匹配，失败不能残留部分列。</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Returning_WhenPropertyIsMissingOrCaseDiffers_ShouldKeepClauseUnchanged(bool missing)
    {
        var columns = CreateColumns(propertyKeys: true);
        if (missing) columns.Items.Remove("Id");
        else columns.Items["Id"] = new ColumnMappingMetadata { PropertyName = "id", ColumnName = "wrong_id" };
        var builder = CreateBuilder(columns);

        Assert.Throws<InvalidOperationException>(() => builder.Returning<Row>(row => new { row.Name, row.Id }));

        Assert.True(builder.ReturningClause.IsEmpty);
        Assert.Equal("Update `samples` Set `physical_name` = @_p_0 Where `id`=@_p_1", builder.ToSql());
    }

    private static ISqlUpdateBuilder CreateBuilder(TrackedColumns columns, bool propertyIndex = false) =>
        new SqlUpdateBuilder(SqliteSqlProvider.Instance,
                new SqlBuilderServices(entityMappingResolver: new Resolver(columns, propertyIndex)))
            .Update(new SqlTableReference { TableName = "samples" }).Set("physical_name", "updated").Where("id", 7);

    private static TrackedColumns CreateColumns(bool propertyKeys) => new(new Dictionary<string, ColumnMappingMetadata>(StringComparer.OrdinalIgnoreCase)
    {
        [propertyKeys ? "Name" : "name_storage"] = new() { PropertyName = "Name", ColumnName = "physical_name" },
        [propertyKeys ? "Id" : "id_storage"] = new() { PropertyName = "Id", ColumnName = "physical_id" }
    });

    private sealed class Resolver(TrackedColumns columns, bool propertyIndex) : IEntityMappingResolver
    {
        public EntityDescriptor GetDescriptor(Type entityType) => throw new NotSupportedException();
        public EntityMappingMetadata Resolve(Type entityType, DatabaseContext databaseContext) =>
            new() { EntityType = entityType, Columns = columns, HasPropertyNameIndex = propertyIndex };
    }

    private sealed class Row
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    private sealed class TrackedColumns(Dictionary<string, ColumnMappingMetadata> items) : IReadOnlyDictionary<string, ColumnMappingMetadata>
    {
        internal Dictionary<string, ColumnMappingMetadata> Items { get; } = items;
        internal int LookupCount { get; private set; }
        internal int ValuesReadCount { get; private set; }
        public IEnumerable<ColumnMappingMetadata> Values { get { ValuesReadCount++; return Items.Values; } }
        public IEnumerable<string> Keys => Items.Keys;
        public int Count => Items.Count;
        public ColumnMappingMetadata this[string key] => Items[key];
        public bool ContainsKey(string key) => Items.ContainsKey(key);
        public bool TryGetValue(string key, out ColumnMappingMetadata value)
        {
            LookupCount++;
            return Items.TryGetValue(key, out value);
        }
        public IEnumerator<KeyValuePair<string, ColumnMappingMetadata>> GetEnumerator() => Items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
