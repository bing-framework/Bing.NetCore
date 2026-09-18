# 架构决策记录（ADR）

> **适用版本**：框架 `7.0.0`（`version.props`）｜ 目标框架：类库 `netstandard2.0`、Web / 应用 `net6.0`｜ 内容核对：2026-09-18｜ 版本历史见 [发行说明](../../ReleaseNotes.md)

> **这是什么**：ADR（Architecture Decision Record）把框架的关键决策用「一决策一文件」的方式固化下来，记录**当时的背景、被否决的备选方案、最终决策与后果**。
>
> 与 [《设计思路文档》](../设计思路文档.md) 的关系：设计思路文档是**按主题组织**的叙述（读它了解全景），ADR 是**按决策组织**的档案（想知道"当初为什么这么定、还考虑过什么"时读它）。
>
> **写法遵循 Michael Nygard 的经典格式**：标题 / 状态 / 背景 / 备选方案 / 决策 / 后果（正面·负面·中性）/ 相关文档。

---

## 状态图例

| 状态 | 含义 |
| --- | --- |
| 🟢 **已接受**（Accepted） | 现行有效，代码已按此实现 |
| 🟡 **提案中**（Proposed） | 提出但尚未实施或仍在验证 |
| 🔴 **已废弃**（Superseded） | 被后续 ADR 取代（会注明取代者） |
| ⚪ **已拒绝**（Rejected） | 讨论过但决定不采用 |

> 目前 10 份全部为 🟢 已接受——它们都是**事后补录**的（决策早已在代码中落地），本文的价值在于把「背景与备选」那部分补写出来，供日后回看与新人理解。

---

## 决策清单

| 编号 | 决策 | 状态 | 一句话 |
| --- | --- | --- | --- |
| [ADR-0001](ADR-0001-module-two-phase-bootstrap.md) | 模块化两阶段启动 | 🟢 | 用 `BingModule` 替代手写 Startup 注册，按 `Level` 排序 |
| [ADR-0002](ADR-0002-conventional-di-registration.md) | 约定式依赖注入 | 🟢 | 实现标记接口即自动注册，`[Dependency]` 可覆盖 |
| [ADR-0003](ADR-0003-multi-orm-coexistence.md) | 多 ORM 并存 + 共享领域抽象 | 🟢 | 领域接口与 ORM 解耦，EF / FreeSQL / Dapper 各自实现 |
| [ADR-0004](ADR-0004-unified-api-response.md) | 统一 API 响应（结果过滤器） | 🟢 | 用全局结果过滤器自动包装 `ApiResult` |
| [ADR-0005](ADR-0005-unit-of-work-aop-commit.md) | 工作单元 + AOP 自动提交 | 🟢 | `[UnitOfWork]` 切面在方法成功后统一提交 |
| [ADR-0006](ADR-0006-multi-tenancy-resolve-chain.md) | 多租户解析链 | 🟢 | 可组合的 `ITenantResolveContributor` 链，不做便捷封装 |
| [ADR-0007](ADR-0007-cross-cutting-by-convention.md) | 横切能力靠约定自动注册 | 🟢 | 多数能力没有 `AddXxx`，靠约定注册 + Options 配置 |
| [ADR-0008](ADR-0008-exception-hierarchy.md) | 异常体系与统一错误 | 🟢 | 语义化异常 + 转换器 → 统一错误响应，生产不暴露堆栈 |
| [ADR-0009](ADR-0009-dual-target-framework.md) | 双目标框架 `netstandard2.0` / `net6.0` | 🟢 | 默认 `netstandard2.0`，与 ASP.NET Core 强耦合的工程升 `net6.0` |
| [ADR-0010](ADR-0010-roslyn-source-generators.md) | Roslyn 源生成器与分析器 | 🟢 | 编译期生成变更跟踪代码，运行期零依赖 |

---

## 我该读哪一份？

| 你的问题 | 读 |
| --- | --- |
| 为什么注册入口这么分散、启动逻辑找不到？ | ADR-0001 |
| 为什么没有 `services.AddXxx()` 也能注入？ | ADR-0002 / ADR-0007 |
| 为什么有三套 ORM？能不能统一？ | ADR-0003 |
| 为什么返回值被自动包了一层？ | ADR-0004 |
| 事务到底谁在提交？ | ADR-0005 |
| 多租户为什么还要手动注册中间件？ | ADR-0006 |
| 为什么有些类奇奇怪怪是生成的？ | ADR-0010 |

---

## 新增 ADR 的约定

1. **文件命名**：`ADR-<四位序号>-<英文短横线主题>.md`，序号只增不改。
2. **已废弃的决策不要删除**，把状态改为 🔴 并注明「被 ADR-00XX 取代」——历史决策的可追溯性是 ADR 的核心价值。
3. **同步更新本索引**（在清单表加一行）。
4. **语言**：中文；日期用 ISO 格式；每条后果都要写**负面**那一栏——只写好处的 ADR 没有参考价值。
5. 若某份 ADR 的结论被推翻，同时更新 [《设计思路文档》§4](../设计思路文档.md) 与本文。
