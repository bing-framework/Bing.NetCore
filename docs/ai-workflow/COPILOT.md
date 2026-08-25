# Copilot V4.1 使用说明

## 1. Copilot Hook

新增：

```text
.github/hooks/ai-workflow.json
```

VS Code Workspace Hook 使用：

```json
{
  "hooks": {
    "UserPromptSubmit": [...],
    "Stop": [...]
  }
}
```

它和 Antigravity `.agents/hooks.json` 是两套不同的 Harness Adapter。

## 2. UserPromptSubmit

当提交：

```text
/create-plan
/review-plan
/run-plan
/execute-plan
/repair-review
/fix-review
```

Hook 会写入：

```text
.agents/runtime/copilot-stage.json
```

用于记录当前阶段。

## 3. Stop

Copilot Stop Hook 会：

### Plan

检测新的 `plan.md`，调用：

```bash
workflow-notify.mjs plan-created
```

### Execute / Fix

如果 runtime 仍：

```text
active=true
execution.md=IN_PROGRESS
```

第一次 Stop 会返回：

```text
decision=block
```

要求 Agent 继续。

如果 execution.md 已合法终态：

```text
task-finish.mjs
```

兜底 Finalize + 飞书。

### Review

检测新的 `review.md`，调用：

```bash
workflow-notify.mjs review-completed
```

## 4. 防止无限循环

VS Code Stop Hook 会检查：

```text
stop_hook_active
```

如果已经因为 Stop Hook 续跑过一次，则不会再次无限 block。

## 5. 通知幂等

`workflow-notify.mjs` 使用：

```text
.agents/runtime/notification-ledger.json
```

去重。

因此 Prompt 显式通知和 Stop Hook 兜底同时存在，也不会重复发同一阶段消息。

## 6. 调试 Hook

VS Code 可使用：

```text
Developer: Show Agent Debug Logs
```

查看 Hook 输入/输出。

也可：

```text
/hooks
```

检查 Hook 是否被发现。

## 7. 修复范围

默认：

```text
fixScope=recommended
```

修：

```text
MUST_FIX + SHOULD_FIX
```

不修：

```text
OPTIONAL
```


## V4.2 Agent Profile

Copilot 自定义 Agent 的 `model:` 由：

```bash
node .agents/scripts/sync-agent-profiles.mjs <profile> --target copilot
```

统一维护。

不要再逐个 Agent 手工改 model。

Copilot effort 当前按 `remembered` 模式记录期望值，见：

```text
.agents/runtime-profiles/copilot.json
```
