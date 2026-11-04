# ChangeTrackable 实体自治与 GetChanges 组合调用方案（修订 3）

## 1. 执行基线与范围

- Task ID：`20261104-domain-change-tracking-autonomous-contract`。
- 修订日期：2026-11-04。本文件整体替代该任务旧版 plan.md，不是在旧方案末尾叠加新约定。
- 用户已确认 DEC-01：统一按类型实际值判等，保留原值展示；特殊比较规则由实体自身定义。
- 用户已确认 DEC-04：删除调用方身份覆盖和路径覆盖 API；保留关系级忽略、实体自身配置以及不改变实体规则的过程选项。
- 用户已确认 DEC-02：对子领域对象通过 `GetChanges` 获取差异并汇总，手写及生成的 `AddChanges` 在直接、嵌套比较中一致生效。
- 用户已确认 DEC-03：实现 `IEntity` 时优先使用 `IEntity.GetKeys()`；仅实现 `IKey<TKey>` 时使用 `Id`；不新增身份标记。
- 保留此前决定：继承 `DomainObjectBase<TObject>` 自动生成，必须为 partial；手写 override 优先；删除五个跟踪特性，只保留 `ChangeIgnore`；功能尚未发布，不提供迁移别名和兼容占位。
- 范围：Domain 变更跟踪、相关基类 hook、SourceGenerator、对应测试、AOT fixtures/smoke、使用文档。SQL、数据库、验证框架、实体全局相等性重构不在本任务范围。
- 本轮仅更新计划。后续由 OpenCode 实施；不自动 Git add/commit/push/merge，不发布包，不回退其他人的工作区变更。
- `review.md` 和 `review-evidence/` 保留为旧候选的独立审计证据，不改写为新方案已通过。实施者在新的 execution.md 中引用并更新处理状态。

### 1.1 决策状态

| 决策 | 当前状态 | 对实施者的约束 |
|---|---|---|
| DEC-02 子实体手写规则 | 已确认，待实现验收 | 父级匹配子对象后调用其 GetChanges，不能绕过手写 AddChanges |
| DEC-03 实体身份 | 已确认，待实现验收 | IEntity.GetKeys 优先，其次仅 IKey.Id；原计划 IKey 优先失效 |
| DEC-01 类型值比较 | 已确认，待实现验收 | 先按类型值判等再格式化；删除 ToLower/Trim 文本归一化判等；特殊规则由实体手写 |
| DEC-04 覆盖 API 清理 | 已确认，待实现验收 | 删除 WithIdentity/WithCollectionKey 所有覆盖重载、手工 ignorePaths 及兼容分支；不恢复父级子字段配置 |

DEC-01～04 均已由用户确认，无需就这四项重复请求批准；“已确认”不等于“已实现或验收”。直接和嵌套使用同一个类型值比较实现，不能按模型形状切换判等算法。删除覆盖能力不等于删除快照、移动报告、深度限制或容器固有语义，按 §3.4 实施。

## 2. 当前真实实现与差距

路径均相对仓库根 `H:/Bing_Framework/Bing.NetCore`。以下为修订时核实的行为，不是完成声明。

| 位置 | 已有行为 | 本轮所需变化/状态 |
|---|---|---|
| `framework/src/Bing.Ddd.Domain/Bing/Domain/Entities/DomainObjectBase.cs:38` | GetChanges 清空实体持有的 Context，执行 AddChangesInternal，返回同一可变集合 | 改为调用级结果生命周期；嵌套、重入不能清空外层或此前返回值 |
| 同文件 `:163`、`:175` | 类型检查后虚调用 AddChanges；手写 override 可生效 | 保留这个统一 hook 分派，父级必须复用 |
| `framework/src/Bing.Ddd.Domain/Bing/Domain/ChangeTracking/ChangeTrackingContext.cs:54` | 已有子对象 GetChanges + AddRange | 缺路径、循环、结果隔离；null 分支跳过增删 |
| `ChangeTrackingContext.cs:45` 标量 Add | 实际值不同后仍 ToLower/Trim 并比较文本，文本相同时丢弃变化 | DEC-01：类型值比较决定差异，格式化不参与判等 |
| `ChangeGraphOptions.cs:259/303`、`ChangeGraphModel.cs:87/107` | 类型/集合身份覆盖，手工 ignorePaths 及兼容构造器仍存在 | DEC-04：删除覆盖入口、状态和执行分支，保留独立图功能 |
| `ChangeTrackingContext.cs` 集合 Add | 按 Math.Min 长度逐项比较 | 不能承担新集合契约：会遗漏新增删除，且没有身份匹配 |
| `framework/src/Bing.Ddd.Domain.Extensions.Analyzers/Bing/Analyzers/SourceGenerator/ChangeTrackableSourceGenerator.EntryPoint.cs` | 复杂实体 AddChanges 整体调用 GraphTracker，再投影文本 | 改为逐成员调用；对子领域对象禁止以模型比较绕开其 hook |
| `SourceGenerator/ChangeTrackableSourceGenerator.Models.cs:590` | ResolveKeySpec 优先 IKey，才 IEntity | 统一调整为 IEntity 接口分派优先，覆盖显式实现与继承 |
| `framework/src/Bing.Ddd.Domain/Bing/Domain/Entities/EntityBase.cs:122` | GetKeys 默认返回单项 Id，非 virtual；Equals/GetHashCode 围绕 Id | 普通单键实体无需变化；复合示例不要隐藏 GetKeys，不改全局相等性 |
| `framework/src/Bing.Ddd.Domain/Bing/Domain/ChangeTracking/ChangedValueDescriptor.cs` | 公开结果只有名称、描述、旧/新文本 | 必须区分文本结果与内部结构化结果，不宣称 GetChanges 已返回 Kind/原始类型 |
| `ChangeGraphLegacyExtensions.cs` | 图结果到描述符为显式有损文本投影 | 可复用结构事件格式化，但不能投影整个子图取代子 hook |

旧候选审计已执行：生成器 net8 29/29；Domain 跟踪子集 net6/net8 各 99/99；JIT smoke 62 项通过。独立探针仍发现 FIX-001～004。NativeAOT 因缺少 net10.0/win-x64 资产阻断，当前包消费证据缺失，execution.md 缺失。本修订没有运行代码测试；旧结果不能证明本修订已经实现。

