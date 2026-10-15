using System.Reflection;
using Bing.Aspects;
using Bing.Validation;
using Shouldly;
using Xunit;

namespace Bing.Aop.AspectCore;

/// <summary>
/// 验证中立参数特性的公开契约。
/// </summary>
public sealed class ValidationAttributeContractTest
{
    /// <summary>
    /// 验证参数特性不依赖 AspectCore 基类。
    /// </summary>
    [Fact]
    public void Attributes_ShouldInheritDirectlyFromAttribute()
    {
        typeof(NotNullAttribute).BaseType.ShouldBe(typeof(Attribute));
        typeof(NotEmptyAttribute).BaseType.ShouldBe(typeof(Attribute));
        typeof(ValidAttribute).BaseType.ShouldBe(typeof(Attribute));
    }

    /// <summary>
    /// 验证三个特性的标记范围保持兼容。
    /// </summary>
    [Fact]
    public void Attributes_ShouldTargetParametersOnly()
    {
        ShouldBeParameterOnly(typeof(NotNullAttribute));
        ShouldUseDefaultAttributeUsage(typeof(NotEmptyAttribute));
        ShouldUseDefaultAttributeUsage(typeof(ValidAttribute));
    }

    /// <summary>
    /// 验证特性保留原有命名空间和类型名称。
    /// </summary>
    [Fact]
    public void Attributes_ShouldKeepPublicNames()
    {
        typeof(NotNullAttribute).FullName.ShouldBe("Bing.Aspects.NotNullAttribute");
        typeof(NotEmptyAttribute).FullName.ShouldBe("Bing.Aspects.NotEmptyAttribute");
        typeof(ValidAttribute).FullName.ShouldBe("Bing.Validation.ValidAttribute");
    }

    /// <summary>
    /// 验证特性仅能标记参数。
    /// </summary>
    /// <param name="attributeType">待检查的特性类型。</param>
    private static void ShouldBeParameterOnly(Type attributeType)
    {
        var usage = attributeType.GetCustomAttribute<AttributeUsageAttribute>(inherit: false);

        usage.ShouldNotBeNull();
        usage.ValidOn.ShouldBe(AttributeTargets.Parameter);
        usage.AllowMultiple.ShouldBeFalse();
        usage.Inherited.ShouldBeTrue();
    }

    /// <summary>
    /// 验证特性使用旧版本未显式声明时的默认范围。
    /// </summary>
    /// <param name="attributeType">待检查特性类型。</param>
    private static void ShouldUseDefaultAttributeUsage(Type attributeType)
    {
        attributeType.GetCustomAttribute<AttributeUsageAttribute>(inherit: false).ShouldBeNull();
    }
}
