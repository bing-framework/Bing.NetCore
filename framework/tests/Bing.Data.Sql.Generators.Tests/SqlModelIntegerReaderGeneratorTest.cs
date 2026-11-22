using System.Data.Common;
using System.Reflection;
using Bing.Data.Sql.Metadata;
using Moq;
using Xunit;

namespace Bing.Data.Sql.Generators.Tests;

public sealed partial class SqlModelGeneratorTest
{
    /// <summary>字符和枚举结果应使用类型化读取器，避免进入 GetValue 转换路径。</summary>
    [Fact]
    public void Generate_CharacterAndEnumReader_ShouldUseTypedStorageWithoutObjectFallback()
    {
        var result = RunGenerator("using Bing.Data.Sql.Generated; public enum ReaderState : int { None = 0, Ready = 7 } " +
            "public sealed class CharacterEnumResult { public char Marker {get;set;} = 'A'; public ReaderState State {get;set;} = ReaderState.None; " +
            "public ReaderState? OptionalState {get;set;} = ReaderState.None; } " +
            "[SqlResult(typeof(CharacterEnumResult))] public static partial class CharacterEnumContext { }");
        AssertNoCompilationErrors(result);
        var assembly = Assembly.Load(result.AssemblyImage);
        var model = Assert.Single((EntityModelMetadata[])assembly.GetType("CharacterEnumContext")!
            .GetMethod("CreateModels")!.Invoke(null, null)!);
        var instance = model.GeneratedFactory!();
        var marker = model.Properties["Marker"].GeneratedReaderSetter;
        var state = model.Properties["State"].GeneratedReaderSetter;
        var optionalState = model.Properties["OptionalState"].GeneratedReaderSetter;
        Assert.NotNull(marker);
        Assert.NotNull(state);
        Assert.NotNull(optionalState);

        var reader = new Mock<DbDataReader>(MockBehavior.Strict);
        reader.Setup(item => item.GetFieldType(0)).Returns(typeof(char));
        reader.Setup(item => item.GetChar(0)).Returns('Z');
        reader.Setup(item => item.GetFieldType(1)).Returns(typeof(long));
        reader.Setup(item => item.GetInt64(1)).Returns(7L);
        reader.Setup(item => item.GetFieldType(2)).Returns(typeof(int));
        reader.Setup(item => item.GetInt32(2)).Returns(7);

        Assert.True(marker!(instance, reader.Object, 0));
        Assert.True(state!(instance, reader.Object, 1));
        Assert.True(optionalState!(instance, reader.Object, 2));
        Assert.Equal('Z', model.EntityType.GetProperty("Marker")!.GetValue(instance));
        Assert.Equal(7, Convert.ToInt32(model.EntityType.GetProperty("State")!.GetValue(instance)));
        Assert.Equal(7, Convert.ToInt32(model.EntityType.GetProperty("OptionalState")!.GetValue(instance)));
        reader.Verify(item => item.GetValue(It.IsAny<int>()), Times.Never);
    }

    /// <summary>无符号底层枚举仍应生成完整的受检 Int64 读取 Lambda，并把精确字段交给兼容回退。</summary>
    [Fact]
    public void Generate_UnsignedEnumReader_ShouldKeepCheckedInt64PathAndCompile()
    {
        var result = RunGenerator("using Bing.Data.Sql.Generated; public enum UnsignedState : uint { None = 0, Ready = 7 } " +
            "public sealed class UnsignedEnumResult { public UnsignedState State {get;set;} } " +
            "[SqlResult(typeof(UnsignedEnumResult))] public static partial class UnsignedEnumContext { }");
        AssertNoCompilationErrors(result);
        var assembly = Assembly.Load(result.AssemblyImage);
        var model = Assert.Single((EntityModelMetadata[])assembly.GetType("UnsignedEnumContext")!
            .GetMethod("CreateModels")!.Invoke(null, null)!);
        var instance = model.GeneratedFactory!();
        var assign = model.Properties["State"].GeneratedReaderSetter;
        Assert.NotNull(assign);

        var reader = new Mock<DbDataReader>(MockBehavior.Strict);
        reader.Setup(item => item.GetFieldType(0)).Returns(typeof(long));
        reader.Setup(item => item.GetInt64(0)).Returns(7L);

        Assert.True(assign!(instance, reader.Object, 0));
        Assert.Equal(7u, Convert.ToUInt32(model.EntityType.GetProperty("State")!.GetValue(instance)));
        reader.Verify(item => item.GetValue(It.IsAny<int>()), Times.Never);
    }

