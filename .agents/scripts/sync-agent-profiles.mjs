import {
  copyFileSync,
  existsSync,
  mkdirSync,
  readFileSync,
  renameSync,
  writeFileSync,
} from 'node:fs';
import path from 'node:path';
import process from 'node:process';

const MANAGED_BEGIN = '# BEGIN UNIVERSAL_AGENT_WORKFLOW_PROFILE';
const MANAGED_END = '# END UNIVERSAL_AGENT_WORKFLOW_PROFILE';
const GENERATED_MARKER = '<!-- UNIVERSAL_AGENT_PROFILE_GENERATED -->';

const ROLE_META = {
  'plan-writer': {
    displayName: '计划规划器',
    notes: '只负责规划，不修改业务代码。',
    codexName: 'plan_writer',
    description: '负责读取项目上下文并生成 ai_docs/tasks/<taskId>/plan.md；只规划，不实施。',
    codexInstructions:
      '你是 Planner。读取项目 AGENTS.md 和真实源码，生成 plan.md，不修改业务代码。优先遵循 .github/prompts/create-plan.prompt.md。',
    antigravityInstructions: [
      '你是 Planner。',
      '读取项目 AGENTS.md、真实源码和需求，生成 ai_docs/tasks/<taskId>/plan.md。',
      '优先遵循 .github/prompts/create-plan.prompt.md。',
      '只规划，不修改业务代码、测试、配置或数据库。',
    ],
  },
  'plan-executor': {
    displayName: '计划执行器',
    notes: '按既定计划实施，不重新规划，不自动提交或推送。',
    codexName: 'plan_executor',
    description: '负责执行既定 plan.md，完成代码、测试、execution.md 与任务收口。',
    codexInstructions:
      '你是 Implementation Executor。必须读取并遵循 .agents/skills/execute-plan/SKILL.md；执行时 source=codex；不自动 commit/push/PR。',
    antigravityInstructions: [
      '你是 Implementation Executor。',
      '必须读取并遵循 .agents/skills/execute-plan/SKILL.md。',
      'Antigravity 状态注册使用 --source antigravity。',
      '不重新规划，不自动 git commit、git push 或创建 PR。',
    ],
  },
  'code-reviewer': {
    displayName: '代码审查器',
    notes: '独立验收，不直接修改业务代码。',
    codexName: 'code_reviewer',
    description: '负责独立验收 plan/execution/Git Diff，输出结构化 review.md。',
    codexInstructions:
      '你是独立 Reviewer。优先遵循 .github/prompts/review-plan.prompt.md；不修改业务代码。未解决 MUST_FIX/SHOULD_FIX 时保持 NEEDS_FIX。',
    antigravityInstructions: [
      '你是独立 Reviewer。',
      '优先遵循 .github/prompts/review-plan.prompt.md。',
      '只审查，不修改业务代码。',
      'NEEDS_FIX 时输出结构化 FIX-xxx；未解决 MUST_FIX/SHOULD_FIX 不得判 PASS。',
    ],
  },
  'review-fixer': {
    displayName: '审查修复器',
    notes: '默认处理 MUST_FIX + SHOULD_FIX，OPTIONAL 默认跳过。',
    codexName: 'review_fixer',
    description: '负责修复 review.md 的 NEEDS_FIX；默认处理 MUST_FIX + SHOULD_FIX。',
    codexInstructions:
      '你是 Review Fix Executor。必须读取并遵循 .agents/skills/fix-review/SKILL.md；默认 fixScope=recommended；执行时 source=codex；不修改 review.md。',
    antigravityInstructions: [
      '你是 Review Fix Executor。',
      '必须读取并遵循 .agents/skills/fix-review/SKILL.md。',
      '默认 fixScope=recommended，处理 MUST_FIX + SHOULD_FIX。',
      'Antigravity 状态注册使用 --source antigravity；不修改 review.md。',
    ],
  },
};

function readOption(name) {
  const index = process.argv.indexOf(name);
  if (index >= 0 && process.argv[index + 1]) {
    return process.argv[index + 1];
  }
  return null;
}

function hasFlag(name) {
  return process.argv.includes(name);
}

