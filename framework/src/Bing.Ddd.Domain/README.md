# Bing.Ddd.Domain
[![NuGet](https://img.shields.io/nuget/v/Bing.Ddd.Domain.svg)](https://www.nuget.org/packages/Bing.Ddd.Domain/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.Ddd.Domain.svg)](https://www.nuget.org/packages/Bing.Ddd.Domain/) ![TFM](https://img.shields.io/badge/TFM-netstandard2.0-blue.svg)

> DDD 领域层：实体 / 聚合根 / 值对象、仓储接口、领域事件与变更跟踪。
>
> **目标框架**：`netstandard2.0` ｜ **分层**：L4 领域层 ｜ **当前版本**：7.0.0 ｜ **源文件**：63
>
> **上游 Bing 包**：`Bing.Aop.AspectCore`、`Bing.Auditing.Contracts`、`Bing.Data`、`Bing.Logging`、`Bing.Uow`、`Bing.Validation`

---

## 这是什么

提供领域建模的全部基础设施：

- 实体基类 `Entity` / `AggregateRoot` / `ValueObject` 与审计接口；
- 仓储与存储契约 `IRepository<TEntity,TKey>` / `IStore<TEntity,TKey>` / `IQueryStore`；
- 领域事件分发 `AddDomainEventDispatcher(...)`。

⚠ **运行时命名空间与包名不同**：仓储接口在 **`Bing.Domain.Repositories`**，不在 `Bing.Data`。

## 安装

```bash
dotnet add package Bing.Ddd.Domain
```

## 快速上手

```csharp
public class Order : AggregateRoot<Guid>
{
    public string OrderNo { get; private set; }
    public void ChangeNo(string no) { OrderNo = no; AddDomainEvent(new OrderNoChangedEvent(Id, no)); }
}

// 仓储注入（接口在 Bing.Domain.Repositories）
public class OrderService(IRepository<Order, Guid> repo) { }
```

## 注册入口

| 入口方法 | 命名空间 | 说明 |
| --- | --- | --- |
| `AddDomainEventDispatcher(services)` / `(services, Type[])` / `(services, Assembly[])` | `Bing.Domain.Entities.Events` | 扫描并注册领域事件处理器分发器 |

## 关键类型

| 类型 | 命名空间 | 说明 |
| --- | --- | --- |
| `Entity` / `AggregateRoot` / `ValueObject` | `Bing.Domain.Entities` | 实体与聚合根基类 |
| `IRepository<TEntity,TKey>` / `IStore<TEntity,TKey>` | `Bing.Domain.Repositories` | ⚠ 仓储契约在 **Domain** 包，不在 `Bing.Data` |
| `IHasErrorCode` / 审计接口 | `Bing.Domain.Entities` | 实体能力接口 |

## 与其它包的关系

**依赖本包**：`Bing.EntityFrameworkCore`

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.Ddd.Domain

## 变更跟踪（GetChanges）

继承 `DomainObjectBase<TObject>` 并声明 `partial` 的领域对象会在编译期生成自己的 `AddChanges(TObject other)`，无需任何类级标记。生成代码只读取 `this.Property` / `other.Property`，并调用四个固定的运行时辅助方法：

```csharp
public partial class Order : DomainObjectBase<Order>
{
    public string Number { get; set; }
    public Customer Customer { get; set; }
    public List<OrderLine> Lines { get; set; }
    public Dictionary<string, string> Attributes { get; set; }
}

// 生成结果（示意）
protected override void AddChanges(Order other)
{
    AddChange(nameof(Number), "订单编号", this.Number, other.Number);
    AddObjectChanges(nameof(Customer), "客户", this.Customer, other.Customer);
    AddCollectionChanges(nameof(Lines), "订单明细", this.Lines, other.Lines);
    AddDictionaryChanges(nameof(Attributes), "扩展属性", this.Attributes, other.Attributes);
}
```

- `AddChange<T>`：使用 `EqualityComparer<T>.Default` 判断类型值是否变化，变化后格式化新旧值；字符串区分大小写、不 Trim，`null`、空字符串与缺失状态可区分。
- `AddObjectChanges<T>`：处理双方为空、单侧为空、运行时类型不同；双方都是领域对象时直接调用旧对象的 `GetChanges`，并按关系路径合并结果。
- `AddCollectionChanges<T>`：有序集合按索引或实体身份匹配，Set 按元素身份或容器相等语义匹配；元素是领域对象时调用其自身 `GetChanges`。
- `AddDictionaryChanges<TKey,TValue>`：使用字典容器自身键比较器匹配键；值是领域对象时调用值的 `GetChanges`，标量值使用类型值比较。

集合身份来自领域契约：实现 `IEntity` 时使用 `GetKeys()`（复合键按既有顺序），仅实现唯一 `IKey<TKey>` 时使用 `Id`；`Dictionary` 使用容器键，有序集合无身份时按索引。无序复杂 `Set` 的元素缺少身份契约时，生成器报告 `CG019` 并拒绝生成。父实体不能为子实体另选身份键。

审计时间、`Version` 和 `IsDeleted` 固定由生成器按默认预设忽略；需要跟踪这些字段时请手写 `AddChanges`。`[ChangeIgnore]` 只能标记当前实体声明或继承得到的成员，父实体忽略整个关系时直接标记关系属性。特殊比较（忽略大小写、浮点容差）由实体手写 `AddChanges` 表达。

`GetChanges` 的运行时语义：

- 每次最外层调用拥有独立结果帧，返回集合不会被后续调用修改；
- 嵌套调用只通过路径前缀合并，异常时清理当前帧；
- 只阻止当前比较链上的同一旧/新引用对再次进入，兄弟路径共享对象仍分别汇总；
- 普通 POCO 结构属性不参与自动比较（生成器报告 `CG006`）；需要比较时请建模为领域对象或手写 `AddChanges`。

```csharp
var changes = before.GetChanges(after);
foreach (var change in changes)
    Console.WriteLine($"{change.PropertyName}: {change.OldValue} -> {change.NewValue}");
```

### 独立对象图引擎（可选）

`ChangeGraphTracker` / `ChangeGraphModel` / `ChangeGraphOptions` 仍作为**独立功能**保留，用于冻结快照、结构/适配器比较等场景；它不再参与 `DomainObjectBase.GetChanges` 的生成实现，也不作为领域对象的隐式回退。

```csharp
var options = ChangeGraphOptions.Default
    .WithTrackMoves()
    .WithKeyComparer<string>(StringComparer.Ordinal);
var tracker = new ChangeGraphTracker(options);
var snapshot = tracker.Capture(order);
// 修改 order 后：
var changes = tracker.CompareSnapshot(snapshot, order);
```

## 许可证

ChangeGraph性能基准与运行方式见 [Bing.Ddd.Domain.Benchmarks](../../tests/Bing.Ddd.Domain.Benchmarks/README.md)。

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。

领域比较入口是 `GetChanges`：它建立一次调用级会话，执行实体自身的 `AddChanges`（手写或生成），并通过 `AddObjectChanges`/`AddCollectionChanges`/`AddDictionaryChanges` 对子领域对象调用其 `GetChanges` 并按关系路径汇总。生成 hook 逐成员发射，不使用整图比较替换子实体手写规则。手写 override 不会被覆盖，且会收到 `CG015` 提示；非 `partial` 报 `CG001`，`new` 同名 hook 报 `CG020`。

类型值判等先于文本展示：字符串按 ordinal 区分大小写、不 Trim，`null`/空字符串/缺失可区分；`decimal 1.0m` 与 `1.00m` 无差异。文本展示统一使用文化无关格式并对字符串加引号（保留源值内容）。需要特殊比较（如忽略大小写、浮点容差）时在实体手写 `AddChanges` 中自行判断。

树结构的 `Parent` 默认参与遍历；只比较子树时应在实体上明确 `[ChangeIgnore] Parent`，否则可能经父对象访问兄弟实体。会话按引用对祖先链防环，不同兄弟路径共享同一对象仍分别汇总。完整示例及生成物见 [消费示例](../../tests/Bing.Ddd.Domain.Aot.Smoke/README.md)。
