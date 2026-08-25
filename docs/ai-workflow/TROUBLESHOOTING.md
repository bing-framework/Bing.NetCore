# Troubleshooting

## 先运行

```bash
node .agents/scripts/workflow-doctor.mjs
```

## 飞书测试

Dry run：

```bash
node .agents/scripts/notify-feishu.mjs --dry-run --workspace .
```

发送测试：

```bash
node .agents/scripts/notify-feishu.mjs --send-test --workspace .
```

## 查看任务状态

```bash
node .agents/scripts/task-state.mjs status
```

## task-finish 报错

检查 execution.md 前三行是否严格：

```text
AI_EXECUTION_STATUS
AI_TASK_ID
AI_EXECUTION_FINISHED_AT
```

并确认 `AI_EXECUTION_FINISHED_AT >= current-task.startedAt`。

## Antigravity Hook 不工作

当前项目必须保持：

```text
.agents/hooks.json
```

Stop command：

```text
node ./scripts/stop-guard.mjs
```

需要诊断时临时启用：

```json
"hook-diagnostic": {
  "enabled": true
}
```

然后看：

```text
.agents/runtime/hooks.log
```

诊断完成后再关闭。

## Copilot 找不到 Skill

确认：

```text
.agents/skills/execute-plan/SKILL.md
.agents/skills/fix-review/SKILL.md
```

并使用 VS Code Chat Customizations / Diagnostics 检查发现状态。

## Codex 找不到 Skill

确认从 Git 仓库内启动 Codex，并且 `.agents/skills` 位于当前目录到仓库根目录的扫描链上。

必要时重启 Codex。


## Copilot 每一步完成但不通知

先确认：

```text
.github/hooks/ai-workflow.json
```

被 VS Code 加载。

执行：

```text
/hooks
```

或者：

```text
Chat: Configure Hooks
```

并查看：

```text
Developer: Show Agent Debug Logs
```

确认 `UserPromptSubmit` / `Stop` 都有执行。

再检查：

```dotenv
AI_WORKFLOW_NOTIFY=true
FEISHU_ENABLED=true
FEISHU_WEBHOOK_URL=...
```

手工测试：

```bash
node .agents/scripts/notify-feishu.mjs --send-test --workspace .
```

Plan 测试：

```bash
node .agents/scripts/workflow-notify.mjs plan-created <taskId> --source copilot --force
```

Review 测试：

```bash
node .agents/scripts/workflow-notify.mjs review-completed <taskId> --source copilot --force
```

## SHOULD_FIX 没有执行

查看：

```bash
node .agents/scripts/task-state.mjs status
```

Review Fix 应看到：

```json
"fixScope": "recommended"
```

如果为：

```text
must
```

重新：

```bash
node .agents/scripts/task-state.mjs review-fix <taskId> --source copilot --fix-scope recommended
```
