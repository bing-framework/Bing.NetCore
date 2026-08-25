# Universal Agent Workflow V4.3 快速开始

## Agent Profile 首次初始化

首次使用模型/思考等级 Profile 时，推荐先执行：

```bash
node .agents/scripts/init-agent-profiles.mjs
```

初始化器使用中文交互生成：

```text
.agents/agent-profiles.json
```

以后主要维护这一份配置，然后执行：

```bash
node .agents/scripts/sync-agent-profiles.mjs balanced
```

如果希望自动使用模板生成并立即同步：

```bash
node .agents/scripts/init-agent-profiles.mjs --preset balanced --yes --force
```

---


## 1. 先选择工作路线

### 路线一：纯 Copilot / Codex 自闭环

```text
Copilot / Codex
Plan → Execute → Review → Fix → Re-review
```

适合日常和中小型任务，减少工具切换。

### 路线二：Copilot / Codex + Antigravity 协作闭环

```text
Copilot / Codex：Plan
        ↓
Antigravity：Execute
        ↓
Copilot / Codex：Review
        ↓
Antigravity：Fix Review
        ↓
Copilot / Codex：Re-review
```

推荐复杂、高风险、跨模块任务使用。

详细路线说明：

```text
docs/ai-workflow/ROUTES.md
```

## 2. V4.1 / V4.2 核心能力

默认 Review Fix：

```text
fixScope = recommended
```

即：

```text
MUST_FIX       ✅ 修
SHOULD_FIX     ✅ 修
OPTIONAL       ⏭ 默认不修
```

Copilot 增加确定性 Workspace Hooks：

```text
.github/hooks/ai-workflow.json
```

用于：

```text
UserPromptSubmit → 识别当前阶段
Stop             → 通知 / Finalize / 未完成保护
```

## 2. 安装

把压缩包内容合并到项目根目录。

保留真实：

```text
.agents/.env.local
```

建议：

```dotenv
AI_WORKFLOW_NOTIFY=true
FEISHU_ENABLED=true
FEISHU_MESSAGE_MODE=card
```

## 3. 自检

```bash
node .agents/scripts/workflow-doctor.mjs
```

Copilot Hook 可以通过 VS Code：

```text
Chat: Configure Hooks
```

或：

```text
/hooks
```

确认加载。

## 4. Copilot

```text
/create-plan
```

完成后：

```text
📋 Plan Created
```

执行：

```text
/execute-plan
```

或者严格自定义 Agent：

```text
/run-plan
```

完成后：

```text
✅ Implementation Completed
```

Review：

```text
/review-plan
```

完成后飞书会显示：

```text
PASS
PASS_WITH_ISSUES
NEEDS_FIX
BLOCKED
```

以及：

```text
MUST_FIX 数量
SHOULD_FIX 数量
OPTIONAL 数量
```

如果 NEEDS_FIX：

```text
/fix-review
```

或：

```text
/repair-review
```

默认：

```text
MUST_FIX + SHOULD_FIX
```

修完再次：

```text
/review-plan
```

## 5. 自定义修复范围

只修 MUST_FIX：

```bash
node .agents/scripts/task-state.mjs review-fix <taskId> --source copilot --fix-scope must
```

默认：

```bash
--fix-scope recommended
```

全部连 OPTIONAL 都修：

```bash
--fix-scope all
```

## 6. Antigravity

仍使用：

```text
/execute-plan <taskId>
/fix-review <taskId>
```

Antigravity 的 `.agents/hooks.json` 保持原 schema。

不要把 `.github/hooks/ai-workflow.json` 复制成 Antigravity Hook。

## 7. Codex

```text
$execute-plan
$fix-review
```

fix-review 默认同样是：

```text
recommended
```



## 两条路线的实际入口

### 路线一：Copilot

```text
/create-plan
→ /execute-plan 或 /run-plan
→ /review-plan
→ NEEDS_FIX 时 /fix-review 或 /repair-review
→ /review-plan
```

### 路线一：Codex

```text
创建/准备 plan.md
→ $execute-plan
→ Review
→ NEEDS_FIX 时 $fix-review
→ Re-review
```

### 路线二

```text
Copilot / Codex 创建 plan.md
→ Antigravity /execute-plan <taskId>
→ Copilot / Codex Review
→ NEEDS_FIX 时 Antigravity /fix-review <taskId>
→ Copilot / Codex Re-review
```

全程保持相同 `<taskId>`。

## V4.2：Agent Model / Effort Profile

首次安装建议：

```bash
node .agents/scripts/sync-agent-profiles.mjs balanced --dry-run
```

确认模型名符合你的 Copilot / Codex / Antigravity 环境后：

```bash
node .agents/scripts/sync-agent-profiles.mjs balanced
```

之后切换质量档位只需：

```bash
node .agents/scripts/sync-agent-profiles.mjs fast
node .agents/scripts/sync-agent-profiles.mjs balanced
node .agents/scripts/sync-agent-profiles.mjs quality
node .agents/scripts/sync-agent-profiles.mjs max-quality
```

详细见：

```text
docs/ai-workflow/AGENT-PROFILES.md
```