    /// <summary>无符号和窄整数枚举的精确存储应使用生成读取器，并覆盖可空属性。</summary>
    [Theory]
    [InlineData("sbyte", "-3")]
    [InlineData("ushort", "500")]
    [InlineData("uint", "42")]
    [InlineData("ulong", "9000")]
    public void Generate_EnumReader_ShouldUseExactStorageValueWithoutFallback(string underlyingType, string rawValue)
    {
        var result = RunGenerator("using Bing.Data.Sql.Generated; public enum ExactState : " + underlyingType +
            " { Ready = " + rawValue + " } public sealed class ExactEnumResult { public ExactState State {get;set;} " +
            "public ExactState? OptionalState {get;set;} } " +
            "[SqlResult(typeof(ExactEnumResult))] public static partial class ExactEnumContext { }");
        AssertNoCompilationErrors(result);
        var assembly = Assembly.Load(result.AssemblyImage);
        var model = Assert.Single((EntityModelMetadata[])assembly.GetType("ExactEnumContext")!
            .GetMethod("CreateModels")!.Invoke(null, null)!);
        var instance = model.GeneratedFactory!();
        var stateType = model.EntityType.GetProperty("State")!.PropertyType;
        var storageType = Enum.GetUnderlyingType(stateType);
        var storageValue = Convert.ChangeType(rawValue, storageType)!;
        var reader = new Mock<DbDataReader>(MockBehavior.Strict);
        reader.Setup(item => item.GetFieldType(0)).Returns(storageType);
        reader.Setup(item => item.GetValue(0)).Returns(storageValue);
        reader.Setup(item => item.GetFieldType(1)).Returns(storageType);
        reader.Setup(item => item.GetValue(1)).Returns(storageValue);

        Assert.True(model.Properties["State"].GeneratedReaderSetter!(instance, reader.Object, 0));
        Assert.True(model.Properties["OptionalState"].GeneratedReaderSetter!(instance, reader.Object, 1));
        Assert.Equal(rawValue, Convert.ChangeType(model.EntityType.GetProperty("State")!.GetValue(instance), storageType)!.ToString());
        Assert.Equal(rawValue, Convert.ChangeType(model.EntityType.GetProperty("OptionalState")!.GetValue(instance), storageType)!.ToString());
        reader.Verify(item => item.GetValue(0), Times.Once);
        reader.Verify(item => item.GetValue(1), Times.Once);

        reader.Setup(item => item.GetFieldType(0)).Returns(typeof(string));
        Assert.False(model.Properties["State"].GeneratedReaderSetter(instance, reader.Object, 0));
        Assert.Equal(rawValue, Convert.ChangeType(model.EntityType.GetProperty("State")!.GetValue(instance), storageType)!.ToString());
    }

    /// <summary>二进制结果应直接使用生成读取器，避免进入通用转换路径。</summary>
    [Fact]
    public void Generate_BinaryReader_ShouldUseGeneratedValueWithoutConversionFallback()
    {
        var result = RunGenerator("#nullable enable\nusing Bing.Data.Sql.Generated; " +
            "public sealed class BinaryResult { public byte[] Payload {get;set;} = System.Array.Empty<byte>(); " +
            "public byte[]? OptionalPayload {get;set;} } " +
            "[SqlResult(typeof(BinaryResult))] public static partial class BinaryContext { }");
        AssertNoCompilationErrors(result);
        var assembly = Assembly.Load(result.AssemblyImage);
        var model = Assert.Single((EntityModelMetadata[])assembly.GetType("BinaryContext")!
            .GetMethod("CreateModels")!.Invoke(null, null)!);
        var instance = model.GeneratedFactory!();
        var payload = new byte[] { 1, 2, 3 };
        var reader = new Mock<DbDataReader>(MockBehavior.Strict);
        reader.Setup(item => item.GetFieldType(0)).Returns(typeof(byte[]));
        reader.Setup(item => item.GetValue(0)).Returns(payload);
        reader.Setup(item => item.GetFieldType(1)).Returns(typeof(byte[]));
        reader.Setup(item => item.GetValue(1)).Returns(payload);

        var assign = model.Properties["Payload"].GeneratedReaderSetter;
        var optionalAssign = model.Properties["OptionalPayload"].GeneratedReaderSetter;
        Assert.NotNull(assign);
        Assert.NotNull(optionalAssign);
        Assert.True(assign!(instance, reader.Object, 0));
        Assert.True(optionalAssign!(instance, reader.Object, 1));
        Assert.Equal(payload, model.EntityType.GetProperty("Payload")!.GetValue(instance));
        Assert.Equal(payload, model.EntityType.GetProperty("OptionalPayload")!.GetValue(instance));

        reader.Setup(item => item.GetFieldType(0)).Returns(typeof(string));
        Assert.False(assign(instance, reader.Object, 0));
        Assert.Equal(payload, model.EntityType.GetProperty("Payload")!.GetValue(instance));
    }

