# copilot-instructions.md (Bing.NetCore / framework)

> 适用范围：本仓库 `framework/`  
> - `framework/src`：框架源码  
> - `framework/tests`：测试项目（已按模块拆分，含 Unit + Integration + Shared Test Infrastructure）

本指引用于约束 GitHub Copilot / Copilot Chat / Codex 在本仓库生成代码与测试的方式，使输出 **可合并、可维护、可测试、与现有结构一致**。

---

## 1. 现有测试结构（必须遵守）

### 1.1 目录真实结构（以当前仓库为准）
- 单元测试（Unit Tests）：`framework/tests/<Module>.Tests`
  - 示例：`Bing.Core.Tests`、`Bing.Auditing.Tests`、`Bing.MultiTenancy.Tests`
- 集成测试（Integration Tests）：`framework/tests/<Module>.Tests.Integration`
  - 示例：`Bing.Caching.CSRedis.Tests.Integration`、`Bing.Dapper.MySql.Tests.Integration`
- 共享测试基建（Test Infrastructure / Shared）
  - `Bing.Test.Shared`
  - `Bing.TestShare`
  - `Bing.TestShare.MySql`
- 其它：存在 `Bing.Tests`（聚合/兼容/历史用途），生成内容时需避免与现有职责冲突。

> 规则：新增测试项目必须优先沿用上述命名与分层，不要引入新的目录体系。

### 1.2 Unit vs Integration 的边界（强制）
- **Unit Test（默认）**
  - 只测纯逻辑与边界行为
  - 不依赖网络、真实 DB、真实缓存、真实文件系统
  - 必须确定性、可重复运行、速度快
- **Integration Test（仅在必须时）**
  - 仅用于验证：真实数据库/缓存/DI 组合/ASP.NET Core 管道/中间件链路
  - 必须具备可重复运行能力：可本地跑、可 CI 跑
  - 若依赖外部服务（MySql/PostgreSql/Redis），需提供可控的启动方式（优先容器化/测试环境变量配置），不得硬编码连接信息

---

## 2. 生成代码的通用原则

### 2.1 不引入破坏性变更
- 不随意修改 public API（类名、方法签名、异常类型、默认行为）。
- 如必须调整 API：
  - 同步：单元测试/集成测试（按影响范围）
  - 更新文档或注释（如该模块已有说明/README/使用示例）

### 2.2 模块依赖方向必须合理
- `Core/Abstractions` 不允许依赖 `AspNetCore` 或其它上层实现。
- 上层模块可依赖基础模块，但不能反向引用。
- 跨模块能力（异常、多租户、审计、事件总线、缓存等）通过抽象/接口/扩展点协作，避免硬耦合。

### 2.3 优先做“最小变更”
- 避免引入无意义格式化/大规模重命名导致 diff 失真。
- 改动必须聚焦目标，保持可 review。

---

## 3. 测试规范（你这个仓库的强约束）

### 3.1 测试项目引用规则
当为某个 `framework/src/<Module>` 增加或完善测试时：

- Unit Test 项目：`framework/tests/<Module>.Tests`
  - 必须 `ProjectReference` 指向被测 `framework/src/<Module>/<Module>.csproj`
  - 必须引用共享测试基建：优先使用仓库现有的 `Bing.Test.Shared` / `Bing.TestShare*`（不要新造轮子）

- Integration Test 项目：`framework/tests/<Module>.Tests.Integration`
  - 除上述规则外，还需要明确：
    - 测试运行前置条件（DB/Redis 等）
    - 如何在 CI 运行（环境变量/容器/跳过策略）

> 禁止：为了让测试“看起来能跑”，在 Integration 里写 sleep、随机等待、依赖公网。

### 3.2 命名规范
- 测试方法名：**英文**，建议：`Method_State_Expected()`。
- 测试注释：**中文**，每个测试必须写测试目的，参照下面的示例内容。
- 结构：AAA（Arrange / Act / Assert）。

