import {
  existsSync,
  mkdirSync,
  readFileSync,
  renameSync,
  statSync,
  writeFileSync,
} from 'node:fs';
import path from 'node:path';
import process from 'node:process';
import {
  readEnvFile,
  sanitizeNotificationText,
  sendFeishuNotification,
} from './notify-feishu.mjs';

function parseBoolean(value, defaultValue = false) {
  if (value == null || value === '') {
    return defaultValue;
  }

  return ['1', 'true', 'yes', 'on', 'enabled'].includes(
    String(value).trim().toLowerCase(),
  );
}

function readOption(name) {
  const index = process.argv.indexOf(name);
  if (index >= 0 && process.argv[index + 1]) {
    return process.argv[index + 1];
  }
  return null;
}

function resolveWorkspaceRoot() {
  const explicit = readOption('--workspace');
  if (explicit) {
    return path.resolve(explicit);
  }

  let current = path.resolve(process.cwd());

  if (path.basename(current).toLowerCase() === '.agents') {
    current = path.dirname(current);
  }

  for (;;) {
    if (existsSync(path.join(current, '.agents'))) {
      return current;
    }

    const parent = path.dirname(current);
    if (parent === current) break;
    current = parent;
  }

  throw new Error('无法定位包含 .agents 的项目根目录。');
}

