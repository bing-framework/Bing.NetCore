import {
  existsSync,
  mkdirSync,
  readFileSync,
  renameSync,
  writeFileSync,
} from 'node:fs';
import path from 'node:path';
import process from 'node:process';

/**
 * execute-plan / fix-review 的确定性运行状态注册器。
 *
 * 用法（建议在项目根目录运行）：
 *
 *   node .agents/scripts/task-state.mjs start <taskId>
 *   node .agents/scripts/task-state.mjs review-fix <taskId>
 *   node .agents/scripts/task-state.mjs status
 *
 * 也兼容 cwd 位于 .agents 目录的情况。
 */

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

  throw new Error('无法定位包含 .agents 的项目根目录。请在项目目录执行，或传入 --workspace <path>。');
}

function assertTaskId(taskId) {
  if (!taskId || !/^[A-Za-z0-9._-]+$/u.test(taskId)) {
    throw new Error(
      'taskId 无效。建议只使用字母、数字、点、下划线和短横线，例如：fund-analysis-v2-convergence。',
    );
  }
}


function readOption(name) {
  const index = process.argv.indexOf(name);
  if (index >= 0 && process.argv[index + 1]) {
    return process.argv[index + 1];
  }
  return null;
}

function normalizeAgentSource(value) {
  const normalized = String(value || '')
    .trim()
    .toLowerCase();

  if (['copilot', 'antigravity', 'codex'].includes(normalized)) {
    return normalized;
  }

  return normalized || 'unknown';
}