当前属于“基础功能落地，组合语义与边界不完整”，不是只有接口或 Mock；也不是可宣布全部交付。具体 Review 结论保留在同目录 review.md。

## 3. 对外契约与特性

### 3.1 唯一领域差异入口

保持签名：

```csharp
ChangedValueDescriptorCollection GetChanges(object otherObject);
```

- GetChanges 管理本次比较、调用实体自身 AddChanges、返回独立结果。
- AddChanges 是可重写的领域 hook，负责声明比较内容。生成器不覆盖、不补写已手写的正确 override。
- 自动与手写 hook 对嵌套具有同等效力。手写只加入 City，父级也只得到 City 差异；不能额外以生成模型补齐手写故意未比较的字段。
- AddChange(name, description, oldValue, newValue) 保留；生成器使用 nameof 和编译期描述/静态资源访问，不生成表达式反射入口。
- 手写 hook 自行负责加入哪些内容。ChangeIgnore、默认审计字段裁剪约束自动生成，不静默删除手写主动加入的字段。
- 不增加新的启用、节点、键、包含或路径特性。

### 3.2 自动发现与保留特性

1. 使用真实 `Bing.Domain.Entities.DomainObjectBase<TObject>` 符号沿继承链判断，禁止简单类型名兜底。
2. 必须 partial；不满足时报定位诊断。泛型、嵌套、EntityBase/ValueObjectBase、跨程序集派生均按真实 hook 参数生成。
3. 手写正确 override：不生成重复 hook；其他必要身份/快照元数据仍可生成。
4. 同签名 new、CaptureChanges 等辅助成员冲突：源声明处诊断，禁止先生成重复成员再让消费者收到 CS0111。其他无关 overload 不视作冲突。
5. 已删除的 ChangeGraph、ChangeKey、ChangeIgnorePath、ChangeIncludePath、ChangeInclude 不恢复；ChangeNode 不引入。
6. ChangeIgnore 仅表达本实体成员忽略。父级可忽略整个关系，不能通过路径裁剪子实体成员。
7. 自动审计忽略预设沿用现行 IHasCreationTime/IHasModificationTime/IHasDeletionTime、Version、IsDeleted 规则；需要恢复采集时手写 AddChanges，不恢复 ChangeInclude。

### 3.3 DEC-01：类型值比较与原值展示

标量 AddChange 统一采用 `EqualityComparer<TValue>.Default.Equals(oldValue, newValue)` 或语义等价的静态类型实现。字符串默认 ordinal、区分大小写；不调用 ToLower、Trim、文化相关字符串比较或 ToString 判等。Nullable、枚举、日期和数值遵循各自类型相等语义；“精确”指类型值语义，不是字节比较，decimal 的小数位数差异不会被当作值变化，NaN 等边界遵循对应 .NET 类型规则。

| 输入 | 预期结果 |
|---|---|
| `"ABC"` → `"abc"` | 有变化，旧/新文本保持各自大小写 |
| `"上海"` → `" 上海 "` | 有变化，保留新值前后空格 |
| `null` → `""` | 有变化，格式化必须能够区分 null 与空字符串 |
| `decimal 1.0m` → `1.00m` | 无变化 |
| 同类型的相等日期、枚举、Nullable 数值 | 无变化 |
| 同样字段变化，实体增加一个 null 子对象属性 | 结果不变，不切换标量算法 |
| 自定义值类型不相等但 ToString 相同 | 仍报告变化，不经第二次文本判等取消结果 |

判等后才生成 ChangedValueDescriptor 文本。复用或统一明确的转义/格式化规则；字符串引用/转义属于展示而非值修改。null、空字符串、Missing 要可区分，不能全部变成空文本。数值和日期采用稳定文化无关格式，不能把四舍五入后的显示值用于比较。描述符继续是文本契约，不声称已保留原始 CLR 值字段；源值内容不得被改成小写或 Trim 后版本。

生成器只把真正标量送入标量 AddChange；领域对象、结构对象和集合走对应规则，不能因泛型入口可接收 object 就退化为 ToString 比较。已有表达式便捷重载若保留，应复用统一 Add 比较语义，新生成路径仍不得用该反射重载。

特殊比较由实体手写，例如忽略名称大小写：

```csharp
protected override void AddChanges(Customer other)
{
    if (!string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase))
        AddChange(nameof(Name), "客户名称", Name, other.Name);
}
```

浮点容差等也由实体决定何时调用 AddChange，不增加特性或父级 comparer 覆盖。手写规则在直接与嵌套中一致。旧测试若断言自动小写/去空格，按本次已批准契约替换为更严格的原值和差异断言，记录旧→新行为；禁止简单删测试或减断言。

### 3.4 DEC-04：移除跨实体覆盖，保留过程选项

| 能力 | 本轮处理 |
|---|---|
| WithIdentity 单键/复合键覆盖重载 | 删除 API、注册状态、克隆/解析/执行分支，不提供 Obsolete 空壳 |
| WithCollectionKey 单键/复合键覆盖重载 | 删除；父关系不得为子实体另选键 |
| 手工 ignorePaths、路径包含/恢复规则及兼容构造器 | 删除配置入口及专用实现；不删除结果路径表达/转义能力 |
| 父级配置子字段是否跟踪、子字段判等或身份判等 | 不提供；不能换名恢复同样的覆盖能力 |
| 实体自身 ChangeIgnore、手写 AddChanges | 保留；父属性 ChangeIgnore 可忽略整个关系 |
| 列表移动报告、遍历深度等过程选项 | 保留既有能力，不能借过程选项改写实体规则 |
| Dictionary/Set 自身合法 comparer 语义 | 保留容器语义，不等同于额外配置实体身份 |
| 第三方结构访问适配器 | 仅保留必要访问能力；不能覆盖具有领域契约的对象 GetChanges/身份 |

一并检查 `WithComparer<T>`、`WithKeyComparer<T>` 和手工模型 selector：不能删掉两种方法名，却仍让全局字符串 comparer 改写子实体字段或复合身份分量。仍服务于非领域结构/容器访问的能力应限定适用边界；无法隔离的覆盖能力删除，记录调用链及保留/删除理由。不新增高级覆盖框架，不批量删除无关图选项。

