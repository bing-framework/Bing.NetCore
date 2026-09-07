# Universal Agent Workflow

公共四阶段 Skill：

- 创建计划：`.agents/skills/create-plan/SKILL.md`
- 执行计划：`.agents/skills/execute-plan/SKILL.md`
- 独立 Review：`.agents/skills/review-code/SKILL.md`
- 修复 Review：`.agents/skills/fix-review/SKILL.md`

默认任务文件：

```text
ai_docs/tasks/<taskId>/plan.md
ai_docs/tasks/<taskId>/execution.md
ai_docs/tasks/<taskId>/review.md
```

在支持 Goal / 目标推理的 Harness 中：

```bash
node .agents/scripts/goal-task.mjs <create-plan|execute-plan|review-code|fix-review> <taskId> --harness <codex|antigravity|copilot>
```

Goal 只负责持续推理/执行到阶段完成，不得绕过 Skill 的职责、边界或 Git 安全规则。

任务执行/修复开始：

```text
node .agents/scripts/task-state.mjs ...
```

execution.md 写入合法终态后：

```text
node .agents/scripts/task-finish.mjs <taskId>
```

不要自动 git commit、git push 或创建 PR。
不要修改 plan.md/review.md 伪造执行或验收状态。
