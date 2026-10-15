using System.Reflection;
using System.IO;
using Bing.Aspects;
using Bing.Validation;
using Shouldly;
using Xunit;

namespace Bing.Aop.AspectCore;

/// <summary>
/// 验证旧编译消费者通过类型转发加载中立特性。
/// </summary>
public sealed class LegacyConsumerAssemblyTest
{
    /// <summary>
    /// 验证仅引用旧 AspectCore 程序集限定名的消费者可以读取新类型。
    /// </summary>
    [Fact]
    public void LegacyConsumer_ShouldResolveForwardedAttributes()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "LegacyConsumer", "Bing.Aop.AspectCore.LegacyConsumer.dll");
        var assembly = Assembly.LoadFrom(path);
        var type = assembly.GetType("Bing.Aop.AspectCore.LegacyConsumer.LegacyMarkerConsumer", throwOnError: true);

        type.GetProperty(nameof(LegacyMarkerConsumerAssemblyProxy.Injected))
            .GetCustomAttribute<AutowiredAttribute>()
            .ShouldNotBeNull();
        type.GetMethod(nameof(LegacyMarkerConsumerAssemblyProxy.Ignored))
            .GetCustomAttribute<IgnoreAttribute>()
            .ShouldNotBeNull();

        var parameters = type.GetMethod(nameof(LegacyMarkerAssemblyProxy.Validate)).GetParameters();
        parameters[0].GetCustomAttribute<NotNullAttribute>().ShouldNotBeNull();
        parameters[1].GetCustomAttribute<NotEmptyAttribute>().ShouldNotBeNull();
        parameters[2].GetCustomAttribute<ValidAttribute>().ShouldNotBeNull();
    }

    /// <summary>
    /// 暴露旧消费者属性注入标记的测试代理类型。
    /// </summary>
    private sealed class LegacyMarkerConsumerAssemblyProxy
    {
        /// <summary>
        /// 由旧消费者属性注入标记标注的成员。
        /// </summary>
        public object Injected { get; set; }

        /// <summary>
        /// 由旧消费者忽略标记标注的方法。
        /// </summary>
        public void Ignored()
        {
        }
    }

    /// <summary>
    /// 暴露旧消费者参数验证标记的测试代理类型。
    /// </summary>
    private sealed class LegacyMarkerAssemblyProxy
    {
        /// <summary>
        /// 声明旧消费者的参数验证标记。
        /// </summary>
        /// <param name="notNull">非空参数。</param>
        /// <param name="notEmpty">非空文本参数。</param>
        /// <param name="valid">验证模型参数。</param>
        public void Validate(object notNull, string notEmpty, object valid)
        {
        }
    }
}
