import {
  existsSync,
  mkdirSync,
  readFileSync,
  readdirSync,
  renameSync,
  statSync,
  writeFileSync,
} from 'node:fs';
import { spawnSync } from 'node:child_process';
import path from 'node:path';
import process from 'node:process';
import { readEnvFile } from './notify-feishu.mjs';

function parseBoolean(value, defaultValue = false) {
  if (value == null || value === '') return defaultValue;
  return ['1', 'true', 'yes', 'on', 'enabled'].includes(
    String(value).trim().toLowerCase(),
  );
}

function safeReadJson(filePath, fallback = null) {
  if (!existsSync(filePath)) return fallback;
  try {
    return JSON.parse(readFileSync(filePath, 'utf8'));
  } catch {
    return fallback;
  }
}

function safeWriteJson(filePath, value) {
  mkdirSync(path.dirname(filePath), { recursive: true });
  const temp = `${filePath}.${process.pid}.${Date.now()}.tmp`;
  writeFileSync(temp, `${JSON.stringify(value, null, 2)}\n`, 'utf8');
  renameSync(temp, filePath);
}

function readInput() {
  try {
    return JSON.parse(readFileSync(0, 'utf8') || '{}');
  } catch {
    return {};
  }
}

function resolveWorkspaceRoot(input) {
  const candidates = [
    input?.cwd,
    process.cwd(),
  ].filter(Boolean);

  for (const candidate of candidates) {
    let current = path.resolve(candidate);

    for (;;) {
      if (existsSync(path.join(current, '.agents'))) return current;
      const parent = path.dirname(current);
      if (parent === current) break;
      current = parent;
    }
  }

  return path.resolve(process.cwd());
}

function loadEnv(workspaceRoot) {
  const envRelative =
    process.env.AI_WORKFLOW_ENV_FILE ||
    process.env.ANTIGRAVITY_ENV_FILE ||
    '.agents/.env.local';

  const envPath = path.isAbsolute(envRelative)
    ? envRelative
    : path.join(workspaceRoot, envRelative);

  return {
    ...readEnvFile(envPath),
    ...process.env,
  };
}

function stageFile(workspaceRoot) {
  return path.join(
    workspaceRoot,
    '.agents',
    'runtime',
    'copilot-stage.json',
  );
}

function inferStage(prompt) {
  const text = String(prompt || '').trim();

  const patterns = [
    { command: 'create-plan', stage: 'plan' },
    { command: 'review-plan', stage: 'review' },
    { command: 'run-plan', stage: 'execute' },
    { command: 'execute-plan', stage: 'execute' },
    { command: 'repair-review', stage: 'fix' },
    { command: 'fix-review', stage: 'fix' },
  ];

  for (const item of patterns) {
    const regex = new RegExp(
      `(?:^|\\s)/${item.command}(?:\\s+([A-Za-z0-9._-]+))?\\b`,
      'iu',
    );
    const match = text.match(regex);
    if (match) {
      return {
        stage: item.stage,
        command: item.command,
        taskId: match[1] || null,
      };
    }
  }

  return null;
}

function registerStage(workspaceRoot, input) {
  const detected = inferStage(input.prompt);
  if (!detected) return;

  safeWriteJson(stageFile(workspaceRoot), {
    active: true,
    stage: detected.stage,
    command: detected.command,
    taskId: detected.taskId,
    sessionId: input.session_id || null,
    startedAt: input.timestamp || new Date().toISOString(),
    prompt: String(input.prompt || '').slice(0, 500),
  });
}

function findRecentTaskId(workspaceRoot, artifact, startedAt) {
  const root = path.join(workspaceRoot, 'ai_docs', 'tasks');
  if (!existsSync(root)) return null;

  const started = Number.isFinite(Date.parse(startedAt || ''))
    ? Date.parse(startedAt)
    : Date.now() - 60 * 60 * 1000;

  const candidates = [];

  for (const name of readdirSync(root)) {
    const file = path.join(root, name, artifact);
    if (!existsSync(file)) continue;

    try {
      const stat = statSync(file);
      if (stat.mtimeMs >= started - 2000) {
        candidates.push({
          taskId: name,
          mtimeMs: stat.mtimeMs,
        });
      }
    } catch {
      // ignore
    }
  }

  candidates.sort((a, b) => b.mtimeMs - a.mtimeMs);

  if (candidates.length === 0) return null;
  return candidates[0].taskId;
}

function callNode(workspaceRoot, script, args = []) {
  const result = spawnSync(
    process.execPath,
    [path.join(workspaceRoot, '.agents', 'scripts', script), ...args],
    {
      cwd: workspaceRoot,
      encoding: 'utf8',
      windowsHide: true,
      env: process.env,
    },
  );

  if (result.stderr) {
    process.stderr.write(result.stderr);
  }

  return {
    code: result.status ?? 1,
    stdout: result.stdout || '',
  };
}

