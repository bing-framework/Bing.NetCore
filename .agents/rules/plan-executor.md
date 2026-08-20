# Universal Agent Workflow 执行规则

## 核心

本项目的 Plan / Execute / Review / Fix 通过任务文件传递：

```text
ai_docs/tasks/<taskId>/
├─ plan.md
├─ execution.md
└─ review.md
```

公共执行能力：

```text
.agents/skills/execute-plan/SKILL.md
.agents/skills/fix-review/SKILL.md
```

Antigravity 只是 Harness 之一，不维护第二套实施协议。

## Antigravity

执行：

```text
/execute-plan <taskId>
/fix-review <taskId>
```

时必须使用对应 Skill。

状态注册必须带：

```text
--source antigravity
```

## Stop Guard

Antigravity IDE 当前项目已验证 Hook cwd 为：

```text
<project>/.agents
```

因此 `.agents/hooks.json` 保持：

```text
node ./scripts/stop-guard.mjs
```

不要改成：

```text
node ./.agents/scripts/stop-guard.mjs
```

## 通用 Finalize

任务真正形成合法 execution.md 终态后，优先主动调用：

```text
node .agents/scripts/task-finish.mjs <taskId>
```

Stop Hook 主要负责：

1. IN_PROGRESS + model_stop 时有限续跑；
2. Agent 忘记 task-finish 时兜底收口；
3. 已经 active=false 时直接允许 Stop。

## Git

默认禁止：

- git add
- git commit
- git push
- git reset --hard
- git clean
- git restore/checkout 丢弃用户修改
- 自动 PR

## Review Fix

review.md 属于 Reviewer 独立证据。

Executor 不修改 review.md。

只在 execution.md 记录修复证据。

## 项目规则

执行前继续遵守项目真实：

- AGENTS.md
- DESIGN / ARCHITECTURE
- docs_ai / ai_docs / docs
- Build / Test / Lint / Typecheck / CI

更具体的子目录规则优先。
