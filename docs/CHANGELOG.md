# 文档体系变更记录

> **适用版本**：框架 `7.0.0`（`version.props`）｜ 目标框架：类库 `netstandard2.0`、Web / 应用 `net6.0`｜ 内容核对：2026-09-18｜ 版本历史见 [发行说明](ReleaseNotes.md)

本文记录的是 **`docs/` 文档体系自身**的演进——新增/重构了哪些文档、修正了哪些事实错误、建立了哪些约定。

**它不是框架的发行说明**。框架的功能与破坏性变更请看 [发行说明](ReleaseNotes.md)；跨版本升级步骤看 [迁移指南](migrations/README.md)。

---

## 1. 维护约定（改文档前先读）

| 约定 | 说明 | 谁在自动检查 |
| --- | --- | --- |
| **`docs/index.md` 是派生产物** | 它由根 `README.md` 自动生成，**禁止手工编辑**。改完根 README 后运行 `python eng/ci/sync-docs-index.py` | `eng/ci/check-docs.py` + `.github/workflows/docs-lint.yml` |
| **新增包必须同时做三件事** | ① 写包级 `README.md`（含 NuGet 徽章）；② 把包加进根 README 的包表；③ 若属于核心能力，补进《架构文档》§11/§12 | 同上（包表覆盖 67/67、徽章覆盖 67/67 是硬门槛） |
| **新增文档要挂三处** | ① `docs/toc.yml`（docfx 站点导航）；② `docs/README.md`（人工导航 hub）；③ 若是核心文档，在四份核心文档头部「配套文档」里互相引用 | `check-docs.py` 校验 toc 与 hub 的链接可达 |
| **HTML 不能进 toc** | `docfx.json` 的 `content.files` 只收 `**/*.{md,yml}`，`选型向导.html`、`cheatsheet.html` 这类工具页放进 `toc.yml` 会产生"找不到内容"告警 | 人工约定 |
| **文档必须放进主题子目录** | `docs/` 顶层只允许 5 个入口类文件（`README.md` / `README.en.md` / `index.md` / `CHANGELOG.md` / `ReleaseNotes.md`）。新增文档一律放进 `getting-started` / `architecture` / `guides` / `operations` / `sql` / `testing` 等既有子目录 | `check-docs.py` 的「顶层归位」检查 |
| **`docs/api/` 是 docfx 的生成目录** | `docfx.json` 的 `metadata.dest` 指向它，`docs/api/*.yml` 是生成物（已在 `.gitignore` 里）。**不要往里写手工内容**——`index.md` 是 docfx 的落地页存根，手工的 API 索引在 `getting-started/API速查索引.md` | 人工约定 + `.gitignore` |
| **文档必须标适用版本** | 每份文档 H1 下方有一行「适用版本」，含框架版本、目标框架、核对日期 | 人工约定 |
| **包级 README 的文档链接必须写绝对 URL** | 相对链接在 nuget.org 包页面上会 404 | 人工约定 |
| **改了公共 API / 注册入口要回头改文档** | 映射表见 [CONTRIBUTING.md 文档维护表](../CONTRIBUTING.md) | 人工约定 |

**一键自检**：

```bash
python eng/ci/check-docs.py                # 12 项一致性检查
python eng/ci/sync-docs-index.py --check   # 只查 index.md 是否与 README 同步
```

---

## 2. 记录

### 2026-09-19 — 第十八轮：新建「本地事件与消息事件」对称手册，修正两处既有事件文档的错误

**背景**：事件相关已有两篇——[事件与消息文档](architecture/事件与消息文档.md)（四套机制全景，605 行）与[分布式事件使用说明](guides/分布式事件使用说明.md)（只讲 CAP，713 行）。但**本地事件（`ISimpleEventBus`）没有对称的写法说明**，且现有文档里没有一张「本地 vs 消息」的逐维度对照表（三张既有表口径各不相同）。用户要求「明确说明两者的定义、区别与各自适用场景，涵盖典型使用方式、关键参数、完整示例、常见问题」。

**一、新建 `docs/guides/本地事件与消息事件使用说明.md`（10 节）**

| 节 | 内容 |
| --- | --- |
| §1 先记 6 条 | `ISimpleEventBus` 自身无成员（只是 `IEventBus` 的别名式契约）；`PublishAsync` **无 `CancellationToken`**；`HandleAsync` **无 `CancellationToken`**；`MessageEvent.Send` **默认 `false`**；本地事件处理器**必须手工注册**；两套不继承、靠 CAP 版 `IEventBus` 组合 |
| §2 定义与区别 | 一张**逐维度对照表**（契约 / 传递范围 / 持久化 / 可靠投递 / 事务参与 / 延迟发布 / 处理器发现 / 异常隔离 / 跨进程 / 适用与不适用场景）+ 契约继承关系图 |
| §3 本地事件 | 五步上手（装包 → `AddDefaultEventBus()` → 定义事件 → 写处理器 → 注册处理器并发布）；完整可编译示例（**本手册构造**）；处理器解析机制与 `ServiceLocator.Instance` 前置条件 |
| §4 消息事件 | 五步上手摘要；`Send` 两种语义；发布两个重载；订阅三件套；**深挖部分反链**到分布式手册，不重复 |
| §5 两级发布 | `Bing.Events.Cap.EventBus` 的组合语义——一次 `PublishAsync` 先走本地、再按需走 CAP |
| §6 选型 | 场景决策表（该用哪套 / 为什么） |
| §7 陷阱 | 按「静默失败」「事务」「性能」分组 |
| §8 防误用清单 | 不存在的方法与类型（负面清单） |
| §9 相关文档 | 三方互链矩阵 |

