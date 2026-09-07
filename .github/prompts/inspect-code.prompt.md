---
name: inspect-code
description: 使用 code-reviewer 自定义 Agent 和公共 review-code Skill 独立验收实现，生成 review.md。若希望直接使用跨工具 Skill，请使用 /review-code。
agent: code-reviewer
argument-hint: "输入 taskId"
---

对 `${input:taskId:请输入 taskId}` 进行独立 Review。

必须使用：

```text
.agents/skills/review-code/SKILL.md
```

必须读取：

```text
plan.md
execution.md（如存在）
Git Diff
真实源码/测试/配置/文档
```

只 Review，不修改业务代码。

完成后必须生成合法：

```text
ai_docs/tasks/${input:taskId}/review.md
```

如果 Hook 未自动通知，可执行：

```text
node .agents/scripts/workflow-notify.mjs review-completed ${input:taskId} --source copilot
```

NEEDS_FIX 时停止，交给独立 Fixer。
