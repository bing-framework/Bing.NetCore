# 工作流程闭环路线

Universal Agent Workflow V4.4 支持两条闭环线路，并统一使用四个公共 Skill：

```text
create-plan
execute-plan
review-code
fix-review
```

共享任务目录：

```text
ai_docs/tasks/<taskId>/
├─ plan.md
├─ execution.md
└─ review.md
```

## 路线一：纯 Copilot / Codex 自闭环

```text
Copilot / Codex
      ↓
Create Plan
      ↓
Execute
      ↓
Review
      ├─ PASS → 完成
      └─ NEEDS_FIX
             ↓
            Fix
             ↓
          Re-review
             ↓
            PASS
```

Copilot：

```text
/create-plan
/execute-plan
/review-code
/fix-review
/review-code
```

或严格 Custom Agent Adapter：

```text
/draft-plan
/run-plan
/inspect-code
/repair-review
/inspect-code
```

Codex：

```text
$create-plan
$execute-plan
$review-code
$fix-review
$review-code
```

Codex 可按角色配置 `executionMode=goal`，复杂 Plan / Execute / Review / Fix 都可以使用 Goal 目标推理。

## 路线二：Copilot / Codex + Antigravity

职责：

```text
Copilot / Codex
= Planner + Reviewer

Antigravity
= Executor + Fixer
```

闭环：

```text
Copilot / Codex
Create Plan
      ↓
Antigravity Goal
Execute
      ↓
Copilot / Codex
Review
      ↓
NEEDS_FIX
      ↓
Antigravity Goal
Fix
      ↓
Copilot / Codex
Re-review
      ↓
PASS
```

复杂 Antigravity 阶段推荐：

```bash
node .agents/scripts/goal-task.mjs execute-plan <taskId> --harness antigravity
node .agents/scripts/goal-task.mjs fix-review <taskId> --harness antigravity
```

## 共享原则

无论哪条路线：

- taskId 不变；
- plan.md / execution.md / review.md 协议不变；
- MUST_FIX + SHOULD_FIX 默认进入 Review Fix；
- OPTIONAL 默认不自动修；
- 不自动 commit/push/PR；
- Planner / Reviewer 与 Executor / Fixer 可以来自不同 Harness；
- Goal 只控制自主执行深度，不改变 Skill 的任务边界。
