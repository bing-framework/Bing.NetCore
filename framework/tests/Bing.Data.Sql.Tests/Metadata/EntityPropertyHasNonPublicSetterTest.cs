using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Tests.Metadata;

/// <summary>
/// 验证实体属性元数据保留非公开 setter 信息，同时保持公开写入能力契约。
/// </summary>
public sealed class EntityPropertyHasNonPublicSetterTest
{
    /// <summary>
    /// 测试目的：反射元数据应识别私有 setter，但不能把它作为公开写入器。
    /// </summary>
    [Fact]
    public void Constructor_WhenPropertyHasPrivateSetter_ShouldRecordItWithoutAllowingPublicWrite()
    {
        var privateSetter = new EntityPropertyMetadata(
            typeof(ReflectionModel).GetProperty(nameof(ReflectionModel.PrivateSetter))!);
        var publicSetter = new EntityPropertyMetadata(
            typeof(ReflectionModel).GetProperty(nameof(ReflectionModel.PublicSetter))!);
        var getterOnly = new EntityPropertyMetadata(
            typeof(ReflectionModel).GetProperty(nameof(ReflectionModel.GetterOnly))!);

        Assert.True(ReadHasNonPublicSetter(privateSetter));
        Assert.False(privateSetter.CanWrite);
        Assert.False(ReadHasNonPublicSetter(publicSetter));
        Assert.True(publicSetter.CanWrite);
        Assert.False(ReadHasNonPublicSetter(getterOnly));
        Assert.False(getterOnly.CanWrite);
    }

    /// <summary>
    /// 测试目的：旧生成工厂应默认不标记，新 15 参数工厂应保留非公开 setter 信息。
    /// </summary>
    [Fact]
    public void CreateGenerated_WhenUsingLegacyOrExtendedSignature_ShouldPreserveSetterMarkerContract()
    {
        var legacy = EntityPropertyMetadata.CreateGenerated(
            nameof(GeneratedModel.Value), typeof(int), true, false);
        var extendedFactory = typeof(EntityPropertyMetadata).GetMethods()
            .SingleOrDefault(method => method.Name == nameof(EntityPropertyMetadata.CreateGenerated) &&
                                       method.GetParameters().Length == 15 &&
                                       method.GetParameters()[^1].ParameterType == typeof(bool));
        Assert.NotNull(extendedFactory);
        var withNonPublicSetter = (EntityPropertyMetadata)extendedFactory!.Invoke(null, new object[]
        {
            nameof(GeneratedModel.Value), typeof(int), true, false, nameof(GeneratedModel.Value),
            false, false, DatabaseGeneratedOption.None, false, false, null, null, null, null, true
        })!;

        Assert.False(ReadHasNonPublicSetter(legacy));
        Assert.True(ReadHasNonPublicSetter(withNonPublicSetter));
        Assert.False(withNonPublicSetter.CanWrite);
    }

    private static bool ReadHasNonPublicSetter(EntityPropertyMetadata metadata)
    {
        var property = typeof(EntityPropertyMetadata).GetProperty("HasNonPublicSetter");
        Assert.NotNull(property);
        return Assert.IsType<bool>(property!.GetValue(metadata));
    }

    private sealed class ReflectionModel
    {
        public int PrivateSetter { get; private set; }

        public int PublicSetter { get; set; }

        public int GetterOnly => 1;
    }

    private sealed class GeneratedModel
    {
        public int Value { get; private set; }
    }
}
