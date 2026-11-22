using System.Data;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Metadata;
using Xunit;

namespace Bing.Dapper.Core.Tests;

/// <summary>静态读取器使用实际结果形状、最终映射和数值转换的直接测试。</summary>
public sealed class SqlStaticResultReaderTest
{
    [Theory]
    [InlineData(256L)]
    [InlineData(-1L)]
    public void ConvertValue_EnumOverflow_ShouldFailInsteadOfTruncating(long value)
    {
        Assert.Throws<OverflowException>(() => SqlStaticResultReader<Result>.ConvertValue(value, typeof(ByteFlags)));
        Assert.Throws<OverflowException>(() => SqlStaticResultReader<Result>.ConvertValue(value, typeof(ByteFlags?)));
    }

    [Fact]
    public void ConvertValue_EnumFlagsAndUnnamedValues_ShouldRemainValid()
    {
        Assert.Equal((ByteFlags)3, SqlStaticResultReader<Result>.ConvertValue(3L, typeof(ByteFlags)));
        Assert.Equal((ByteFlags)255, SqlStaticResultReader<Result>.ConvertValue(255L, typeof(ByteFlags?)));
        Assert.Equal((ByteFlags)3, SqlStaticResultReader<Result>.ConvertValue("One, Two", typeof(ByteFlags)));
        Assert.Equal((ByteFlags)255, SqlStaticResultReader<Result>.ConvertValue("255", typeof(ByteFlags)));
        Assert.Throws<OverflowException>(() => SqlStaticResultReader<Result>.ConvertValue("256", typeof(ByteFlags)));
    }

    [Flags]
    public enum ByteFlags : byte { One = 1, Two = 2 }

    [Fact]
    public void Bind_OnlyComputedColumn_ShouldKeepConstructedValueWithoutRequiringSetter()
    {
        var model = new EntityModelMetadata(typeof(ReadOnlyResult), "results", null, new[]
        {
            EntityPropertyMetadata.CreateGenerated(nameof(ReadOnlyResult.Computed), typeof(int), true, false)
        }, () => new ReadOnlyResult());
        var mapping = new DefaultEntityMappingResolver(new RegisteredEntityModelMetadataProvider(model)).Resolve(typeof(ReadOnlyResult), new DatabaseContext());
        using var reader = CreateReader(("Computed", typeof(long), 99L));
        var read = new SqlStaticResultReader<ReadOnlyResult>(mapping, new DefaultFieldValueConverterSelector(), new DatabaseContext()).Bind(reader);
        Assert.True(reader.Read());
        Assert.Equal(34, read().Computed);
    }

    public sealed class ReadOnlyResult { public int Computed => 34; }

    [Fact]
    public void Bind_ShouldRespectActualColumnOrderAndNumericTypes()
    {
        var mapping = CreateMapping();
        var mapper = new SqlStaticResultReader<Result>(mapping, new DefaultFieldValueConverterSelector(), new DatabaseContext());
        using var first = CreateReader(("display_name", typeof(string), "one"), ("id", typeof(long), 12L));
        var readFirst = mapper.Bind(first);
        Assert.True(first.Read());
        Assert.Equal(12, readFirst().Id);
        using var second = CreateReader(("ID", typeof(short), (short)24), ("Name", typeof(string), "two"));
        var readSecond = mapper.Bind(second);
        Assert.True(second.Read());
        var result = readSecond();
        Assert.Equal(24, result.Id);
        Assert.Equal("two", result.Name);
    }

    [Fact]
    public void Bind_ShouldRejectDuplicateTargetsAndKeepInitialValuesForNullOrMissingColumns()
    {
        var mapper = new SqlStaticResultReader<Result>(CreateMapping(), new DefaultFieldValueConverterSelector(), new DatabaseContext());
        using var duplicate = CreateReader(("Name", typeof(string), "one"), ("display_name", typeof(string), "two"));
        Assert.Throws<InvalidOperationException>(() => mapper.Bind(duplicate));
        using var nullId = CreateReader(("id", typeof(long), DBNull.Value));
        var read = mapper.Bind(nullId);
        Assert.True(nullId.Read());
        var result = read();
        Assert.Equal(17, result.Id);
        Assert.Equal("initial", result.Name);
    }

    private static EntityMappingMetadata CreateMapping()
    {
        var model = new EntityModelMetadata(typeof(Result), "results", null, new[]
        {
            EntityPropertyMetadata.CreateGenerated(nameof(Result.Id), typeof(int), true, true,
                generatedGetter: source => ((Result)source).Id, generatedSetter: (source, value) => ((Result)source).Id = (int)value),
            EntityPropertyMetadata.CreateGenerated(nameof(Result.Name), typeof(string), true, true, columnName: "display_name",
                generatedGetter: source => ((Result)source).Name, generatedSetter: (source, value) => ((Result)source).Name = (string)value)
        }, () => new Result());
        return new DefaultEntityMappingResolver(new RegisteredEntityModelMetadataProvider(model)).Resolve(typeof(Result), new DatabaseContext());
    }

    private static DataTableReader CreateReader(params (string Name, Type Type, object Value)[] columns)
    {
        var table = new DataTable();
        foreach (var column in columns) table.Columns.Add(column.Name, column.Type);
        table.Rows.Add(columns.Select(column => column.Value).ToArray());
        return table.CreateDataReader();
    }

    public sealed class Result { public int Id { get; set; } = 17; public string Name { get; set; } = "initial"; }
}