**二、核准出的关键结论（含两处对我此前口径的修正）**

- ⚠ **`MessageEvent.Send` 默认值是 `false`**（`MessageEvent.cs:28`，无初始化器）。我此前在别处写过"默认 `true`"——那是**错的**，`UserLoginMessageEvent.cs:18` 的 `Send = true` 只是该子类构造参数的默认值。
- ⚠ **`ISimpleEventBus` 全仓零生产调用**：`modules/admin/src` 与 `samples` 搜 `ISimpleEventBus` = 0 命中；唯一触及本地总线的发布在 `framework/tests/Bing.Tests/Events/EventBusTest.cs:37`，**且被注释**。
- ⚠ **全仓找不到任何真实 `IEventHandler<T>` 实现类**（生产与测试均无，测试里唯一引用是 NSubstitute 替身）。故 §3 的示例由本手册构造，并在醒目位置声明「仓库内无先例」。
- **本地事件是异步但串行、无 try/catch、不参与事务、不支持延迟发布**：`Default/EventBus.cs:29-40` 是 `foreach` + `await`，某处理器抛异常会**中断后续处理器并把异常抛给调用方**。
- **`EventHandlerManager` 用全局 `ServiceLocator.Instance`**（不是注入 `IServiceProvider`），且 `GetServices<T>()` **没有** `IsProviderEnabled` 保护——未装 `AddBing()` 直接发布会抛。
- **`AddCapEventBus` 顺带注册 `ISimpleEventBus`**（`Cap/Extensions.Service.cs`），所以装了 CAP 的用户其实同时拿到了本地总线。
- **生命周期不同**：`ISimpleEventBus` 是 **Singleton**，`IMessageEventBus` / `IEventBus` 是 **Scoped**。
- **`[EventHandler]` 只接一个 `name` 参数**，`Group` 来自它继承的 CAP `TopicAttribute`。

**三、修正既有文档 2 处错误**

| 位置 | 原来 | 修正后 |
| --- | --- | --- |
| [使用文档 §8.3](getting-started/使用文档.md) | 声称事件定义 `IEvent` / `IDistributedEvent`、处理器实现 `ILocalEventHandler<TEvent>`、**按约定式 DI 自动注册**、领域事件**由分发器在提交时派发** | 四项全错或属遗留包口径：`IDistributedEvent` / `ILocalEventHandler` 属遗留 `Bing.EventBus*`；本地事件处理器**必须手工注册**；领域事件**全仓无人派发** |
| [recipes/cap-event-outbox.md](guides/recipes/cap-event-outbox.md) §4 | 把**普通 DTO** 传给 `IMessageEventBus.PublishAsync` | 泛型约束 `where TEvent : IMessageEvent` **不成立**，照抄编译不过；已改为继承 `MessageEvent` 的事件类 |

**四、同步更新**：`docs/README.md` 角色表 +1、专项表 +1；`toc.yml` guides 组 +1（48 项）；`CONTRIBUTING.md` 文档维护表补指向新文档。

---

### 2026-09-18 — 第十七轮：新建分布式事件使用手册，把 Outbox 的三个前提讲透

**背景**：[事件与消息文档](architecture/事件与消息文档.md) 是**全景图**（四套机制各占一节），分布式这一套只占其中约 170 行且偏"对照与选型"，读者看完仍不知道"第一条消息怎么发出去、怎么收"。故补一篇只在分布式这一套上做深的操作手册。

**一、新建 `docs/guides/分布式事件使用说明.md`（11 节）**

| 节 | 内容 |
| --- | --- |
| §0 先记 8 条 | 只有一个分布式入口；`send` 的两种语义；⛔ `send=false` 事务外静默丢；`[EventHandler]` 而非 `[CapSubscribe]`；`bing-trace-id`；`IEventBus` 是两级发布合并入口 |
| §1 五分钟上手 | 装包 → 只有本地事件 / 要分布式两套注册 → 定义 `IMessageEvent` → 发布 → 订阅 |
| §2 核心抽象 | 类型总表、继承链、三个总线接口的分工 |
| §3 发布 | 两个重载、`send` 参数语义、两级发布、本地 vs 分布式怎么选 |
| §4 Outbox | 链路图 + 源码依据表、**三个前提**、事务内顺序、`send=true` 无保护 |
| §5 订阅 | 处理器三件套、`[EventHandler]` 与 `[CapSubscribe]` 的关系、允许的签名形态、发现与注册、分组与并发 |
| §6–§7 | `bing-trace-id` 贯通（发布端自动 / 消费端两种）、CAP 配置实例 |
| §8–§11 | 注册入口全表、admin 登录消息完整示例、陷阱与防误用清单、自行复核命令 |

**二、核准出的关键结论**

- **登记动作忽略传入的事务**：`MessageEventBus.cs:73-76` 的 `Register(async transaction => ...)` 里，`transaction` 参数**从未被使用**；是否写发件箱取决于 **CAP 自己的环境事务**（`ICapPublisher.Transaction`）是否已开启。所以"基类工作单元把事务对象递进去"**不等于**接上了 Outbox——这解释了为什么 admin 必须用 `Database.BeginTransaction(publisher, autoCommit: false)`。
- **基类工作单元对 CAP 零引用**：全仓 grep `ICapPublisher` / `DotNetCore.CAP` 在 `Bing.EntityFrameworkCore` 工程**零命中**（已复核）。故裸用 `UnitOfWorkBase` 子类时，`send=false` 退化为"立即发送、且在业务事务外"。
- **`AdminUnitOfWork` 的实现与文档引用一致**（`AdminUnitOfWork.cs:44-65`）。