容器键和实体身份分开：字典先按容器键匹配条目，条目中的领域值仍遵循自身 GetKeys/GetChanges；容器 comparer 不能传播到子实体字段/复合身份。复杂对象 Set 的跨版本实体配对仍按领域身份，不能以集合业务 comparer 替换实体身份；标量集合按容器自身支持的相等语义处理。

新旧字典或标量 Set 的 comparer 必须明确兼容判断，不能仅因类型相同就假设两个有状态 comparer 等价，不能任意选择一侧。默认、已知标准比较器和可确认相同的实例走支持路径；未知或不兼容语义明确失败并带关系路径，不用反射猜测或无声回退。自定义容器不能静态获得必要语义时明确诊断/不支持，不声称支持全部容器。

第三方类型有特殊业务规则时优先领域包装对象，本任务不扩展适配体系。生成器产出的实体自身身份 selector 继续保留，这是领域契约的实现，不是父级覆盖入口。快照捕获、内部 Kind 和冻结值继续验证，移除 ignorePaths 不应破坏结果路径或快照能力。

## 4. DEC-02：可组合 GetChanges 调用链

### 4.1 目标生成形态

以下为拟议生成代码，AddObjectChanges/AddCollectionChanges 为本任务需要实现的 protected 辅助方法名；不是当前已存在的 API。

```csharp
partial class Order
{
    protected override void AddChanges(Order other)
    {
        AddChange(nameof(Number), "订单编号", Number, other.Number);
        AddObjectChanges(nameof(Address), "收货地址", Address, other.Address);
        AddCollectionChanges(nameof(Lines), "订单明细", Lines, other.Lines);
    }
}

partial class OrderLine
{
    protected override void AddChanges(OrderLine other)
    {
        AddChange(nameof(Quantity), "数量", Quantity, other.Quantity);
        AddObjectChanges(nameof(Product), "产品", Product, other.Product);
    }
}
```

辅助方法公共概念参数为关系名称、描述、新旧值；重载或内部 typed delegate 可携带生成的身份选择器和容器形状。父级只引用子实体自身身份提供者，不复制子字段/Description/GetKeys 实现。辅助方法完整签名由 Executor 按现有类型约束落地并写入 execution.md，不引入 dynamic 或反射工厂。

调用链必须为：

```text
oldOrder.GetChanges(newOrder)
  -> Order.AddChanges
     -> 本实体 AddChange
     -> AddObjectChanges / AddCollectionChanges
        -> null / 身份 / 容器结构处理
        -> 已匹配子实体.GetChanges(对应新实体)
           -> 子实体手写或生成的 AddChanges
        -> 复制子描述符，添加当前关系路径，合入父结果
```

不再根据“纯标量/包含关系”选择两条判等路径。对子领域对象比较禁止使用整图 Capture/Compare 替代 GetChanges。普通结构值对象未实现 IChangeTrackable 时可继续使用静态生成结构访问；若结构内部包含领域对象，遍历到该边界仍须分派其 GetChanges。

### 4.2 会话、重入与结果生命周期

- 最外层 GetChanges 建立比较会话；同步嵌套复用会话，但每次调用拥有独立的结果 frame。
- 每个 frame 绑定旧/新对象引用对和自己的结果缓冲。用引用身份，不使用 Entity.Equals、GetHashCode 或领域键判定循环。
- 防环只检查当前递归祖先链中的同一新旧引用对。A→B→A 回边停止向下比较；从另一条兄弟路径再次到达共享对象时正常比较并保留路径。
- 不允许单独按“旧对象已访问”去重：同一旧对象对应不同新对象仍是不同配对。
- 正常返回和异常退出均通过 finally 恢复外层 frame；异常向调用方传播，不把部分结果作为成功返回。
- 返回集合脱离本次活动缓冲；后续 GetChanges、重入、父级合并不得清空或修改它。合并时新建描述符，不原地改写子结果路径。
- 同一领域对象在稳定只读数据上被不同线程独立比较时结果隔离。同步 hook 不应创建后台任务写入上下文；本任务不提供异步 AddChanges，也不承诺实体在比较过程中被并发修改的一致快照。
- 保留公开 GetChanges 和 AddChanges 签名。优先内部调用作用域管理；如采用 AsyncLocal，使用可恢复的作用域/frame，不让并发执行共享可变栈。禁止全局 static 结果集合、全局访问集合或实体锁递归替代会话隔离。
- Context 字段可以保留为访问门面，但 Add/Flush/Populate/GetChangedValueDescriptor 必须解析当前调用 frame，不能继续持有跨调用共享结果。复核现有手写调用方后调整，不触碰 DescriptionContext/Validation 功能。
- GetChanges 入口与关系辅助方法之间明确唯一的防环登记责任，避免双方重复登记而误判第一次进入就是循环。

### 4.3 路径、null 与结构事件

| 场景 | 要求 |
|---|---|
| 两侧都有相同身份的子对象 | 调用旧子对象 GetChanges(新子对象)，复制并前缀合并 |
| 无身份单对象，两侧存在 | 同类型按自身 hook 比较 |
| null → 对象 | 父关系记录新增事件，不要求在 null 上调用 GetChanges |
| 对象 → null | 父关系记录删除事件，不依赖当前 GetChanges(null) 的空结果 |
| null → null | 无变化 |
| 同一关系身份变化 | 删除旧身份并新增新身份，不把不同实体当作属性更新 |
| 不支持的运行时类型变化 | 明确类型不匹配异常；不能使用 ToString 或反射兜底 |
| 根 GetChanges(null) | 本次保持现有返回空集合行为；不把关系新增删除等同根 null 比较 |
| 直接获取 Address.City | 子结果 PropertyName 为 City；经 Order 合并为 Address.City |

路径使用现有结构化路径/键转义规则，按段组合。包含点、括号、引号的字典键不得直接用裸字符串拼接；前缀只能添加一次，不能修改子结果。字段没有 Description 时使用稳定名称回退，不能因空描述被 AddRange 静默丢弃。

