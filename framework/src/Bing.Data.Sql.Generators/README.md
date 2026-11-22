# Bing.Data.Sql.Generators

[![NuGet](https://img.shields.io/nuget/v/Bing.Data.Sql.Generators.svg)](https://www.nuget.org/packages/Bing.Data.Sql.Generators/)

Bing SQL 的 Roslyn 增量生成器，提供独立模型上下文与可选 Lambda 调用点生成。生成只固定 CLR 结构和直接属性访问，不固定租户表名、Schema、运行时映射或 Reader 列序号。

包通过 `analyzers/dotnet/cs` 加载，buildTransitive props 向编译器传递 `BingSqlAotStrict/PublishAot`。严格配置关闭运行库反射回退特性开关，运行时使用 `SqlExecutionMode.GeneratedOnly`；普通 JIT 可选择 Compatible 或 GeneratedPreferred。基础模型、Raw SQL 和 CRUD 不依赖拦截器。

```csharp
using Bing.Data.Sql.Generated;

[SqlModel(typeof(Order))]
[SqlResult(typeof(OrderResult))]
[SqlParameters(typeof(OrderFilter))]
public static partial class ApplicationSqlModels { }

// 在宿主组合根中登记，支持预编译模型程序集：
services.AddSqlGeneratedModels(ApplicationSqlModels.CreateModels());
```

生成上下文必须是非嵌套、非泛型 partial class；模型支持可从生成代码访问的 public/internal 类型。生成公开成员的 getter/setter；只有模型非抽象、没有 required 成员且有 public 无参构造时才生成工厂，运行时映射覆盖继续生效。不可变 DTO、init-only 或私有 setter 不能自动变成严格结果模型。未知类型在执行准备阶段失败，不扫描程序集。`Register()` 保留全局登记兼容用法，新宿主优先使用 DI 快照。

派生参数对象应登记执行时的具体类型；生成器会同时处理可访问的继承属性和派生属性：

```csharp
public class BaseFilter { public int TenantId { get; set; } }
public sealed class AdvancedFilter : BaseFilter { public int Status { get; set; } }

[SqlParameters(typeof(AdvancedFilter))]
public static partial class FilterModels { }
```

仅登记 `BaseFilter` 时，`GeneratedOnly` 会拒绝 `AdvancedFilter` 参数；启用反射回退时，Compatible 和
GeneratedPreferred 将对象交给兼容参数绑定路径，以保留完整参数。

严格 Lambda 值条件使用 `Where<TEntity, TValue>(row => row.Id, value, Operator.Equal)`。常量和列对列比较可生成，闭包比较在严格模式报告 BINGSQLG001，并建议显式值重载或参数化 Raw SQL；JIT 保留原路径。严格单类型列表使用统一静态结果映射，包含运行时配置及转换器。

Dapper 全局 TypeHandler 需显式 `SqlTypeHandlerAdapter<T>` 适配；JSON 需静态转换器和生成 JSON 类型信息。NULL/缺列保留属性初始值，不自动开启严格 NULL 规则。完整合同、包消费矩阵与迁移见 [读写分离与 AOT](../../../docs/migrations/sqlquery-command-aot.md)。

生成器编译依赖 Roslyn 5.0.0，保持 netstandard2.0；验证环境为 .NET 10 SDK、常规 net6/net8 和 Native net10/win-x64/SQLite。具体 SDK/包组合证据见任务执行报告，未验证的旧编译器和其他 Native Provider 不视为已支持。Native 验收包含真实 NuGet 生成器加载、跨程序集模型、Raw-only/CRUD-only 消费及完整读写事务程序。