function resolveWorkspaceRoot() {
  const explicit = readOption('--workspace');
  if (explicit) return path.resolve(explicit);

  let current = path.resolve(process.cwd());
  if (path.basename(current).toLowerCase() === '.agents') {
    current = path.dirname(current);
  }

  for (;;) {
    if (existsSync(path.join(current, '.agents'))) return current;
    const parent = path.dirname(current);
    if (parent === current) break;
    current = parent;
  }

  throw new Error('无法定位包含 .agents 的项目根目录。');
}

function readJson(filePath) {
  return JSON.parse(readFileSync(filePath, 'utf8'));
}

function safeWrite(filePath, content) {
  mkdirSync(path.dirname(filePath), { recursive: true });
  const temp = `${filePath}.${process.pid}.${Date.now()}.tmp`;
  writeFileSync(temp, content, 'utf8');
  renameSync(temp, filePath);
}

function backupOnce(filePath) {
  if (!existsSync(filePath)) return null;
  const backup = `${filePath}.universal-profile.bak`;
  if (!existsSync(backup)) {
    copyFileSync(filePath, backup);
  }
  return backup;
}

function yamlScalar(value) {
  if (Array.isArray(value)) {
    return `[${value.map((item) => JSON.stringify(String(item))).join(', ')}]`;
  }
  return JSON.stringify(String(value));
}

function updateCopilotModel(filePath, model, dryRun) {
  if (!existsSync(filePath)) {
    throw new Error(`Copilot Agent 不存在：${filePath}`);
  }

  const text = readFileSync(filePath, 'utf8');
  const match = text.match(/^---\r?\n([\s\S]*?)\r?\n---\r?\n?/u);
  if (!match) throw new Error(`缺少 YAML frontmatter：${filePath}`);

  let lines = match[1].split(/\r?\n/u);
  lines = lines.filter((line) => !/^model\s*:/u.test(line));

  if (model != null && model !== '' && model !== 'inherit') {
    const nameIndex = lines.findIndex((line) => /^name\s*:/u.test(line));
    const insertAt = nameIndex >= 0 ? nameIndex + 1 : 0;
    lines.splice(insertAt, 0, `model: ${yamlScalar(model)}`);
  }

  const next = `---\n${lines.join('\n')}\n---\n${text.slice(match[0].length)}`;
  if (!dryRun && next !== text) safeWrite(filePath, next);
  return next !== text;
}

function tomlString(value) {
  return JSON.stringify(String(value));
}

function removeManagedBlock(text) {
  const start = text.indexOf(MANAGED_BEGIN);
  const end = text.indexOf(MANAGED_END);
  if (start < 0 || end < 0 || end < start) return text;
  return `${text.slice(0, start).replace(/\s+$/u, '')}\n${text
    .slice(end + MANAGED_END.length)
    .replace(/^\s+/u, '')}`.replace(/^\s+|\s+$/gu, '');
}

function buildCodexManagedBlock(profileName, roleConfigs) {
  const lines = [
    MANAGED_BEGIN,
    `# profile = ${profileName}`,
    '# 由 .agents/scripts/sync-agent-profiles.mjs 自动生成',
    '# 请勿手工修改此区块；请修改 .agents/agent-profiles.json 后重新同步。',
    '',
  ];

  for (const [role, cfg] of Object.entries(roleConfigs)) {
    const meta = ROLE_META[role];
    lines.push(`[agents.${meta.codexName}]`);
    lines.push(`description = ${tomlString(meta.description)}`);
    lines.push(`config_file = ${tomlString(`agents/${role}.toml`)}`);
    lines.push('');
  }

  lines.push(MANAGED_END);
  return lines.join('\n');
}

function assertNoCodexRoleConflicts(baseText) {
  const clean = removeManagedBlock(baseText);
  for (const meta of Object.values(ROLE_META)) {
    const pattern = new RegExp(`^\\[agents\\.${meta.codexName.replace(/[.*+?^${}()|[\\]\\]/g, '\\$&')}\\]\\s*$`, 'mu');
    if (pattern.test(clean)) {
      throw new Error(
        `现有 .codex/config.toml 已定义 [agents.${meta.codexName}]。为避免覆盖自定义配置，sync 已停止。请先合并/移除冲突表，或改用不同角色名。`,
      );
    }
  }
}