新增/删除是父关系的结构变化，不要求伪造一个“空实体”执行子 hook。本期不新增“按手写 AddChanges 展开新增对象全部字段”的契约；若保留既有冻结快照展开，必须遵循该实体自动忽略规则，说明它与两侧匹配比较不同，并保留相关测试。

公开 ChangedValueDescriptor 仍是文本契约。本次不为 Kind/原始值擅自扩展公共字段；内部结构事件和既有 Graph API 的 Kind、类型、冻结值须保留并直接测试，GetChanges 的文本投影必须精确断言。不能把文本格式化结果反解析回来作为身份或判等依据。

### 4.4 原有 Graph 能力的边界

GeneratedChangeGraphModel/CaptureChanges 可继续服务身份元数据、结构值对象、冻结快照及既有 Graph API；不得在 GetChanges 的匹配领域对象分支绕开手写 hook。Capture/CompareSnapshot 本质是快照 API，不是执行任意两对象手写规则的替代物，文档必须区分。删除只为旧整图 hook 桥接服务且无调用方的代码，保留仍被独立快照功能使用的代码，不以本重构为由删除已有冻结快照验收。

## 5. DEC-03：实体自己定义身份

### 5.1 身份规则

| 顺序/容器 | 选择规则 | 生成要求 |
|---|---|---|
| 实现 IEntity（也实现 IKey 时同样适用） | ((IEntity)item).GetKeys() | 使用真实接口符号和接口分派，支持显式实现，禁止按方法同名猜测 |
| 未实现 IEntity，仅实现唯一有效 IKey<TKey> | ((IKey<TKey>)item).Id | 直接静态接口调用，支持显式实现；多种 IKey 无唯一选择时报定位诊断 |
| 有序 List/数组，无身份契约 | 按索引匹配 | 比较共同区间，并记录剩余新增/删除，不能只 Math.Min 截断 |
| 无序复杂对象 Set，无身份契约 | 编译期拒绝 | 直接和任意层嵌套均报 CG019，定位到拥有者成员 |
| Dictionary<TKey,TValue> | 容器键决定条目对应 | 匹配条目内的领域值仍执行对象身份与 GetChanges 规则 |

IEntity.GetKeys 是身份真源，不绕过自定义实现做 Id 快速通道。不扫描其返回值对应的属性名，不增加 ChangeKey，也不让父实体配置复合键组成。

键返回值按原顺序逐项比较、冻结数组内容；不能拼接字符串后比较，不能用 object[] 引用相等。复用现有 ChangeGraphKey 和键分量类型支持约束；实体键按类型值语义逐项比较，调用方全局 comparer 不得覆盖，容器 comparer 边界见 §3.4。空键数组、null 返回值、重复身份以及不支持的键分量按明确异常和路径处理，不捕获后退化为索引。不要擅自把 0 或 Guid.Empty 一律判非法；沿用已记录的领域/图键有效性规则，新增差异需单独说明。

ChangeIgnore 控制成员内容采集，不重定义 IEntity 身份；已匹配非忽略关系可调用其 GetKeys。被忽略的整个关系必须在 getter、枚举和 GetKeys 之前短路。不得根据 GetKeys 方法体推断或裁剪复合身份分量。文档说明身份读取与字段显示是不同职责，避免承诺“标记某键属性后连身份契约也不会读取”。

### 5.2 单键与复合键示例

```csharp
// 单键实体：已有基类 GetKeys 返回 { Id }，不需要增加特性或重写。
public partial class OrderLine : EntityBase<OrderLine, Guid>
{
    public decimal Quantity { get; set; }
}

// 复合身份：不继承以 Id 为唯一相等性依据的泛型 EntityBase。
public partial class ProductPrice : DomainObjectBase<ProductPrice>, IEntity
{
    public Guid ProductId { get; set; }
    public string Currency { get; set; }
    public decimal Amount { get; set; }

    public object[] GetKeys() => new object[] { ProductId, Currency };
    public void Init() { }
}
```

| 输入变化 | 预期 |
|---|---|
| 同 ProductId、Currency，Amount 改变 | 同一元素，调用 ProductPrice.GetChanges，报告 Amount |
| 同商品不同 Currency | 旧身份删除、新身份新增 |
| 实体列表换序但身份未变 | 仍按身份匹配，不伪报字段变化；移动报告保持现有显式选项语义 |
| 同一侧重复 GetKeys 值 | 明确失败，不取第一条或覆盖前项 |
| 同时实现 IEntity、IKey 且值刻意不一致 | 仅 GetKeys 决定跟踪匹配，测试用于防止旧优先级回归 |

不得通过 new GetKeys 隐藏 EntityBase 方法作为复合键示例。泛型 EntityBase 的 Equals/GetHashCode 仍围绕 Id，本任务不修改这套全局规则。复合领域对象如还需业务 Equals，应另按既有领域规范实现，不由跟踪生成器偷偷生成。

## 6. 多场景使用与验收

### 6.1 手写子对象规则在父级生效

```csharp
public partial class Address : DomainObjectBase<Address>
{
    public string City { get; set; }
    public string Street { get; set; }

    protected override void AddChanges(Address other)
    {
        AddChange(nameof(City), "城市", City, other.City);
    }
}
```

City、Street 同时变更：直接 Address.GetChanges 只输出 City；放入 Order.Address 后只输出 Address.City，Street 不可由自动模型补回。将同一 Address 放入 BillingAddress 和 ShippingAddress，两条路径均输出对应 City 变化。两侧未变不输出。

### 6.2 手写审计字段与嵌套集合

手写 AuditEntry.AddChanges 显式加入 Action、CreationTime：直接比较、作为对象属性、作为有序集合元素和字典值比较都必须出现同样字段。自动生成的其他实体仍默认忽略其审计时间，不能因为某个子实体手写而改变全局预设。

### 6.3 树与自关联

Category.Parent、Category.Children 可同时存在，无需 ChangeNode。A.Parent=A、A→B→A、父子双向引用均终止；共享节点仍按不同有效路径汇总。ChangeIgnore 标记 Parent 时，该 getter 零访问，而 Children 正常比较。测试通过引用配对计数和精确结果证明，不以“未超时”作为唯一断言。

### 6.4 集合、字典、交错容器

