---
name: draft-plan
description: 使用 plan-writer 自定义 Agent 和公共 create-plan Skill 生成 plan.md。若希望直接使用跨工具 Skill，请使用 /create-plan。
agent: plan-writer
argument-hint: "输入 taskId，并附上需求"
---

为 `${input:taskId:请输入 taskId}` 创建实施计划。

必须使用：

```text
.agents/skills/create-plan/SKILL.md
```

只允许生成/更新计划文件，不修改业务代码。

完成后确保：

```text
ai_docs/tasks/${input:taskId}/plan.md
```

存在且内容完整。

如 Copilot Hook 未自动通知，可执行：

```text
node .agents/scripts/workflow-notify.mjs plan-created ${input:taskId} --source copilot
```

不要自动进入实施阶段。
