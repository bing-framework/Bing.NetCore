# 工作流程闭环路线

Universal Agent Workflow 支持 **两条独立闭环线路**。

两条线路共享完全相同的任务文件、Agent Skills、状态机和 Review/Fix 协议，不需要因为切换工具重新创建任务。

共享任务目录：

```text
ai_docs/tasks/<taskId>/
├─ plan.md
├─ execution.md
└─ review.md
```

共享公共能力：

```text
.agents/skills/
├─ execute-plan/
└─ fix-review/

.agents/scripts/
├─ task-state.mjs
├─ task-finish.mjs
├─ workflow-notify.mjs
└─ ...
```

---

## 路线一：纯 Copilot / Codex 自闭环

适用于希望尽量不切换工具，由 Copilot 或 Codex 自己完成 Planning、Implementation、Review、Review Fix 和 Re-review 的场景。

```text
Copilot / Codex
      │
      ▼
创建计划
      │
      ▼
plan.md
      │
      ▼
执行计划
      │
      ▼
execution.md
      │
      ▼
Review
      │
      ▼
review.md
      │
 ┌────┴───────────┐
 │                │
PASS           NEEDS_FIX
 │                │
 ▼                ▼
完成          Fix Review
                  │
                  ▼
            execution.md
                  │
                  ▼
              Re-review
                  │
             ┌────┴─────┐
             │          │
            PASS    NEEDS_FIX
             │          │
             ▼          └──→ 再次 Fix
            完成
```

### Copilot

推荐入口：

```text
/create-plan
    ↓
/execute-plan
或 /run-plan
    ↓
/review-plan
    ↓
如果 NEEDS_FIX
    ↓
/fix-review
或 /repair-review
    ↓
/review-plan
```

角色对应：

```text
Planner   = Copilot
Executor  = Copilot
Reviewer  = Copilot
Fixer     = Copilot
```

### Codex

推荐逻辑：

```text
创建/准备 plan.md
    ↓
$execute-plan
    ↓
Review
    ↓
如果 NEEDS_FIX
    ↓
$fix-review
    ↓
Re-review
```

角色对应：

```text
Planner   = Codex
Executor  = Codex
Reviewer  = Codex
Fixer     = Codex
```

### 适用场景

- 中小型需求；
- 希望减少 IDE 切换；
- 单一 Coding Agent 已有足够上下文；
- 快速迭代、原型、日常维护；
- 希望在 Copilot 或 Codex 内完成整个闭环。

---

## 路线二：Copilot / Codex + Antigravity 协作闭环

该线路将 **Planning / Review** 与 **Implementation / Review Fix** 分离。

职责：

```text
Copilot / Codex
负责：
Planning
+
Review / Re-review

Antigravity IDE
负责：
Implementation
+
Review Fix
```

完整闭环：

```text
Copilot / Codex
      │
      ▼
创建计划
      │
      ▼
plan.md
      │
      ▼
────────────────────────
切换 Antigravity IDE
────────────────────────
      │
      ▼
/execute-plan <taskId>
      │
      ▼
真实实现 / 测试 / Build / 验证
      │
      ▼
execution.md
      │
      ▼
────────────────────────
回到 Copilot / Codex
────────────────────────
      │
      ▼
Review
      │
      ▼
review.md
      │
 ┌────┴───────────┐
 │                │
PASS           NEEDS_FIX
 │                │
 ▼                ▼
完成       ─────────────────
           Antigravity IDE
           ─────────────────
                  │
                  ▼
          /fix-review <taskId>
                  │
                  ▼
          MUST_FIX + SHOULD_FIX
                  │
                  ▼
          execution.md 更新
                  │
                  ▼
           ─────────────────
           Copilot / Codex
           ─────────────────
                  │
                  ▼
               Re-review
                  │
             ┌────┴─────┐
             │          │
            PASS    NEEDS_FIX
             │          │
             ▼          └──→ 再交给 Antigravity
            完成
```

职责表：

| 阶段 | 默认执行工具 |
|---|---|
| 创建计划 | Copilot / Codex |
| 执行计划 | Antigravity IDE |
| Review | Copilot / Codex |
| 修复 Review | Antigravity IDE |
| Re-review | Copilot / Codex |

### 推荐复杂任务使用该路线

这条线路天然形成：

```text
Planner / Reviewer
        ≠
Executor / Fixer
```

可以减少实现 Agent 自己实现、再自己证明自己正确的偏差。

适合：

- 较大功能；
- 架构调整；
- 多文件/多模块变更；
- API / 数据库 / UI 联动；
- 性能优化；
- 需要独立验收的高风险任务；
- 希望 Antigravity Stop Hook 持续执行到任务终态。

---

## 两条路线可以混合工具，但不能混乱职责

任务只认 `taskId` 和任务文件协议，不绑定某个 Harness。

例如以下组合都合法：

```text
Copilot Plan
→ Antigravity Execute
→ Codex Review
→ Antigravity Fix
→ Copilot Re-review
```

```text
Codex Plan
→ Antigravity Execute
→ Copilot Review
→ Antigravity Fix
→ Codex Re-review
```

```text
Copilot Plan
→ Codex Execute
→ Copilot Review
→ Codex Fix
→ Copilot Re-review
```

关键要求：

1. 全程使用同一个 `taskId`；
2. 不重新创建另一份 plan；
3. Executor 不修改 review.md 伪造通过；
4. Reviewer 不修改业务代码；
5. Fixer 把修复证据写入 execution.md；
6. Re-review 重新验证上一轮 FIX；
7. 默认不自动 git commit / push / PR。

---

## Agent Runtime Profile 与路线相互独立

`.agents/agent-profiles.json` 管理的是：

```text
Role
→ Harness
→ Model
→ Effort Policy
```

它不决定你必须走哪条路线。

例如 `quality` Profile 可以同时用于：

```text
路线一：
Copilot Plan → Copilot Execute → Copilot Review → Copilot Fix
```

也可以用于：

```text
路线二：
Copilot Plan → Antigravity Execute → Copilot Review → Antigravity Fix
```

因此：

```text
Route
= 谁负责哪个阶段

Profile
= 每个 Role 在具体 Harness 下使用什么模型/思考等级策略
```

二者是正交关系。

---

## 推荐选择

### 便捷模式

```text
路线一
```

优先用于日常、小中型任务。

### 复杂任务模式

```text
路线二
```

优先用于重要、复杂、高风险任务。

最终原则：

> Universal Agent Workflow 支持“单 Harness 自闭环”和“Planner/Reviewer 与 Executor/Fixer 分离”两种闭环模式。两种模式共享同一套 Plan、Execution、Review、FIX、Runtime 和通知协议，可以在 Copilot、Codex、Antigravity IDE 之间切换，而无需重新生成任务。
