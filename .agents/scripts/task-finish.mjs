import {
  existsSync,
  mkdirSync,
  readFileSync,
  renameSync,
  writeFileSync,
} from 'node:fs';
import path from 'node:path';
import process from 'node:process';
import {
  readEnvFile,
  sanitizeNotificationText,
  sendFeishuNotification,
} from './notify-feishu.mjs';

const TERMINAL_STATUSES = new Set([
  'COMPLETED',
  'PARTIAL',
  'BLOCKED',
  'FAILED',
]);

function parseBoolean(value, defaultValue = false) {
  if (value == null || value === '') {
    return defaultValue;
  }

  return ['1', 'true', 'yes', 'on', 'enabled'].includes(
    String(value).trim().toLowerCase(),
  );
}

function resolveWorkspaceRoot() {
  const workspaceIndex = process.argv.indexOf('--workspace');

  if (workspaceIndex >= 0 && process.argv[workspaceIndex + 1]) {
    return path.resolve(process.argv[workspaceIndex + 1]);
  }

  let current = path.resolve(process.cwd());

  if (
    path.basename(current).toLowerCase() === '.agents' &&
    existsSync(path.join(current, 'hooks.json'))
  ) {
    return path.dirname(current);
  }

  for (;;) {
    if (existsSync(path.join(current, '.agents'))) {
      return current;
    }

    const parent = path.dirname(current);
    if (parent === current) {
      break;
    }
    current = parent;
  }

  throw new Error(
    '无法定位项目根目录。请在项目目录执行，或传入 --workspace <path>。',
  );
}

function safeReadJson(filePath) {
  if (!existsSync(filePath)) {
    return null;
  }

  return JSON.parse(readFileSync(filePath, 'utf8'));
}

function safeWriteJson(filePath, value) {
  mkdirSync(path.dirname(filePath), { recursive: true });

  const temp = `${filePath}.${process.pid}.${Date.now()}.tmp`;
  writeFileSync(temp, `${JSON.stringify(value, null, 2)}\n`, 'utf8');
  renameSync(temp, filePath);
}

