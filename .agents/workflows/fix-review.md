---
description: 使用公共 fix-review Skill 修复 Reviewer 的 NEEDS_FIX
---

# Fix Review

1. 确定 `taskId`。
2. 完整读取：
   - `plan.md`
   - `execution.md`
   - `review.md`
3. review.md 必须是严格：
   - `AI_REVIEW_STATUS: NEEDS_FIX`
   - `AI_TASK_ID` 一致
   - `AI_REVIEWED_AT` 有效
4. 使用 Workspace Skill：
   - `fix-review`
5. 在项目根目录执行：
   - `node .agents/scripts/task-state.mjs review-fix <taskId> --source antigravity --fix-scope recommended`
6. 确认：
   - `active=true`
   - `mode=review-fix`
   - `agentSource=antigravity`
   - `reviewRound` 已递增
7. 默认 fixScope=recommended：处理全部 MUST_FIX + SHOULD_FIX；OPTIONAL 默认跳过。
8. 不修改 review.md。
9. 所有 MUST_FIX 处理后执行专项测试、回归、Build/Typecheck/Lint、git diff --check。
10. execution.md 写入 Review 修复记录，并形成合法终态前三行。
11. 主动执行：
    - `node .agents/scripts/task-finish.mjs <taskId>`
12. 不 git add / commit / push，不自动 PR。
13. 结束后重新进行独立 Review。

Stop Hook 仍作为提前停止保护和 task-finish 遗漏兜底。