function getAgentSource() {
  return normalizeAgentSource(
    readOption('--source') ||
      process.env.AI_AGENT_SOURCE ||
      process.env.AGENT_SOURCE ||
      'unknown',
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

function getPaths(workspaceRoot, taskId) {
  return {
    runtimeFile: path.join(workspaceRoot, '.agents', 'runtime', 'current-task.json'),
    planRelative: `ai_docs/tasks/${taskId}/plan.md`,
    executionRelative: `ai_docs/tasks/${taskId}/execution.md`,
    reviewRelative: `ai_docs/tasks/${taskId}/review.md`,
    planFile: path.join(workspaceRoot, 'ai_docs', 'tasks', taskId, 'plan.md'),
    executionFile: path.join(workspaceRoot, 'ai_docs', 'tasks', taskId, 'execution.md'),
    reviewFile: path.join(workspaceRoot, 'ai_docs', 'tasks', taskId, 'review.md'),
  };
}

function parseStrictReviewMetadata(reviewFile, expectedTaskId) {
  if (!existsSync(reviewFile)) {
    throw new Error(`review.md 不存在：${reviewFile}`);
  }

  const lines = readFileSync(reviewFile, 'utf8')
    .replace(/^\uFEFF/u, '')
    .split(/\r?\n/u)
    .slice(0, 3)
    .map((line) => line.trim());

  const statusMatch = lines[0]?.match(
    /^<!--\s*AI_REVIEW_STATUS\s*:\s*(PASS|PASS_WITH_ISSUES|NEEDS_FIX|BLOCKED)\s*-->$/u,
  );
  const taskMatch = lines[1]?.match(/^AI_TASK_ID\s*:\s*(\S+)$/u);
  const reviewedAtMatch = lines[2]?.match(/^AI_REVIEWED_AT\s*:\s*(\S+)$/u);

  if (!statusMatch || !taskMatch || !reviewedAtMatch) {
    throw new Error(
      'review.md 缺少前三行机器元数据。请先使用新版 /review-plan 重新生成 review.md。',
    );
  }

  if (taskMatch[1] !== expectedTaskId) {
    throw new Error(
      `review.md 的 AI_TASK_ID=${taskMatch[1]} 与当前 taskId=${expectedTaskId} 不一致。`,
    );
  }

  if (!Number.isFinite(Date.parse(reviewedAtMatch[1]))) {
    throw new Error('review.md 的 AI_REVIEWED_AT 不是有效 ISO-8601 时间。');
  }

  return {
    status: statusMatch[1],
    reviewedAt: reviewedAtMatch[1],
  };
}

function stripOldExecutionMetadata(lines) {
  let index = 0;

  if (/^<!--\s*AI_EXECUTION_STATUS\s*:/u.test(lines[index] || '')) {
    index += 1;
  }

  if (/^AI_TASK_ID\s*:/u.test(lines[index] || '')) {
    index += 1;
  }

  if (/^AI_EXECUTION_(?:FINISHED|STARTED)_AT\s*:/u.test(lines[index] || '')) {
    index += 1;
  }

  while (lines[index] === '') {
    index += 1;
  }

  return lines.slice(index);
}

function markExecutionInProgress(executionFile, taskId, startedAt) {
  mkdirSync(path.dirname(executionFile), { recursive: true });

  let body = [];
  if (existsSync(executionFile)) {
    body = stripOldExecutionMetadata(
      readFileSync(executionFile, 'utf8')
        .replace(/^\uFEFF/u, '')
        .split(/\r?\n/u),
    );
  }

  if (body.length === 0) {
    body = [
      '# 实施执行报告',
      '',
      '> 当前任务正在执行中。最终完成时由 Executor 写入严格终态机器元数据。',
    ];
  }

  const content = [
    '<!-- AI_EXECUTION_STATUS: IN_PROGRESS -->',
    `AI_TASK_ID: ${taskId}`,
    `AI_EXECUTION_STARTED_AT: ${startedAt}`,
    '',
    ...body,
  ].join('\n');

  writeFileSync(executionFile, `${content.replace(/\s+$/u, '')}\n`, 'utf8');
}

function buildRuntime({
  taskId,
  mode,
  reviewRound,
  paths,
  startedAt,
  agentSource,
}) {
  return {
    active: true,
    taskId,
    mode,
    agentSource,
    planPath: paths.planRelative,
    executionPath: paths.executionRelative,
    reviewPath: paths.reviewRelative,
    reviewRound,
    status: 'implementing',
    startedAt,
    updatedAt: startedAt,
  };
}

function printJson(value) {
  process.stdout.write(`${JSON.stringify(value, null, 2)}\n`);
}

function startPlan(workspaceRoot, taskId) {
  assertTaskId(taskId);
  const paths = getPaths(workspaceRoot, taskId);

  if (!existsSync(paths.planFile)) {
    throw new Error(`plan.md 不存在：${paths.planRelative}`);
  }

  const existing = safeReadJson(paths.runtimeFile);
  if (
    existing?.active === true &&
    existing?.taskId === taskId &&
    existing?.mode === 'plan-execution'
  ) {
    printJson({
      reused: true,
      message: '当前 plan-execution 已处于 active 状态，不重复重置 startedAt。',
      runtime: existing,
    });
    return;
  }

  const now = new Date().toISOString();
  markExecutionInProgress(paths.executionFile, taskId, now);

  const runtime = buildRuntime({
    taskId,
    mode: 'plan-execution',
    reviewRound: 0,
    paths,
    startedAt: now,
    agentSource: getAgentSource(),
  });

  safeWriteJson(paths.runtimeFile, runtime);
  printJson({ reused: false, runtime });
}

function startReviewFix(workspaceRoot, taskId) {
  assertTaskId(taskId);
  const paths = getPaths(workspaceRoot, taskId);

  if (!existsSync(paths.planFile)) {
    throw new Error(`plan.md 不存在：${paths.planRelative}`);
  }

  const review = parseStrictReviewMetadata(paths.reviewFile, taskId);
  if (review.status !== 'NEEDS_FIX') {
    throw new Error(
      `review.md 当前状态为 ${review.status}，只有 NEEDS_FIX 才允许进入 REVIEW_FIX。`,
    );
  }

  const existing = safeReadJson(paths.runtimeFile);

  if (
    existing?.active === true &&
    existing?.taskId === taskId &&
    existing?.mode === 'review-fix'
  ) {
    printJson({
      reused: true,
      message: '当前 review-fix 已处于 active 状态，不重复增加 reviewRound。',
      runtime: existing,
    });
    return;
  }

  const previousRound =
    existing?.taskId === taskId && Number.isInteger(existing?.reviewRound)
      ? existing.reviewRound
      : 0;

  const reviewRound = previousRound + 1;
  const now = new Date().toISOString();

  // 关键：重新激活 Review Fix 时必须把旧终态改为非终态，
  // 否则 Stop Guard 会把上一轮 COMPLETED/PARTIAL 当成本轮已完成。
  markExecutionInProgress(paths.executionFile, taskId, now);

  const runtime = buildRuntime({
    taskId,
    mode: 'review-fix',
    reviewRound,
    paths,
    startedAt: now,
    agentSource: getAgentSource(),
  });

  safeWriteJson(paths.runtimeFile, runtime);
  printJson({
    reused: false,
    reviewStatus: review.status,
    runtime,
  });
}

function showStatus(workspaceRoot) {
  const runtimeFile = path.join(
    workspaceRoot,
    '.agents',
    'runtime',
    'current-task.json',
  );

  printJson({
    workspaceRoot,
    runtimeFile,
    runtime: safeReadJson(runtimeFile),
  });
}

function main() {
  const workspaceRoot = resolveWorkspaceRoot();
  const action = process.argv[2];

  if (action === 'status') {
    showStatus(workspaceRoot);
    return;
  }

  const taskId = process.argv[3];

  if (action === 'start') {
    startPlan(workspaceRoot, taskId);
    return;
  }

  if (action === 'review-fix') {
    startReviewFix(workspaceRoot, taskId);
    return;
  }

  throw new Error(
    [
      '用法：',
      '  node .agents/scripts/task-state.mjs start <taskId> [--source copilot|antigravity|codex]',
      '  node .agents/scripts/task-state.mjs review-fix <taskId> [--source copilot|antigravity|codex]',
      '  node .agents/scripts/task-state.mjs status',
    ].join('\n'),
  );
}

try {
  main();
} catch (error) {
  console.error(`[task-state] ${error?.stack || error}`);
  process.exitCode = 1;
}