function output(value = {}) {
  process.stdout.write(`${JSON.stringify(value)}\n`);
}

function parseExecutionStatus(executionFile) {
  if (!executionFile || !existsSync(executionFile)) {
    return 'MISSING';
  }

  const first = readFileSync(executionFile, 'utf8')
    .replace(/^\uFEFF/u, '')
    .split(/\r?\n/u)[0]
    ?.trim();

  const match = first?.match(
    /^<!--\s*AI_EXECUTION_STATUS\s*:\s*(IN_PROGRESS|COMPLETED|PARTIAL|BLOCKED|FAILED)\s*-->$/u,
  );

  return match?.[1] || 'UNKNOWN';
}

function handleStop(workspaceRoot, input, env) {
  const stagePath = stageFile(workspaceRoot);
  const stage = safeReadJson(stagePath, null);
  const runtimeFile = path.join(
    workspaceRoot,
    '.agents',
    'runtime',
    'current-task.json',
  );
  const runtime = safeReadJson(runtimeFile, null);

  // Execute / Fix：优先依赖 runtime 状态机。
  if (runtime?.active === true && runtime?.taskId) {
    const executionPath =
      runtime.executionPath ||
      `ai_docs/tasks/${runtime.taskId}/execution.md`;
    const executionFile = path.join(
      workspaceRoot,
      executionPath.replaceAll('/', path.sep),
    );

    const status = parseExecutionStatus(executionFile);

    if (['COMPLETED', 'PARTIAL', 'BLOCKED', 'FAILED'].includes(status)) {
      callNode(workspaceRoot, 'task-finish.mjs', [runtime.taskId]);

      if (stage?.active) {
        safeWriteJson(stagePath, {
          ...stage,
          active: false,
          completedAt: new Date().toISOString(),
          taskId: stage.taskId || runtime.taskId,
        });
      }

      output({});
      return;
    }

    const guardEnabled = parseBoolean(
      env.AI_WORKFLOW_COPILOT_STOP_GUARD,
      true,
    );

    if (
      guardEnabled &&
      status === 'IN_PROGRESS' &&
      input.stop_hook_active !== true
    ) {
      output({
        hookSpecificOutput: {
          hookEventName: 'Stop',
          decision: 'block',
          reason:
            '当前 Universal Agent Workflow 任务仍为 IN_PROGRESS。请继续完成剩余实现/测试，写入合法 execution.md 终态并执行 task-finish.mjs 后再结束。',
        },
      });
      return;
    }

    // 已经被 Stop Hook 续跑过一次，避免无限循环。
    output({});
    return;
  }

  // 如果 task-finish 已经把 runtime 收口，避免重复通知。
  if (
    runtime?.active === false &&
    stage?.active &&
    ['execute', 'fix'].includes(stage.stage)
  ) {
    safeWriteJson(stagePath, {
      ...stage,
      active: false,
      completedAt: new Date().toISOString(),
      taskId: stage.taskId || runtime.taskId || null,
    });
    output({});
    return;
  }

  // Plan / Review：通过阶段文件 + 实际产物做确定性通知。
  if (stage?.active && stage.stage === 'plan') {
    const taskId =
      stage.taskId ||
      findRecentTaskId(workspaceRoot, 'plan.md', stage.startedAt);

    if (taskId) {
      callNode(workspaceRoot, 'workflow-notify.mjs', [
        'plan-created',
        taskId,
        '--source',
        'copilot',
      ]);

      safeWriteJson(stagePath, {
        ...stage,
        active: false,
        taskId,
        completedAt: new Date().toISOString(),
      });
    }

    output({});
    return;
  }

  if (stage?.active && stage.stage === 'review') {
    const taskId =
      stage.taskId ||
      findRecentTaskId(workspaceRoot, 'review.md', stage.startedAt);

    if (taskId) {
      callNode(workspaceRoot, 'workflow-notify.mjs', [
        'review-completed',
        taskId,
        '--source',
        'copilot',
      ]);

      safeWriteJson(stagePath, {
        ...stage,
        active: false,
        taskId,
        completedAt: new Date().toISOString(),
      });
    }

    output({});
    return;
  }

  output({});
}

function main() {
  const input = readInput();
  const workspaceRoot = resolveWorkspaceRoot(input);
  const env = loadEnv(workspaceRoot);

  if (!parseBoolean(env.AI_WORKFLOW_COPILOT_HOOK, true)) {
    output({});
    return;
  }

  const event = String(
    input.hook_event_name || input.hookEventName || '',
  );

  if (event === 'UserPromptSubmit') {
    registerStage(workspaceRoot, input);
    output({});
    return;
  }

  if (event === 'Stop') {
    handleStop(workspaceRoot, input, env);
    return;
  }

  output({});
}

try {
  main();
} catch (error) {
  console.error(`[copilot-hook] ${error?.stack || error}`);
  // Hook 故障不能把普通 Copilot 会话锁死。
  output({});
}
