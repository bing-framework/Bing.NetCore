---
name: chinese-comments
description: 为 C#/.NET 项目自动补全规范、简洁的中文 XML 注释。
---

# 中文注释规范

生成、修改、重构或评审 C#/.NET 代码时，应同步完善当前改动范围内的中文 XML 文档注释，不得改变业务逻辑、公开契约和代码行为。

## 一、基本要求

- 使用简体中文，注释应准确、简洁、专业。
- `<summary>` 只描述成员的核心用途，避免堆叠实现细节。
- 需要进一步说明的规则、限制、副作用、缓存、事务、线程安全、性能等内容，优先放入 `<remarks>`。
- 注释应表达代码名称本身无法完整体现的信息，不得机械翻译成员名称。
- 不因访问级别、`static`、异步或泛型而省略注释。
- 优先处理本次新增或修改的代码；同文件中明显缺失、错误或低质量的注释应一并修正。

## 二、必须补充注释的对象

- 类型：class、interface、abstract class、static class、record、struct、enum、delegate、Controller、ApplicationService、Repository、DTO、Entity、Options 等。
- 构造函数。
- 所有具名方法：public、protected、internal、private、static、实例、异步、泛型、扩展方法。
- 属性、索引器、事件。
- 所有类字段，包括 private、protected、internal、public、static、readonly、static readonly。
- `const` 常量、缓存键、配置键、权限名、Header、Claim、正则表达式、超时和重试参数。
- 枚举类型及每个枚举成员。

本地函数不强制使用 XML 注释；逻辑复杂时使用普通 `//` 注释说明原因或关键步骤。

## 三、`summary` 文案规范

### 1. 构造函数

构造函数统一使用简洁的初始化描述，并引用当前类型：

```csharp
/// <summary>
/// 初始化一个 <see cref="SqlBuilder"/> 类型的实例。
/// </summary>
public SqlBuilder()
{
}
```

要求：

- `<see cref="..."/>` 必须引用实际构造类型。
- 不在 `<summary>` 中描述依赖注入参数、初始化流程等细节。
- 存在特殊初始化行为时，放入 `<remarks>`。

### 2. 属性

属性注释保持简洁，优先使用“获取”“设置”“获取或设置”等明确表述：

```csharp
/// <summary>
/// 获取或设置 SQL 操作配置。
/// </summary>
public SqlOptions SqlOptions { get; set; }
```

根据实际访问方式使用：

- 可读写属性：`获取或设置……`
- 只读属性：`获取……`
- 只写属性：`设置……`
- `init` 属性：可使用 `获取或初始化……`

复杂约束、默认值、生命周期、计算逻辑等内容放入 `<remarks>`。

### 3. 字段与常量

字段必须补充注释，包括类中的私有字段。

字段和常量的 `<summary>` 应直接说明用途，不使用“获取或设置”：

```csharp
/// <summary>
/// SQL 操作配置。
/// </summary>
private readonly SqlOptions _sqlOptions;
```

```csharp
/// <summary>
/// 默认命令超时时间（秒）。
/// </summary>
private const int DefaultCommandTimeout = 30;
```

如需说明生命周期、线程安全、缓存策略、锁保护对象或特殊边界，放入 `<remarks>`。

### 4. 方法

方法 `<summary>` 应清晰说明“做什么”，保持简洁，不在摘要中展开实现过程。

```csharp
/// <summary>
/// 创建 SQL 查询对象。
/// </summary>
public SqlQuery CreateQuery()
{
}
```

对于语义一致的重载方法：

- `<summary>` 尽量保持一致；
- 参数差异通过 `<param>` 说明；
- 重载之间的行为差异、默认策略或特殊规则放入 `<remarks>`；
- 不为了区分重载而刻意生成冗长或不同风格的摘要。

## 四、`inheritdoc` 规则

以下成员在上游契约已有有效注释时，优先使用：

```csharp
/// <inheritdoc />
```

适用于：

- 接口实现和显式接口实现；
- 抽象成员实现；
- `override` 重写成员；
- 接口属性、索引器和事件的实现。

不得在实现类中重复复制接口或基类已有的 `<summary>`、`<param>`、`<returns>`。

实现存在额外行为时，可在 `inheritdoc` 后追加 `<remarks>` 或必要的 `<exception>`。

普通私有方法、静态辅助方法、字段、常量、构造函数和新增业务方法没有可继承契约时，必须编写独立注释。

如果接口或抽象基类由当前项目维护且缺少注释，应先完善上游契约，再在实现成员使用 `/// <inheritdoc />`。

## 五、XML 标签规则

- `<summary>`：仅说明核心用途和业务语义，保持简洁。
- `<param>`：仅为实际参数补充，说明含义、格式、单位、范围或空值规则。
- `<typeparam>`：说明泛型参数的职责和约束。
- `<returns>`：对有实际返回结果的成员说明返回内容；构造函数和 `void` 方法不得添加。
- `Task` / `ValueTask`：无结果异步方法可不写 `<returns>`；`Task<T>` / `ValueTask<T>` 应说明最终返回结果。
- 返回 `bool` 时应说明 `true` 和 `false` 的含义。
- 可空返回值在必要时说明返回 `null` 的条件。
- `<exception>`：仅记录调用方需要关注且代码明确抛出或传播的异常。
- `<remarks>`：用于补充事务、缓存、线程安全、幂等性、性能、权限、默认行为、实现差异和重要副作用。
- `<example>`：仅用于复杂或容易误用的公共 API。

## 六、方法体注释

仅在以下场景添加普通注释：

- 复杂业务规则或边界处理；
- 非直观算法；
- 并发、锁、重试、幂等；
- 性能优化；
- 兼容性或临时规避方案；
- 看似可删除但实际不可删除的代码。

不要逐行解释显而易见的代码。

## 七、排除范围

除非明确要求，不处理：

- `bin`、`obj`；
- `*.g.cs`、`*.generated.cs`、`*.Designer.cs`；
- EF Core Migration 和 ModelSnapshot；
- 自动生成客户端、代理代码和第三方源码。

## 八、执行要求

1. 先检查接口、抽象类和公共契约注释。
2. 实现和重写成员优先使用 `/// <inheritdoc />`。
3. 构造函数、普通方法、属性、私有字段和常量按本规范补充独立注释。
4. 重载方法语义一致时，保持相同或高度一致的 `<summary>`。
5. `<summary>` 保持简洁，扩展说明优先放入 `<remarks>`。
6. 类中的 private 字段不得因访问级别而遗漏。
7. 清理重复、错误、过时、冗长或无意义的注释。
8. 不进行无关重构，不修改签名、命名空间、业务逻辑和公开 API。
9. 无法确认业务语义时使用谨慎、客观的描述，不得编造。
10. 完成后检查当前改动文件，确保注释与代码签名一致。
