# Copilot 使用说明

## 两种执行方式

### 方案 A：Agent Skill

VS Code 可以直接发现 `.agents/skills`。

使用：

```text
/execute-plan
/fix-review
```

这是最便携的方式。

### 方案 B：自定义 Agent

项目提供：

```text
plan-writer
plan-executor
code-reviewer
review-fixer
```

如果希望严格控制角色和工具：

```text
/run-plan
/repair-review
```

Prompt 会自动选择对应自定义 Agent。

所有 `.agent.md` 都没有固定 `model:`，因此继续使用 VS Code 当前 Model Picker 选择的模型。

## 推荐流程

```text
/create-plan
→ /run-plan
→ /review-plan
→ NEEDS_FIX?
   → /repair-review
   → /review-plan
```

也可以全部用 Skill：

```text
/create-plan
→ /execute-plan
→ /review-plan
→ /fix-review
→ /review-plan
```

## Handoff

四个自定义 Agent 已配置 Handoff：

```text
plan-writer
  → plan-executor
  → code-reviewer
  → review-fixer
  → code-reviewer
```

`send=false`，不会自动开始下一阶段。

## Copilot Hooks

V4 不要求 Copilot Hooks 才能完成任务。

原因：

- 核心状态由 `task-state.mjs` / `task-finish.mjs` 管理；
- Hook 只属于 Harness 增强；
- VS Code Agent Hooks 仍可能随版本变化。

因此 Copilot 即使完全不配置 Hook，也能正常：

```text
execute
fix
runtime finalize
Feishu notify
```