    /// <summary>日期偏移和时间间隔的精确字段应使用生成读取器，类型不匹配时交给兼容转换。</summary>
    [Fact]
    public void Generate_DateTimeOffsetAndTimeSpanReader_ShouldUseExactStorageWithoutReflectionFallback()
    {
        var result = RunGenerator("using System; using Bing.Data.Sql.Generated; " +
            "public sealed class TemporalResult { public DateTimeOffset Stamp {get;set;} = default; " +
            "public DateTimeOffset? OptionalStamp {get;set;} = default; public TimeSpan Duration {get;set;} = default; " +
            "public TimeSpan? OptionalDuration {get;set;} = default; } " +
            "[SqlResult(typeof(TemporalResult))] public static partial class TemporalContext { }");
        AssertNoCompilationErrors(result);
        var assembly = Assembly.Load(result.AssemblyImage);
        var model = Assert.Single((EntityModelMetadata[])assembly.GetType("TemporalContext")!
            .GetMethod("CreateModels")!.Invoke(null, null)!);
        var instance = model.GeneratedFactory!();
        var stamp = new DateTimeOffset(2026, 11, 12, 8, 30, 0, TimeSpan.FromHours(8));
        var duration = TimeSpan.FromMinutes(42);
        var reader = new Mock<DbDataReader>(MockBehavior.Strict);
        reader.Setup(item => item.GetFieldType(0)).Returns(typeof(DateTimeOffset));
        reader.Setup(item => item.GetValue(0)).Returns(stamp);
        reader.Setup(item => item.GetFieldType(1)).Returns(typeof(DateTimeOffset));
        reader.Setup(item => item.GetValue(1)).Returns(stamp);
        reader.Setup(item => item.GetFieldType(2)).Returns(typeof(TimeSpan));
        reader.Setup(item => item.GetValue(2)).Returns(duration);
        reader.Setup(item => item.GetFieldType(3)).Returns(typeof(TimeSpan));
        reader.Setup(item => item.GetValue(3)).Returns(duration);

        Assert.True(model.Properties["Stamp"].GeneratedReaderSetter!(instance, reader.Object, 0));
        Assert.True(model.Properties["OptionalStamp"].GeneratedReaderSetter!(instance, reader.Object, 1));
        Assert.True(model.Properties["Duration"].GeneratedReaderSetter!(instance, reader.Object, 2));
        Assert.True(model.Properties["OptionalDuration"].GeneratedReaderSetter!(instance, reader.Object, 3));
        Assert.Equal(stamp, model.EntityType.GetProperty("Stamp")!.GetValue(instance));
        Assert.Equal(stamp, model.EntityType.GetProperty("OptionalStamp")!.GetValue(instance));
        Assert.Equal(duration, model.EntityType.GetProperty("Duration")!.GetValue(instance));
        Assert.Equal(duration, model.EntityType.GetProperty("OptionalDuration")!.GetValue(instance));

        reader.Setup(item => item.GetFieldType(0)).Returns(typeof(string));
        Assert.False(model.Properties["Stamp"].GeneratedReaderSetter(instance, reader.Object, 0));
        Assert.Equal(stamp, model.EntityType.GetProperty("Stamp")!.GetValue(instance));
    }