**三、同步更新**：`功能完成度.md` §7.2 补"登记动作忽略事务"的附注与手册链接；`docs/README.md` 角色表与专项表各 +1；`toc.yml` guides 组 +1（47 项）；`CONTRIBUTING.md` 文档维护表 +1。

---

### 2026-09-18 — 第十六轮：新建写路径手册（工作单元 / 仓储 / 应用服务），修正 `IRepository` 的错误结论

**背景**：这三块此前只有 [使用文档 §5](getting-started/使用文档.md) 约 50 行的速览，没有能照抄的手册；且它们是**同一个写路径上的三个环节**，拆开讲容易写出"改了但不落库"的代码，故合成一篇。

**一、新建 `docs/guides/工作单元仓储与应用服务.md`（8 节）**

| 节 | 内容 |
| --- | --- |
| §0 先记 6 条 | `IUnitOfWork` 只有 2 个成员；`IUnitOfWorkManager` **没有 `Begin()`**；仓储自己不落库；⛔ 手写服务不提交**静默丢变更**；`AppServiceBase` 不提供 UoW；`Manager.CommitAsync()` 提交**所有**已登记 UoW |
| §1 五分钟上手 | 装包 → 三步注册 → 一次完整写操作 → 各 Provider 入口名对照表 |
| §2 工作单元 | 契约全表、`RegisterToManager` 自动登记机制、`[UnitOfWork]` 拦截器源码、事务/回滚分支、只读 UoW 现状、19 个可重写成员 |
| §3 仓储 | `IStore` vs `IRepository` 对照、写入 12 方法、查询成员表、基类家族选型表、约定式注册、分页、映射、⚠ 仓储内不要自己提交 |
| §4 应用服务 | 两个工程分工、基类清单、`IQueryAppService` 方法名、`CrudAppServiceBase` 强制 `IRepository`、`[Valid]` AOP 验证、Controller 基类 |
| §5–§8 | 联合链路图、两段 admin 真实源码、13 条防误用清单 |

**二、修正「Bing 没有 `IRepository`」这个错误结论（2 处）**

此前 `abp-migration.md:41` 与迁移步骤第 1 条均写「Bing 没有 `IRepository`」。实际它一直存在（`IRepository.cs:37`），且是 `IStore` 的**超集**（多 `GetUnitOfWork()`，实体约束加 `IAggregateRoot`）。术语表 §8.1 上一轮已修正，本轮补齐 ABP 对照表与迁移清单，并写明选择依据与「`CrudAppServiceBase` 强制要求 `IRepository`」。

**三、本轮核准出的关键事实**

- **`IUnitOfWorkManager` 没有 `Begin()`**：从 ABP 迁移过来的人会找它。「开启一个工作单元」= 解析一次 Scoped UoW，构造函数 `RegisterToManager()` 自动登记（`UnitOfWorkBase.cs:79`）。
- **`DeleteAsync` 基类已内部提交**（`DeleteAppServiceBase.cs:134`），`IDeleteAppService` 上没有 `[UnitOfWork]` —— 再手动加会双提交。
- `[UnitOfWork]` 只在框架 3 处标注（`ICrudAppService.cs:50/57/80`），**自建服务默认没有**。
- EF 支可重写成员 19 个，FreeSQL 支**只有 3 个**（无审计拦截、无 `PublishEventsAsync`）。

### 2026-09-18 — 第十五轮：新建 `ILog` 使用说明，修掉既有日志文档的错误签名

**背景**：此前的日志内容只有 `operations/日志与可观测性.md` 一篇，是**链路视角**（五个层次怎么串起来），日志 API 本身只占约 100 行。用户要"能照抄的手册"时没有对应文档。落笔前核准源码，还发现既有文档有一处签名错误与一处遗漏。

**一、新建 `docs/operations/日志使用说明.md`（12 节）**

| 节 | 内容 |
| --- | --- |
| §1 五分钟上手 | 装包清单 → 两步注册 → 三种拿日志器的方式 → 第一条日志 → 取自集成测试 `Startup.cs` 的最小装配模板 |
| §2 链式 API 全表 | `ILog` 11 个成员 + `ILogExtensions` 11 个扩展方法；`Property`(string) vs `ExtraProperty`(object) 的四维差异；写完自动 `Clear()` |
| §3 消息模板 | 具名占位符、`{@X}` 解构、`{$X}` 字符串化、多次 `Message` 拼接 |
| §4 级别与配置 | `Logging:LogLevel` 处理规则 + `LogLevelSwitcher` 完整映射表 |
| §6 `LogContext` 与 TraceId | 13 个字段、谁填充、TraceId 四级优先级 |
| §10 / §11 | 7 条已知缺陷 + 9 行防误用清单 |

**二、修正既有文档 `日志与可观测性.md`**

