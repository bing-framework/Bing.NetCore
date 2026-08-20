# 任务文件与状态协议

## plan.md

Planner 的实施契约。

Executor 不修改 Checkbox 伪造进度。

## execution.md 执行中

前三行：

```text
<!-- AI_EXECUTION_STATUS: IN_PROGRESS -->
AI_TASK_ID: <taskId>
AI_EXECUTION_STARTED_AT: <ISO-8601>
```

## execution.md 最终

```text
<!-- AI_EXECUTION_STATUS: COMPLETED|PARTIAL|BLOCKED|FAILED -->
AI_TASK_ID: <taskId>
AI_EXECUTION_FINISHED_AT: <ISO-8601>
```

## review.md

前三行：

```text
<!-- AI_REVIEW_STATUS: PASS|PASS_WITH_ISSUES|NEEDS_FIX|BLOCKED -->
AI_TASK_ID: <taskId>
AI_REVIEWED_AT: <ISO-8601>
```

## NEEDS_FIX

Reviewer 生成：

```text
FIX-001
FIX-002
```

每项包含：

```text
严重程度：BLOCKER/HIGH/MEDIUM/LOW
处理要求：MUST_FIX/SHOULD_FIX/OPTIONAL
```

Fixer 默认只自动处理 MUST_FIX。

## runtime

```text
.agents/runtime/current-task.json
```

关键字段：

```json
{
  "active": true,
  "taskId": "...",
  "mode": "plan-execution|review-fix",
  "agentSource": "copilot|antigravity|codex",
  "reviewRound": 0,
  "status": "implementing"
}
```

## Finalize

合法终态形成后：

```bash
node .agents/scripts/task-finish.mjs <taskId>
```

使 runtime 收口并通知。
