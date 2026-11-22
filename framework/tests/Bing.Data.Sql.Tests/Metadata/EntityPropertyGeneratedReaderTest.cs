using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Tests.Metadata;

/// <summary>强类型读取器必须作为不可变元数据扩展，并保留既有映射信息。</summary>
public sealed class EntityPropertyGeneratedReaderTest
{
    [Fact]
    public void WithGeneratedReader_ShouldPreserveGeneratedContractWithoutMutatingSource()
    {
        Func<object, object> getter = value => ((Row)value).Id;
        Action<object, object> setter = (value, id) => ((Row)value).Id = (int)id;
        Func<object, DbDataReader, int, bool> reader = (_, _, _) => false;
        var source = EntityPropertyMetadata.CreateGenerated("Id", typeof(int), true, true, "row_id", false, true,
            DatabaseGeneratedOption.Identity, true, true, 40, "integer", getter, setter, true);

        var extended = source.WithGeneratedReader(reader);

        Assert.NotSame(source, extended);
        Assert.Null(source.GeneratedReaderSetter);
        Assert.Same(reader, extended.GeneratedReaderSetter);
        Assert.Same(getter, extended.GeneratedGetter);
        Assert.Same(setter, extended.GeneratedSetter);
        Assert.Equal("Id", extended.PropertyName);
        Assert.Equal("row_id", extended.ColumnName);
        Assert.Equal(typeof(int), extended.ClrType);
        Assert.True(extended.CanRead && extended.CanWrite && extended.HasNonPublicSetter);
        Assert.True(extended.IsKey && extended.IsConcurrencyToken && extended.IsRequired);
        Assert.Equal(DatabaseGeneratedOption.Identity, extended.DatabaseGeneratedOption);
        Assert.Equal(40, extended.MaxLength);
        Assert.Equal("integer", extended.ProviderTypeName);
    }

    [Fact]
    public void WithGeneratedReader_ShouldPreserveReflectionDescriptor()
    {
        var property = typeof(Row).GetProperty(nameof(Row.Id));
        var source = new EntityPropertyMetadata(property);
        var extended = source.WithGeneratedReader((_, _, _) => false);
        Assert.Same(property, extended.Property);
        Assert.Null(source.GeneratedReaderSetter);
    }

    [Fact]
    public void WithGeneratedReader_WhenNull_ShouldRejectWithoutChangingSource()
    {
        var source = EntityPropertyMetadata.CreateGenerated("Id", typeof(int), true, true);
        Assert.Equal("readerSetter", Assert.Throws<ArgumentNullException>(() => source.WithGeneratedReader(null)).ParamName);
        Assert.Null(source.GeneratedReaderSetter);
    }

    private sealed class Row { public int Id { get; set; } }
}