| 位置 | 原来 | 修正为 |
| --- | --- | --- |
| §2.2 签名 | `public static **IServiceCollection** AddBingLogging(...)` | **`BingLoggingBuilder`**（`ServiceCollectionExtensions.cs:17`）——照原文写无法链式注册，已补 `.Services` 用法 |
| §2.4 级别映射 | 只写"未知值 → `Warning`" | 补上 **`"None"` → `Fatal`**（`LogLevelSwitcher.cs:42`）：想关日志反而只剩最严重级别 |
| §5.2 | 未提及 | 新增 ⛔ **`Log.AddLogContext()` 中 TraceId 注入整段被注释**（`Log.cs:260-264`），附源码摘录 |
| 头部 | — | 加"与《日志使用说明》的分工" |

**三、登记 3 处源码缺陷**（`功能完成度.md` 新增 §9.1）

共同点是**编译通过、运行不报错、日志照常输出，只是字段值是错的**：

1. `AspNetCoreLogContextAccessor.cs:57` —— `Browser` 被赋成**客户端 IP**
2. `LogContextEnricher.cs:106-108` —— `AddTenantId` 判空用 `TenantId`、**写入的却是 `UserId`**
3. `Log.cs:260-264` —— TraceId 注入被注释，`ILog` 写的日志不带 TraceId

另附两处静默降级（`"None"`→`Fatal`、未知级别→`Warning`）。**只做记录，未改代码。**

**结果**：`check-docs.py` 12 项全绿；新增交叉链接全部可达。

---

### 2026-09-18 — 第十四轮：重写 `ISqlQuery` 使用说明（补上手路径，删掉编造的 API）

**背景**：`docs/sql/sqlquery-usage.md` 原有 689 行，但读起来像"变更记录汇编"——**通篇没有"怎么装包、怎么注册、怎么拿到实例、第一条查询怎么写"**，读者无法在 5 分钟内跑通。同时它和 `sqlquery-lambda-usage.md` 在 Raw Fluent / 原生文本上大段重复。更麻烦的是：文中有若干**编译不过的示例**。

**一、重写主文档**（`docs/sql/sqlquery-usage.md`，13 节）

新增此前完全没有的上手路径：

| 节 | 内容 |
| --- | --- |
| §1 五分钟上手 | 装包清单 → `AddMySqlProvider()` + `AddSqlDataSource(...)` 两步注册 → `ISqlQueryFactory.Create()` → 三种等价的第一条查询 |
| §2 四种查询形态 | `ISqlQuery` 仅 5 个入口 / 4 种描述；澄清"**结果类型由终结方法决定**" |
| §3 终结方法全表 | 通用 9 组（含 `ToEntity` 的真实语义）+ Dapper 多映射 + 存储过程专有 `Execute*` 系列 |
| §5 分页 | `Pager` / `PagerList` / `IsTotalCountKnown` 的**跳过计数查询**机制 |
| §10 生命周期 | 状态机 `Draft→Frozen→Executing→Completed`、并发租约、不能共享实例、`Clone()` |

**二、删掉 6 处不存在的方法**（这些签名在源码里根本不存在，照抄会编译失败）

| 原文写的 | 实际情况 |
| --- | --- |
| `ToListAsync(buffered: false)` | ❌ 无此参数。`buffered` 是**内部写死**的：`ToList*` 族固定 `true`，`AsEnumerable*` 族固定 `false`。已沉淀为 §11 防误用清单 |
| `StreamAsync<T>()` | ❌ 不存在。流式入口是 `AsEnumerable<T>()` / `AsAsyncEnumerable<T>()` |
| `GetCountAsync()` | ❌ 不存在。取总数走 `ToPage` 的 `Pager.TotalCount` |
| `ToDynamicList()` | ❌ 不存在 |
| `ExecuteSql<TEntity>()` 挂在 `ISqlQuery` 上 | ❌ `ExecuteSql` 属于 **`ISqlExecutor`**，不属于 `ISqlQuery` |
| `SingleOrDefault` | 已删除，语义并入 `ToEntity`（保留说明，未作为可用 API） |

**三、两篇文档去重与分工**（`sqlquery-lambda-usage.md`）

- Lambda 篇开头加定位声明：**只讲 `From<T>()` 的 Lambda 子句**，其余一律指向主文档
- 原「Raw Fluent 与原生文本」节改写为「**多映射（Fluent / 原生文本专有）**」——该节真正独有的是 Dapper 多映射（且 `SqlLambdaQuery` **没有**多映射重载），其余终结方法列表删除
- 迁移对照节末尾补一条指向主文档 §11 的链接
- `docs/sql/README.md` 的目录项加注分工说明

### 2026-09-18 — 第十三轮：docs 目录按主题重排（终结平铺）+ 归位 docfx 生成目录

**背景**：`docs/` 顶层此前平铺 **35 个 `.md`**，加上 8 个子目录，找一份文档要在 40 多个条目里扫。同时 `docs/api/index.md` 被我改成了 98 行手工 API 索引——但那一层是 **docfx 的生成目录**（`docfx.json` 的 `metadata.dest: "api"`），手工内容放进去会被生成流程覆盖，位置本身是错的。

**一、`docs/api/index.md` 归位**

| 项 | 处理 |
| --- | --- |
| 手工索引内容 | 迁到 [getting-started/API速查索引.md](getting-started/API速查索引.md)，并改写导语：明确它与 `docs/api/`（docfx 生成）的**分工**——自动生成的逐方法级参考缺语义分组，本文补这一层 |
| `docs/api/index.md` | **恢复为 docfx 存根原样**（`git checkout`），不再承载手工内容 |
| `docs/api/*.yml` | 在**根** `.gitignore` 显式补一条规则。注：`docs/api/.gitignore`（docfx 模板既有文件）已覆盖 `*.yml` / `.manifest`，此处只是把「`api/` 是生成目录」这一约定在仓库级忽略文件里再说一次，避免将来误删子目录忽略文件后生成物被提交 |

