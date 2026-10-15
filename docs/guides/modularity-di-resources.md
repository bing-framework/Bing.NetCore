# 模块 DI 与资源所有权指南

> **适用版本**：框架 `7.0.0`｜源码核对：2026-10-13

## 注册与校验

`AddBingApplication<T>()` 只扫描选中模块的程序集和显式附加程序集。约定注册实现 `ISingletonDependency`、`IScopedDependency`、`ITransientDependency` 或标记 `[Dependency]` 的具体类；`IDisposable`、`IAsyncDisposable` 不作为业务接口注册。要关闭约定扫描，设置 `options.AutoRegisterServices = false`，再在模块配置阶段手工注册服务。

开放泛型约定注册支持单个业务接口。若 Singleton/Scoped 的开放泛型实现需要通过多个普通业务接口共享实例，注册阶段会明确拒绝，因为默认容器不能以开放泛型工厂转发次接口；请改为单接口约定注册，并为其他接口手工注册封闭类型或适配器。瞬时服务的 `[Dependency(TryAdd = true)]` 与 `[Dependency(ReplaceExisting = true)]` 分别遵循尝试添加和替换语义；没有这两个标记时继续使用多实现枚举注册。

构建根容器时可传入原生校验选项，不要为预检另建第二个容器：

```csharp
var provider = services.BuildBingServiceProvider(new ServiceProviderOptions
{
    ValidateOnBuild = true,
    ValidateScopes = true
});
```

构造函数自环、多节点环、缺失依赖和单例构造函数捕获 Scoped 通常在构建时暴露；从根容器解析 Scoped 在解析时拒绝。开放泛型的具体封闭类型、工厂委托、动态解析和属性注入不能靠 `ValidateOnBuild` 完整校验。`Lazy<T>` 只延迟解析；在构造期间访问 `Value` 仍可能递归。工厂或属性形成的无限递归应在受控子进程排查，不要在常规测试宿主中直接触发。测试同时保留一条不启用 AspectCore 的普通 Microsoft DI 工厂递归路径，并在固定深度抛出诊断异常；同一个 `Lazy<T>` 实例重入 `Value` 则由运行库立即拒绝。

## 多接口与释放

约定注册的 Singleton/Scoped 实现有多个业务接口时，次接口通过主接口转发，同一作用域通常得到同一实例。`TryAdd` 的主接口已存在时，次接口改为直接注册当前实现，避免错误转发给已有实现。若后续注册替换主接口，旧次接口解析会明确报错；应调整注册顺序或手工注册适配器，不能继续假设旧实现仍能通过主接口取得。`AddSelf` 会额外注册自身，不保证自身与接口是同一个实例；`MultipleDependency` 保留接口下的所有实现。

Microsoft DI 会记录工厂返回的可释放对象。若同一个 `IDisposable` 实例通过两个业务接口转发并均被解析，容器可能重复调用 `Dispose`；直接测试中观察到两次。对于有状态资源，优先只注册一个拥有资源的服务，并为其他接口注册**不实现释放接口**的适配器；或者手工注册调用方拥有的单例实例，并由调用方在容器关闭后释放一次。不要依赖资源自身的 `Dispose` 幂等性来掩盖注册所有权不明。

下面的两个适配器自身不实现 `IDisposable`，共享资源只由 `ResourceOwner` 的一个容器描述符持有：

```csharp
using System;
using Microsoft.Extensions.DependencyInjection;

public interface IReader { }
public interface IWriter { }

public sealed class ResourceOwner : IDisposable
{
    public void Dispose() { /* 释放共享资源 */ }
}

public sealed class ReaderAdapter : IReader
{
    public ReaderAdapter(ResourceOwner owner) => Owner = owner;
    public ResourceOwner Owner { get; }
}

public sealed class WriterAdapter : IWriter
{
    public WriterAdapter(ResourceOwner owner) => Owner = owner;
    public ResourceOwner Owner { get; }
}

services.AddSingleton<ResourceOwner>();
services.AddSingleton<IReader>(provider =>
    new ReaderAdapter(provider.GetRequiredService<ResourceOwner>()));
services.AddSingleton<IWriter>(provider =>
    new WriterAdapter(provider.GetRequiredService<ResourceOwner>()));
```

