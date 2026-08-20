import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import process from 'node:process';

function root() {
  let current = path.resolve(process.cwd());

  if (path.basename(current).toLowerCase() === '.agents') {
    current = path.dirname(current);
  }

  while (true) {
    if (existsSync(path.join(current, '.agents'))) {
      return current;
    }

    const parent = path.dirname(current);
    if (parent === current) {
      return process.cwd();
    }
    current = parent;
  }
}

const workspace = root();

const checks = [
  '.agents/hooks.json',
  '.agents/scripts/task-state.mjs',
  '.agents/scripts/task-finish.mjs',
  '.agents/scripts/stop-guard.mjs',
  '.agents/scripts/notify-feishu.mjs',
  '.agents/skills/execute-plan/SKILL.md',
  '.agents/skills/fix-review/SKILL.md',
  '.agents/workflows/execute-plan.md',
  '.agents/workflows/fix-review.md',
  '.github/agents/plan-writer.agent.md',
  '.github/agents/plan-executor.agent.md',
  '.github/agents/code-reviewer.agent.md',
  '.github/agents/review-fixer.agent.md',
  '.github/prompts/create-plan.prompt.md',
  '.github/prompts/review-plan.prompt.md',
];

let failed = false;

console.log(`Workspace: ${workspace}`);
console.log(`Node: ${process.version}`);
console.log('');

for (const relative of checks) {
  const ok = existsSync(path.join(workspace, relative));
  console.log(`${ok ? 'PASS' : 'FAIL'}  ${relative}`);
  if (!ok) failed = true;
}

console.log('');

const hooksFile = path.join(workspace, '.agents/hooks.json');

if (existsSync(hooksFile)) {
  try {
    const hooks = JSON.parse(readFileSync(hooksFile, 'utf8'));
    const command =
      hooks?.['plan-executor-stop-guard']?.Stop?.[0]?.command || '';

    const ok = command === 'node ./scripts/stop-guard.mjs';
    console.log(
      `${ok ? 'PASS' : 'WARN'}  Antigravity Hook command = ${command || '<missing>'}`,
    );

    if (!ok) {
      console.log(
        '      当前已验证的 Antigravity IDE 项目应使用 node ./scripts/stop-guard.mjs',
      );
    }
  } catch (error) {
    console.log(`FAIL  hooks.json 无法解析：${error.message}`);
    failed = true;
  }
}

const envFile = path.join(workspace, '.agents/.env.local');
console.log(
  `${existsSync(envFile) ? 'PASS' : 'INFO'}  .agents/.env.local${
    existsSync(envFile) ? '' : '（未配置飞书时可不存在）'
  }`,
);

console.log('');
console.log(
  failed
    ? 'Workflow Doctor: FAILED'
    : 'Workflow Doctor: PASS',
);

process.exitCode = failed ? 1 : 0;
