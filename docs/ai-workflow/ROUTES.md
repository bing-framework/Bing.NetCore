# 工作流程闭环路线

通用 Agent 工作流 V4.4 支持两条闭环线路，并统一使用四个公共 Skill：

> 本文是跨 Harness 的协作协议与历史资产说明，不是产品实现保证；实际执行以当前 Skill、脚本和源码为准。

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
创建计划
      ↓
执行
      ↓
审查
      ├─ PASS → 完成
      └─ NEEDS_FIX
             ↓
            修复
             ↓
          重新审查
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

Codex 可按角色配置 `executionMode=goal`，复杂计划 / 执行 / 审查 / 修复都可以使用 Goal 目标推理。

## 路线二：Copilot / Codex + Antigravity

职责：

```text
Copilot / Codex
= 规划者 + 审查者

Antigravity
= 执行者 + 修复者
```

闭环：

```text
Copilot / Codex
创建计划
      ↓
Antigravity Goal
执行
      ↓
Copilot / Codex
审查
      ↓
NEEDS_FIX
      ↓
Antigravity Goal
修复
      ↓
Copilot / Codex
重新审查
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
- MUST_FIX + SHOULD_FIX 默认进入审查修复；
- OPTIONAL 默认不自动修；
- 不自动 commit/push/PR；
- 规划者 / 审查者与执行者 / 修复者可以来自不同 Harness；
- Goal 只控制自主执行深度，不改变 Skill 的任务边界。
