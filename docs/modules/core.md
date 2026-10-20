# 核心、模块化与依赖注入

## 功能说明

`Bing.Core` 提供模块发现、服务注册、启动生命周期、反射与选项基础设施；`Bing.Aop.AspectCore` 提供接口代理拦截，`Bing.ExceptionHandling` 提供异常通知与处理契约。

## 核心实现逻辑与关键文件

| 路径 | 职责 |
| --- | --- |
| `framework/src/Bing.Core/Bing/Core/Builders/` | `BingBuilder` 纳入业务模块及依赖，并执行服务注册。 |
| `framework/src/Bing.Core/Bing/Core/Modularity/` | 模块基类、依赖声明、层级和模块发现。 |
| `framework/src/Bing.Core/Bing/DependencyInjection/` | 生命周期标记、属性配置、服务定位与作用域。 |
| `framework/src/Bing.Aop.AspectCore/` | AOP 配置及拦截器实现。 |
| `framework/src/Bing.ExceptionHandling/` | 异常处理扩展；Web 中间件位于 `Bing.AspNetCore`。 |

`AddBing` 注册基础服务并扫描类型。`AddModule<T>` 选择模块；`AddModules(params Type[] exceptModuleTypes)` 加载发现的模块并排除指定类型，参数不是要加载的清单。注册阶段与 `UseBing` 初始化阶段分开，模块依赖声明不等同于拓扑排序。

## 对外接口与用法

以下使用宿主中已定义的 `AppModule`：

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddBing().AddModule<AppModule>();
// Web 宿主在管道装配阶段调用 app.UseBing()；
// 非 Web 宿主在创建服务提供器后调用 serviceProvider.UseBing()。
```

类型通过 `ISingletonDependency`、`IScopedDependency`、`ITransientDependency` 参与约定注册；`DependencyAttribute` 可指定生命周期，`IgnoreDependencyAttribute` 可排除注册。需要拦截时使用 `EnableAop`。

## 依赖与配置项

核心依赖 Microsoft.Extensions 的 DI、Options、Logging 与程序集加载能力；AOP 依赖 AspectCore。模块的 `Level`、`Order` 决定排序；`AddBing` 接收的 `BingOptions` 当前为空类，具体配置由组件自己的 Options 传入，没有统一自动绑定所有配置节的保证。

## 已知限制与注意事项

- 自行创建的对象不经过 DI 代理，不能依赖接口 AOP 拦截。
- `AddModule<T>` 属于 `IBingBuilder`，不是 `IServiceCollection` 方法。
- `AddModules` 排除的是发现阶段的入口类型；该类型仍可能通过其他模块的依赖声明被加载。
- 非 Web 宿主需显式完成初始化；只注册服务不能代替 `UseBing`。
- 约定注册扩展点中未接入执行链的自定义能力属于“待实现/待确认”，详见[功能完成度](../architecture/功能完成度.md)。

更多示例见[架构文档](../architecture/架构文档.md)和[扩展指南](../guides/扩展指南.md)。
