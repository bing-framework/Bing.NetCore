---
name: fix-review
description: 根据 ai_docs/tasks/<taskId>/review.md 中 NEEDS_FIX 的结构化 FIX-xxx 项继续修复。适用于 Copilot、Antigravity、Codex；默认处理 MUST_FIX，保留 review.md 为 Reviewer 独立证据，修复记录写入 execution.md，不自动 git commit/push/PR。
---

# Fix Review

## 1. 角色

你是 **Review Fix Executor**。

输入：

```text
plan.md
execution.md
review.md
Git Diff
```

直接任务契约是：

`review.md`

原始范围边界仍由：

`plan.md`

定义。

你不是 Reviewer。

不要修改 review.md 把 `OPEN` 改成 `DONE`。

修复结果写到：

`execution.md -> Review 修复记录`

最终是否通过，必须由下一轮独立 Review 判定。

---

## 2. 前置条件

默认文件：

```text
ai_docs/tasks/<taskId>/plan.md
ai_docs/tasks/<taskId>/execution.md
ai_docs/tasks/<taskId>/review.md
```

review.md 前三行必须严格是：

```text
<!-- AI_REVIEW_STATUS: NEEDS_FIX -->
AI_TASK_ID: <taskId>
AI_REVIEWED_AT: <ISO-8601>
```

如果状态是：

- `PASS`：无需修复；
- `PASS_WITH_ISSUES`：默认不自动修；
- `BLOCKED`：先解除阻塞；
- `NEEDS_FIX`：进入本 Skill。

---

## 3. 支持 Harness

可由：

- Copilot；
- Antigravity；
- Codex；

共享使用。

注册时尽量传入当前执行器：

```bash
node .agents/scripts/task-state.mjs review-fix <taskId> --source copilot
```

或：

```bash
node .agents/scripts/task-state.mjs review-fix <taskId> --source antigravity
```

或：

```bash
node .agents/scripts/task-state.mjs review-fix <taskId> --source codex
```

---

## 4. 开始修复

### 4.1 读取规则和完整证据

必须读取：

1. 适用 `AGENTS.md` / 项目规则；
2. 完整 plan.md；
3. 完整 execution.md；
4. 完整 review.md；
5. 当前 Git Diff；
6. Review 涉及代码、测试、文档。

不能只根据聊天中的 Review 摘要修复。

### 4.2 注册 Review Fix 状态

执行：

```bash
node .agents/scripts/task-state.mjs review-fix <taskId> --source <当前执行器>
```

脚本会：

- 校验 review.md 为 NEEDS_FIX；
- 校验 AI_TASK_ID；
- 增加 `reviewRound`；
- 设置 `active=true`；
- `mode=review-fix`；
- 使用本轮新的 startedAt；
- 把旧 execution.md 终态切换为：

```text
<!-- AI_EXECUTION_STATUS: IN_PROGRESS -->
AI_TASK_ID: <taskId>
AI_EXECUTION_STARTED_AT: <ISO-8601>
```

不要使用 `PARTIAL` 表示“正在修复”。

`PARTIAL` 是真正终态。

---

## 5. FIX 处理规则

Reviewer 的每个问题应具有：

```text
FIX-001
FIX-002
...
```

及：

```text
严重程度
处理要求
问题
证据
影响
修复目标
修复要求
验证方式
```

处理要求：

```text
MUST_FIX
SHOULD_FIX
OPTIONAL
```

默认：

- `MUST_FIX`：必须处理；
- `SHOULD_FIX`：仅当与 MUST_FIX 同根因、属于必要依赖或用户明确要求时处理；
- `OPTIONAL`：不自动处理。

不要把 Reviewer 所有建议都变成无边界重构。

---

## 6. 单个 FIX 循环

对每个 MUST_FIX：

```text
读取 Review 证据
→ 检查当前代码是否已变化
→ 定位真实根因
→ 实施修复
→ 运行 FIX 指定的最小验证
→ 失败则继续修复
→ 通过
→ 记录 execution.md
→ 下一 FIX
```

如果多个 FIX 共享同一根因，可以合并代码变更，但 execution.md 必须分别说明每个 FIX 如何被覆盖。

---

## 7. 范围约束

REVIEW_FIX 禁止：

- 从头重新执行整个 plan；
- 重写已经通过 Review 的无关模块；
- 因个人风格偏好扩大改动；
- 顺手修复完全无关问题；
- 创建第二套实现；
- 修改 review.md 伪造通过。

只处理 FIX 及完成 FIX 所必需的直接依赖。

---

## 8. 测试规则

不能为了通过 Review：

- 删除失败测试；
- 注释测试；
- 降低关键断言；
- 跳过关键验证；
- 用 Mock 代替 Reviewer 要求验证的真实行为；
- 吞异常。

对每个 FIX 先跑最小验证。

全部 MUST_FIX 完成后，按项目适用性运行：

1. FIX 专项测试；
2. 相关回归测试；
3. Typecheck / Compile；
4. Lint；
5. Build；
6. Formatter Check；
7. 必要 Smoke Test；
8. `git diff --check`；
9. 最终 Diff Review。

---

## 9. execution.md Review 修复记录

保留原实施报告，在其中增加：

```markdown
## Review 修复记录

### Round N

- Review 状态：NEEDS_FIX
- Review 文件：`ai_docs/tasks/<taskId>/review.md`

#### FIX-001

- 严重程度：HIGH
- 处理要求：MUST_FIX
- 执行状态：COMPLETED
- 修改文件：
  - `...`
- 根因：
  ...
- 修复：
  ...
- 验证：
  - `...`：PASS

### Round N 汇总

- MUST_FIX：
- 已完成：
- PARTIAL：
- BLOCKED：
- FAILED：
- 回归验证：
- 下一步：重新 Review
```

---

## 10. Review Fix 终态

本轮所有 MUST_FIX 完成并必要验证通过：

```text
<!-- AI_EXECUTION_STATUS: COMPLETED -->
AI_TASK_ID: <taskId>
AI_EXECUTION_FINISHED_AT: <ISO-8601>
```

如果不能全部完成：

```text
PARTIAL
BLOCKED
FAILED
```

同样必须提供严格三行机器元数据。

这里的 `COMPLETED` 仅表示：

> Executor 已完成当前 Review 要求的修复。

不代表 Reviewer 已经 PASS。

---

## 11. 通用收口

写好终态后执行：

```bash
node .agents/scripts/task-finish.mjs <taskId>
```

它会：

- 校验终态；
- current-task.json `active=false`；
- 保存 finalStatus；
- 发送项目级飞书 Card（如启用）；
- 显示执行器和 Review Round；
- 幂等避免重复通知。

下一步始终是：

> 重新进行独立 Review。

---

## 12. Git 安全

默认禁止：

```text
git add
git commit
git push
git reset --hard
git clean
git checkout .
git restore .
自动 PR
```

保护用户已有修改。

---

## 13. 最终回复

报告：

- Task ID；
- 模式：REVIEW_FIX；
- Review Round；
- 已完成 FIX；
- 未完成 FIX；
- 测试/Build 结果；
- execution.md；
- task-finish 结果；
- 下一步：重新 Review；
- 未 commit / push。
