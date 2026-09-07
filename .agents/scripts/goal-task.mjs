import {
  existsSync,
  readFileSync,
} from 'node:fs';
import path from 'node:path';
import process from 'node:process';

const STAGES = {
  'create-plan': {
    role: 'plan-writer',
    skill: 'create-plan',
    artifact: 'plan.md',
    title: '创建实施计划',
  },
  'execute-plan': {
    role: 'plan-executor',
    skill: 'execute-plan',
    artifact: 'execution.md',
    title: '执行实施计划',
  },
  'review-code': {
    role: 'code-reviewer',
    skill: 'review-code',
    artifact: 'review.md',
    title: '独立代码审查',
  },
  'fix-review': {
    role: 'review-fixer',
    skill: 'fix-review',
    artifact: 'execution.md',
    title: '修复 Review',
  },
};

function option(name) {
  const index = process.argv.indexOf(name);
  return index >= 0 ? process.argv[index + 1] || null : null;
}
function flag(name) { return process.argv.includes(name); }

function root() {
  const explicit = option('--workspace');
  if (explicit) return path.resolve(explicit);
  let current = path.resolve(process.cwd());
  if (path.basename(current).toLowerCase() === '.agents') current = path.dirname(current);
  for (;;) {
    if (existsSync(path.join(current, '.agents'))) return current;
    const parent = path.dirname(current);
    if (parent === current) break;
    current = parent;
  }
  throw new Error('无法定位包含 .agents 的项目根目录。');
}

function json(file) {
  return JSON.parse(readFileSync(file, 'utf8'));
}

function assertId(taskId) {
  if (!/^[A-Za-z0-9._-]+$/u.test(taskId || '')) {
    throw new Error(`taskId 无效：${taskId || '<empty>'}`);
  }
}

function render(stageName, taskId, harness, profileName, roleConfig, policy) {
  const stage = STAGES[stageName];
  const taskDir = `ai_docs/tasks/${taskId}`;
  const shared = policy.goal?.shared || {};
  const stagePolicy = policy.goal?.[stageName] || {};
  const lines = [
    `目标：${stage.title}（taskId=${taskId}）`,
    '',
    `必须使用公共 Skill：.agents/skills/${stage.skill}/SKILL.md`,
    `当前 Harness：${harness}`,
    `当前 Profile：${profileName}`,
    `角色：${stage.role}`,
    `模型策略：${Array.isArray(roleConfig.model) ? roleConfig.model.join(' -> ') : roleConfig.model || 'inherit'}`,
    `思考等级：${roleConfig.effort || 'inherit'}`,
    `执行模式：${roleConfig.executionMode || 'goal'}`,
    '',
    '总原则：',
    '- 持续推理和执行，直到本阶段满足完成条件，或出现明确 BLOCKED/FAILED。',
    '- Goal 只增加自主执行时长，不改变 Skill 的职责、范围和禁止项。',
    '- 不自动 git add / commit / push，不自动创建 PR。',
    '- 保护用户已有未提交修改。',
    '- 以真实源码、真实调用链和实际验证为依据，不把接口/骨架/占位当作完成。',
  ];

  if (stageName === 'create-plan') {
    lines.push(
      '',
      '阶段目标：',
      `- 生成完整 ${taskDir}/plan.md。`,
      '- 只规划，不修改业务代码、测试、配置或数据库。',
      '- 主动补齐与需求直接相关的源码、测试、配置和文档证据。',
      '- plan.md 必须可直接交给 Executor 执行。',
    );
  } else if (stageName === 'execute-plan') {
    lines.push(
      '',
      '阶段目标：',
      `- 严格读取 ${taskDir}/plan.md，并按计划完成真实实现。`,
      '- plan.md 只读，不重新规划整体方案。',
      '- 持续执行适用测试、Build、Lint、Typecheck/专项验证并修复本任务引入的问题。',
      `- 最终生成合法 ${taskDir}/execution.md 终态并运行 task-finish.mjs。`,
    );
  } else if (stageName === 'review-code') {
    lines.push(
      '',
      '阶段目标：',
      `- 读取 ${taskDir}/plan.md、execution.md、Git Diff 和真实源码。`,
      '- 独立 Review，不修改业务代码和测试。',
      '- 未解决 MUST_FIX 或 SHOULD_FIX 时必须 NEEDS_FIX。',
      '- 只剩 OPTIONAL 时才允许 PASS_WITH_ISSUES。',
      `- 最终生成合法 ${taskDir}/review.md。`,
    );
  } else {
    const scope = stagePolicy.fixScope || 'recommended';
    lines.push(
      '',
      '阶段目标：',
      `- 读取 ${taskDir}/review.md。`,
      `- fixScope=${scope}。默认处理全部 MUST_FIX + SHOULD_FIX；OPTIONAL 默认跳过。`,
      '- 不修改 review.md 伪造通过。',
      '- 对每个纳入范围的 FIX 持续修复并执行必要回归验证。',
      `- 最终更新 ${taskDir}/execution.md 为合法终态并运行 task-finish.mjs。`,
    );
  }

  lines.push(
    '',
    '安全上限：',
    `- maxRuntimeMinutes=${stagePolicy.maxRuntimeMinutes ?? '未配置'}`,
    `- stopOnNoProgress=${shared.stopOnNoProgress !== false}`,
    `- maxConsecutiveNoProgress=${shared.maxConsecutiveNoProgress ?? 2}`,
  );

  if (stageName === 'fix-review') {
    lines.push(`- maxFixAttemptsPerItem=${stagePolicy.maxFixAttemptsPerItem ?? 3}`);
  }

  lines.push(
    '',
    '停止条件：',
    '- 阶段完成并成功写入要求的 Artifact；',
    '- 存在必须人工决策的 BLOCKED；',
    '- 连续无有效进展达到策略上限；',
    '- 环境/依赖故障导致无法继续并已记录 FAILED。',
  );

  return lines.join('\n');
}