覆盖 List<OrderLine>、OrderLine[]、HashSet<ProductPrice>、Dictionary<string,Address>、List<List<Address>>、Dictionary<string,List<OrderLine>>。匹配元素调用各自 GetChanges；新增、删除、键变化单独断言。List<HashSet<UnkeyedDomainObject>> 和 Dictionary<string,HashSet<UnkeyedDomainObject>> 与直接 Set 一样产生 CG019。

### 6.5 描述、忽略与继承

Description 文本、Display ResourceType、中文/引号/换行由同一静态解析器服务所有生成路径。不支持的派生描述特性必须诊断，不静默取属性名。无 Description 的字段也可输出。父关系忽略、子字段忽略、继承成员、显式接口身份、跨程序集派生、泛型闭包均保留回归。

## 7. 实施顺序与任务

所有任务执行前先核对 review finding 是否仍成立，先写能失败的必要回归，再改实现。新结果记录在 execution.md，不修改 review.md 以关闭问题。下列 CT-A0～A5 延续原需求编号，子任务 R 编号表示本次整改。

### Phase 0 / CT-A0-R1：基线和契约回归

- 目标：冻结本修订的调用、身份与删除契约，保留已有有效工作。
- 现状/证据：review.md；本文件 §2；旧 task 缺 execution.md。
- 修改范围：当前 task execution.md（新增）；Analyzer/Domain 测试；五个特性的活动源码与文档扫描。
- 步骤：记录工作区已有改动；把 DEC-01～04 全部标为已确认待实现；引入手写子 hook、IEntity/IKey 冲突优先级、大小写/空格/null 和已删覆盖 API 不可用的回归；复现 FIX-001～004；旧测试按失效原因迁移，不恢复已删特性。
- 依赖：无。
- 验证：精准新增测试先失败，记录失败原因与对应符号；活动源码无已删除特性定义/有效使用。
- 风险：工作区已有大量其他修改，不得归因或回退；历史报告中的旧名称不应删除。
- 验收：形成可执行回归与需求映射；不因发现代码存在标完成。

### Phase 1 / CT-A1-R1：正确的生成入口和签名（FIX-003、004）

- 目标：只识别真实领域基类；正确保留手写 hook。
- 现状/证据：生成器 IsDomainObjectBase 名称回退；new AddChanges 独立探针 CS0111。
- 修改范围（已确认）：ChangeTrackableSourceGenerator.cs、DomainObjectBaseSyntaxReceiver.cs、EntryPoint.cs 及生成诊断测试。
- 步骤：符号匹配；按真实 hook 签名判断 override/冲突；独立检查 CaptureChanges 冲突；为继承/跨程序集/泛型/外部同名类型补回归。
- 依赖：CT-A0-R1。
- 验证：Analyzer 全套或先定位测试；错误位置在源声明，正常生成源码可编译。
- 风险：不能把无关 AddChanges overload 误判为手写 hook；不能删除合法泛型闭包诊断。
- 验收：FIX-003/004 反例通过；无重复成员；手写实现保持原样。

### Phase 2 / CT-A2-R1：比较会话及结果隔离

- 目标：为同步组合 GetChanges 提供可靠调用 frame 和防环。
- 现状/证据：DomainObjectBase.GetChanges 清缓存、返回实体共享集合；Context.Add 已有简单递归。
- 修改范围（已确认）：DomainObjectBase.cs、ChangeTrackingContext.cs、ChangedValueDescriptorCollection.cs；候选新增：内部比较会话/frame 文件（名称由 Executor 决定）。
- 步骤：先实现调用级缓冲和作用域；所有 Context 结果操作指向当前 frame；引用对祖先链防环；finally 清理；复制返回结果；类型错误继续显式失败。
- 依赖：CT-A0-R1；可与 CT-A1-R1 在不同文件分工，但集成后验证。
- 验证：连续两次调用不改变第一次结果；同实例并发独立比较；子 hook 抛异常后下一次正常；重入不清外层；同一旧对象不同新对象不被误去重。
- 风险：AsyncLocal 的共享可变引用、重复进入判环、实体 Equals 被错误用于防环；禁止用大范围锁掩盖问题。
- 验收：§4.2 每项都有运行断言；DescriptionContext 与 Validation 不受影响。

### Phase 2B / CT-A2-R3：统一标量比较和文本输出（DEC-01）

- 目标：按类型值判断变化，展示保留源值内容，直接/嵌套不因模型形状变化而改变结果。
- 现状/证据：ChangeTrackingContext.Add 的 ToLower/Trim 文本比较；review DEC-01 独立探针。
- 修改范围（已确认）：ChangeTrackingContext.cs、标量格式化 helper、生成器标量发射及 Domain/Analyzer 测试；ChangeGraphLegacyExtensions 仅在统一输出所需范围调整。
- 步骤：先补 §3.3 反例；移除归一化和第二次文本判等；类型值相等和格式化分离；各 Add 路径复用同语义；迁移旧规范化断言并记录契约变更。
- 依赖：CT-A0-R1、CT-A2-R1；在逐成员生成链路最终集成前完成。
- 验证：大小写、空格、null/空、decimal 等值、Nullable、日期/枚举、ToString 相同但值不同；手写特殊比较正反例；不同 Culture 输出稳定；直接/父级结果一致。
- 风险：展示格式掩盖真实变化，复杂对象误走标量入口，误将精确比较当作位级比较。
- 验收：§3.3 完整文本和数量断言通过；无自动 ToLower/Trim 判等；新增关系属性不改变旧标量比较结果。

### Phase 3 / CT-A3-R1：统一身份与容器匹配（DEC-03、FIX-002）

- 目标：实体身份归实体所有，嵌套容器一致校验。
- 现状/证据：Models.ResolveKeySpec 当前 IKey 优先；EnsureShapeModel 内层 Set 漏 CG019。
- 修改范围（已确认）：ChangeTrackableSourceGenerator.Models.cs、身份选择器发射、Graph 身份匹配/路径 helper、关系集合 helper 和对应测试。
- 步骤：IEntity 真实接口优先；冻结复合键；统一直接/递归 Set 检查并传递源位置；实现匹配、新增、删除、字典键及无键序列规则；同步保留模型消费者的身份优先级，避免两套 selector。
- 依赖：CT-A1-R1、CT-A2-R1。
- 验证：§5 全部场景；显式 GetKeys；复合键顺序/类型；重复身份；嵌套无键 Set 负例；有键 Set 正例。
- 风险：同名 new 方法与真实接口映射不同；不得字符串化键，不得将身份错误静默回退到索引。
- 验收：生成身份选择器与 GetChanges 关系 helper 一致；不修改 EntityBase 全局 Equals/GetHashCode；CG019 在直接/嵌套同等生效。