**二、目录重排**（顶层 35 个 `.md` → **5 个入口文件 + 13 个子目录**）

| 新目录 | 收纳 |
| --- | --- |
| `getting-started/`（7） | 使用文档、能力矩阵、包索引、术语表、API速查索引、abp-migration、FAQ与排错 |
| `architecture/`（5+） | 架构文档、设计思路文档、子系统深挖、事件与消息文档、功能完成度；`adr/`（11）整体移入 |
| `guides/`（6+） | 领域建模指南、扩展指南、配置参考、最佳实践、横切能力文档、组件库文档；`recipes/`（7）整体移入 |
| `operations/`（6） | 日志与可观测性、安全指南、性能指南、测试指南、工程化文档、资产索引 |
| `sql/`（4→10） | 顺手把顶层遗留的 6 个 SQL 专项（`sql-mutation-*`、`sqlquery-*`、`sql-table-reference-*`）归位进已有的 `sql/` |
| `testing/`（1→2） | 顺带把顶层 `integration-testing.md` 归位 |

**三、链接改写（这是本轮的实际工作量）**

全仓有 **~990 条**指向 `docs/` 的链接，分布在 121 个文件里：**290 条是绝对 GitHub URL**（67 份包级 README 各 4–5 条，因为它们必须在 nuget.org 上可达），**512 条是 `docs/` 相对引用**，另有 docs 内部 md→md 相对链接 593 条。

改写**不能做字符串替换**，必须路径感知：同一条 `../架构文档.md` 从 `docs/` 和从 `docs/sql/` 解析出的目标不同，移动后的新相对路径也不同。三轮算法最终定型为：

```
1. resolve    用文件的【旧】位置把相对链接解析成仓库根相对路径
2. map        过 old→new 映射表
3. relativize 用文件的【新】位置重新算出相对路径
绝对 GitHub URL 走另一分支：只对 /docs/ 之后的部分做 map
```

**踩到的两个坑（都已写进脚本注释）**：

1. **「目标没移动、引用方移动了」必须也算** —— 第一版只判断"目标路径变了才改"，于是 `docs/ReleaseNotes.md`、`docs/sql/*.md` 这类未移动的目标被跳过，而引用方已从 `docs/` 下沉到 `docs/architecture/`，基准变了 → **96 处断链**。
2. **变换必须幂等** —— 第二版对"已被正确改写过的链接"又施加了一次变换，多出一层 `../`（如 `../guides/x.md` 变成 `../../guides/x.md`）→ **225 处断链**。最终修法是判定原则改成「**当前目录能解析到真实文件就一个字都不动**」，只有解析不到时才试反变换/映射，且**命中才替换**。

**四、防回归**：`eng/ci/check-docs.py` 新增两项检查——

- 「**顶层归位**」：`docs/` 顶层出现白名单之外的 `.md` 直接判失败（防止重新退化成平铺）；
- 「**交叉链接**」从"仅顶层"改为**递归全 docs**（原实现只看 `os.listdir(DOCS)`，子目录里的文档从来没被检查过）。

同轮把 toc 结构检查扩展为支持两级 `items:`（导航现在按 6 个分组呈现），检查项 11 → **12 项**。

**结果**：`check-docs.py` 12 项全绿；docs 下交叉链接 **594 条全部可达**；`sync-docs-index.py --check` 幂等通过；`docs/` 递归 md 共 **74 篇**（顶层仅 5 篇）。

### 2026-09-18 — 第十二轮：事件与消息专题（四套机制一次讲清）

**背景**：事件是本项目**最容易踩的区域**——四套机制并存、两套存在同名类型、其中一套根本不派发、现行那套还有一个会静默丢事件的开关。此前这些事实散在五份文档里，没有一处能一次看全。本轮补齐这个专题。

**新增文档**：

- [事件与消息文档](architecture/事件与消息文档.md) —— 四套机制的完整对照（进程内事件 / CAP 集成事件 / DDD 领域事件 / 遗留总线），含各自抽象与注册入口、完整发布链路、Outbox 事务一致性落点、CAP 重试·分组·序列化·TraceId、**类型名撞车对照表**、选型决策树、admin 一次登录的两条路径解剖、12 条陷阱、自行复核方法。

**本轮新登记 / 精确化的缺口**（并入 [功能完成度](architecture/功能完成度.md)）：

