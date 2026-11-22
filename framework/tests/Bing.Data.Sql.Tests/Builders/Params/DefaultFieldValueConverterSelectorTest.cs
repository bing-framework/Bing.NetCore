using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Metadata;
using Moq;
using Xunit;

namespace Bing.Data.Sql.Tests.Builders.Params;

/// <summary>字段转换选择器的优先级、上下文与无匹配合同。</summary>
public class DefaultFieldValueConverterSelectorTest
{
    [Fact]
    public void UsesDefaultConverter_ShouldExposeOnlyTheExactBuiltInPipeline()
    {
        var capability = typeof(DefaultFieldValueConverterSelector).GetProperty("UsesDefaultConverter");
        Assert.NotNull(capability);
        Assert.Equal(true, capability.GetValue(new DefaultFieldValueConverterSelector()));
        Assert.Equal(false, capability.GetValue(new DerivedSelector()));
        Assert.Equal(false, capability.GetValue(new DefaultFieldValueConverterSelector(new[] { new DerivedConverter() })));
        Assert.Equal(false, capability.GetValue(new DefaultFieldValueConverterSelector(new[] { Mock.Of<IFieldValueConverter>() })));
    }

    private sealed class DerivedSelector : DefaultFieldValueConverterSelector { }
    private sealed class DerivedConverter : DefaultFieldValueConverter { }

    [Fact]
    public void Convert_ShouldUseFirstMatchingConverterWithOriginalColumnAndContext()
    {
        var column = new ColumnMappingMetadata { PropertyName = "Id", ClrType = typeof(int) };
        var context = new DatabaseContext();
        var ignored = new Mock<IFieldValueConverter>(MockBehavior.Strict);
        ignored.Setup(item => item.CanConvert(column, context)).Returns(false);
        var first = new Mock<IFieldValueConverter>(MockBehavior.Strict);
        first.Setup(item => item.CanConvert(column, context)).Returns(true);
        first.Setup(item => item.ConvertToProvider(5, column, context)).Returns(6);
        first.Setup(item => item.ConvertFromProvider(7, column, context)).Returns(8);
        var unused = new Mock<IFieldValueConverter>(MockBehavior.Strict);
        var selector = new DefaultFieldValueConverterSelector(new[] { ignored.Object, first.Object, unused.Object });
        Assert.Equal(6, selector.ConvertToProvider(5, column, context));
        Assert.Equal(8, selector.ConvertFromProvider(7, column, context));
        first.VerifyAll();
        unused.VerifyNoOtherCalls();
    }

    [Fact]
    public void Convert_WhenNoConverterMatches_ShouldKeepValue()
    {
        var converter = new Mock<IFieldValueConverter>();
        var selector = new DefaultFieldValueConverterSelector(new[] { converter.Object });
        var value = new object();
        Assert.Same(value, selector.ConvertToProvider(value, null, null));
        Assert.Same(value, selector.ConvertFromProvider(value, null, null));
    }
}