示例：
```csharp
/// <summary>
/// 测试 - 当租户标识缺失时，解析器应返回 null，避免抛异常影响上游管道。
/// </summary>
[Fact]
public void Resolve_WhenTenantIdMissing_ShouldReturnNull()
{
    // Arrange
    ...

    // Act
    ...

    // Assert
    ...
}
```

### 3.3 Mock 边界（重点）
- 只 Mock 外部依赖：时间、Guid、随机数、IO、HTTP、数据库、缓存、日志等。
- 不 Mock 被测模块内部实现细节（避免“验证调用次数”替代“验证行为结果”）。
- 日志测试：通常只验证“不抛异常/路径正确/包含关键字段（若有结构化事件）”，不要断言完整文本。

---

## 4. 针对你当前模块的“优先补测策略”

### P0（必须先补齐）
- `Bing.Core.Tests`
  - 基础工具类、Guard/参数校验、扩展方法、序列化/转换、线程安全与边界
- `Bing.MultiTenancy.Tests`
  - 租户解析优先级、TenantScope/上下文传播、fallback 行为
- `Bing.AspNetCore.Mvc.Tests` / `Bing.Aop.AspectCore.Tests`（如涉及关键管道/拦截）
  - 中间件/过滤器/拦截器的关键行为（可用少量集成测试兜底）
- `Bing.Logging.Tests` / `Bing.Logging.Serilog.Tests`
  - logger provider 组合、结构化字段是否被正确输出（避免测具体文本）
- `Bing.EventBus.Tests`
  - 发布/订阅基本语义、异常传播、重试策略（若有）

### P1（增强）
- `Bing.Auditing.Tests`
  - 审计字段填充策略、忽略规则、边界数据
- `Bing.AutoMapper.Tests`
  - profile 注册、映射配置验证、关键映射行为（避免测 AutoMapper 内部）

### P2（最后）
- 多 DB / 多缓存的集成链路补齐与稳定性增强
  - `Bing.Dapper.*.Tests.Integration`
  - `Bing.Caching.*.Tests.Integration`
  - 重点：连接配置、隔离（schema/db）、清理策略、并发稳定性

---

## 5. Copilot 输出要求（强制工作流）

当你让 Copilot “完善某模块/补单测/修 bug”时，必须按以下步骤输出：

1) **变更计划**
   - 修改/新增文件路径列表
   - 影响的模块与依赖
   - 是否需要 Integration Test（为什么）
2) **用例矩阵**
   - Given/When/Then（至少：正常 + 边界 + 负例）
   - Mock 边界说明
3) **落地代码**
   - 可编译通过、可运行的测试代码
   - 如新增项目：给出 `.csproj` 关键引用片段

> 禁止：直接生成大量代码却不说明测试意图与用例覆盖范围。

---

## 6. CI/可执行性约束（提交验收）

每次 PR 必须满足：
1. `dotnet build` 通过
2. `dotnet test` 通过（Unit Tests 必须全绿）
3. 新增/修改行为必须包含对应测试
4. Integration Tests 如需跳过必须有明确条件（例如 `RUN_INTEGRATION_TESTS=true`），并在说明中写清楚

---

## 7. 常用 Prompt 模板（按你仓库结构）

### 7.1 针对某模块补齐 Unit Tests（默认）
“请为 `framework/src/<Module>` 补齐 P0 单元测试，写到 `framework/tests/<Module>.Tests`。先列 public API，再给用例矩阵（Given/When/Then），再输出可运行的 xUnit 测试代码，并复用 `Bing.Test.Shared / Bing.TestShare*` 的现有能力，不要新造测试基建。”

### 7.2 针对某数据库实现补齐 Integration Tests
“请为 `framework/src/Bing.Dapper.<Db>` 补齐集成测试，写到 `framework/tests/Bing.Dapper.<Db>.Tests.Integration`。说明依赖（DB/连接配置/容器化建议），并提供可重复运行的初始化与清理策略，避免 sleep 与非确定性。”

### 7.3 仅做问题审计（不改代码）
“只扫描 `framework/src` + `framework/tests`，列出：测试覆盖缺口、P0 风险、依赖方向问题、API 不一致点，并给出可执行 PR 列表（每个 PR 的文件路径与验收标准）。”

---
