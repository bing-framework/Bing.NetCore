using AspectCore.DynamicProxy;
using AspectCore.DynamicProxy.Parameters;
using Bing.Aspects;
using Shouldly;
using Xunit;

namespace Bing.Aop.AspectCore;

/// <summary>
/// 反射结构测试：验证 <see cref="AutowiredAttribute"/>、<see cref="IgnoreAttribute"/>、
/// <see cref="InterceptorBase"/>、<see cref="ParameterInterceptorBase"/>、<see cref="IAopProxy"/>
/// 的类型层次结构与元数据约束。
/// </summary>
public class AttributeReflectionTest
{
    // ═══════════════════════════════════════════════════════════
    // AutowiredAttribute
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：AutowiredAttribute 应具有 [AttributeUsage(Property | Field)]，
    /// 确保属性注入标记只能用于属性和字段，不能用于其他目标。
    /// </summary>
    [Fact]
    public void AutowiredAttribute_AttributeUsage_ShouldTargetPropertyAndField()
    {
        // Arrange & Act
        var usage = typeof(AutowiredAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), inherit: true)
            .Cast<AttributeUsageAttribute>()
            .FirstOrDefault();

        // Assert
        usage.ShouldNotBeNull();
        usage.ValidOn.ShouldBe(AttributeTargets.Property | AttributeTargets.Field);
    }

    /// <summary>
    /// 测试目的：AutowiredAttribute 是容器无关的普通特性，避免核心契约绑定 AspectCore。
    /// </summary>
    [Fact]
    public void AutowiredAttribute_ShouldBeContainerNeutralAttribute()
    {
        // Assert
        typeof(AutowiredAttribute).BaseType.ShouldBe(typeof(Attribute));
    }

    /// <summary>
    /// 验证旧 AspectCore 程序集限定名仍可解析到中立特性。
    /// </summary>
    [Fact]
    public void AutowiredAttribute_ShouldResolveThroughLegacyAspectCoreAssemblyName()
    {
        var type = Type.GetType(
            "Bing.Aspects.AutowiredAttribute, Bing.Aop.AspectCore",
            throwOnError: true);

        type.ShouldBe(typeof(AutowiredAttribute));
    }

    /// <summary>
    /// 验证迁移后的中立特性仍可通过旧 AspectCore 程序集限定名解析。
    /// </summary>
    [Fact]
    public void NeutralAttributes_ShouldResolveThroughLegacyAspectCoreAssemblyName()
    {
        Type.GetType("Bing.Aspects.IgnoreAttribute, Bing.Aop.AspectCore", throwOnError: true)
            .ShouldBe(typeof(IgnoreAttribute));
        Type.GetType("Bing.Aspects.NotNullAttribute, Bing.Aop.AspectCore", throwOnError: true)
            .ShouldBe(typeof(Bing.Aspects.NotNullAttribute));
        Type.GetType("Bing.Aspects.NotEmptyAttribute, Bing.Aop.AspectCore", throwOnError: true)
            .ShouldBe(typeof(Bing.Aspects.NotEmptyAttribute));
        Type.GetType("Bing.Validation.ValidAttribute, Bing.Aop.AspectCore", throwOnError: true)
            .ShouldBe(typeof(Bing.Validation.ValidAttribute));
    }

    // ═══════════════════════════════════════════════════════════
    // IgnoreAttribute
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：IgnoreAttribute 是容器无关的普通特性，执行器负责解释其语义。
    /// </summary>
    [Fact]
    public void IgnoreAttribute_ShouldBeContainerNeutralAttribute()
    {
        // Assert
        typeof(IgnoreAttribute).BaseType.ShouldBe(typeof(Attribute));
    }

    // ═══════════════════════════════════════════════════════════
    // InterceptorBase
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：InterceptorBase 应是抽象类，防止被直接实例化。
    /// </summary>
    [Fact]
    public void InterceptorBase_ShouldBeAbstractClass()
    {
        // Assert
        typeof(InterceptorBase).IsAbstract.ShouldBeTrue();
        typeof(InterceptorBase).IsClass.ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：InterceptorBase 应继承自 AbstractInterceptorAttribute，
    /// 确保所有业务拦截器均走 AspectCore 标准方法拦截流程。
    /// </summary>
    [Fact]
    public void InterceptorBase_ShouldInheritFromAbstractInterceptorAttribute()
    {
        // Assert
        typeof(AbstractInterceptorAttribute)
            .IsAssignableFrom(typeof(InterceptorBase))
            .ShouldBeTrue();
    }

    // ═══════════════════════════════════════════════════════════
    // ParameterInterceptorBase
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：ParameterInterceptorBase 应是抽象类，防止被直接实例化。
    /// </summary>
    [Fact]
    public void ParameterInterceptorBase_ShouldBeAbstractClass()
    {
        // Assert
        typeof(ParameterInterceptorBase).IsAbstract.ShouldBeTrue();
        typeof(ParameterInterceptorBase).IsClass.ShouldBeTrue();
    }

    /// <summary>
    /// 验证参数拦截器基类的继承关系。
    /// </summary>
    /// <remarks>
    /// 该继承关系用于符合 AspectCore 参数拦截机制的类型约束。
    /// </remarks>
    [Fact]
    public void ParameterInterceptorBase_ShouldInheritFromParameterInterceptorAttribute()
    {
        // Assert
        typeof(ParameterInterceptorAttribute)
            .IsAssignableFrom(typeof(ParameterInterceptorBase))
            .ShouldBeTrue();
    }

    // ═══════════════════════════════════════════════════════════
    // IAopProxy
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：IAopProxy 应是空标记接口（无成员），
    /// 仅用于在运行时识别由 AOP 代理包装的服务实例。
    /// </summary>
    [Fact]
    public void IAopProxy_ShouldBeEmptyMarkerInterface()
    {
        // Assert
        typeof(IAopProxy).IsInterface.ShouldBeTrue();
        typeof(IAopProxy).GetMembers().Length.ShouldBe(0);
    }
}
