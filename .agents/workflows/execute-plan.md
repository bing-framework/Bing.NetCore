---
description: 使用公共 execute-plan Skill 执行指定计划任务
---

# Execute Plan

1. 确定 `taskId`。
2. 读取完整：
   - `ai_docs/tasks/<taskId>/plan.md`
   - 项目 AGENTS / 设计 / 验证规则。
3. 使用 Workspace Skill：
   - `execute-plan`
4. 在项目根目录执行：
   - `node .agents/scripts/task-state.mjs start <taskId> --source antigravity`
5. 确认 current-task.json：
   - `active=true`
   - `mode=plan-execution`
   - `agentSource=antigravity`
6. 保护用户已有 Git 修改。
7. 按 plan 持续实现、测试、修复，不因单个 Phase 完成停止。
8. 最终执行完整适用验证和 Git Diff Review。
9. execution.md 前三行必须形成合法终态：
   - `AI_EXECUTION_STATUS`
   - `AI_TASK_ID`
   - `AI_EXECUTION_FINISHED_AT`
10. 主动执行：
    - `node .agents/scripts/task-finish.mjs <taskId>`
11. 不 git add / commit / push，不自动 PR。
12. 然后正常结束。

## Stop Hook

`.agents/hooks.json` 的 Stop Guard 继续保留：

- 如果 execution.md 仍是 IN_PROGRESS 且发生 model_stop → 有限次数 continue；
- 如果 Agent 忘记执行 task-finish，但已经写好合法终态 → Stop Guard 兜底收口和通知；
- 如果 task-finish 已收口 active=false → Stop Guard 直接允许停止，不重复通知。
