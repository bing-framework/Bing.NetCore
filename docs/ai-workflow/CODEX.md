# Codex 使用说明

Codex 直接使用仓库里的：

```text
.agents/skills/execute-plan/SKILL.md
.agents/skills/fix-review/SKILL.md
```

不依赖 `.github/prompts` 或 Antigravity Workflow。

## 执行计划

在 Codex CLI / IDE 中显式调用 Skill：

```text
$execute-plan
```

并提供：

```text
taskId=fund-analysis-v2-convergence
```

要求状态注册使用：

```text
--source codex
```

## 修复 Review

```text
$fix-review
```

并提供同一个 taskId。

## Codex 项目规则

Codex 仍会读取项目 `AGENTS.md`。

本包不会覆盖你的根 `AGENTS.md`。

如希望进一步强化路由，可以把：

```text
templates/AGENTS.workflow.snippet.md
```

合并到项目已有 `AGENTS.md`。

## 飞书

Codex 结束执行后，Skill 会要求：

```bash
node .agents/scripts/task-finish.mjs <taskId>
```

因此无需 Antigravity Hook，也能发送当前项目的飞书 Card。
