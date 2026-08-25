# Antigravity IDE 使用说明

本包继续基于当前项目已经验证可运行的 Hook 行为。

## Hook cwd

当前项目实测：

```text
processCwd=<project>/.agents
```

因此：

```json
"command": "node ./scripts/stop-guard.mjs"
```

保持不变。

不要改成：

```text
node ./.agents/scripts/stop-guard.mjs
```

## 执行

```text
/execute-plan <taskId>
```

Workflow 会：

```text
task-state --source antigravity
→ execute-plan Skill
→ implementation
→ execution.md
→ task-finish
→ Feishu
```

## Stop Guard

Stop Hook 现在有三层作用：

1. `IN_PROGRESS + model_stop` → 有限 continue；
2. Agent 已写终态但忘记 `task-finish` → 兜底收口/通知；
3. `task-finish` 已经收口 → active=false，直接 Stop，不重复通知。

## Review Fix

```text
/fix-review <taskId>
```

同样：

```text
task-state review-fix --source antigravity
→ fix-review Skill
→ MUST_FIX
→ execution.md
→ task-finish
```


## V4.2 Agent Profile

同步：

```bash
node .agents/scripts/sync-agent-profiles.mjs <profile> --target antigravity
```

模型写入 Workspace Custom Agent 的 `model:` frontmatter。

思考等级保留为 `session` 策略，不写入未经当前版本确认的 Agent effort 字段。
