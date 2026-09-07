# Codex 使用说明

V4.4 将 Codex 提升为完整闭环 Harness。

## 四个公共 Skill

Codex 可直接使用：

```text
$create-plan
$execute-plan
$review-code
$fix-review
```

对应：

```text
Plan
→ Execute
→ Review
→ Fix
→ Re-review
```

公共文件：

```text
.agents/skills/create-plan/SKILL.md
.agents/skills/execute-plan/SKILL.md
.agents/skills/review-code/SKILL.md
.agents/skills/fix-review/SKILL.md
```

## 创建计划

```text
$create-plan
taskId=<taskId>
需求：...
```

输出：

```text
ai_docs/tasks/<taskId>/plan.md
```

只规划，不实施代码。

## 执行计划

```text
$execute-plan
taskId=<taskId>
```

状态注册：

```bash
node .agents/scripts/task-state.mjs start <taskId> --source codex
```

完成后：

```bash
node .agents/scripts/task-finish.mjs <taskId>
```

## 代码 Review

```text
$review-code
taskId=<taskId>
```

输出：

```text
ai_docs/tasks/<taskId>/review.md
```

未解决 MUST_FIX 或 SHOULD_FIX 时保持 NEEDS_FIX。

## 修复 Review

```text
$fix-review
taskId=<taskId>
```

默认：

```text
fixScope=recommended
= MUST_FIX + SHOULD_FIX
```

修复完成后再次：

```text
$review-code
```

直到 PASS / PASS_WITH_ISSUES，或明确 BLOCKED。

## Goal / 目标推理

V4.4 把 Codex Goal 作为正式 executionMode。

`agent-profiles.json` 中 Codex 角色默认可配置：

```json
{
  "executionMode": "goal"
}
```

生成标准 Goal 目标：

```bash
node .agents/scripts/goal-task.mjs create-plan <taskId> --harness codex
node .agents/scripts/goal-task.mjs execute-plan <taskId> --harness codex
node .agents/scripts/goal-task.mjs review-code <taskId> --harness codex
node .agents/scripts/goal-task.mjs fix-review <taskId> --harness codex
```

将生成的目标文本交给当前 Codex 的 Goal 入口。

原则：

```text
Skill = WHAT / RULES / BOUNDARY
Goal  = AUTONOMY / HORIZON / TARGET REASONING
```

Goal 不替代 Skill。

## Agent Profile

同步：

```bash
node .agents/scripts/sync-agent-profiles.mjs balanced --target codex
```

生成：

```text
.codex/config.toml
.codex/agents/*.toml
.agents/runtime-profiles/codex.json
```

Codex 角色 TOML 会记录模型、model_reasoning_effort，并以注释记录 executionMode。
