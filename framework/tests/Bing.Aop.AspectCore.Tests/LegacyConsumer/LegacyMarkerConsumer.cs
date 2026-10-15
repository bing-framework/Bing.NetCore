using Bing.Aspects;
using Bing.Validation;

namespace Bing.Aop.AspectCore.LegacyConsumer;

/// <summary>
/// 使用旧程序集限定名编译的特性消费者。
/// </summary>
public sealed class LegacyMarkerConsumer
{
    /// <summary>
    /// 使用旧版属性注入标记声明的属性。
    /// </summary>
    [Autowired]
    public object Injected { get; set; }

    /// <summary>
    /// 使用旧版忽略标记声明的方法。
    /// </summary>
    [Ignore]
    public void Ignored()
    {
    }

    /// <summary>
    /// 使用旧版参数标记声明的方法。
    /// </summary>
    public void Validate([NotNull] object notNull, [NotEmpty] string notEmpty, [Valid] object valid)
    {
    }
}