| 缺口 | 之前的口径 | 精确后 |
| --- | --- | --- |
| 领域事件不派发 | "派发器注册与否不明" | **派发器其实已注册**（`Bing.Admin/Modules/AppModule.cs:74`、`Bing.Admin.FreeSQL/Modules/AppModule.cs:68`）——缺的是**调用**。`DispatchAsync` 全仓仅 3 处：接口声明、实现、测试 |
| 工作单元是否排空领域事件 | 只写"全仓零命中" | EF 支**恰好有一个为此预留的钩子**，但它是空的：`UnitOfWorkBase.cs:799-802` 的 `protected virtual Task PublishEventsAsync() => Task.CompletedTask;`。FreeSQL 与 `Bing.Uow` 则连钩子都没有 |
| `ClearDomainEvents()` | 未提及 | 在生产代码中**从未被调用**——所以准确说法是"既没清也没派发"，而非"清了却没派发" |
| **`send:false` 在事务外会静默丢事件** 🆕 | 未登记 | `MessageEventBus` 对 `Send=false` **完全不调用 `Publisher`**，只 `TransactionActionManager.Register`；而 `SaveChanges` 仅在 `Count>0` 时才走 `CommitAsync` → **无 UoW 提交则动作永不执行、无报错无日志无兜底**。admin 恰好因 `SecurityService.cs:105` 那行 `CommitAsync()` 才躲过 |
| `Bing.Events.Cap.MySql` 的版本 | 写"CAP 2.6 的源码镜像" | **该包内部标注自相矛盾**：`csproj:9` 的 `Description` 说 "CAP 2.6"，而 `dependency.props:3` 实际 pin 的是 `DotNetCore.CAP` **3.1.2**。按源码为准 |

**本轮修正的既有文档错误**：

| 位置 | 原来 | 修正为 |
| --- | --- | --- |
| `子系统深挖.md` §1.2 | 「`Cap.MessageEventBus.PublishAsync` **先** `TransactionActionManager.Register(...)`，**再** `Publisher.PublishAsync(...)`」 | **写反了顺序关系**。它不是一个两步过程，而是**按 `Send` 二选一**：`true` → 直接 `Publisher.PublishAsync`；`false` → **完全不调用 `Publisher`**，只 `Register` |
| `子系统深挖.md` §1.2 | 「本地分支……适合进程内**领域事件**」 | 改为"进程内**本地事件**"，并注明领域事件走独立派发器且当前不会派发 |
| `术语表.md` §5 | 「**两级发布**——一个**领域事件**先本地 `ISimpleEventBus` 分发，再由 Handler 通过 `IMessageEventBus` 发出去」 | 该描述**把领域事件与消息事件混为一谈**（领域事件不经过任何事件总线）。改为准确的"**组合总线**"——描述的是 `Bing.Events.Cap.EventBus` 先本地、若为 `IMessageEvent` 再发 CAP 的行为；并新增「领域事件」独立词条 |
| `术语表.md` §8.3 | 「简单事件 vs 消息事件」两栏 | 扩为**三栏**（简单 / 消息 / 领域事件），并点明三者关系与"领域事件不实现 `IEvent`、不经过总线" |

**同步更新**：`横切能力文档.md` §5 开头加范围声明（本节只覆盖"包"的选型，四套机制的完整对照另见新文档）+ 修正 fork 包版本口径；`领域建模指南.md` §4.3、`日志与可观测性.md` §5.3、`配置参考.md` §5.7 各加交叉引用。

**新增导航**：`docs/toc.yml` +1；`docs/README.md` 角色表 +1、专项表 +1；`CONTRIBUTING.md` 文档维护表 +1 行。

---

### 2026-09-18 — 第十一轮：补四块结构性空白（建模 / 扩展 / 配置 / 可观测性）

**背景**：至此文档已覆盖"怎么用、长什么样、为什么这么设计、缺什么"，但四类**动手时才发现没处可查**的问题始终没有答案：实体该怎么建模、想给框架加东西从哪下手、某个行为改哪个配置、日志与追踪怎么接。这四块是本轮补的对象。

**新增文档（4 篇）**：

| 文档 | 补的空白 | 本轮新挖出的关键事实 |
| --- | --- | --- |
| [领域建模指南](guides/领域建模指南.md) | 实体 / 聚合根 / 值对象 / 领域事件 / 仓储 / 应用服务怎么建 | ⛔ **领域事件机制实际是死的**——`DispatchAsync` 只出现在实现自身与测试里，**没有任何生产代码调用**（见下） |
| [扩展指南](guides/扩展指南.md) | 新增模块、新增 Provider、换掉内置实现 | ⚠ **`IConventionalRegistrar` / `ConventionalRegistrarBase` 全仓无实现、无注册调用**——是条没接通的预留路径，不能照它扩展 |
| [配置参考](guides/配置参考.md) | Options 与配置节对照、默认值 | ⚠ **全仓真正绑定配置节的 Options 只有 `JwtOptions` 一个**；`AddBing()` 的 `BingOptions` **是空类**；`[OptionsType]` 机制存在但**零使用** |
| [日志与可观测性](operations/日志与可观测性.md) | Serilog 接入、Enricher、请求日志、TraceId、SkyAPM、诊断面板 | ⚠ 请求/响应日志**无任何脱敏**（Header/Cookie 原样输出）；`UseBingSerilogEnrichers` 在 `Bing.AspNetCore.Serilog` 而非 `Logging.Serilog`，且**必须在 `UseAuthentication` 之后**；**框架无 HealthChecks** |

**本轮纠正的既有文档错误**：

| 位置 | 原来 | 修正为 |
| --- | --- | --- |
| `术语表.md` §8.1 | "本框架**没有** `IRepository`" | **`IRepository` 确实存在**（`Bing.Ddd.Domain/Bing/Domain/Repositories/IRepository.cs:37`），约束为 `IAggregateRoot, IKey<TKey>`，且**是 `IStore` 的超集**（额外带 `IUnitOfWork GetUnitOfWork()`）。§8.1 重写为"两个都存在、怎么选" |

**本轮新登记的功能缺口**（补进 [功能完成度](architecture/功能完成度.md) §7.1）：