    /// <summary>日期和时间值的精确字段应使用生成读取器，类型不匹配时交给兼容转换。</summary>
    [Fact]
    public void Generate_DateOnlyAndTimeOnlyReader_ShouldUseExactStorageWithoutReflectionFallback()
    {
        var result = RunGenerator("using System; using Bing.Data.Sql.Generated; " +
            "public sealed class CalendarResult { public DateOnly Date {get;set;} = default; " +
            "public DateOnly? OptionalDate {get;set;} = default; public TimeOnly Time {get;set;} = default; " +
            "public TimeOnly? OptionalTime {get;set;} = default; } " +
            "[SqlResult(typeof(CalendarResult))] public static partial class CalendarContext { }");
        AssertNoCompilationErrors(result);
        var assembly = Assembly.Load(result.AssemblyImage);
        var model = Assert.Single((EntityModelMetadata[])assembly.GetType("CalendarContext")!
            .GetMethod("CreateModels")!.Invoke(null, null)!);
        var instance = model.GeneratedFactory!();
        var date = new DateOnly(2026, 11, 13);
        var time = new TimeOnly(9, 42, 15);
        var reader = new Mock<DbDataReader>(MockBehavior.Strict);
        reader.Setup(item => item.GetFieldType(0)).Returns(typeof(DateOnly));
        reader.Setup(item => item.GetValue(0)).Returns(date);
        reader.Setup(item => item.GetFieldType(1)).Returns(typeof(DateOnly));
        reader.Setup(item => item.GetValue(1)).Returns(date);
        reader.Setup(item => item.GetFieldType(2)).Returns(typeof(TimeOnly));
        reader.Setup(item => item.GetValue(2)).Returns(time);
        reader.Setup(item => item.GetFieldType(3)).Returns(typeof(TimeOnly));
        reader.Setup(item => item.GetValue(3)).Returns(time);

        Assert.True(model.Properties["Date"].GeneratedReaderSetter!(instance, reader.Object, 0));
        Assert.True(model.Properties["OptionalDate"].GeneratedReaderSetter!(instance, reader.Object, 1));
        Assert.True(model.Properties["Time"].GeneratedReaderSetter!(instance, reader.Object, 2));
        Assert.True(model.Properties["OptionalTime"].GeneratedReaderSetter!(instance, reader.Object, 3));
        Assert.Equal(date, model.EntityType.GetProperty("Date")!.GetValue(instance));
        Assert.Equal(date, model.EntityType.GetProperty("OptionalDate")!.GetValue(instance));
        Assert.Equal(time, model.EntityType.GetProperty("Time")!.GetValue(instance));
        Assert.Equal(time, model.EntityType.GetProperty("OptionalTime")!.GetValue(instance));

        reader.Setup(item => item.GetFieldType(0)).Returns(typeof(string));
        Assert.False(model.Properties["Date"].GeneratedReaderSetter(instance, reader.Object, 0));
        Assert.Equal(date, model.EntityType.GetProperty("Date")!.GetValue(instance));
    }

    /// <summary>object 结果应直接保留 Provider 原值，避免进入通用转换路径。</summary>
    [Fact]
    public void Generate_ObjectReader_ShouldAssignProviderValueWithoutFallback()
    {
        var result = RunGenerator("#nullable enable\nusing Bing.Data.Sql.Generated; " +
            "public sealed class ObjectResult { public object Value {get;set;} = \"initial\"; " +
            "public object? OptionalValue {get;set;} } " +
            "[SqlResult(typeof(ObjectResult))] public static partial class ObjectContext { }");
        AssertNoCompilationErrors(result);
        var assembly = Assembly.Load(result.AssemblyImage);
        var model = Assert.Single((EntityModelMetadata[])assembly.GetType("ObjectContext")!
            .GetMethod("CreateModels")!.Invoke(null, null)!);
        var instance = model.GeneratedFactory!();
        var value = new byte[] { 4, 5, 6 };
        var reader = new Mock<DbDataReader>(MockBehavior.Strict);
        reader.Setup(item => item.GetValue(0)).Returns("typed");
        reader.Setup(item => item.GetValue(1)).Returns(value);

        var assign = model.Properties["Value"].GeneratedReaderSetter;
        var optionalAssign = model.Properties["OptionalValue"].GeneratedReaderSetter;
        Assert.NotNull(assign);
        Assert.NotNull(optionalAssign);
        Assert.True(assign!(instance, reader.Object, 0));
        Assert.True(optionalAssign!(instance, reader.Object, 1));
        Assert.Equal("typed", model.EntityType.GetProperty("Value")!.GetValue(instance));
        Assert.Same(value, model.EntityType.GetProperty("OptionalValue")!.GetValue(instance));
        reader.Verify(item => item.GetValue(0), Times.Once);
        reader.Verify(item => item.GetValue(1), Times.Once);
    }

