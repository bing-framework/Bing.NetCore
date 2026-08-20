---
name: review-plan
description: 对照实施计划、实际代码、Git 变更和验证结果独立验收，生成机器可消费的 review.md；NEEDS_FIX 时输出 FIX-xxx 修复任务供 Antigravity 继续处理；不自动修代码。
agent: code-reviewer
argument-hint: "输入 taskId；如 plan/execution/review 文件不在默认目录，请在命令后明确路径"
---

# 验收实施计划执行结果

请使用 `code-reviewer` 的全部规则，对本次计划实施结果进行独立 Review。

## 1. 任务标识

Task ID：

`${input:taskId:请输入任务编号，例如 feature-name-p1}`

默认任务目录：

`ai_docs/tasks/${input:taskId}/`

默认文件：

- 计划：`ai_docs/tasks/${input:taskId}/plan.md`
- 执行报告：`ai_docs/tasks/${input:taskId}/execution.md`
- Review 输出：`ai_docs/tasks/${input:taskId}/review.md`

### 路径覆盖规则

如果用户在执行 `/review-plan` 时明确给出其他 `plan.md`、`execution.md` 或 `review.md` 路径，则以用户明确路径为准。

如果 `execution.md` 不存在：

- 不要直接失败；
- 继续以 `plan.md`、当前源码、Git Diff 和实际验证结果为主完成 Review；
- 在报告中标记“缺少执行报告”。

如果 `plan.md` 不存在或无法读取，则无法完成基于计划的正式验收，应给出 `BLOCKED` 并说明原因；不要自行编造计划。

## 2. Review 输入

必须综合：

1. `plan.md`；
2. `execution.md`（如存在）；
3. 当前工作区和暂存区 Git 变更；
4. 当前源码、配置、测试和文档；
5. 仓库 `AGENTS.md` / Copilot instructions；
6. 与本任务相关的设计/架构规范；
7. 实际执行的构建、测试、Lint、类型检查等结果；
8. 用户执行 `/review-plan` 时附加的额外验收要求。

不得只根据 `execution.md` 的“完成”声明判定通过。

## 3. 先确定 Review 边界

Review 前先回答：

- 本次计划期望修改哪些模块？
- 当前 Git 变更是否都属于本任务？
- 是否存在与本任务无关的预先已有改动？
- 是否存在计划要求的变更没有出现在 Diff 中？
- 是否存在 Diff 中出现但计划没有说明的行为变化？

无法确认某项改动归属时，标记为风险，不要擅自删除或修改。

## 4. 逐项验收计划

将主要 Phase / Task 形成验收矩阵，使用：

- `PASS`
- `PARTIAL`
- `FAIL`
- `DEVIATED_OK`
- `NOT_VERIFIABLE`

逐项给出实际证据。

特别检查：

- 是否真正接入主流程；
- 是否只有 API/类型/空实现；
- 是否引入重复或兼容性 API；
- 是否存在第二套实现；
- 是否完成计划要求的可见性收敛（如 `internal`）；
- 是否存在大文件、大接口、目录/命名空间不合理；
- 是否有性能和资源退化；
- 测试是否真正覆盖关键行为；
- 文档是否与代码一致。

## 5. Git 与验证

优先使用只读 Git 检查，不修改用户工作区状态。

验证命令必须从仓库真实配置发现，禁止臆造。

按任务风险选择适用验证：

1. 相关单元测试；
2. 类型/编译检查；
3. Lint；
4. 格式检查（只检查）；
5. Build；
6. 集成测试；
7. Smoke Test；
8. 任务要求的专项验证。

不要运行会自动修改源码的 fix/format 命令。

验证失败时，只记录和分析，不修复代码。

## 6. 问题分级

所有问题按：

- `BLOCKER`
- `HIGH`
- `MEDIUM`
- `LOW`

分类。

每个 BLOCKER/HIGH 必须给出足够具体的修复方向和修复后验证方式，使后续实现 Agent 可以直接消费。

## 7. 最终结论

必须且只能给出一个：

- `PASS`
- `PASS_WITH_ISSUES`
- `NEEDS_FIX`
- `BLOCKED`

如果为 `NEEDS_FIX`：

- 在 `review.md` 中生成明确的 `FIX-xxx` 修复清单；
- 每项包含问题、目标、影响范围、建议方向、验收方法；
- 不要自动进入修复。

## 8. 输出文件

最终完整验收报告必须实际写入最终 `review.md` 路径。