- **领域事件收集了但没有任何地方派发**。聚合根能通过 `AddDomainEvent` 收集事件，`IDomainEventDispatcher.DispatchAsync` 也有完整实现——但**全仓无生产调用**。admin 的登录日志之所以还能写，是因为它走的是**另一条路**（`UserLoginMessageEvent` + CAP，`Send=false` 即 Outbox 延迟发送），与领域事件派发无关。

**说明**：本轮同样**零代码改动**，四篇文档只记录现状与扩展方式，未新增或修改任何生产代码。

---

### 2026-09-18 — 第十轮：登记功能缺口（只补文档，不改代码）

**背景**：前九轮都在补"文档缺什么"。本轮反过来问"**框架缺什么**"，对生产代码做了一次全仓扫描。结论是缺口并非均匀分布，且**其中一处会静默失败**。

**新增文档**：

- `docs/功能完成度.md` —— 如实记录 7 个区域的功能缺口。**声明只描述不改代码**，文末给出缺口的三种处置方式（补实现 / 显式声明 / 标记废弃），但不替维护者做选择。含**自行复核方法**（怎么重跑扫描、怎么读框架自认的口径、怎么分辨"引用存在但被注释"）。

**登记的主要缺口**：

| 区域 | 缺口 |
| --- | --- |
| FreeSQL 支 | 异步 Provider **65 处** `NotImplementedException`，`CanExecute` 恒 `false` → 经 `AsyncQueryableExecuter.FindProvider` 兜底后**静默降级为同步阻塞**，不报错 |
| Dapper / `Bing.Data.Sql` | 框架**自认**的 **10 处** `ProviderImplementationGap`（9 处在 `SqlExecutorBase.cs`，1 处在 `SqlTransactionScopeFactory.cs`）；Lambda 多源查询 **6 处**不支持 |
| 组件包 | `Bing.EasyCaching` 注册扩展**整文件 25 行注释**，`AddCaching` 不存在 |
| `admin` 参考实现 | **三块整段注释**（Identity/权限 42 行、两个宿主的迁移种子各 46 行）；全仓 **25 处**被注释的注册调用 |
| 未接线工程 | `Bing.Events.Cap.MySql`（唯一引用被注释）、`Bing.EventBus.Abstractions`（零引用）、多租户三包（无生产宿主） |
| Provider 覆盖 | Oracle「未完成安全 fixture 时不得声明支持」、Doris 只读；两库**零运行记录** |

**顺带纠正的两处文档错误**：

| 位置 | 原来 | 修正为 |
| --- | --- | --- |
| `FAQ与排错.md` Q11 | "分页查询会走 `IAsyncQueryableExecuter` 吗？**不会**" | 它**确实走**这个执行器，只是找不到 provider 后**降级为同步执行**。已改为完整链路说明 + 判断方法（返回的 `Task` 是否立即 `RanToCompletion`） |
| `能力矩阵.md` §5.5 | 只写"Lambda 多源 `From` 不支持" | 实为 **From / Join / Select / Group By / Order By 五项全不支持** + 原子投影替换，共 6 处 |

**新挖出、且已写进文档的一条实用事实**：Dapper 支那 10 处能力缺口里，**8 处只在显式策略下才抛**——`SqlBatchDeleteStrategy.Auto` 与 `SqlBatchInsertStrategy.Auto` 会**自动降级为逐条命令并正常返回**，批量写入默认可用，只是失去 Provider 侧优化。唯一例外是 `MultiRowValues`（由 Provider Profile 决定，`Auto` 也抛）。

**更新**：`modules/admin/README.md` §5 扩为"迁移、种子与 Identity 注册尚未接通"；`docs/README.md` 角色表与专项表各加一行；`docs/toc.yml` 加导航项。

---

### 2026-09-18 — 第九轮：把校验固化进 CI，修掉一批既有错误

**背景**：本轮之前的文档靠「每轮临时写脚本抽查」，查完即弃；而`docs/index.md` 与根 `README.md` 长期双份维护，已经漂移出多处事实错误。

**修正的既有错误**（全部是"文档写错"而非"缺文档"）：

| 问题 | 原来 | 修正为 |
| --- | --- | --- |
| **`toc.yml` YAML 解析失败** | 用 `<!-- -->` 写注释 | 改为 `#` 注释。**YAML 里 `<!--` 不是注释语法**，实测会让文件整体解析失败（`ScannerError: could not find expected ':'`），docfx 构建直接报错 |
| 包表漏包 | 只列 55 个 | 补齐 13 个可发包，**67/67 全覆盖** |
| 测试栈写错 | `XUnit` / `NSubstitute` | `xUnit` + `Shouldly` + `Moq` + `Coverlet` |
| PostgreSQL 驱动名拼错 | `NPostgreSql.EntityFrameworkCore.PostgreSQL` | `Npgsql.EntityFrameworkCore.PostgreSQL` |
| 徽章 URL 多一个句点 | `.../Bing.AspNetCore.Mvc.Contracts./` | 去掉尾部句点（该链接原已失效） |
| docfx 首页漂移 | `docs/index.md` 停留旧版，缺「按需求快速入口」整节 | 改为从根 README 派生 |

**新增的机制**：

- `eng/ci/check-docs.py` —— **11 项**一致性校验（toc 链接、toc 结构与 YAML 注释误用、根 README 链接、根 README.en 链接、docs/README 链接、顶层正文交叉链接、包表覆盖、index.md 同步、包级 README 与徽章、历史坏模式回归）。零依赖，只用标准库；
  > ⚠ 此计数为**当时快照**。第十三轮新增「顶层归位」并把「交叉链接」改为递归全 docs 后，现为 **12 项**。