以上注册方式有直接消费测试 `ManualAdapters_ShouldDisposeSharedOwnerOnce`。若在实际模块中使用，关闭约定扫描或确保不会再由扫描重复注册同一资源实现。

模块实例由框架创建和释放；容器创建的服务由容器释放；通过 `AddSingleton(instance)` 传入 Microsoft DI 的实例由调用方负责。正常异步路径应使用 `await using`、`UseBingAsync` 与 `ShutdownBingAsync`，尤其是仅实现 `IAsyncDisposable` 的资源。初始化失败时框架会逆序关闭已开始的模块并释放其拥有的模块资源；原始异常保留在主异常，清理异常附加在 `Exception.Data`。模块不要保存初始化上下文内的 Scoped 服务，也不要替容器释放它。

## AspectCore 属性注入

`AutowiredAttribute` 定义在 `Bing.Core`，是容器无关的标记；AspectCore 的执行器位于 `Bing.Aop.AspectCore`。使用 AspectCore 服务上下文时，必须调用 `services.EnableAutowired()`，或者调用会自动启用它的 `services.EnableAop()`：

```csharp
hostBuilder.UseServiceContext();
services.EnableAutowired();
```

仅调用 `UseServiceContext()` 不会自动启用中立 `[Autowired]`，普通 Microsoft DI 也不会执行属性注入。真实 AspectCore 容器测试确认公共属性和继承属性可注入；服务缺失时保留属性已有值，未预设时保持 `null`，setter 异常会传播。当前路径不会注入标记字段、索引器或非公开 setter，改用公开可写属性或构造函数。原生 `[FromServiceContext]` 仍保持兼容；同一属性同时使用两个标记时只注入一次。缺失依赖如必须失败，优先用构造函数注入，或在初始化时显式检查属性。

实例所有权随容器而异：上述 `AddSingleton(instance)` 不由 Microsoft DI 释放；当前 AspectCore 3.0 宿主测试观察到传入的单例实例由宿主释放。通过属性注入解析的瞬时可释放服务在该容器路径下不会自动转为宿主拥有，资源类服务应改用作用域或单例生命周期，或由明确的业务所有者释放。不要把一个容器的所有权规则直接套用到另一个容器。

属性自环以及构造/属性混合环在受控子进程测试中均导致栈溢出；容器构建校验没有发现它们。不要把这类依赖放进常规测试进程或生产模块，改为明确的单向依赖。缓存 `IServiceProvider` 或 `LazyServiceProvider` 时，作用域服务的缓存不得跨作用域或跨宿主长期保存。

## 关闭与取消

取消令牌表示请求停止，不表示模块钩子、活动调用或资源释放已经完成。插件热更新在切换服务入口后等待旧代活动调用退出；调用不响应取消时，`Status.Retiring` 仍保留旧代，`ReloadAsync` 仍在等待。调用方可以自行停止等待并读取状态，但不能把“超时”报告为“已释放”；活动调用退出后再等待关闭完成。不要在初始化或插件调用还运行时提前释放根容器。

Generic Host 停止会调用模块关闭；非 Host 场景显式调用 `ShutdownBingAsync`。强杀进程和断电不能保证托管 `Dispose`、`DisposeAsync` 或关闭钩子执行。对需要进程外恢复的资源，应由业务服务实现持久化与恢复协议。

若异步关闭已开始，另一线程随后同步释放容器会等待同一关闭任务完成，不会再次执行关闭钩子。若关闭钩子不结束，同步释放也会一直等待；应由宿主协调停止顺序，避免把不响应取消的钩子误当成已清理。

## 资源探针

`ModuleResourceProbeTest` 为正常、初始化失败和预取消场景分别预热 20 次、记录 100 个测量样本；插件更新也记录 100 个连续退役代。测量循环在同步边界等待每次异步操作完成，不额外构造收尾代，因此最后一个样本也接受回收检查；末次插件上下文采用有界 GC 重试，不用下一代更新掩盖保留路径。测试另有初始化回调执行期间取消的直接用例，断言已创建模块仍只释放一次。测试对全部样本检查弱引用，记录总分配、托管堆、各代 GC、进程工作集和释放次数。指标快照先于测试的强制 GC，弱引用检查随后执行。工作集受 JIT、加载器与操作系统影响，单次变化不能证明泄漏或无泄漏。启动期默认加载上下文程序集会保留到进程结束，不适用卸载断言。
