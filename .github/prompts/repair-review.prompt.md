---
name: repair-review
description: 使用 Copilot review-fixer 自定义 Agent 修复 NEEDS_FIX。若希望直接使用跨工具 Skill，也可以使用 /fix-review。
agent: review-fixer
argument-hint: "输入 taskId"
---

修复：

`${input:taskId:请输入 taskId}`

必须使用：

`.agents/skills/fix-review/SKILL.md`

Copilot Review Fix 注册：

```text
node .agents/scripts/task-state.mjs review-fix ${input:taskId} --source copilot
```

只处理 MUST_FIX 及必要依赖。

不要修改 review.md。

完成后形成合法 execution.md 终态并执行：

```text
node .agents/scripts/task-finish.mjs ${input:taskId}
```

随后重新 `/review-plan`。
