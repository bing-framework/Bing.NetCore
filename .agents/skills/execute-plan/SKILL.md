---
name: execute-plan
description: 执行已经批准的 ai_docs/tasks/<taskId>/plan.md。适用于 Copilot、Antigravity、Codex 等支持 Agent Skills 的执行 Agent；持续完成真实实现、测试、验证和 execution.md，不重新规划整体方案，不自动 git commit/push/PR。
---

# Execute Plan

## 1. 目标

你是 **Implementation Executor**。

输入是已经批准的：

`ai_docs/tasks/<taskId>/plan.md`

输出是：

1. 真实代码/测试/文档变更；
2. `ai_docs/tasks/<taskId>/execution.md`；
3. 正确的任务运行状态；
4. 必要验证结果。

你不是 Planner，也不是 Reviewer。

如果任务已经存在 `review.md = NEEDS_FIX`，并且用户的目标是修复 Review，请停止本 Skill，改用 `fix-review` Skill。

---

## 2. 支持的 Agent Harness

本 Skill 是公共核心，可被以下环境复用：

- GitHub Copilot / VS Code；
- Google Antigravity；
- OpenAI Codex；
- 其他兼容 Agent Skills 的 Coding Agent。

Host 专属入口只负责调用本 Skill，不应复制另一套实施规则。

---

## 3. taskId 与文件

优先级：

1. 用户明确提供的 taskId；
2. 用户明确提供的 plan.md 路径；
3. 当前上下文唯一明确的 taskId；
4. `.agents/runtime/current-task.json` 中 active 的任务。

默认：

```text
plan.md
ai_docs/tasks/<taskId>/plan.md

execution.md
ai_docs/tasks/<taskId>/execution.md

review.md
ai_docs/tasks/<taskId>/review.md
```

存在多个候选且无法可靠判断时才询问用户。

---

## 4. 识别当前执行器

注册任务状态时尽量传入当前 Harness：

```text
copilot
antigravity
codex
```

例如：

```bash
node .agents/scripts/task-state.mjs start <taskId> --source copilot
```

```bash
node .agents/scripts/task-state.mjs start <taskId> --source antigravity
```

```bash
node .agents/scripts/task-state.mjs start <taskId> --source codex
```

如果无法识别，可省略 `--source`。

---

## 5. 开始执行前

### 5.1 读取项目规则

按项目实际情况读取并遵守：

1. 根目录 `AGENTS.md`；
2. 目标目录更具体的 `AGENTS.md`；
3. Copilot instructions / CLAUDE.md 等已启用项目规则；
4. `DESIGN.md`、`ARCHITECTURE.md`、README、CONTRIBUTING、ADR/RFC；
5. `docs_ai/`、`ai_docs/`、`docs/`；
6. 构建、测试、Lint、Typecheck、Formatter、CI 配置。

子目录更具体的规则优先。

源码/测试/配置与文档冲突时，不要静默猜测；以可验证事实为主要实现依据，并在 execution.md 记录必要偏差。

### 5.2 完整读取 plan.md

不能只根据聊天摘要实施。

必须理解：

- 目标与非目标；
- 当前实现；
- Phase / Task；
- 设计决策；
- API / 数据 / UI / 配置影响；
- 测试策略；
- 文档要求；
- 风险；
- Definition of Done。

### 5.3 Git 基线

至少：

```bash
git status --short
git diff --stat
```

必要时：

```bash
git diff
```

保护用户已有未提交修改。

禁止为了获得 clean workspace 自动 reset/restore/clean。

### 5.4 注册执行状态

在修改业务代码前执行：

```bash
node .agents/scripts/task-state.mjs start <taskId> --source <当前执行器>
```

然后确认：

```text
.agents/runtime/current-task.json
```

满足：

```json
{
  "active": true,
  "taskId": "<taskId>",
  "mode": "plan-execution",
  "status": "implementing"
}
```

脚本会把 execution.md 切换到：

```text
<!-- AI_EXECUTION_STATUS: IN_PROGRESS -->
AI_TASK_ID: <taskId>
AI_EXECUTION_STARTED_AT: <ISO-8601>
```

`IN_PROGRESS` 不是终态。

---

## 6. 内部执行清单

从 plan.md 建立内部任务清单，但不要修改 plan.md Checkbox。

状态：

```text
PENDING
IN_PROGRESS
COMPLETED
PARTIAL
BLOCKED
FAILED
```

通常按：

```text
基础契约
→ 核心实现
→ 调用链接入
→ 数据/状态迁移
→ UI/API/IPC
→ 测试
→ 文档
→ 完整验证
```

执行。

如果 plan.md 已明确顺序，以计划为准。

---

## 7. 实施循环

对每个计划项：

```text
读取真实实现
→ 判断当前完成度
→ 定位根因
→ 实施最小合理变更
→ 运行最近的验证
→ 失败则修复
→ 再验证
→ 记录证据
→ 下一项
```

不要因为以下情况停止：

- 完成一个文件；
- 完成一个 Phase；
- 第一次测试失败；
- 第一次 Build 失败；
- 出现可以自行定位的 Bug；
- 需要补测试；
- 需要同步计划要求的文档。

只要仍有计划内、可安全继续的工作，就继续。

---

## 8. 真实完成要求

禁止用以下方式假装完成：

- 只增加接口/类型；
- 空实现；
- TODO；
- 固定返回值；
- 临时 Mock 代替真实业务；
- 吞异常；
- 静默兜底成成功；
- 删除失败测试；
- 降低关键断言；
- 跳过关键验证。

必须确认新能力真正进入主调用链。

如果计划项在当前源码中已经真实完成，不要为了制造 Diff 重复修改；在 execution.md 记录“已验证存在，无需改动”。

---

## 9. 范围控制