    /// <summary>精确整数 Reader 每次赋值只检查一次字段类型，避免重复访问 Provider 元数据。</summary>
    [Fact]
    public void Generate_IntegerReader_ShouldCacheFieldTypeForSingleAssignment()
    {
        var result = RunGenerator("using Bing.Data.Sql.Generated; public sealed class IntegerResult { public int Value {get;set;} } " +
            "[SqlResult(typeof(IntegerResult))] public static partial class IntegerContext { }");
        AssertNoCompilationErrors(result);
        var assembly = Assembly.Load(result.AssemblyImage);
        var model = Assert.Single((EntityModelMetadata[])assembly.GetType("IntegerContext")!
            .GetMethod("CreateModels")!.Invoke(null, null)!);
        var instance = model.GeneratedFactory!();
        var reader = new Mock<DbDataReader>(MockBehavior.Strict);
        reader.Setup(item => item.GetFieldType(0)).Returns(typeof(int));
        reader.Setup(item => item.GetInt32(0)).Returns(9);

        var assign = model.Properties["Value"].GeneratedReaderSetter;
        Assert.NotNull(assign);
        Assert.True(assign!(instance, reader.Object, 0));
        Assert.Equal(9, model.EntityType.GetProperty("Value")!.GetValue(instance));
        reader.Verify(item => item.GetFieldType(0), Times.Once);
        reader.Verify(item => item.GetInt32(0), Times.Once);
    }

    /// <summary>SQLite 的 Int64 存储直接写入整数属性，可空属性沿用相同转换且不读取 object 值。</summary>
    [Theory]
    [InlineData("int", (long)int.MinValue, (long)int.MaxValue)]
    [InlineData("short", (long)short.MinValue, (long)short.MaxValue)]
    [InlineData("byte", 0L, (long)byte.MaxValue)]
    [InlineData("sbyte", (long)sbyte.MinValue, (long)sbyte.MaxValue)]
    [InlineData("ushort", 0L, (long)ushort.MaxValue)]
    [InlineData("uint", 0L, (long)uint.MaxValue)]
    [InlineData("ulong", 0L, long.MaxValue)]
    public void Generate_IntegerReader_ShouldReadInt64WithoutBoxingAndKeepFallback(
        string typeName, long minimum, long maximum)
    {
        foreach (var nullable in new[] { false, true })
        {
            var model = CreateIntegerReaderModel(typeName, nullable);
            var instance = model.GeneratedFactory!();
            var property = model.EntityType.GetProperty("Value")!;
            var assign = model.Properties["Value"].GeneratedReaderSetter;
            Assert.NotNull(assign);
            var reader = new Mock<DbDataReader>(MockBehavior.Strict);
            reader.Setup(value => value.GetFieldType(0)).Returns(typeof(long));
            var current = minimum;
            reader.Setup(value => value.GetInt64(0)).Returns(() => current);
            var target = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

            Assert.True(assign!(instance, reader.Object, 0));
            Assert.Equal(Convert.ChangeType(minimum, target), property.GetValue(instance));
            current = maximum;
            Assert.True(assign(instance, reader.Object, 0));
            Assert.Equal(Convert.ChangeType(maximum, target), property.GetValue(instance));
            reader.Verify(value => value.GetValue(It.IsAny<int>()), Times.Never);

            // 存储类型不匹配时由原有转换处理，生成分支不应先改写目标。
            reader.Setup(value => value.GetFieldType(0)).Returns(typeof(string));
            Assert.False(assign(instance, reader.Object, 0));
            Assert.Equal(Convert.ChangeType(maximum, target), property.GetValue(instance));
        }
    }

