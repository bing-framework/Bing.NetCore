# Universal Agent Workflow V4.4 快速开始

## 1. 初始化 Agent Profile

首次：

```bash
node .agents/scripts/init-agent-profiles.mjs
```

后续同步：

```bash
node .agents/scripts/sync-agent-profiles.mjs balanced
```

## 2. 四个公共 Skill

```text
create-plan
execute-plan
review-code
fix-review
```

### Copilot

```text
/create-plan
/execute-plan
/review-code
/fix-review
```

严格 Custom Agent Adapter：

```text
/draft-plan
/run-plan
/inspect-code
/repair-review
```

### Codex

```text
$create-plan
$execute-plan
$review-code
$fix-review
```

### Antigravity

```text
/create-plan
/execute-plan
/review-code
/fix-review
```

## 3. Goal

Codex：

```bash
node .agents/scripts/goal-task.mjs create-plan <taskId> --harness codex
node .agents/scripts/goal-task.mjs execute-plan <taskId> --harness codex
node .agents/scripts/goal-task.mjs review-code <taskId> --harness codex
node .agents/scripts/goal-task.mjs fix-review <taskId> --harness codex
```

Antigravity：

```bash
node .agents/scripts/goal-task.mjs execute-plan <taskId> --harness antigravity
node .agents/scripts/goal-task.mjs fix-review <taskId> --harness antigravity
```

## 4. Review Fix 默认范围

```text
fixScope=recommended

MUST_FIX      ✅
SHOULD_FIX    ✅
OPTIONAL      ⏭
```

## 5. 两条路线

路线一：

```text
Copilot / Codex
Plan → Execute → Review → Fix → Re-review
```

路线二：

```text
Copilot / Codex：Plan + Review
Antigravity：Execute + Fix
```

详情：

```text
docs/ai-workflow/ROUTES.md
docs/ai-workflow/GOAL.md
```

## 6. 自检

```bash
node .agents/scripts/workflow-doctor.mjs
```