### Phase 3B / CT-A3-R2：删除身份/路径覆盖 API（DEC-04）

- 目标：子实体掌握自身规则，删除未发布功能的兼容覆盖入口。
- 现状/证据：ChangeGraphOptions.WithIdentity/WithCollectionKey、ChangeGraphModel.ignorePaths；全局 comparer 可能覆盖身份分量。
- 修改范围（已确认）：ChangeGraphOptions.cs、ChangeGraphModel.cs、覆盖规则消费者、模型构造调用点、测试/README/smoke；相关 Graph 捕获/匹配分支按真实引用定位。
- 步骤：列 API→存储→执行→调用方清单；删除全部身份覆盖重载、路径配置及专用状态；更新构造发射；检查 comparer/手工 selector 绕过路径；保留过程选项、实体 selector、合法容器语义和第三方访问边界；同步活动示例，不改历史报告。
- 依赖：CT-A3-R1；共享文件修改顺序执行。
- 验证：编译负例证实旧 API 不可用；生成/手工模型消费者重新编译；移动、深度限制、快照正例；字典 comparer 兼容成功、不兼容或未知明确失败；父级 comparer 不影响实体字段/身份。
- 风险：只删名称却保留等效覆盖、误删路径格式化、错误猜测 comparer 等价、破坏快照。
- 验收：§3.4 每项有处理位置和证据；无兼容别名/占位；保留能力有运行证据，不能只靠名称扫描宣布完成。

### Phase 4 / CT-A2-R2：生成逐成员 hook 并接通子 GetChanges（DEC-02）

- 目标：直接与嵌套遵守同一实体 hook。
- 现状/证据：EntryPoint.GenerateGraphAddChanges 整体 Tracker 桥接绕过子手写规则。
- 修改范围（已确认）：EntryPoint.cs、生成器成员发射、DomainObjectBase 关系辅助方法、Context 合并和路径逻辑；Graph 模型仅调整必要边界。
- 步骤：生成 §4.1 形态；实现 AddObjectChanges/AddCollectionChanges 的静态类型调用；匹配领域对象一律 GetChanges；null/身份替换由父级结构处理；复制和路径前缀合并；保留结构值对象/快照能力；扫描并删除无调用方旧桥接。
- 依赖：CT-A2-R1、CT-A2-R3、CT-A3-R1、CT-A3-R2。
- 验证：§6 手写 Address/AuditEntry、多层集合/字典、共享路径/树、自循环；精确断言文本集合，不只 Contains；生成代码显示本实体标量 AddChange 与关系调用。
- 风险：手写遗漏字段被模型补回、子结果前缀重复、结构增删遗漏、类型切换字符串兜底。
- 验收：两侧匹配子对象执行手写 hook 的计数和结果均正确；自动路径只用静态访问，关系忽略零 getter/枚举/key；DEC-01 类型值语义在所有入口一致，DEC-04 无跨实体覆盖入口。

### Phase 5 / CT-A4-R1：统一描述与实际消费者（FIX-001）

- 目标：所有生成路径使用同一可静态求值的描述解析，验证 AOT 与可查看产物。
- 现状/证据：纯标量用完整解析器，复杂模型 GetDescription 只取字面量；旧 native 缺 RID 资产。
- 修改范围（已确认）：生成器描述 helper、Models 发射、Analyzer 描述测试、Domain.Aot.Fixtures、Domain.Aot.Smoke。
- 步骤：统一描述解析/诊断；生成 nameof 与正确转义文本/静态资源；真实 consumer 同时包含手写/自动子 hook、单/复合身份、树、忽略；调整 smoke 固定 GraphSmoke 提示为实际生成目录；先 JIT 后 native。
- 依赖：CT-A2-R2、CT-A3-R1。
- 验证：Description/Display 资源/派生特性反例；本文件 §9 命令；确认生成文件不是旧残留，native 运行带 --require-aot。
- 风险：把动态代码可用的 JIT smoke 当 AOT，旧包/旧 exe 当新证据；禁止全局屏蔽 trimming/AOT 警告。
- 验收：FIX-001 所有生成形态生效；明确区分托管、原生、包消费者状态；缺工具链/RID 资产时只标阻塞，不假 PASS。

### Phase 6 / CT-A5-R1：文档、包消费及证据收口（FIX-005）

- 目标：使下一轮 Review 能从需求追到生产符号和运行证据。
- 现状/证据：旧 README 展示 Tracker 为主入口、IKey 优先，执行报告缺失。
- 修改范围（已确认）：Domain README、AOT smoke README、execution.md、相关当前使用指南；候选：独立本地包消费者 fixture（先复用现有脚本）。
- 步骤：文档以 GetChanges 为领域比较入口；展示自动、手写、集合、树、复合身份；解释快照/文本边界；补执行矩阵与诊断场景迁移理由；使用当前 runtime/analyzer 本地包测试消费者，禁止发公共 NuGet；记录 DEC-01～04 的真实实现/验收状态及已删除 API 的调用方更新结果。
- 依赖：前述实施任务完成；外部 native gate 可独立保留阻塞。
- 验证：文档中活动示例编译/执行；包消费者确实加载本轮包内 analyzer，不能仅用项目引用代替；输出文件和实际命令对应。
- 风险：仅改状态而不补证据，删除历史报告旧名称，因外部证据缺失反复自动 Fix。
- 验收：每项有实现状态和验收状态；未运行的 native/包验证不关闭；review.md 保持独立证据。

依赖顺序汇总：CT-A0-R1 → CT-A1-R1 / CT-A2-R1 → CT-A2-R3 / CT-A3-R1 → CT-A3-R2 → CT-A2-R2 → CT-A4-R1 → CT-A5-R1。斜线表示可独立开展；共享生产文件按顺序修改，不要求并行。

