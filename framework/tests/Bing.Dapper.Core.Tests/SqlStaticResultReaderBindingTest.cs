using System.Data;
using Bing.Data.Sql;
using Bing.Data.Sql.Metadata;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Dapper.Core.Tests;

/// <summary>结果布局绑定的分配上界以及窄、宽布局共同遵守的名称规则。</summary>
public sealed class SqlStaticResultReaderBindingTest(ITestOutputHelper output)
{
    [Fact]
    public void Bind_TwoColumns_ShouldStayWithinSmallLayoutAllocationBudget()
    {
        var mapper = new SqlStaticResultReader<Row>(Mapping(2), null, null);
        using var table = new DataTable();
        table.Columns.Add("Id", typeof(long));
        table.Columns.Add("display_name", typeof(string));
        table.Rows.Add(12L, "one");
        using var reader = table.CreateDataReader();
        for (var i = 0; i < 32; i++) mapper.Bind(reader);

        // 仅测绑定，不计入 DataTable、Reader、映射建立或实体物化成本。
        const int iterations = 1000;
        Func<Row> read = null;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < iterations; i++) read = mapper.Bind(reader);
        var bytesPerBind = (GC.GetAllocatedBytesForCurrentThread() - before) / iterations;

        output.WriteLine($"Two-column Bind: {bytesPerBind} B/op");
        Assert.InRange(bytesPerBind, 0, 512);
        Assert.True(reader.Read());
        Assert.Equal(12, read().Id);
        Assert.Equal("one", read().Name);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(32)]
    public void Bind_Layouts_ShouldKeepAliasesOrderNullAndDuplicateChecks(int width)
    {
        var mapper = new SqlStaticResultReader<Row>(Mapping(width), null, null);
        using var table = new DataTable();
        table.Columns.Add("DISPLAY_NAME", typeof(string));
        table.Columns.Add("unmapped", typeof(long));
        table.Columns.Add("id", typeof(long));
        table.Rows.Add(DBNull.Value, 99L, 12L);
        using var reader = table.CreateDataReader();
        var read = mapper.Bind(reader);
        Assert.True(reader.Read());
        var row = read();
        Assert.Equal(12, row.Id);
        Assert.Equal("initial", row.Name);

        using var duplicate = new DataTable();
        duplicate.Columns.Add("Name", typeof(string));
        duplicate.Columns.Add("display_name", typeof(string));
        using var duplicateReader = duplicate.CreateDataReader();
        Assert.Throws<InvalidOperationException>(() => mapper.Bind(duplicateReader));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(32)]
    public void Bind_AliasConflictsWithAnotherPhysicalName_ShouldRejectAmbiguity(int width)
    {
        var mapper = new SqlStaticResultReader<Row>(Mapping(width, true), null, null);
        using var table = new DataTable();
        table.Columns.Add("NAME", typeof(string));
        using var reader = table.CreateDataReader();
        Assert.Throws<InvalidOperationException>(() => mapper.Bind(reader));
    }

    private static EntityMappingMetadata Mapping(int width, bool ambiguous = false)
    {
        var properties = new List<EntityPropertyMetadata>
        {
            EntityPropertyMetadata.CreateGenerated("Id", typeof(int), true, true,
                columnName: ambiguous ? "Name" : "Id",
                generatedSetter: (row, value) => ((Row)row).Id = (int)value),
            EntityPropertyMetadata.CreateGenerated("Name", typeof(string), true, true,
                columnName: "display_name", generatedSetter: (row, value) => ((Row)row).Name = (string)value)
        };
        for (var i = 2; i < width; i++)
            properties.Add(EntityPropertyMetadata.CreateGenerated("Unused" + i, typeof(int), true, true,
                generatedSetter: (_, _) => { }));
        var model = new EntityModelMetadata(typeof(Row), "rows", null, properties, () => new Row());
        return new DefaultEntityMappingResolver(new RegisteredEntityModelMetadataProvider(model))
            .Resolve(typeof(Row), new DatabaseContext());
    }

    public sealed class Row
    {
        public int Id { get; set; }
        public string Name { get; set; } = "initial";
    }
}
