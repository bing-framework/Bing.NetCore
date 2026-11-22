using Bing.Data.Sql;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Metadata;
using Xunit;

namespace Bing.Data.Sql.Tests.Builders.Params;

/// <summary>默认枚举映射应保留合法数值，并拒绝超出底层整数范围的值。</summary>
public sealed class DefaultFieldValueConverterEnumRangeTest
{
    private readonly DefaultFieldValueConverter _converter = new();

    [Fact]
    public void ConvertFromProvider_WhenByteEnumValueIsInRange_ShouldPreserveUnnamedAndFlagsValues()
    {
        Assert.Equal((ByteState)255, Convert<ByteState>(255));
        Assert.Equal((ByteFlags)3, Convert<ByteFlags>(3));
    }

    [Fact]
    public void ConvertFromProvider_WhenLongAndULongValuesAreAtTheirBoundaries_ShouldPreserveThem()
    {
        Assert.Equal((LongState)long.MinValue, Convert<LongState>(long.MinValue));
        Assert.Equal((LongState)long.MaxValue, Convert<LongState>(long.MaxValue));
        Assert.Equal((ULongState)ulong.MaxValue, Convert<ULongState>(ulong.MaxValue));
    }

    [Theory]
    [InlineData(256)]
    [InlineData(-1)]
    public void ConvertFromProvider_WhenByteEnumValueOverflows_ShouldThrow(int value)
    {
        Assert.Throws<OverflowException>(() => Convert<ByteState>(value));
    }

    [Theory]
    [InlineData(-129)]
    [InlineData(128)]
    public void ConvertFromProvider_WhenSByteEnumValueOverflows_ShouldThrow(int value)
    {
        Assert.Throws<OverflowException>(() => Convert<SByteState>(value));
    }

    [Theory]
    [InlineData(-32769L)]
    [InlineData(32768L)]
    public void ConvertFromProvider_WhenInt16EnumValueOverflows_ShouldThrow(long value)
    {
        Assert.Throws<OverflowException>(() => Convert<ShortState>(value));
    }

    [Theory]
    [InlineData(-2147483649L)]
    [InlineData(2147483648L)]
    public void ConvertFromProvider_WhenInt32EnumValueOverflows_ShouldThrow(long value)
    {
        Assert.Throws<OverflowException>(() => Convert<IntState>(value));
    }

    [Fact]
    public void ConvertFromProvider_WhenInt64EnumReceivesUnsignedOverflow_ShouldThrow()
    {
        Assert.Throws<OverflowException>(() => Convert<LongState>(ulong.MaxValue));
    }

    [Fact]
    public void ConvertFromProvider_WhenUInt64EnumReceivesNegativeValue_ShouldThrow()
    {
        Assert.Throws<OverflowException>(() => Convert<ULongState>(-1L));
    }

    [Fact]
    public void ConvertFromProvider_WhenEnumValueIsFractional_ShouldThrow()
    {
        Assert.Throws<InvalidCastException>(() => Convert<ByteState>(1.5m));
    }

    [Fact]
    public void ConvertFromProvider_WhenNullableEnumValueIsOutOfRange_ShouldThrow()
    {
        var column = new ColumnMappingMetadata
        {
            ClrType = typeof(ByteState?),
            ConverterKind = FieldValueConverterKind.EnumToValue
        };

        Assert.Throws<OverflowException>(() => _converter.ConvertFromProvider(256, column, null));
    }

    [Fact]
    public void ConvertFromProvider_WhenNullableEnumValueIsNull_ShouldKeepNull()
    {
        var column = new ColumnMappingMetadata
        {
            ClrType = typeof(ByteState?),
            ConverterKind = FieldValueConverterKind.EnumToValue
        };

        Assert.Null(_converter.ConvertFromProvider(null, column, null));
    }

    private TEnum Convert<TEnum>(object value) where TEnum : struct, Enum
    {
        var column = new ColumnMappingMetadata
        {
            ClrType = typeof(TEnum),
            ConverterKind = FieldValueConverterKind.EnumToValue
        };

        return (TEnum)_converter.ConvertFromProvider(value, column, null);
    }

    private enum ByteState : byte { Zero = 0, One = 1 }
    [Flags]
    private enum ByteFlags : byte { None = 0, Read = 1, Write = 2 }
    private enum SByteState : sbyte { Zero = 0, One = 1 }
    private enum ShortState : short { Zero = 0, One = 1 }
    private enum IntState : int { Zero = 0, One = 1 }
    private enum LongState : long { Zero = 0, One = 1 }
    private enum ULongState : ulong { Zero = 0, One = 1 }
}
