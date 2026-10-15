# 通用 Agent 工作流 V4.4

> 本文描述的是 Agent 协作协议与历史资产，不是产品功能说明，也不构成当前产品实现的保证；实际行为以当前 Skill、脚本与源码为准。

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
│  ├─ create-plan/SKILL.md
│  ├─ execute-plan/SKILL.md
│  ├─ review-code/SKILL.md
│  └─ fix-review/SKILL.md
├─ scripts/
│  ├─ task-state.mjs
│  ├─ task-finish.mjs
│  ├─ stop-guard.mjs
│  ├─ notify-feishu.mjs
│  ├─ hook-diagnostic.mjs
│  └─ workflow-doctor.mjs
├─ workflows/                  # Antigravity 适配器
│  ├─ execute-plan.md
│  └─ fix-review.md
├─ rules/
│  └─ plan-executor.md
└─ hooks.json                  # Antigravity 停止守卫

.github/
├─ agents/                     # Copilot 适配器
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
计划
 ↓
执行
 ↓
审查
 ├─ PASS → 完成
 └─ NEEDS_FIX
       ↓
      修复
       ↓
    重新审查
```

详细使用请查看：

- `QUICKSTART.md`
- `COPILOT.md`
- `ANTIGRAVITY.md`
- `CODEX.md`
- `PROTOCOL.md`
- `TROUBLESHOOTING.md`


Agent 模型与思考等级统一配置见 `AGENT-PROFILES.md`。


## 工作路线

本工作流支持两条闭环路线：

1. 纯 Copilot / Codex 自闭环；
2. Copilot / Codex 负责计划 + 审查，Antigravity 负责执行 + 审查修复。

详见：

```text
docs/ai-workflow/ROUTES.md
```


Goal / 目标推理详见 `GOAL.md`。