P3 单列：OPT-001 术语与目录提示可在 CT-A4/5 顺手纠正；没有正式性能阈值，不发起大规模 Benchmark。若测量新分配/递归热路径，仅做相关代表性工作负载并报告实测，不宣称未经验证的提升。

## 8. 需求—验收追踪矩阵（本修订待执行）

| 需求 | 来源 | 实施任务/关键符号 | 必须取得的证据 | 当前状态 |
|---|---|---|---|---|
| R01 无标记且无重复 hook | CT-A1、FIX-003/004 | CT-A1-R1 / IsDomainObjectBase、hook 签名判断 | 真实/同名基类、new/override/overload、跨程序集生成测试 | 部分实现，待整改验证 |
| R02 手写规则嵌套生效 | DEC-02 | CT-A2-R2 / AddObjectChanges、GetChanges | Address 直接/父级/集合/字典结果一致 | 未满足，待实现 |
| R03 结果和会话隔离 | DEC-02 必要条件 | CT-A2-R1 / Context、frame | 连续、并发、重入、异常清理 | 未满足，待实现 |
| R04 树、循环、共享路径 | CT-A2、DEC-02 | CT-A2-R1/R2 / 引用配对祖先链 | A→B→A 终止、多路径保留、不同新对象独立 | 原图有覆盖，新调用链待验证 |
| R05 IEntity 身份优先 | DEC-03 | CT-A3-R1 / ResolveKeySpec、GetKeys selector | 单键、复合键、双接口冲突、显式接口 | 当前优先级偏离，待实现 |
| R06 完整容器差异 | CT-A3 | CT-A3-R1、CT-A2-R2 / 集合 helper | 新增删除、身份替换、无键索引、字典、嵌套 Set | 图引擎部分已有，新入口待验证 |
| R07 无键 Set 递归诊断 | FIX-002 | CT-A3-R1 / EnsureShapeModel | 直接/嵌套 CG019 位置、有键正例 | 部分实现，待整改验证 |
| R08 描述静态化 | CT-A4、FIX-001 | CT-A4-R1 / 描述解析器 | 资源值、派生诊断、中文转义、无描述回退 | 部分实现，待整改验证 |
| R09 自治忽略与审计 | CT-A0/A3 | CT-A2-R2、CT-A3-R1 | 忽略关系零访问；手写审计在嵌套生效 | 原有部分证据，新入口待验证 |
| R10 产物/AOT/包 | CT-A4/A5 | CT-A4-R1、CT-A5-R1 | 当前生成源码、JIT、native、独立包消费 | 托管旧证据；新候选待验证 |
| R11 回归及执行记录 | FIX-005 | CT-A0-R1、CT-A5-R1 | 符号→测试方法映射、命令、退出码、阻塞项 | 未闭环 |
| R12 类型值比较与原值展示 | DEC-01 | CT-A2-R3 / 标量 Add、格式化 | 大小写/空格/null/等值数值、文化、模型形状一致性 | 已确认，旧实现偏离，待验证 |
| R13 删除跨实体覆盖 | DEC-04 | CT-A3-R2 / Options、Model、调用方 | 旧 API 编译负例；无等效绕过；容器/快照/过程选项正例 | 已确认，待实施验收 |

Executor 增补实际文件行号、测试项目/方法名、证据路径；每项分别填“已修复且验证/已修复待验证/未修复/阻塞/不成立”。测试通过不自动等于该需求全部通过，写清已覆盖边界。以上不与旧审计阶段数或旧需求数混加。

## 9. 验证命令与执行边界

实施前记录 Change Impact Analysis：Domain/Analyzer/Consumer；公共 GetChanges、AddChanges 签名不变，新增 protected 关系辅助 API；删除身份/路径覆盖 API；标量判等与展示、运行路径和身份优先级改变；net6/net8 托管、net10/win-x64 native；风险 HIGH。本次文档修订只有 Docs/UTF-8 检查，不跑代码测试。

从仓库根执行；首先跑新增精准测试，然后运行以下已有项目。配置采用已存在的 GraphSmoke，输出另设目录以区分旧候选。不得以筛选结果 0 tests 作为通过。

```powershell
dotnet test framework/tests/Bing.Ddd.Domain.Extensions.Analyzers.Tests/Bing.Ddd.Domain.Extensions.Analyzers.Tests.csproj -f net8.0 --no-restore
dotnet test framework/tests/Bing.Ddd.Domain.Tests/Bing.Ddd.Domain.Tests.csproj -f net8.0 --no-restore --filter 'FullyQualifiedName~ChangeTracking|FullyQualifiedName~DomainObjectBaseTest'
dotnet test framework/tests/Bing.Ddd.Domain.Tests/Bing.Ddd.Domain.Tests.csproj -f net6.0 --no-restore --filter 'FullyQualifiedName~ChangeTracking|FullyQualifiedName~DomainObjectBaseTest'
dotnet run --project framework/tests/Bing.Ddd.Domain.Aot.Smoke/Bing.Ddd.Domain.Aot.Smoke.csproj -c GraphSmoke --no-restore -p:DomainSmokeAot=false
```

新增测试类必须被筛选涵盖；本次修改基类共享上下文，阶段收口再运行 Domain 测试项目的 net6/net8 全套，不扩展到 SQL/外部数据库/全 Solution。

```powershell
dotnet test framework/tests/Bing.Ddd.Domain.Tests/Bing.Ddd.Domain.Tests.csproj -f net8.0 --no-restore
dotnet test framework/tests/Bing.Ddd.Domain.Tests/Bing.Ddd.Domain.Tests.csproj -f net6.0 --no-restore
dotnet publish framework/tests/Bing.Ddd.Domain.Aot.Smoke/Bing.Ddd.Domain.Aot.Smoke.csproj -c GraphSmoke -r win-x64 --self-contained true -p:DomainSmokeAot=true --no-restore -o artifacts/change-tracking-autonomous-r3/native
& './artifacts/change-tracking-autonomous-r3/native/Bing.Ddd.Domain.Aot.Smoke.exe' --require-aot
```

