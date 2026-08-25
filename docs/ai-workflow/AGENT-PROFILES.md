# Agent Runtime Profiles 使用说明

V4.3 将 **模型选择 + 思考等级策略** 统一收敛到：

```text
.agents/agent-profiles.json
```

并新增初始化器：

```bash
node .agents/scripts/init-agent-profiles.mjs
```

## 1. 推荐使用方式

第一次接入项目时，不建议直接面对整份 JSON 手工修改。

直接执行：

```bash
node .agents/scripts/init-agent-profiles.mjs
```

初始化器会使用中文交互完成：

```text
选择默认 Profile
→ 可选读取现有 Copilot / Codex / Antigravity 模型配置
→ 可选逐角色调整 model / effort
→ 生成 agent-profiles.json
→ 可选立即同步三个 Harness
```

## 2. 非交互初始化

使用均衡模式初始化：

```bash
node .agents/scripts/init-agent-profiles.mjs \
  --preset balanced \
  --yes
```

如果已有配置并确认重新生成：

```bash
node .agents/scripts/init-agent-profiles.mjs \
  --preset balanced \
  --yes \
  --force
```

从现有下游 Agent 配置读取模型/思考等级后初始化：

```bash
node .agents/scripts/init-agent-profiles.mjs \
  --preset balanced \
  --from-existing \
  --force
```

只生成，不立即同步：

```bash
node .agents/scripts/init-agent-profiles.mjs \
  --preset balanced \
  --yes \
  --force \
  --no-sync
```

## 3. 配置职责

```text
init-agent-profiles.mjs
        ↓
首次生成 / 重新初始化
        ↓
.agents/agent-profiles.json
        ↓
后续只维护这一份
        ↓
sync-agent-profiles.mjs
        ↓
Copilot / Codex / Antigravity
```

`agent-profiles.json` 是模型与思考等级策略的 **Source of Truth**。

不要长期分别手工维护：

```text
.github/agents/*.agent.md
.codex/config.toml
.codex/agents/*.toml
.agents/agents/*/agent.md
```

否则下一次同步可能覆盖这些模型绑定配置。

## 4. 中文元数据规范

机器协议字段保持英文：

```text
fast
balanced
quality
max-quality

plan-writer
plan-executor
code-reviewer
review-fixer

copilot
codex
antigravity

model
effort
capabilities
workflowRoles
```

给人阅读的元数据统一中文：

```text
displayName
description
notes
CLI 提示
CLI 校验错误
使用说明
```

例如：

```json
{
  "balanced": {
    "displayName": "均衡模式",
    "description": "在执行质量、推理深度和模型使用成本之间取得平衡，适合作为日常开发默认配置。",
    "notes": "推荐作为默认 Profile。"
  }
}
```

## 5. 内置 Profile

| ID | 中文名称 | 用途 |
|---|---|---|
| `fast` | 快速模式 | 简单改动、低风险维护 |
| `balanced` | 均衡模式 | 日常开发默认 |
| `quality` | 质量优先 | 复杂实现、重构、关键 Review |
| `max-quality` | 最高质量 | 架构设计、复杂根因分析、关键代码审查 |

查看：

```bash
node .agents/scripts/sync-agent-profiles.mjs --list
```

## 6. 同步

预览：

```bash
node .agents/scripts/sync-agent-profiles.mjs balanced --dry-run
```

正式同步三边：

```bash
node .agents/scripts/sync-agent-profiles.mjs balanced
```

只同步 Copilot：

```bash
node .agents/scripts/sync-agent-profiles.mjs balanced --target copilot
```

只同步 Codex：

```bash
node .agents/scripts/sync-agent-profiles.mjs balanced --target codex
```

只同步 Antigravity：

```bash
node .agents/scripts/sync-agent-profiles.mjs balanced --target antigravity
```

## 7. 从现有配置导入

`--from-existing` 会尽力读取：

```text
Copilot
.github/agents/<role>.agent.md
→ model

Codex
.codex/agents/<role>.toml
→ model
→ model_reasoning_effort

Antigravity
.agents/agents/<role>/agent.md
→ model
→ 已生成说明中的期望思考等级
```

导入结果只覆盖当前选择的默认 Profile，不会尝试猜测其他 Profile 应该如何配置。

## 8. 平台差异

### Copilot

同步 Agent `model`。

思考等级保存在 Profile 中作为期望值；当前 IDE 仍按模型/会话能力应用。

### Codex

同步：

```toml
model = "..."
model_reasoning_effort = "high"
```

模型和思考等级都可按角色原生应用。

### Antigravity

同步 Custom Agent `model`。

Profile 中保留期望 `effort`，但不会写入未经当前版本确认的 Agent frontmatter 字段。

## 9. 两条工作路线

Profile 与工作路线是两个维度：

```text
Route
= 哪个 Harness 负责哪个阶段

Profile
= 每个 Role 在某个 Harness 下使用什么模型/effort
```

因此同一个 `balanced` / `quality` 可以用于：

```text
路线一：Copilot / Codex 自闭环
```

也可以用于：

```text
路线二：Copilot / Codex Plan + Review，Antigravity Execute + Fix
```

详见 `ROUTES.md`。
