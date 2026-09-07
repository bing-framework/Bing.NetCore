# Antigravity IDE 使用说明

V4.4 保留项目中已验证的 Hook 行为，并增加 Goal 目标生成器和完整四阶段 Skill。

## Hook cwd

当前项目已验证：

```text
processCwd=<project>/.agents
```

因此 `.agents/hooks.json` 保持：

```json
"command": "node ./scripts/stop-guard.mjs"
```

不要改成：

```text
node ./.agents/scripts/stop-guard.mjs
```

## 四阶段 Skill

```text
/create-plan <taskId>
/execute-plan <taskId>
/review-code <taskId>
/fix-review <taskId>
```

路线二默认仍建议：

```text
Copilot / Codex：Plan + Review
Antigravity：Execute + Fix
```

## Goal

复杂 Execute：

```bash
node .agents/scripts/goal-task.mjs execute-plan <taskId> --harness antigravity
```

复杂 Review Fix：

```bash
node .agents/scripts/goal-task.mjs fix-review <taskId> --harness antigravity
```

把输出目标交给 Antigravity 原生 Goal。

Goal 的职责是持续推理、执行、测试和修复到当前阶段完成；它不得改变 plan.md / review.md 的任务边界。

## Stop Guard

Stop Hook 继续负责：

1. `IN_PROGRESS + model_stop` → 有限继续；
2. 已写合法终态但遗漏 `task-finish` → 兜底收口/通知；
3. 已 `active=false` → 正常停止，不重复通知。

## Agent Profile

```bash
node .agents/scripts/sync-agent-profiles.mjs balanced --target antigravity
```

模型写入 Workspace Custom Agent。

思考等级仍按当前 Antigravity 会话/版本能力应用。

`executionMode` 会写入生成 Agent 的说明区，用于提示是否采用 Goal。
