# Universal Agent Workflow V4

一套任务协议，同时服务：

- GitHub Copilot / VS Code
- Google Antigravity IDE
- OpenAI Codex

核心思想：

```text
Harness 适配层
      ↓
公共 Agent Skills
      ↓
公共 Node 状态脚本
      ↓
plan.md / execution.md / review.md
```

## 核心目录

```text
.agents/
├─ skills/
│  ├─ execute-plan/
│  │  └─ SKILL.md
│  └─ fix-review/
│     └─ SKILL.md
├─ scripts/
│  ├─ task-state.mjs
│  ├─ task-finish.mjs
│  ├─ stop-guard.mjs
│  ├─ notify-feishu.mjs
│  ├─ hook-diagnostic.mjs
│  └─ workflow-doctor.mjs
├─ workflows/                  # Antigravity Adapter
│  ├─ execute-plan.md
│  └─ fix-review.md
├─ rules/
│  └─ plan-executor.md
└─ hooks.json                  # Antigravity Stop Guard

.github/
├─ agents/                     # Copilot Adapter
│  ├─ plan-writer.agent.md
│  ├─ plan-executor.agent.md
│  ├─ code-reviewer.agent.md
│  └─ review-fixer.agent.md
└─ prompts/
   ├─ create-plan.prompt.md
   ├─ review-plan.prompt.md
   ├─ run-plan.prompt.md
   └─ repair-review.prompt.md
```

## 任务目录

```text
ai_docs/tasks/<taskId>/
├─ plan.md
├─ execution.md
└─ review.md
```

## 推荐闭环

```text
Plan
 ↓
Execute
 ↓
Review
 ├─ PASS → Done
 └─ NEEDS_FIX
       ↓
      Fix
       ↓
    Re-review
```

详细使用请查看：

- `QUICKSTART.md`
- `COPILOT.md`
- `ANTIGRAVITY.md`
- `CODEX.md`
- `PROTOCOL.md`
- `TROUBLESHOOTING.md`
