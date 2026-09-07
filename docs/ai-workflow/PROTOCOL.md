# Universal Workflow V4.1 协议

## execution.md

执行中：

```text
<!-- AI_EXECUTION_STATUS: IN_PROGRESS -->
AI_TASK_ID: <taskId>
AI_EXECUTION_STARTED_AT: <ISO-8601>
```

终态：

```text
<!-- AI_EXECUTION_STATUS: COMPLETED|PARTIAL|BLOCKED|FAILED -->
AI_TASK_ID: <taskId>
AI_EXECUTION_FINISHED_AT: <ISO-8601>
```

## review.md

```text
<!-- AI_REVIEW_STATUS: PASS|PASS_WITH_ISSUES|NEEDS_FIX|BLOCKED -->
AI_TASK_ID: <taskId>
AI_REVIEWED_AT: <ISO-8601>
```

## Fix Scope

```text
must
recommended
all
```

默认：

```text
recommended
```

映射：

```text
must
  MUST_FIX

recommended
  MUST_FIX
  SHOULD_FIX

all
  MUST_FIX
  SHOULD_FIX
  OPTIONAL
```

## Review 结论

未解决：

```text
MUST_FIX
或
SHOULD_FIX
```

则：

```text
NEEDS_FIX
```

只有 OPTIONAL 剩余时：

```text
PASS_WITH_ISSUES
```

完全通过：

```text
PASS
```

## 通知

阶段：

```text
PLAN_CREATED
REVIEW_PASS
REVIEW_PASS_WITH_ISSUES
REVIEW_NEEDS_FIX
REVIEW_BLOCKED
```

执行：

```text
COMPLETED
PARTIAL
BLOCKED
FAILED
```

项目通知总开关：

```dotenv
AI_WORKFLOW_NOTIFY=true
```

旧：

```dotenv
STOP_GUARD_NOTIFY
```

仅作为兼容 fallback。


## V4.4 公共 Skill 协议

```text
create-plan  → plan.md
execute-plan → execution.md
review-code  → review.md
fix-review   → execution.md Review 修复记录
```

Goal 是 executionMode，不新增 Artifact 协议。

Goal 仍必须遵守：

```text
execution.md / review.md 机器元数据
fixScope
Git 安全边界
workflow-policy.json
```