function safeReadJson(filePath, fallback) {
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

function readActiveProfile(workspaceRoot) {
  const file = path.join(
    workspaceRoot,
    '.agents',
    'generated',
    'agent-profile.json',
  );
  const value = safeReadJson(file, null);
  return value?.profile || null;
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

function assertTaskId(taskId) {
  if (!taskId || !/^[A-Za-z0-9._-]+$/u.test(taskId)) {
    throw new Error(`taskId 无效：${taskId || '<empty>'}`);
  }
}

function parseReview(reviewFile, expectedTaskId) {
  if (!existsSync(reviewFile)) {
    throw new Error(`review.md 不存在：${reviewFile}`);
  }

  const content = readFileSync(reviewFile, 'utf8').replace(/^\uFEFF/u, '');
  const lines = content.split(/\r?\n/u).slice(0, 3).map((line) => line.trim());

  const status = lines[0]?.match(
    /^<!--\s*AI_REVIEW_STATUS\s*:\s*(PASS|PASS_WITH_ISSUES|NEEDS_FIX|BLOCKED)\s*-->$/u,
  )?.[1];
  const taskId = lines[1]?.match(/^AI_TASK_ID\s*:\s*(\S+)$/u)?.[1];
  const reviewedAt = lines[2]?.match(/^AI_REVIEWED_AT\s*:\s*(\S+)$/u)?.[1];

  if (!status || !taskId || !reviewedAt) {
    throw new Error('review.md 前三行不是合法 Review 机器协议。');
  }

  if (taskId !== expectedTaskId) {
    throw new Error(
      `review.md 的 AI_TASK_ID=${taskId} 与 ${expectedTaskId} 不一致。`,
    );
  }

  const count = (value) =>
    [...content.matchAll(
      new RegExp(`处理要求\\s*[：:]\\s*${value}\\b`, 'giu'),
    )].length;

  return {
    status,
    reviewedAt,
    mustFix: count('MUST_FIX'),
    shouldFix: count('SHOULD_FIX'),
    optional: count('OPTIONAL'),
  };
}

function notificationKey(stage, taskId, artifactFile, suffix = '') {
  const stat = statSync(artifactFile);
  return `${stage}:${taskId}:${stat.mtimeMs}:${stat.size}:${suffix}`;
}

async function main() {
  const stage = String(process.argv[2] || '').trim().toLowerCase();
  const taskId = String(process.argv[3] || '').trim();
  const source = readOption('--source') || 'unknown';
  const force = process.argv.includes('--force');

  if (!['plan-created', 'review-completed'].includes(stage)) {
    throw new Error(
      '用法：workflow-notify.mjs plan-created|review-completed <taskId> [--source copilot|antigravity|codex] [--force]',
    );
  }

  assertTaskId(taskId);

  const workspaceRoot = resolveWorkspaceRoot();
  const env = loadMergedEnv(workspaceRoot);
  const notifyEnabled = parseBoolean(
    env.AI_WORKFLOW_NOTIFY,
    parseBoolean(env.STOP_GUARD_NOTIFY, true),
  );

  const taskDir = path.join(workspaceRoot, 'ai_docs', 'tasks', taskId);
  const ledgerFile = path.join(
    workspaceRoot,
    '.agents',
    'runtime',
    'notification-ledger.json',
  );
  const ledger = safeReadJson(ledgerFile, { sent: {} });

  let status;
  let mode;
  let artifactFile;
  let artifactRelative;
  let extraLines = [];
  let suffix = '';

  if (stage === 'plan-created') {
    artifactFile = path.join(taskDir, 'plan.md');
    artifactRelative = `ai_docs/tasks/${taskId}/plan.md`;

    if (!existsSync(artifactFile)) {
      throw new Error(`plan.md 不存在：${artifactRelative}`);
    }

    status = 'PLAN_CREATED';
    mode = 'plan';
    extraLines = [
      `Plan：${artifactRelative}`,
      '下一步：执行 /execute-plan 或 /run-plan；Antigravity/Codex 可使用对应 execute-plan 入口。',
    ];
  } else {
    artifactFile = path.join(taskDir, 'review.md');
    artifactRelative = `ai_docs/tasks/${taskId}/review.md`;

    const review = parseReview(artifactFile, taskId);
    suffix = review.status;

    status = `REVIEW_${review.status}`;
    mode = 'review';

    extraLines = [
      `Review：${artifactRelative}`,
      `MUST_FIX：${review.mustFix}`,
      `SHOULD_FIX：${review.shouldFix}`,
      `OPTIONAL：${review.optional}`,
    ];

    if (review.status === 'NEEDS_FIX') {
      extraLines.push(
        '下一步：执行 /fix-review 或 /repair-review；默认 fixScope=recommended，会处理 MUST_FIX + SHOULD_FIX。',
      );
    } else if (review.status === 'PASS_WITH_ISSUES') {
      extraLines.push('下一步：当前只应剩 OPTIONAL；可由用户决定是否继续处理。');
    } else if (review.status === 'PASS') {
      extraLines.push('下一步：人工检查 Git Diff 后再决定 Commit / Push。');
    } else {
      extraLines.push('下一步：先处理 Review 报告中的阻塞原因。');
    }
  }

  const key = notificationKey(stage, taskId, artifactFile, suffix);

  if (!force && ledger.sent?.[key]) {
    process.stdout.write(
      `${JSON.stringify({
        ok: true,
        idempotent: true,
        stage,
        taskId,
        notificationKey: key,
      })}\n`,
    );
    return;
  }

  if (!notifyEnabled) {
    process.stdout.write(
      `${JSON.stringify({
        ok: true,
        skipped: true,
        reason: 'AI_WORKFLOW_NOTIFY=false',
        stage,
        taskId,
      })}\n`,
    );
    return;
  }

  const result = await sendFeishuNotification({
    workspaceRoot,
    status,
    taskId,
    mode,
    agentSource: source,
    agentProfile: readActiveProfile(workspaceRoot) || undefined,
    terminationReason: 'stage_completed',
    extraLines,
  });

  if (result?.ok) {
    ledger.sent ??= {};
    ledger.sent[key] = {
      stage,
      taskId,
      status,
      sentAt: new Date().toISOString(),
      source,
    };
    safeWriteJson(ledgerFile, ledger);
  }

  process.stdout.write(
    `${JSON.stringify({
      ok: Boolean(result?.ok),
      skipped: Boolean(result?.skipped),
      stage,
      taskId,
      status,
      notificationKey: key,
    })}\n`,
  );
}

try {
  await main();
} catch (error) {
  console.error(
    `[workflow-notify] ${sanitizeNotificationText(
      error?.stack || error,
      2000,
    )}`,
  );
  process.exitCode = 1;
}
