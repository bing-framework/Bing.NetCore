---
description: 使用公共 review-code Skill 独立验收实现
---

# Review Code

1. 确定 `taskId`。
2. 使用 Workspace Skill：
   - `review-code`
3. 读取完整 plan.md、execution.md、Git Diff、真实源码、测试、配置和文档。
4. 只 Review，不修改业务代码/测试。
5. 如果希望长目标自主审查，可生成 Goal：
   - `node .agents/scripts/goal-task.mjs review-code <taskId> --harness antigravity`
6. 输出合法 review.md：
   - PASS / PASS_WITH_ISSUES / NEEDS_FIX / BLOCKED
7. NEEDS_FIX 必须输出 FIX-xxx。
8. 未解决 MUST_FIX / SHOULD_FIX 时不得 PASS。
9. 完成后发送 Review 通知。