function writeCodex(workspaceRoot, profileName, roles, dryRun) {
  const codexDir = path.join(workspaceRoot, '.codex');
  const agentsDir = path.join(codexDir, 'agents');
  const configFile = path.join(codexDir, 'config.toml');
  const existing = existsSync(configFile) ? readFileSync(configFile, 'utf8') : '';
  assertNoCodexRoleConflicts(existing);

  const clean = removeManagedBlock(existing).trim();
  const block = buildCodexManagedBlock(profileName, roles);
  const merged = `${clean}${clean ? '\n\n' : ''}${block}\n`;

  if (!dryRun) {
    mkdirSync(agentsDir, { recursive: true });
    backupOnce(configFile);
    safeWrite(configFile, merged);
  }

  for (const [role, cfg] of Object.entries(roles)) {
    const meta = ROLE_META[role];
    const lines = [
      `# Generated by Universal Agent Workflow V4.2 (${profileName})`,
      `model = ${tomlString(cfg.model)}`,
    ];

    if (cfg.effort) {
      lines.push(`model_reasoning_effort = ${tomlString(cfg.effort)}`);
    }

    lines.push('developer_instructions = """');
    lines.push(meta.codexInstructions.replaceAll('"""', '\\\"\\\"\\\"'));
    lines.push('"""');
    lines.push('');

    if (!dryRun) {
      safeWrite(path.join(agentsDir, `${role}.toml`), `${lines.join('\n')}\n`);
    }
  }
}

function antigravityAgentContent(role, cfg, effortMode, profileName) {
  const meta = ROLE_META[role];
  const lines = [
    '---',
    `name: ${role}`,
    `description: ${JSON.stringify(meta.description)}`,
  ];

  if (cfg.model && cfg.model !== 'inherit') {
    lines.push(`model: ${yamlScalar(cfg.model)}`);
  }

  lines.push('---', '', GENERATED_MARKER, '', `# ${role}`, '');
  lines.push(...meta.antigravityInstructions.map((line) => `${line}\n`));
  lines.push('');
  lines.push(`角色：${meta.displayName || role}`);
  lines.push(`当前 Agent Profile：\`${profileName}\``);
  lines.push(`期望思考等级：\`${cfg.effort || 'inherit'}\``);
  lines.push(`Effort 应用模式：\`${effortMode}\``);
  lines.push('');
  lines.push('说明：模型通过 Agent frontmatter 原生绑定；思考等级只在当前 Antigravity 版本公开支持的方式下应用。同步器不会写入未经确认的 effort frontmatter 字段。');
  lines.push('');
  return `${lines.join('\n')}\n`;
}

function writeAntigravity(workspaceRoot, profileName, roles, effortMode, dryRun, force) {
  for (const [role, cfg] of Object.entries(roles)) {
    const filePath = path.join(workspaceRoot, '.agents', 'agents', role, 'agent.md');
    if (existsSync(filePath)) {
      const old = readFileSync(filePath, 'utf8');
      if (!old.includes(GENERATED_MARKER) && !force) {
        throw new Error(
          `Antigravity Agent 已存在且不是本工具生成：${filePath}。如确认覆盖，请使用 --force。`,
        );
      }
      if (!dryRun) backupOnce(filePath);
    }

    if (!dryRun) {
      safeWrite(filePath, antigravityAgentContent(role, cfg, effortMode, profileName));
    }
  }
}

function writeRuntimeReport(workspaceRoot, target, profileName, roles, capability, dryRun) {
  const report = {
    profile: profileName,
    target,
    generatedAt: new Date().toISOString(),
    capability,
    roles,
  };
  if (!dryRun) {
    const file = path.join(workspaceRoot, '.agents', 'runtime-profiles', `${target}.json`);
    safeWrite(file, `${JSON.stringify(report, null, 2)}\n`);
  }
  return report;
}

function writeActiveProfile(workspaceRoot, profileName, targets, dryRun) {
  if (dryRun) return;
  const file = path.join(workspaceRoot, '.agents', 'generated', 'agent-profile.json');
  safeWrite(
    file,
    `${JSON.stringify(
      {
        profile: profileName,
        targets,
        generatedAt: new Date().toISOString(),
      },
      null,
      2,
    )}\n`,
  );
}