只有当前 publish 退出 0 才执行产物，防止复用旧 exe。保留日志、退出码、DynamicCode 标志、工具链/TFM/RID、产物标识。当前已知缺 RID 资产；若执行环境允许恢复现有依赖，可先执行 `dotnet restore framework/tests/Bing.Ddd.Domain.Aot.Smoke/Bing.Ddd.Domain.Aot.Smoke.csproj -r win-x64 -p:Configuration=GraphSmoke -p:DomainSmokeAot=true`。此命令不是安装 SDK/本机工具链的授权；不允许恢复依赖或缺工具链时标 BLOCKED_EXTERNAL，继续其他任务。

生成产物目录按 csproj 实际 `Generated/$(Configuration)/$(TargetFramework)` 查阅，不能一概写成 obj；源码归源生成器管理，禁止编辑 .g.cs。独立包验证先复用 AOT task 的既有流程，核对真实 pack/consumer 配置后将完整命令写入 execution.md，不编造一个不存在的 consumer 项目命令。

## 10. 完成条件、停止规则和交接

### 10.1 Definition of Done

1. 本文件 R01～R13 的实现与验证状态逐项可追踪，DEC-01～04 均有本轮候选的真实验收证据。
2. 对领域对象差异只认 GetChanges→AddChanges 主链，不再由整体模型比较替换子实体手写 hook。
3. 有键集合优先 IEntity.GetKeys，其次仅 IKey；复合实体示例可编译且不隐藏 EntityBase.GetKeys。
4. 会话隔离、循环、共享路径、异常恢复、增删、字典、嵌套集合通过运行测试；类型值判等、原值展示和手写特殊比较在直接/嵌套中一致。
5. 活动源码/示例/使用指南不存在已删特性、身份覆盖和路径覆盖作为有效 API；无等效绕过；保留过程选项/容器语义有测试；历史计划、审计和原始证据保留。
6. 标准生成路径无反射/动态编译回退；Description 和 Display 资源处理一致；生成文件可查看。
7. NativeAOT、包消费者、托管测试分别判断；缺真实执行证据的门禁仍为待验证或阻塞。
8. 不自动修改版本号，不提交 Git，不删除/降低现有断言使测试通过，不改无关业务。

### 10.2 状态与停止规则

- Completed（本次规划）：写入已确认 DEC-01～04、清除旧方案矛盾、建立 13 条需求与任务/验收矩阵。
- Open Actionable（交给实施者）：CT-A0-R1～CT-A5-R1 中的实施任务及 FIX-001～005，先验证根因再处理。
- Blocked Approval：DEC-01～04 无剩余待确认项；真正超出计划的新行为或外部操作授权另列，不重复询问这四项。
- Blocked External：当前 native RID 资产/可能的本机工具链；按实际环境复核。
- Not Applicable：数据库、UI、外部系统验收。
- Accepted Limitations：同步 hook，不保证比较过程中业务对象并发修改的一致性；不自动展开手写 hook 的单侧新增对象字段。
- Verified Boundaries：本次没有性能/容量运行结论。
- Deferred：大规模性能/资源门禁、其他 RID，须另有任务和支持范围。

读取用户指定的 GOAL_RULES.md。只有 OPEN_ACTIONABLE > 0 才继续修复；外部阻塞、缺性能阈值不触发无限 Fix。独立 review.md 不由 Executor 改写，状态汇总写 execution.md；最终区分 IMPLEMENTATION_STATUS、TEST_STATUS、NATIVE_AOT_STATUS、PACKAGE_ACCEPTANCE_STATUS、RELEASE_STATUS。

### 10.3 可直接交给 OpenCode 的指令

```text
请在 H:/Bing_Framework/Bing.NetCore 继续实施 ChangeTrackable 整改。

先读取 AGENTS.md、用户级 GOAL_RULES.md（优先 $CODEX_HOME/GOAL_RULES.md，否则 ~/.codex/GOAL_RULES.md），以及：
ai_docs/tasks/20261104-domain-change-tracking-autonomous-contract/plan.md（修订 3）
同目录 review.md、review-evidence 和已有 execution.md（若存在）。

这是实施任务，不是重新规划。DEC-01～04 均已由用户确认，无需重复确认：
1. 按类型实际值判等后再格式化；字符串区分大小写、不自动 Trim，保留原值内容；特殊比较由实体手写 AddChanges。
2. 父级通过子对象 GetChanges 汇总，手写 AddChanges 在嵌套对象、集合和字典中生效；实现调用级结果隔离、引用对防环、共享路径和异常清理。
3. IEntity.GetKeys 优先，其次仅 IKey.Id；复合身份不使用 new GetKeys 隐藏 EntityBase 方法。
4. 删除 WithIdentity/WithCollectionKey、手工 ignorePaths 和兼容覆盖入口，禁止等效绕过；保留合法过程选项、容器固有语义、实体自身配置及必要快照能力。

以最新计划为基线，review.md 是旧候选证据，不按其中已被新计划替代的建议实施。先核对 FIX-001～005，再按 §7 依赖顺序完成全部可执行任务，不只修探针。先补必要失败测试再根因修复；不删测试或降断言。旧契约变化的测试应替换为新契约精确断言并记录原因。

仅修改跟踪功能及必要调用链、测试、当前使用文档。保留无关变更和 review 原始证据，不恢复已删特性，不新增反射兜底，不直接编辑 .g.cs。遵守 UTF-8/PowerShell 规则。不自动 git add/commit/push/merge，不改版本、不发布包。

按计划执行精准测试、受影响项目、JIT consumer，再在条件具备时进行当前产物 NativeAOT publish/run 和本地独立包消费。不能以 JIT、旧 exe、旧测试或仅编译成功充当新功能验收。缺工具链或资产时记录真实阻塞并继续其他工作。

维护 execution.md：逐条关联 R01～R13、FIX-001～005，记录源码位置、修改、测试方法、命令/退出码、证据路径，标记已修复且验证/已修复待验证/未修复/阻塞/不成立。review.md 和旧 review-evidence 保持不变。最终给出矩阵、生成代码示例、已通过与待验证项目，分别报告实现、测试、NativeAOT、包验收和发布状态。

只有 OPEN_ACTIONABLE > 0 才继续修复；不因外部阻塞无限重跑。完成可执行工作后停止并交付实际结果。
```