- `eng/ci/sync-docs-index.py` —— 从根 README 派生 `docs/index.md`，支持 `--check` 幂等校验；
- `.github/workflows/docs-lint.yml` —— 在 **PR 阶段**跑上述校验（`docfx.yml` 只在 push 到主干时才跑，来不及拦 PR）。

**新增内容**：51 份文档加上「适用版本」标注；`ReleaseNotes` ↔ `迁移指南` 建立互链；本文件（`docs/CHANGELOG.md`）建立。

**补的入口**：英文入口（`README.en.md`、`docs/README.en.md`）；多版本文档站点方案（见 §3）。

---

### 2026-09-18 — 第八轮：第三批（体验层）

- `docs/包索引.md`：按「我要做 X」反查 67 个包 + 分组速览表（脚本生成）。
- `docs/选型向导.html`：单页交互式选型工具，回答几个问题输出包清单与可复制启动代码。
- `docs/cheatsheet.html`：一页纸速查（可打印），API 全部按源码核准。
- `docs/recipes/`：索引 + 6 篇端到端配方（CRUD / CAP 事件 / 多租户 / RBAC / 支付 / 批量报表）。
- 67 份包级 README 加上 NuGet 徽章行（版本 / 下载量 / TFM）。

**顺带修正的事实错误**：此前《能力矩阵》称"EF 支自定义过滤器会自动生效"——**错**，`FilterManager.IsEntityEnabled<TEntity>()` 硬编码只查 `ISoftDelete`，自定义过滤器必须重写 `CreateFilterExpression<TEntity>()`。已修正《能力矩阵》《安全指南》两处，并在多租户配方里给出正确写法。

---

### 2026-09-18 — 第七轮：第二批（深度与可信度）

- `docs/adr/`：索引 + 10 份架构决策记录（含"被否决的备选方案"与负面后果）。
- `docs/性能指南.md`：数字取自 `artifacts/benchmarks/` 实测产物。
- `docs/安全指南.md`：含两条实测发现——JWT 的 `ThrowEnabled=true` 路径无过期校验；凭据硬编码清单。
- `docs/测试指南.md`：与既有两篇门控文档明确分工（讲"怎么写"而非"门控怎么开"）。

---

### 2026-09-18 — 第六轮：第一批（读者视角）

- `docs/能力矩阵.md`：三支 ORM × 18 项能力对照 + 选型决策树。
- `docs/术语表.md`：7 类术语 + 6 组易混词辨析。
- `docs/最佳实践.md`：12 组 ✅/❌ 对照 + 反模式速查。
- `docs/abp-migration.md`：ABP ↔ Bing 类型对照 + 8 步迁移清单。

---

### 2026-09-18 — 第四·五轮：包级 README 与工程化

- **67 份包级 README**（此前为零），并通过 `asset/props/package.props` 的 `PackageReadmeFile` 内嵌进 nupkg。
- 修复发版阻塞隐患：`PackageIcon` 声明与图标实际打包分处两个 props 文件，缺一即 `NU5046`。
- `docs/工程化文档.md`（维护者手册）、`docs/横切能力文档.md`、`docs/资产索引.md`。
- `CONTRIBUTING.md` 修正过时测试路径（`tests/` → `framework/tests/`）。

---

### 2026-09-18 — 第三轮：缺口补齐

- `docs/组件库文档.md`（9 个边缘包，含状态徽章）、`docs/migrations/README.md`。
- `samples/` 与 `modules/admin/` 的 5 份 README。
- docfx 配置修正：`docs/api/index.md` 删掉错误的"尚未接入 docfx"（实际早已接入）、`toc.yml` 扩容、`.gitignore` 加 `docs/_site/`。

---

### 2026-09-17 — 第二·一轮：核心四件套

- `docs/使用文档.md`、`docs/架构文档.md`、`docs/设计思路文档.md`、`docs/子系统深挖.md`。
- `docs/images/`：6 张架构与数据流图（PNG + SVG 源文件）。
- `docs/FAQ与排错.md`、`docs/README.md`（导航 hub）、`CONTRIBUTING.md`。

---

## 3. 多版本文档站点

**现状**：`.github/workflows/docfx.yml` 在 push 到 `main` / `master` / `dev` 时构建并部署到 GitHub Pages，**只保留最新版本**——历史版本的文档不会被存档。

**何时需要**：当出现"用户在用 6.0.0，但站点只有 7.0.0 的文档"这类问题时。

**可选方案**（按成本从低到高）：

1. **打 Tag 存档目录**（最低成本）：发版时把 `docs/` 快照复制到 `docs/versions/<version>/`，并在站点上加入口。缺点是仓库体积会增长。
2. **单独分支**：发版时把文档推到 `docs/v7.0.0` 之类的分支，用 GitHub Pages 的多环境或不同 workflow 分别部署。
3. **docfx 多版本构建**：docfx 本身不支持多版本，需要外部工具（如 `docfx` 的 `--version` 包装脚本或 DocFX.Plugins 生态）拼接产物，成本最高。

**当前选择**：暂不实现，采用**文档内版本标注**兜底——每份文档头部的「适用版本」行会明确写出对应的框架版本与核对日期。等真的出现多版本并存的用户群时再按方案 1 起步。