function printSummary(profileName, profile, targets, reports, dryRun) {
  const lines = [
    `当前配置：${profileName}（${profile.displayName || profileName}）`,
    `同步模式：${dryRun ? '仅预览，不写入' : '已同步'}`,
    '',
  ];

  if (profile.description) {
    lines.push(`配置说明：${profile.description}`, '');
  }

  for (const target of targets) {
    const report = reports[target];
    lines.push(target.toUpperCase());
    lines.push('-'.repeat(56));
    for (const [role, cfg] of Object.entries(report.roles)) {
      const effortMode = report.capability.agentEffort;
      const meta = ROLE_META[role] || {};
      lines.push(`${meta.displayName || role}（${role}）`);
      lines.push(`  模型：${Array.isArray(cfg.model) ? cfg.model.join(' -> ') : cfg.model}`);
      lines.push(`  思考等级：${cfg.effort || 'inherit'}`);
      lines.push(`  应用方式：${effortMode || 'unknown'}`);
      if (meta.notes) lines.push(`  说明：${meta.notes}`);
    }
    lines.push('');
  }

  process.stdout.write(`${lines.join('\n')}\n`);
}

function main() {
  const workspaceRoot = resolveWorkspaceRoot();
  const configFile = path.join(workspaceRoot, '.agents', 'agent-profiles.json');
  if (!existsSync(configFile)) throw new Error(`缺少 ${configFile}`);

  const config = readJson(configFile);
  if (hasFlag('--list')) {
    for (const [name, value] of Object.entries(config.profiles || {})) {
      process.stdout.write(`${name}\t${value.displayName || name}\t${value.description || ''}\n`);
    }
    return;
  }

  const positional = process.argv.slice(2).find((arg, index, all) => {
    if (arg.startsWith('--')) return false;
    const prev = all[index - 1];
    return !['--target', '--workspace'].includes(prev);
  });
  const profileName = positional || config.defaultProfile;
  const profile = config.profiles?.[profileName];
  if (!profile) {
    throw new Error(
      `未知 profile：${profileName}。可用：${Object.keys(config.profiles || {}).join(', ')}`,
    );
  }

  const targetValue = (readOption('--target') || 'all').toLowerCase();
  const targets = targetValue === 'all'
    ? ['copilot', 'codex', 'antigravity']
    : [targetValue];
  for (const target of targets) {
    if (!['copilot', 'codex', 'antigravity'].includes(target)) {
      throw new Error(`--target 只允许 all/copilot/codex/antigravity，收到：${target}`);
    }
  }

  const dryRun = hasFlag('--dry-run');
  const force = hasFlag('--force');
  const reports = {};

  for (const target of targets) {
    const roles = {};
    for (const role of Object.keys(ROLE_META)) {
      const cfg = profile.roles?.[role]?.[target];
      if (!cfg?.model) {
        throw new Error(`${profileName}.${role}.${target}.model 未配置。`);
      }
      roles[role] = cfg;
    }

    const capability = config.capabilities?.[target] || {};

    if (target === 'copilot') {
      for (const [role, cfg] of Object.entries(roles)) {
        const file = path.join(workspaceRoot, '.github', 'agents', `${role}.agent.md`);
        updateCopilotModel(file, cfg.model, dryRun);
      }
    } else if (target === 'codex') {
      writeCodex(workspaceRoot, profileName, roles, dryRun);
    } else if (target === 'antigravity') {
      writeAntigravity(
        workspaceRoot,
        profileName,
        roles,
        capability.agentEffort || 'session',
        dryRun,
        force,
      );
    }

    reports[target] = writeRuntimeReport(
      workspaceRoot,
      target,
      profileName,
      roles,
      capability,
      dryRun,
    );
  }

  writeActiveProfile(workspaceRoot, profileName, targets, dryRun);
  printSummary(profileName, profile, targets, reports, dryRun);
}

try {
  main();
} catch (error) {
  console.error(`[sync-agent-profiles] ${error?.stack || error}`);
  process.exitCode = 1;
}
