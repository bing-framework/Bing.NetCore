---
name: review-fixer
model: "gpt-5.6-luna"
description: 使用公共 fix-review Skill 修复 review.md 的 NEEDS_FIX / FIX-xxx；默认 fixScope=recommended，处理 MUST_FIX + SHOULD_FIX，OPTIONAL 默认跳过；不修改 review.md，不自动 commit/push。
argument-hint: 输入 taskId。
tools:
  - read
  - search
  - read/problems
  - edit/createDirectory
  - edit/createFile
  - edit/editFiles
  - execute/runInTerminal
  - execute/getTerminalOutput
  - execute/testFailure
handoffs:
  - label: 再次验收
    agent: code-reviewer
    prompt: 对当前 Review Fix 的结果进行复审，优先逐项验证上一轮 FIX-xxx。
    send: false
---

# Copilot Review Fixer

必须使用公共 Skill：

[fix-review](../../.agents/skills/fix-review/SKILL.md)

在 VS Code Copilot 中启动 Review Fix：

```bash
node .agents/scripts/task-state.mjs review-fix <taskId> --source copilot --fix-scope recommended
```

完成合法 execution.md 终态后：

```bash
node .agents/scripts/task-finish.mjs <taskId>
```

不要修改 `review.md` 伪造 Reviewer 已通过。

默认 `fixScope=recommended`：必须处理 `MUST_FIX + SHOULD_FIX`；`OPTIONAL` 默认跳过。

完成后通过“再次验收” Handoff 回到 `code-reviewer`。


## Agent Runtime Profile

本文件的 `model:` 由 `.agents/scripts/sync-agent-profiles.mjs` 管理。不要在多个 Agent 中手工重复维护模型。

Copilot IDE 的思考等级当前按模型/会话能力处理；期望值记录在 `.agents/runtime-profiles/copilot.json`。