    /// <summary>精确无符号和窄整数存储应读取类型值，SQLite Int64 路径之外仍保持生成赋值。</summary>
    [Theory]
    [InlineData("sbyte", -3L)]
    [InlineData("ushort", 500L)]
    [InlineData("uint", 42L)]
    [InlineData("ulong", 9000L)]
    public void Generate_IntegerReader_ShouldUseExactStorageValueWithoutFallback(string typeName, long rawValue)
    {
        foreach (var nullable in new[] { false, true })
        {
            var model = CreateIntegerReaderModel(typeName, nullable);
            var instance = model.GeneratedFactory!();
            var property = model.EntityType.GetProperty("Value")!;
            var assign = model.Properties["Value"].GeneratedReaderSetter;
            Assert.NotNull(assign);
            var target = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            var value = Convert.ChangeType(rawValue, target)!;
            var reader = new Mock<DbDataReader>(MockBehavior.Strict);
            reader.Setup(item => item.GetFieldType(0)).Returns(target);
            reader.Setup(item => item.GetValue(0)).Returns(value);

            Assert.True(assign!(instance, reader.Object, 0));
            Assert.Equal(value, property.GetValue(instance));
            reader.Verify(item => item.GetValue(0), Times.Once);

            reader.Setup(item => item.GetFieldType(0)).Returns(typeof(string));
            Assert.False(assign(instance, reader.Object, 0));
            Assert.Equal(value, property.GetValue(instance));
        }
    }

    /// <summary>整数缩窄和有符号转无符号必须检查范围，失败时保留属性值。</summary>
    [Theory]
    [InlineData("int", (long)int.MinValue - 1)]
    [InlineData("int", (long)int.MaxValue + 1)]
    [InlineData("short", (long)short.MinValue - 1)]
    [InlineData("short", (long)short.MaxValue + 1)]
    [InlineData("byte", -1L)]
    [InlineData("byte", (long)byte.MaxValue + 1)]
    [InlineData("sbyte", (long)sbyte.MinValue - 1)]
    [InlineData("sbyte", (long)sbyte.MaxValue + 1)]
    [InlineData("ushort", -1L)]
    [InlineData("ushort", (long)ushort.MaxValue + 1)]
    [InlineData("uint", -1L)]
    [InlineData("uint", (long)uint.MaxValue + 1)]
    [InlineData("ulong", -1L)]
    public void Generate_IntegerReader_WhenInt64Overflows_ShouldThrowBeforeAssignment(string typeName, long value)
    {
        foreach (var nullable in new[] { false, true })
        {
            var model = CreateIntegerReaderModel(typeName, nullable);
            var instance = model.GeneratedFactory!();
            var property = model.EntityType.GetProperty("Value")!;
            var initial = property.GetValue(instance);
            var assign = model.Properties["Value"].GeneratedReaderSetter;
            Assert.NotNull(assign);
            var reader = new Mock<DbDataReader>(MockBehavior.Strict);
            reader.Setup(item => item.GetFieldType(0)).Returns(typeof(long));
            reader.Setup(item => item.GetInt64(0)).Returns(value);

            Assert.Throws<OverflowException>(() => assign!(instance, reader.Object, 0));
            Assert.Equal(initial, property.GetValue(instance));
            reader.Verify(item => item.GetValue(It.IsAny<int>()), Times.Never);
        }
    }

    private static EntityModelMetadata CreateIntegerReaderModel(string typeName, bool nullable)
    {
        var result = RunGenerator("using Bing.Data.Sql.Generated; public sealed class IntegerResult { public " +
            typeName + (nullable ? "?" : "") + " Value {get;set;} = 7; } " +
            "[SqlResult(typeof(IntegerResult))] public static partial class IntegerContext { }");
        AssertNoCompilationErrors(result);
        var assembly = Assembly.Load(result.AssemblyImage);
        return Assert.Single((EntityModelMetadata[])assembly.GetType("IntegerContext")!
            .GetMethod("CreateModels")!.Invoke(null, null)!);
    }
}
