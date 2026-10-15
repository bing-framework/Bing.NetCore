using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Bing.Aspects;
using Bing.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Aop.AspectCore;

/// <summary>
/// 验证忽略拦截接口扫描不会保留可收集程序集。
/// </summary>
public sealed class IgnoreAspectAssemblyResourceTest
{
    /// <summary>
    /// 验证 AOP 注册结束后可收集接口不会被全局程序集扫描缓存保留。
    /// </summary>
    [Fact]
    public void EnableAop_ShouldNotRetainCollectibleIgnoredInterface()
    {
        var reference = CreateCollectibleIgnoredInterfaceAndConfigureAop();

        for (var attempt = 0; attempt < 8 && reference.IsAlive; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        Assert.False(reference.IsAlive);
    }

    /// <summary>
    /// 创建带有忽略拦截标记的可收集接口并执行一次 AOP 注册。
    /// </summary>
    /// <returns>动态接口的弱引用。</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateCollectibleIgnoredInterfaceAndConfigureAop()
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("BingAspectCoreCollectibleIgnoredAssembly"),
            AssemblyBuilderAccess.RunAndCollect);
        var module = assembly.DefineDynamicModule("Main");
        var typeBuilder = module.DefineType(
            "CollectibleIgnoredInterface",
            TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract);
        typeBuilder.SetCustomAttribute(new CustomAttributeBuilder(
            typeof(IgnoreAspectAttribute).GetConstructor(Type.EmptyTypes),
            Array.Empty<object>()));
        var interfaceType = typeBuilder.CreateType();
        var reference = new WeakReference(interfaceType);

        var services = new ServiceCollection();
        services.EnableAop();
        services.Clear();

        interfaceType = null;
        typeBuilder = null;
        module = null;
        assembly = null;
        services = null;

        return reference;
    }
}
