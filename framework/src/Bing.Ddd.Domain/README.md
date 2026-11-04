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

## ChangeGraph 对象图跟踪

继承 `DomainObjectBase<TObject>` 并声明 `partial` 的领域对象会自动获得 `AddChanges` 和独立对象图模型，无需任何类级标记。父实体引用子实体自己生成的模型，关系可以是关联、聚合、组合或双向树。采集使用直接属性访问和共享会话，不在运行时扫描属性。

```csharp
public partial class Order : DomainObjectBase<Order>
{
    public List<OrderLine> Lines { get; set; }
}

public partial class OrderLine : EntityBase<OrderLine, int>
{
    public int Id { get; set; }
    [ChangeIgnore]
    public string InternalNote { get; set; }
    public decimal Amount { get; set; }
}

var result = new ChangeGraphTracker().Compare(before, after);
```

集合身份完全来自领域身份契约：实现 `IEntity` 时优先使用 `GetKeys()`（即使同时实现 `IKey`），仅实现唯一 `IKey<TKey>` 时使用 `Id`；`Dictionary<TKey,TValue>` 使用字典键，有序 `List`/数组在无身份时按索引比较。无序 `Set` 的元素必须实现 `IKey`/`IEntity`，否则生成器报告 `CG019` 并拒绝生成（直接与任意嵌套层一致），不会猜测同名属性作为身份。父实体不得通过路径字符串或调用方 API 改写子实体规则。

审计时间、`Version` 和 `IsDeleted` 固定由生成器按默认预设忽略（`IHasCreationTime`、`IHasModificationTime`、`IHasDeletionTime`）。本期不提供逐实体恢复开关；确实需要跟踪这些字段时请手写 `AddChanges`。`[ChangeIgnore]` 只能标记当前实体声明或继承得到的成员；父实体需要忽略子对象或集合时，直接在关系属性上使用 `[ChangeIgnore]`，表示整个关系不读取、不枚举、不读取元素键。

支持对象、交错顺序集合、字典值和带稳定键的 Set。`ChangeGraphOptions.Default.WithTrackMoves()` 可打开有键列表的移动报告。身份覆盖入口（`WithIdentity`/`WithCollectionKey`）与手工 `ignorePaths` 已按 DEC-04 删除，父级不能为子实体另选键或裁剪路径。`Capture` 会生成脱离原对象的冻结快照，随后使用 `CompareSnapshot` 比较原地修改。

```csharp
var options = ChangeGraphOptions.Default
    .WithTrackMoves()
    .WithKeyComparer<string>(StringComparer.Ordinal);
var tracker = new ChangeGraphTracker(options);
var snapshot = tracker.Capture(order);
// 修改 order 后：
var changes = tracker.CompareSnapshot(snapshot, order);
var legacy = changes.ToLegacyDescriptors(); // 显式有损文本投影；保留完整路径
```

`WithComparer<T>` 控制标量值（包括标量 Set 成员）相等，`WithKeyComparer<T>` 独立控制字典、集合身份键的相等与哈希；两者不能覆盖实体字段或实体身份。比较器保留原始载荷值。

`object`、开放接口及派生类型可通过实际实体生成的模型分派；没有生成模型的复杂类型需要显式的 `WithAdapter<T>(ChangeGraphModel<T>)` 模型，内建标量可直接冻结。未注册的复杂形状带路径失败，不会静默只采基类字段。手工模型使用 `members` 和 `collectionMembers` 声明元数据；生成模型自动提供这些元数据。

`ChangePath.Segments` 保留成员、数值索引和类型化键，特殊键的展示文本会转义。循环载荷通过 `NodeId` / `ReferenceId` 标识定义与回边，编号仅在该快照内有效。深度、节点数、集合元素数和遍历步数限制覆盖捕获、差异展开及载荷生成；超限抛出异常，不返回半份结果。快照仅供创建它的同一 tracker 使用。

## 许可证

ChangeGraph性能基准与运行方式见 [Bing.Ddd.Domain.Benchmarks](../../tests/Bing.Ddd.Domain.Benchmarks/README.md)。

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。

领域比较入口是 `GetChanges`：它建立一次调用级会话，执行实体自身的 `AddChanges`（手写或生成），并通过新增的 `AddObjectChanges`/`AddSequenceChanges`/`AddSetChanges`/`AddDictionaryChanges` 对子领域对象调用其 `GetChanges` 并按关系路径汇总。生成 hook 逐成员发射，不再用整图比较替换子实体手写规则。手写 override 不会被覆盖，且会收到 `CG015` 提示；非 `partial` 报 `CG001`，`new` 同名 hook 或自声明 `CaptureChanges` 分别报 `CG020`/`CG021`。

类型值判等先于文本展示：字符串按 ordinal 区分大小写、不 Trim，`null`/空字符串/缺失可区分；`decimal 1.0m` 与 `1.00m` 无差异。文本展示统一使用文化无关格式并对字符串加引号（保留源值内容）。需要特殊比较（如忽略大小写、浮点容差）时在实体手写 `AddChanges` 中自行判断。

树结构的 `Parent` 默认参与遍历；只比较子树时应在实体上明确 `[ChangeIgnore] Parent`，否则可能经父对象访问兄弟实体。会话按引用对祖先链防环，不同兄弟路径共享同一对象仍分别汇总。完整示例及生成物见 [消费示例](../../tests/Bing.Ddd.Domain.Aot.Smoke/README.md)。
