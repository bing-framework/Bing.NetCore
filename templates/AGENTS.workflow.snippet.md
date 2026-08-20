# Universal Agent Workflow

当用户要求执行既定计划或修复 Review 时：

- 执行计划：使用 `.agents/skills/execute-plan/SKILL.md`
- 修复 Review：使用 `.agents/skills/fix-review/SKILL.md`

默认任务文件：

```text
ai_docs/tasks/<taskId>/plan.md
ai_docs/tasks/<taskId>/execution.md
ai_docs/tasks/<taskId>/review.md
```

任务开始：

```text
node .agents/scripts/task-state.mjs ...
```

任务终态写入后：

```text
node .agents/scripts/task-finish.mjs <taskId>
```

不要自动 git commit、git push 或创建 PR。
不要修改 plan.md/review.md 伪造执行或验收状态。