function normalizeRelativePath(value) {
  if (!value || typeof value !== 'string') {
    return null;
  }

  const normalized = path.posix.normalize(value.replaceAll('\\', '/'));

  if (
    path.posix.isAbsolute(normalized) ||
    normalized === '..' ||
    normalized.startsWith('../')
  ) {
    return null;
  }

  return normalized.replace(/^\.\//u, '');
}

function resolveWorkspaceFile(workspaceRoot, relativePath) {
  const normalized = normalizeRelativePath(relativePath);

  if (!normalized) {
    return null;
  }

  const root = path.resolve(workspaceRoot);
  const file = path.resolve(root, normalized);
  const prefix = `${root}${path.sep}`;

  if (file !== root && !file.startsWith(prefix)) {
    return null;
  }

  return file;
}

function parseExecutionTerminal(executionFile, expectedTaskId, startedAt) {
  if (!executionFile || !existsSync(executionFile)) {
    throw new Error('execution.md 不存在，不能收口任务。');
  }

  const lines = readFileSync(executionFile, 'utf8')
    .replace(/^\uFEFF/u, '')
    .split(/\r?\n/u)
    .slice(0, 3)
    .map((line) => line.trim());

  const statusMatch = lines[0]?.match(
    /^<!--\s*AI_EXECUTION_STATUS\s*:\s*(COMPLETED|PARTIAL|BLOCKED|FAILED)\s*-->$/u,
  );
  const taskMatch = lines[1]?.match(/^AI_TASK_ID\s*:\s*(\S+)$/u);
  const finishedMatch = lines[2]?.match(
    /^AI_EXECUTION_FINISHED_AT\s*:\s*(\S+)$/u,
  );

  if (!statusMatch || !taskMatch || !finishedMatch) {
    throw new Error(
      'execution.md 前三行不是合法终态机器协议。必须包含 AI_EXECUTION_STATUS / AI_TASK_ID / AI_EXECUTION_FINISHED_AT。',
    );
  }

  const status = statusMatch[1];

  if (!TERMINAL_STATUSES.has(status)) {
    throw new Error(`非法终态：${status}`);
  }

  if (taskMatch[1] !== expectedTaskId) {
    throw new Error(
      `execution.md 的 AI_TASK_ID=${taskMatch[1]} 与当前任务 ${expectedTaskId} 不一致。`,
    );
  }

  const started = Date.parse(String(startedAt || ''));
  const finished = Date.parse(finishedMatch[1]);

  if (!Number.isFinite(started) || !Number.isFinite(finished)) {
    throw new Error('任务开始时间或完成时间不是合法 ISO-8601。');
  }

  if (finished < started) {
    throw new Error('AI_EXECUTION_FINISHED_AT 早于本轮 startedAt。');
  }

  return {
    status,
    finishedAt: finishedMatch[1],
  };
}

function loadMergedEnv(workspaceRoot) {
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

function buildExtraLines(runtime, terminal) {
  const lines = [];

  if (runtime.mode === 'review-fix') {
    if (runtime.reviewRound > 0) {
      lines.push(`Review Fix Round ${runtime.reviewRound} 已完成执行收口。`);
    }

    lines.push('下一步：重新执行 /review-plan（Codex 可使用对应 Review 流程）进行复审。');
  } else {
    lines.push('下一步：执行 /review-plan 进行独立验收。');
  }

  if (terminal.status !== 'COMPLETED') {
    lines.push(`本轮以 ${terminal.status} 终态结束，请先阅读 execution.md 的遗留/阻塞说明。`);
  }

  lines.push('未自动执行 git commit / git push。');

  return lines;
}

async function main() {
  const workspaceRoot = resolveWorkspaceRoot();
  const taskId = process.argv[2]?.trim();

  if (!taskId) {
    throw new Error(
      '用法：node .agents/scripts/task-finish.mjs <taskId> [--workspace <path>] [--no-notify] [--resend]',
    );
  }

  const env = loadMergedEnv(workspaceRoot);
  const runtimeRelative =
    env.AI_TASK_STATE_FILE || '.agents/runtime/current-task.json';

  const runtimeFile = path.isAbsolute(runtimeRelative)
    ? runtimeRelative
    : path.join(workspaceRoot, runtimeRelative);

  const runtime = safeReadJson(runtimeFile);

  if (!runtime || runtime.taskId !== taskId) {
    throw new Error(
      `current-task.json 不存在，或 taskId 与 ${taskId} 不一致。`,
    );
  }

  const executionPath =
    runtime.executionPath || `ai_docs/tasks/${taskId}/execution.md`;

  const executionFile = resolveWorkspaceFile(
    workspaceRoot,
    executionPath,
  );

  if (!executionFile) {
    throw new Error('executionPath 不是工作区内合法相对路径。');
  }

  const terminal = parseExecutionTerminal(
    executionFile,
    taskId,
    runtime.startedAt,
  );

  const resend = process.argv.includes('--resend');

  if (
    runtime.active === false &&
    runtime.finalStatus === terminal.status &&
    !resend
  ) {
    process.stdout.write(
      `${JSON.stringify(
        {
          ok: true,
          idempotent: true,
          taskId,
          finalStatus: terminal.status,
          message: '任务已完成收口，本次不重复发送通知。',
        },
        null,
        2,
      )}\n`,
    );
    return;
  }

  const now = new Date().toISOString();
  const finalized = {
    ...runtime,
    active: false,
    status: terminal.status,
    finalStatus: terminal.status,
    finishedAt: terminal.finishedAt,
    completedAt: now,
    updatedAt: now,
    finalizedBy: 'task-finish.mjs',
  };

  safeWriteJson(runtimeFile, finalized);

  let notification = {
    skipped: true,
    reason: '--no-notify',
  };

  const notifyAllowed =
    !process.argv.includes('--no-notify') &&
    parseBoolean(env.STOP_GUARD_NOTIFY, true);

  if (notifyAllowed) {
    try {
      notification = await sendFeishuNotification({
        workspaceRoot,
        status: terminal.status,
        taskId,
        mode: finalized.mode || 'plan-execution',
        reviewRound: Number.isInteger(finalized.reviewRound)
          ? finalized.reviewRound
          : 0,
        agentSource: finalized.agentSource || 'unknown',
        terminationReason: 'explicit_finish',
        executionPath,
        extraLines: buildExtraLines(finalized, terminal),
      });
    } catch (error) {
      notification = {
        skipped: false,
        ok: false,
        error: sanitizeNotificationText(
          error?.message || error,
          500,
        ),
      };
    }
  }

  const latest = safeReadJson(runtimeFile) || finalized;
  latest.notificationStatus = notification?.ok
    ? 'sent'
    : notification?.skipped
      ? 'skipped'
      : 'failed';
  latest.notificationUpdatedAt = new Date().toISOString();

  if (notification?.error) {
    latest.notificationError = notification.error;
  } else {
    delete latest.notificationError;
  }

  safeWriteJson(runtimeFile, latest);

  process.stdout.write(
    `${JSON.stringify(
      {
        ok: true,
        idempotent: false,
        taskId,
        finalStatus: terminal.status,
        mode: latest.mode,
        agentSource: latest.agentSource,
        reviewRound: latest.reviewRound,
        notificationStatus: latest.notificationStatus,
      },
      null,
      2,
    )}\n`,
  );
}

try {
  await main();
} catch (error) {
  console.error(
    `[task-finish] ${sanitizeNotificationText(
      error?.stack || error,
      2000,
    )}`,
  );
  process.exitCode = 1;
}
