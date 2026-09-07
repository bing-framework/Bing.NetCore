# Goal / 目标推理

## 定位

Goal 不是第五个业务阶段。

公共业务阶段仍然只有：

```text
create-plan
execute-plan
review-code
fix-review
```

Goal 是执行模式：

```text
Skill
→ 定义做什么、边界、输入、输出和完成条件

Goal
→ 持续目标推理、自主执行、验证和纠错，直到当前阶段完成
```

## 支持 Harness

V4.4 的配置模型：

```text
Copilot      executionMode=agent
Codex        executionMode=goal
Antigravity  Executor/Fixer 默认 executionMode=goal
```

实际能力以当前安装环境为准。

## 标准目标生成

```bash
node .agents/scripts/goal-task.mjs create-plan task-001 --harness codex
node .agents/scripts/goal-task.mjs execute-plan task-001 --harness codex
node .agents/scripts/goal-task.mjs review-code task-001 --harness codex
node .agents/scripts/goal-task.mjs fix-review task-001 --harness codex
```

也支持：

```bash
--harness antigravity
--profile quality
--json
```

## Goal 安全策略

读取：

```text
.agents/workflow-policy.json
```

包括：

```text
maxRuntimeMinutes
maxFixAttemptsPerItem
stopOnNoProgress
maxConsecutiveNoProgress
fixScope
Git 安全边界
```

如果连续没有有效进展，应进入 BLOCKED，而不是无限消耗 Token。

## Review/Fix 闭环

```text
review-code
   ↓
NEEDS_FIX
   ↓
fix-review Goal
   ↓
review-code Goal
   ↓
PASS
```

默认最大 Review Round：

```text
5
```

默认 fixScope：

```text
recommended
= MUST_FIX + SHOULD_FIX
```