- 不擅自扩大需求；
- 不加入与 plan 无关的新功能；
- 不做大而泛的全仓库重写；
- 根因修复优先于症状补丁；
- 复用既有主流程和抽象；
- 避免第二套数据源、状态源、缓存、权限、主题或 API 体系；
- 与当前任务无关的问题只记录，不顺手修改。

如果 plan 与真实代码存在必要偏差，可以局部调整，但必须在 execution.md 记录：

- 原计划；
- 实际情况；
- 调整；
- 原因；
- 风险。

---

## 10. API / 数据 / UI / 性能

### API

检查：

- 命名与职责；
- 参数/返回值；
- async/nullability；
- public/internal/private 边界；
- 兼容性；
- 重复入口；
- Breaking Change。

不要无计划扩大公共 API。

### 数据

考虑：

- Schema / Migration；
- 历史数据；
- 事务；
- 并发；
- 幂等；
- 索引；
- 锁；
- 大数据量；
- 缓存一致性。

不要执行未授权的破坏性生产数据操作。

### UI

优先复用：

- Design Token；
- 主题变量；
- 通用组件；
- 既有布局；
- Loading/Empty/Error；
- 深色模式/响应式/可访问性。

不要建立第二套视觉系统。

### 性能

关注：

- 重复 IO/DB/网络；
- N+1；
- 热路径分配；
- 不必要集合复制；
- 重复序列化；
- 同步阻塞；
- 锁竞争；
- 无界缓存；
- 大数据量复杂度。

没有证据时不要为了“理论性能”制造复杂度。

---

## 11. 测试与验证

### 11.1 自动发现命令

从真实仓库发现：

- Test；
- Integration Test；
- Typecheck / Compile；
- Lint；
- Build；
- Formatter Check；
- Smoke Test；
- Migration/Codegen（如适用）。

不要假设项目一定使用 pnpm、npm、dotnet、Maven、pytest 等。

### 11.2 局部验证

每完成一个逻辑单元优先：

```text
相关单元测试
→ 相关集成测试
→ 局部 Typecheck/Compile
→ 局部 Lint
```

失败时：

1. 阅读完整错误；
2. 定位首个根因；
3. 判断是否由本轮引入；
4. 修复；
5. 重新运行最小验证；
6. 通过后继续。

### 11.3 最终验证

所有计划项处理完后，按项目适用性执行：

1. 相关测试；
2. 完整单元测试；
3. 集成测试；
4. Typecheck / Compile；
5. Lint；
6. Formatter Check；
7. Build；
8. Smoke Test；
9. 专项验证；
10. `git diff --check`。

未执行项要记录原因，不要伪装 PASS。

---

## 12. 最终 Diff Review

结束前检查：

```bash
git status --short
git diff --stat
git diff
git diff --check
```

重点确认：

- 无遗漏计划项；
- 无第二套实现；
- 无意外行为变化；
- 无死代码/TODO；
- 无吞异常；
- 无测试作弊；
- 无 Secret；
- 无无关依赖；
- 无覆盖用户原有改动。

可自行修复的问题继续修复并重新验证。

---

## 13. execution.md 严格协议

执行期间：

```text
<!-- AI_EXECUTION_STATUS: IN_PROGRESS -->
AI_TASK_ID: <taskId>
AI_EXECUTION_STARTED_AT: <ISO-8601>
```

真正结束时前三行必须替换为：

```text
<!-- AI_EXECUTION_STATUS: COMPLETED|PARTIAL|BLOCKED|FAILED -->
AI_TASK_ID: <taskId>
AI_EXECUTION_FINISHED_AT: <ISO-8601>
```

四种终态：

- `COMPLETED`：计划内核心事项全部完成，必要验证通过；
- `PARTIAL`：部分完成，存在明确遗留；
- `BLOCKED`：真实外部阻塞；
- `FAILED`：继续执行存在不可接受风险或不可恢复失败。

普通可修复代码/测试错误不是 BLOCKED。

---

## 14. execution.md 至少记录

```markdown
# 实施执行报告

## 执行结论
## 任务信息
## 计划执行情况
## 已完成事项
## 部分/未完成事项
## 修改文件
## API/数据/配置变化
## 测试结果
## Build/Typecheck/Lint/Format
## 计划偏差
## 基线问题
## 已知问题
## 风险与回归关注点
## Reviewer 注意事项
## Git 状态
```

明确写：

- 未自动 git commit；
- 未自动 git push；
- 未自动创建 PR。

---

## 15. 通用任务收口

写好合法终态前三行以后，执行：

```bash
node .agents/scripts/task-finish.mjs <taskId>
```

这个脚本是跨 Harness 的通用 Finalizer：

- 校验 execution.md 终态；
- 将 current-task.json 设置为 inactive；
- 记录 finalStatus；
- 按项目配置发送飞书 Card；
- 幂等，重复执行默认不会重复发通知。

因此：

- Copilot 不依赖 Antigravity Hook；
- Codex 不依赖 Antigravity Hook；
- Antigravity 也可以主动 Finalize；
- Antigravity Stop Hook 继续作为提前停止保护和 Finalize 遗漏兜底。

如果项目没有飞书配置，通知会安全跳过。

---

## 16. Git 安全

除非用户当前任务明确授权，禁止：

```text
git add
git commit
git push
git reset --hard
git clean
git checkout .
git restore .
```

禁止自动 PR。

允许只读：

```text
git status
git diff
git diff --stat
git diff --check
git log
git show
```

---

## 17. 最终回复

简洁报告：

- Task ID；
- 状态；
- execution.md 路径；
- 关键 Test / Build 结果；
- 遗留/阻塞；
- 是否完成 task-finish；
- 未 commit / push。

不要重复输出整个 plan。
