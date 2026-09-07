---
description: 使用公共 create-plan Skill 创建实施计划
---

# Create Plan

1. 确定 `taskId` 和需求。
2. 使用 Workspace Skill：
   - `create-plan`
3. 完整读取项目规则、真实源码、测试、配置和与需求相关的文档。
4. 只创建/更新：
   - `ai_docs/tasks/<taskId>/plan.md`
5. 不修改业务代码、测试、配置或数据库。
6. 如果希望长目标自主分析，可先生成 Goal：
   - `node .agents/scripts/goal-task.mjs create-plan <taskId> --harness antigravity`
7. plan.md 完成后发送阶段通知。
8. 不自动进入实施，不自动 commit/push/PR。
