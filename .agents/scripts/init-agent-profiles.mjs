import {
  copyFileSync,
  existsSync,
  mkdirSync,
  readFileSync,
  renameSync,
  writeFileSync,
} from 'node:fs';
import { spawnSync } from 'node:child_process';
import path from 'node:path';
import process from 'node:process';
import readline from 'node:readline/promises';

const ROLES = ['plan-writer', 'plan-executor', 'code-reviewer', 'review-fixer'];
const TARGETS = ['copilot', 'codex', 'antigravity'];
const ROLE_NAMES = {
  'plan-writer': '计划规划器',
  'plan-executor': '计划执行器',
  'code-reviewer': '代码审查器',
  'review-fixer': '审查修复器',
};
const TARGET_NAMES = {
  copilot: 'Copilot',
  codex: 'Codex',
  antigravity: 'Antigravity',
};

function readOption(name) {
  const index = process.argv.indexOf(name);
  if (index >= 0 && process.argv[index + 1]) return process.argv[index + 1];
  return null;
}
function hasFlag(name) { return process.argv.includes(name); }

function resolveWorkspaceRoot() {
  const explicit = readOption('--workspace');
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

function readJson(filePath) { return JSON.parse(readFileSync(filePath, 'utf8')); }
function safeWrite(filePath, content) {
  mkdirSync(path.dirname(filePath), { recursive: true });
  const temp = `${filePath}.${process.pid}.${Date.now()}.tmp`;
  writeFileSync(temp, content, 'utf8');
  renameSync(temp, filePath);
}
function backup(filePath) {
  if (!existsSync(filePath)) return null;
  const backupPath = `${filePath}.before-init.bak`;
  copyFileSync(filePath, backupPath);
  return backupPath;
}
function clone(value) { return JSON.parse(JSON.stringify(value)); }

function frontmatterValue(text, key) {
  const match = text.match(/^---\r?\n([\s\S]*?)\r?\n---/u);
  if (!match) return null;
  const line = match[1].split(/\r?\n/u).find((item) => new RegExp(`^${key}\\s*:`,'u').test(item));
  if (!line) return null;
  let value = line.replace(new RegExp(`^${key}\\s*:\\s*`,'u'),'').trim();
  try { return JSON.parse(value); } catch { return value.replace(/^['"]|['"]$/gu,''); }
}
function tomlValue(text, key) {
  const match = text.match(new RegExp(`^${key}\\s*=\\s*(.+)$`, 'mu'));
  if (!match) return null;
  const raw = match[1].trim();
  try { return JSON.parse(raw); } catch { return raw.replace(/^['"]|['"]$/gu,''); }
}

function importExisting(workspaceRoot, config, profileName) {
  const profile = config.profiles?.[profileName];
  if (!profile) return [];
  const imported = [];
  for (const role of ROLES) {
    const roleConfig = profile.roles?.[role];
    if (!roleConfig) continue;

    const copilotFile = path.join(workspaceRoot, '.github', 'agents', `${role}.agent.md`);
    if (existsSync(copilotFile)) {
      const text = readFileSync(copilotFile, 'utf8');
      const model = frontmatterValue(text, 'model');
      if (model) {
        roleConfig.copilot.model = model;
        imported.push(`${ROLE_NAMES[role]} / Copilot：${model}`);
      }
    }

    const codexFile = path.join(workspaceRoot, '.codex', 'agents', `${role}.toml`);
    if (existsSync(codexFile)) {
      const text = readFileSync(codexFile, 'utf8');
      const model = tomlValue(text, 'model');
      const effort = tomlValue(text, 'model_reasoning_effort');
      if (model) roleConfig.codex.model = model;
      if (effort) roleConfig.codex.effort = effort;
      if (model || effort) imported.push(`${ROLE_NAMES[role]} / Codex：${model || '保留模型'} / ${effort || '保留思考等级'}`);
    }

    const antigravityFile = path.join(workspaceRoot, '.agents', 'agents', role, 'agent.md');
    if (existsSync(antigravityFile)) {
      const text = readFileSync(antigravityFile, 'utf8');
      const model = frontmatterValue(text, 'model');
      const effort = text.match(/期望思考等级：`([^`]+)`/u)?.[1] || null;
      if (model) roleConfig.antigravity.model = model;
      if (effort && effort !== 'inherit') roleConfig.antigravity.effort = effort;
      if (model || effort) imported.push(`${ROLE_NAMES[role]} / Antigravity：${model || '保留模型'} / ${effort || '保留思考等级'}`);
    }
  }
  return imported;
}

async function askYesNo(rl, label, defaultValue = true) {
  const suffix = defaultValue ? '[Y/n]' : '[y/N]';
  const value = (await rl.question(`${label} ${suffix}：`)).trim().toLowerCase();
  if (!value) return defaultValue;
  return ['y','yes','1','true','是'].includes(value);
}

async function chooseProfile(rl, config, initial) {
  const names = Object.keys(config.profiles || {});
  console.log('\n可用 Profile：');
  names.forEach((name, index) => {
    const item = config.profiles[name];
    console.log(`${index + 1}. ${item.displayName || name}（${name}）`);
    if (item.description) console.log(`   ${item.description}`);
  });
  const defaultIndex = Math.max(0, names.indexOf(initial));
  const answer = (await rl.question(`\n请选择默认 Profile [默认 ${defaultIndex + 1}]：`)).trim();
  if (!answer) return names[defaultIndex];
  const asNumber = Number.parseInt(answer, 10);
  if (Number.isInteger(asNumber) && asNumber >= 1 && asNumber <= names.length) return names[asNumber - 1];
  if (names.includes(answer)) return answer;
  throw new Error(`未知 Profile：${answer}`);
}

async function customizeProfile(rl, config, profileName) {
  const profile = config.profiles[profileName];
  console.log(`\n开始配置：${profile.displayName || profileName}`);
  for (const role of ROLES) {
    const roleConfig = profile.roles[role];
    console.log(`\n【${roleConfig.displayName || ROLE_NAMES[role]}】`);
    if (roleConfig.description) console.log(roleConfig.description);
    for (const target of TARGETS) {
      const cfg = roleConfig[target];
      console.log(`\n  ${TARGET_NAMES[target]}`);
      const model = (await rl.question(`  模型 [${cfg.model}]：`)).trim();
      if (model) cfg.model = model;
      const effort = (await rl.question(`  思考等级 [${cfg.effort || 'inherit'}]：`)).trim();
      if (effort) cfg.effort = effort;
    }
  }
}

function runSync(workspaceRoot, profileName, target) {
  const script = path.join(workspaceRoot, '.agents', 'scripts', 'sync-agent-profiles.mjs');
  const result = spawnSync(process.execPath, [script, profileName, '--target', target], {
    cwd: workspaceRoot,
    stdio: 'inherit',
    windowsHide: true,
  });
  if ((result.status ?? 1) !== 0) throw new Error('agent-profiles 已生成，但自动同步失败。请根据上方错误修复后重新执行 sync-agent-profiles.mjs。');
}

async function main() {
  const workspaceRoot = resolveWorkspaceRoot();
  const targetFile = path.join(workspaceRoot, '.agents', 'agent-profiles.json');
  const templateFile = path.join(workspaceRoot, '.agents', 'templates', 'agent-profiles.template.json');
  if (!existsSync(templateFile)) throw new Error(`缺少模板：${templateFile}`);

  const force = hasFlag('--force');
  const yes = hasFlag('--yes') || !process.stdin.isTTY;
  const fromExistingFlag = hasFlag('--from-existing');
  const noSync = hasFlag('--no-sync');
  const syncTarget = (readOption('--sync-target') || 'all').toLowerCase();
  if (!['all', ...TARGETS].includes(syncTarget)) throw new Error(`--sync-target 只允许 all/${TARGETS.join('/')}`);

  let config = clone(readJson(templateFile));
  const requestedPreset = readOption('--preset');
  let profileName = requestedPreset || config.defaultProfile || 'balanced';
  if (!config.profiles?.[profileName]) throw new Error(`未知 preset：${profileName}`);

  let rl = null;
  try {
    if (!yes) {
      rl = readline.createInterface({ input: process.stdin, output: process.stdout });
      console.log('Universal Agent Profile 初始化');
      console.log('说明：程序 ID / model / effort 保持英文；displayName / description / notes 使用中文展示。');
      profileName = await chooseProfile(rl, config, profileName);
    }

    config.defaultProfile = profileName;

    let importExistingNow = fromExistingFlag;
    if (!yes && !fromExistingFlag) {
      importExistingNow = await askYesNo(rl, '是否尝试从现有 Copilot / Codex / Antigravity 配置读取模型和思考等级', true);
    }
    if (importExistingNow) {
      const imported = importExisting(workspaceRoot, config, profileName);
      console.log(imported.length ? `\n已读取现有配置：\n- ${imported.join('\n- ')}` : '\n未发现可导入的现有 Agent 模型配置。');
    }

    if (!yes) {
      const customize = await askYesNo(rl, '是否逐角色自定义模型和思考等级', false);
      if (customize) await customizeProfile(rl, config, profileName);
    }

    if (existsSync(targetFile) && !force) {
      if (yes) throw new Error('agent-profiles.json 已存在。若确认重新生成，请使用 --force。');
      const overwrite = await askYesNo(rl, 'agent-profiles.json 已存在，是否备份后重新生成', false);
      if (!overwrite) {
        console.log('已取消，不修改现有配置。');
        return;
      }
    }

    if (existsSync(targetFile)) {
      const backupPath = backup(targetFile);
      console.log(`已备份：${path.relative(workspaceRoot, backupPath)}`);
    }
    safeWrite(targetFile, `${JSON.stringify(config, null, 2)}\n`);
    console.log(`已生成：${path.relative(workspaceRoot, targetFile)}`);
    console.log(`默认 Profile：${profileName}（${config.profiles[profileName].displayName || profileName}）`);

    let syncNow = !noSync && yes;
    if (!yes && !noSync) syncNow = await askYesNo(rl, '是否立即同步到 Copilot / Codex / Antigravity', true);
    if (syncNow) runSync(workspaceRoot, profileName, syncTarget);
    else console.log(`下一步可执行：node .agents/scripts/sync-agent-profiles.mjs ${profileName}`);
  } finally {
    if (rl) rl.close();
  }
}

try {
  await main();
} catch (error) {
  console.error(`[init-agent-profiles] ${error?.stack || error}`);
  process.exitCode = 1;
}
