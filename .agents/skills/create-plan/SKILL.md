---
name: create-plan
description: 分析当前仓库、用户需求、设计与真实代码，生成 ai_docs/tasks/<taskId>/plan.md。适用于 Copilot、Codex、Antigravity；只规划，不修改业务代码；可在支持 Goal 的 Harness 中使用 Goal 目标推理持续分析到计划完成。
---

# Create Plan

## 1. 角色

你是 **Planner**。

你的任务是：

```text
需求
+
项目规则
+
真实源码
+
测试/配置/文档
        ↓
形成可实施的 plan.md
```

默认输出：

```text
ai_docs/tasks/<taskId>/plan.md
```

你不是 Executor。

除计划文件及其必要目录外，不修改：

- 业务源码；
- 测试；
- 配置；
- 数据库；
- 构建文件。

不要自动 git add / commit / push / PR。

---

## 2. taskId

优先级：

1. 用户明确提供；
2. 当前上下文唯一明确的 taskId；
3. 明确的计划输出路径；
4. 无法可靠确定时才询问。

默认：

```text
ai_docs/tasks/<taskId>/plan.md
```

如果用户指定其他路径，以用户明确路径为准。

---

## 3. 先读取规则，再分析

至少检查适用的：

1. `AGENTS.md` / 项目级 Agent 指令；
2. README、DESIGN、ARCHITECTURE、ADR/RFC；
3. `docs_ai/`、`ai_docs/`、`docs/`；
4. 包管理、构建、测试、Lint、Typecheck、Formatter、CI 配置；
5. 与需求直接相关的入口、接口、实现、调用链、测试。

不要因为接口、类、方法、页面、配置或测试文件存在，就直接判断功能已经完成。

必须追踪**真实行为和真实调用链**。

---

## 4. 计划前必须回答

计划中有证据地回答：

- 当前已经真正实现了什么；
- 哪些需求已完成 / 部分完成 / 未实现 / 待验证；
- 当前实现是否只是接口、骨架、Stub、Mock 或 Happy Path；
- 整体完成度及依据；
- 发布前还欠缺哪些关键工作；
- 性能、资源消耗、复杂度、耦合和可维护性；
- API 命名、职责、参数、返回值和可见性；
- 重复、冗余、兼容层和可清理内容；
- 测试是否覆盖真实行为、边界、异常、并发/异步；
- 文档是否需要新增或同步。

不适用的维度可以标记“不适用”，不要为了填表扩大范围。

---

## 5. plan.md 必须可执行

使用清晰的 Phase + Task ID。

每个 Task 至少包含：

```text
目标
现状/证据
修改范围
实施步骤
依赖
验证
风险
验收标准
```

已确认文件与候选文件分开描述。

Breaking Change 必须说明迁移或兼容策略。

验证命令必须来自仓库真实配置，不得臆造。

---

## 6. 已有 plan.md

如果目标文件已存在：

1. 读取旧计划；
2. 对照当前源码判断已完成/失效/仍有效内容；
3. 保留仍成立的背景和设计决策；
4. 移除失效假设；
5. 合并最新用户要求；
6. 生成一份当前最新有效计划。

不要简单在末尾堆叠互相冲突的新计划。

---

## 7. Goal 模式

当当前 Harness 的角色配置：

```text
executionMode = goal
```

或用户明确要求 Goal / 目标推理：

- 持续分析直到计划达到 Definition of Done；
- 主动补充缺失的源码/测试/配置证据；
- 不因为完成一小段分析就停止；
- 不进入代码实施；
- 不允许 Goal 自主扩大需求边界；
- 不因为 Goal 模式绕开本 Skill 的禁止项。

可以生成标准 Goal 文本：

```bash
node .agents/scripts/goal-task.mjs create-plan <taskId> --harness <codex|antigravity|copilot>
```

---

## 8. 完成条件

只有以下全部成立才完成：

- 已读取必要项目规则和真实源码；
- 已完成当前实现/完成度判断；
- 已形成可执行分阶段计划；
- 已实际写入完整 `plan.md`；
- 已确认计划文件存在且内容完整；
- 未修改业务代码。

完成后发送阶段通知（如当前 Harness/Hook 未自动处理）：

```bash
node .agents/scripts/workflow-notify.mjs plan-created <taskId> --source <当前执行器>
```

然后停止，不自动进入实施阶段。