`review.md` 至少包含：

- 验收摘要和最终结论；
- 计划逐项验收矩阵；
- Git 变更分析；
- 功能/真实接入 Review；
- API/契约 Review；
- 架构/维护性 Review；
- 性能/资源 Review；
- 测试 Review；
- 文档 Review；
- BLOCKER/HIGH/MEDIUM/LOW 问题；
- 未完成/偏离项；
- 回归与兼容风险；
- NEEDS_FIX 时的修复清单；
- 最终验收 Checklist。

不要只在 Chat 中输出结果。

## 9. 严格限制

本任务是 Review，不是实现。

禁止：

- 修改业务代码；
- 修改测试代码；
- 修改 `plan.md` / `execution.md`；
- 自动修 Bug；
- 自动重构；
- 自动格式化写入；
- 数据库迁移或数据修改；
- `git add` / `git commit` / `git push`；
- 创建 PR。

## 10. 完成判定

只有在完成证据收集、逐项验收、必要验证，并成功写入 `review.md` 后才能结束。

完成后停止，不要自动进入修复阶段。


## 11. Review 状态标记

`review.md` 必须以前三行严格机器元数据开头。

第一行必须与最终结论对应：

```html
<!-- AI_REVIEW_STATUS: PASS -->
```

或：

```html
<!-- AI_REVIEW_STATUS: PASS_WITH_ISSUES -->
```

或：

```html
<!-- AI_REVIEW_STATUS: NEEDS_FIX -->
```

或：

```html
<!-- AI_REVIEW_STATUS: BLOCKED -->
```

第二、三行必须紧跟：

```text
AI_TASK_ID: ${input:taskId}
AI_REVIEWED_AT: <当前 ISO-8601 时间>
```

完整示例：

```text
<!-- AI_REVIEW_STATUS: NEEDS_FIX -->
AI_TASK_ID: ${input:taskId}
AI_REVIEWED_AT: 2026-08-19T19:00:00+08:00
```

这是 Antigravity Review Fix 工作流读取的严格机器协议，不得遗漏或修改拼写。

## 12. NEEDS_FIX 的 FIX-xxx 输出

当结论为 `NEEDS_FIX`：

每个需要 Executor 继续处理的问题必须形成唯一修复任务：

```text
FIX-001
FIX-002
...
```

每个 FIX 必须包含：

- 严重程度：`BLOCKER/HIGH/MEDIUM/LOW`
- 处理要求：`MUST_FIX/SHOULD_FIX/OPTIONAL`
- 当前状态：`OPEN`
- 对应计划项
- 涉及文件/符号
- 问题
- 证据
- 影响
- 修复目标
- 明确修复要求
- 修复后的验证方式

如果存在任意 `MUST_FIX`，最终状态必须保持 `NEEDS_FIX`。

`PASS_WITH_ISSUES` 不得包含未解决的 `MUST_FIX`。

## 13. 复审识别

如果本次任务目录已经存在旧 `review.md`，并且 `execution.md` 中存在 Review 修复记录：

本次视为复审。

在覆盖/更新当前 `review.md` 前，先读取旧 Review 内容，并优先验证上一轮 FIX。

对每个上一轮 FIX 标记：

- `RESOLVED`
- `PARTIAL`
- `NOT_RESOLVED`
- `REGRESSED`

只有发现新的 BLOCKER/HIGH、与本轮修复直接相关的回归，或之前确实漏验的关键计划目标，才新增 FIX。

不要在每轮复审中持续追加纯风格或非必要优化意见导致无限 Review。

## 14. 与 Antigravity 的交接

当结论为 `NEEDS_FIX`：

不要自动修代码。

确保 `review.md` 足够明确，使后续可以直接执行：

```text
/fix-review <taskId>
```

Antigravity 将以：

```text
plan.md      = 原始设计契约
review.md    = 本轮修复任务契约
execution.md = Executor 实施记录
Git Diff     = 真实变更
```

作为修复输入。


## 15. Review 后续路由

- `PASS`：结束。
- `PASS_WITH_ISSUES`：默认结束，由用户决定是否继续。
- `BLOCKED`：说明阻塞，不自动修。
- `NEEDS_FIX`：生成 FIX-xxx 后停止。

NEEDS_FIX 后可选择：

- Copilot Skill：`/fix-review`
- Copilot 严格角色入口：`/repair-review`
- Antigravity：`/fix-review <taskId>`
- Codex：`$fix-review`
