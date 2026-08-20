---
name: run-plan
description: 使用 Copilot plan-executor 自定义 Agent 执行指定 taskId 的 plan.md。若希望直接使用跨工具 Skill，也可以使用 /execute-plan。
agent: plan-executor
argument-hint: "输入 taskId，例如 fund-analysis-v2-convergence"
---

使用 `plan-executor` 执行：

`${input:taskId:请输入 taskId}`

必须使用：

`.agents/skills/execute-plan/SKILL.md`

Copilot 状态注册：

```text
node .agents/scripts/task-state.mjs start ${input:taskId} --source copilot
```

完成后必须形成合法 execution.md 终态，并执行：

```text
node .agents/scripts/task-finish.mjs ${input:taskId}
```

不重新规划，不自动 commit/push/PR。