function main() {
  const stageName = String(process.argv[2] || '').trim();
  const taskId = String(process.argv[3] || '').trim();
  if (!STAGES[stageName]) {
    throw new Error(`阶段无效：${stageName || '<empty>'}。允许：${Object.keys(STAGES).join(', ')}`);
  }
  assertId(taskId);

  const workspace = root();
  const harness = (option('--harness') || 'codex').toLowerCase();
  if (!['copilot', 'codex', 'antigravity'].includes(harness)) {
    throw new Error('--harness 只允许 copilot / codex / antigravity');
  }

  const profiles = json(path.join(workspace, '.agents', 'agent-profiles.json'));
  const profileName = option('--profile') || profiles.defaultProfile || 'balanced';
  const profile = profiles.profiles?.[profileName];
  if (!profile) throw new Error(`未知 Profile：${profileName}`);

  const stage = STAGES[stageName];
  const roleConfig = profile.roles?.[stage.role]?.[harness];
  if (!roleConfig) {
    throw new Error(`Profile ${profileName} 缺少 ${stage.role}/${harness} 配置。`);
  }

  const policy = json(path.join(workspace, '.agents', 'workflow-policy.json'));
  const text = render(stageName, taskId, harness, profileName, roleConfig, policy);

  if (flag('--json')) {
    process.stdout.write(`${JSON.stringify({
      stage: stageName,
      taskId,
      harness,
      profile: profileName,
      role: stage.role,
      skill: stage.skill,
      model: roleConfig.model,
      effort: roleConfig.effort,
      executionMode: roleConfig.executionMode,
      goal: text,
    }, null, 2)}\n`);
  } else {
    process.stdout.write(`${text}\n`);
  }
}

try {
  main();
} catch (error) {
  console.error(`[goal-task] ${error?.stack || error}`);
  process.exitCode = 1;
}
