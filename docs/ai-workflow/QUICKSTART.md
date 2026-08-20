# Quick Start

## 1. 安装

把本包内容覆盖/合并到项目根目录。

保留你自己的：

```text
.agents/.env.local
```

不要把真实 Webhook/Secret 提交 Git。

将：

```text
templates/gitignore.snippet
```

合并到项目 `.gitignore`。

## 2. 自检

```bash
node .agents/scripts/workflow-doctor.mjs
```

## 3. Copilot

生成计划：

```text
/create-plan
```

执行计划有两种方式：

### 便捷/跨工具 Skill

```text
/execute-plan
```

### 严格自定义 Agent

```text
/run-plan
```

Review：

```text
/review-plan
```

NEEDS_FIX 后：

```text
/fix-review
```

或严格 Agent：

```text
/repair-review
```

## 4. Antigravity

```text
/execute-plan <taskId>
```

Review 后若 NEEDS_FIX：

```text
/fix-review <taskId>
```

## 5. Codex

```text
$execute-plan
```

告诉它：

```text
taskId=fund-analysis-v2-convergence
```

Review NEEDS_FIX 后：

```text
$fix-review
```

## 6. 任务结束

所有执行 Harness 都遵守：

```text
execution.md 合法终态
→ task-finish.mjs
→ current-task inactive
→ 飞书（如启用）
```

Antigravity Stop Hook 额外承担提前停止保护。
