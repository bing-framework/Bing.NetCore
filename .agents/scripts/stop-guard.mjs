import {
  closeSync,
  existsSync,
  mkdirSync,
  openSync,
  readFileSync,
  renameSync,
  statSync,
  unlinkSync,
  writeFileSync,
} from 'node:fs';
import path from 'node:path';
import process from 'node:process';
import {
  loadProjectEnv,
  readEnvFile,
  sanitizeNotificationText,
  sendFeishuNotification,
} from './notify-feishu.mjs';

/**
 * Antigravity Stop Hook：
 * - 只处理当前项目明确标记为 active 的 execute-plan 任务；
 * - 只有 model_stop 且没有严格终态时才允许有限续跑；
 * - 终态先收口 runtime，再发送一次飞书通知（task-finish.mjs 的兜底）；
 * - 如果通用 task-finish.mjs 已经完成收口（active=false），则直接允许 Stop；
 * - stdout 永远只输出 Stop Hook 所需 JSON。
 */

const EXECUTION_STATUSES = new Set(['COMPLETED', 'PARTIAL', 'BLOCKED', 'FAILED']);

function readStdin() {
  return new Promise((resolve, reject) => {
    let input = '';
    process.stdin.setEncoding('utf8');
    process.stdin.on('data', (chunk) => {
      input += chunk;
    });
    process.stdin.on('end', () => {
      try {
        resolve(input.trim() ? JSON.parse(input) : {});
      } catch (error) {
        reject(error);
      }
    });
    process.stdin.on('error', reject);
  });
}

function parseBoolean(value, defaultValue = false) {
  if (value == null || value === '') {
    return defaultValue;
  }

  return ['1', 'true', 'yes', 'on', 'enabled'].includes(String(value).trim().toLowerCase());
}

function parseNonNegativeInteger(value, defaultValue) {
  const text = String(value ?? '').trim();
  if (!/^\d+$/u.test(text)) {
    return defaultValue;
  }

  return Number.parseInt(text, 10);
}

function resolveWorkspaceRoot(context) {
  const candidates = Array.isArray(context?.workspacePaths) ? context.workspacePaths : [];

  // 多根工作区优先选择包含 .agents 的项目，避免状态写入错误根目录。
  for (const candidate of candidates) {
    if (typeof candidate !== 'string' || !candidate.trim()) {
      continue;
    }

    const workspaceRoot = path.resolve(candidate);
    if (existsSync(path.join(workspaceRoot, '.agents'))) {
      return workspaceRoot;
    }
  }

  if (candidates[0]) {
    return path.resolve(candidates[0]);
  }

  return process.cwd();
}

function safeReadJson(filePath) {
  if (!existsSync(filePath)) {
    return null;
  }

  try {
    return JSON.parse(readFileSync(filePath, 'utf8'));
  } catch (error) {
    console.error(
      `[stop-guard] 无法读取 JSON：${filePath}\n${sanitizeNotificationText(error?.message || error, 500)}`,
    );
    return null;
  }
}

function safeWriteJson(filePath, value) {
  const temporaryPath = `${filePath}.${process.pid}.${Date.now()}.tmp`;

  try {
    mkdirSync(path.dirname(filePath), { recursive: true });
    writeFileSync(temporaryPath, `${JSON.stringify(value, null, 2)}\n`, 'utf8');
    renameSync(temporaryPath, filePath);
    return true;
  } catch (error) {
    console.error(
      `[stop-guard] 无法写入 JSON：${filePath}\n${sanitizeNotificationText(error?.message || error, 500)}`,
    );
    try {
      if (existsSync(temporaryPath)) {
        unlinkSync(temporaryPath);
      }
    } catch (cleanupError) {
      console.error(`[stop-guard] 无法清理临时状态文件：${cleanupError?.message || cleanupError}`);
    }
    return false;
  }
}

function normalizeRelativePath(value) {
  if (!value || typeof value !== 'string') {
    return null;
  }

  const normalized = path.posix.normalize(value.replaceAll('\\', '/'));
  if (path.posix.isAbsolute(normalized) || normalized === '..' || normalized.startsWith('../')) {
    return null;
  }

  return normalized.replace(/^\.\//u, '');
}

function resolveWorkspaceFile(workspaceRoot, relativePath) {
  const normalized = normalizeRelativePath(relativePath);
  if (!normalized) {
    return null;
  }

  const workspace = path.resolve(workspaceRoot);
  const filePath = path.resolve(workspace, normalized);
  const workspacePrefix = `${workspace}${path.sep}`;

  if (filePath !== workspace && !filePath.startsWith(workspacePrefix)) {
    return null;
  }

  return filePath;
}

function getTaskContext(workspaceRoot, mergedEnv) {
  const runtimeFileRelative = mergedEnv.AI_TASK_STATE_FILE || '.agents/runtime/current-task.json';
  const runtimeFile = path.isAbsolute(runtimeFileRelative)
    ? runtimeFileRelative
    : path.join(workspaceRoot, runtimeFileRelative);
  const runtime = safeReadJson(runtimeFile);

  // 只有显式 active=true 才代表 execute-plan 正在运行，避免脏数据误触发守卫。
  if (!runtime || runtime.active !== true) {
    return { runtimeFile, task: null };
  }

  const taskId = typeof runtime.taskId === 'string' ? runtime.taskId.trim() : '';
  if (!taskId) {
    return { runtimeFile, task: null };
  }

  const rawPlanPath = runtime.planPath || `ai_docs/tasks/${taskId}/plan.md`;
  const rawExecutionPath = runtime.executionPath || `ai_docs/tasks/${taskId}/execution.md`;
  const planPath = normalizeRelativePath(rawPlanPath);
  const executionPath = normalizeRelativePath(rawExecutionPath);
  const planFile = resolveWorkspaceFile(workspaceRoot, rawPlanPath);
  const executionFile = resolveWorkspaceFile(workspaceRoot, rawExecutionPath);

  return {
    runtimeFile,
    task: {
      ...runtime,
      taskId,
      planPath,
      executionPath,
      planFile,
      executionFile,
      pathError:
        planFile && executionFile ? null : 'planPath 或 executionPath 不是工作区内的相对路径',
    },
  };
}

/**
 * execution.md 的前三行是 Stop Guard 唯一信任的机器协议：
 * <!-- AI_EXECUTION_STATUS: COMPLETED -->
 * AI_TASK_ID: <taskId>
 * AI_EXECUTION_FINISHED_AT: <ISO-8601>
 */
function parseExecutionStatus(executionFile, expectedTaskId, startedAt) {
  if (!executionFile || !existsSync(executionFile)) {
    return { status: 'UNKNOWN', reason: 'execution.md 不存在' };
  }

  let lines;
  try {
    lines = readFileSync(executionFile, 'utf8')
      .replace(/^\uFEFF/u, '')
      .split(/\r?\n/u)
      .slice(0, 3)
      .map((line) => line.trim());
  } catch (error) {
    return {
      status: 'UNKNOWN',
      reason: `execution.md 无法读取：${sanitizeNotificationText(error?.message || error, 300)}`,
    };
  }

  const inProgressMatch = lines[0]?.match(
    /^<!--\s*AI_EXECUTION_STATUS\s*:\s*IN_PROGRESS\s*-->$/u,
  );
  const taskMatch = lines[1]?.match(/^AI_TASK_ID\s*:\s*(\S+)$/u);

  if (inProgressMatch) {
    if (!taskMatch || taskMatch[1] !== expectedTaskId) {
      return { status: 'UNKNOWN', reason: 'IN_PROGRESS 的 AI_TASK_ID 与当前任务不一致' };
    }

    return { status: 'UNKNOWN', reason: 'execution.md 当前处于 IN_PROGRESS' };
  }

  const statusMatch = lines[0]?.match(
    /^<!--\s*AI_EXECUTION_STATUS\s*:\s*(COMPLETED|PARTIAL|BLOCKED|FAILED)\s*-->$/u,
  );
  const finishedMatch = lines[2]?.match(/^AI_EXECUTION_FINISHED_AT\s*:\s*(\S+)$/u);

  if (!statusMatch || !taskMatch || !finishedMatch) {
    return { status: 'UNKNOWN', reason: 'execution.md 缺少前三行机器元数据' };
  }

  const finishedAt = Date.parse(finishedMatch[1]);
  const startedAtTime = Date.parse(String(startedAt || ''));
  if (!Number.isFinite(finishedAt) || !Number.isFinite(startedAtTime)) {
    return { status: 'UNKNOWN', reason: '任务开始或完成时间不是有效 ISO-8601' };
  }

  if (taskMatch[1] !== expectedTaskId) {
    return { status: 'UNKNOWN', reason: 'execution.md 的 AI_TASK_ID 与当前任务不一致' };
  }

  if (finishedAt < startedAtTime) {
    return { status: 'UNKNOWN', reason: 'execution.md 完成时间早于任务开始时间' };
  }

  return {
    status: statusMatch[1],
    reason: '严格机器元数据匹配',
    finishedAt: finishedMatch[1],
  };
}

function getGuardState(workspaceRoot) {
  const file = path.join(workspaceRoot, '.agents/runtime/stop-guard-state.json');
  const persisted = safeReadJson(file);
  const conversations = persisted?.conversations;

  return {
    file,
    value: {
      conversations:
        conversations && typeof conversations === 'object' && !Array.isArray(conversations)
          ? conversations
          : {},
    },
  };
}

function getConversationKey(taskId, conversationId) {
  return `${taskId}::${conversationId || 'unknown-conversation'}`;
}

function getContinueCount(guardState, taskId, conversationId) {
  const key = getConversationKey(taskId, conversationId);
  return Number(guardState.value.conversations[key]?.continueCount || 0);
}

function setContinueCount(guardState, taskId, conversationId, count) {
  const key = getConversationKey(taskId, conversationId);
  guardState.value.conversations[key] = {
    taskId,
    conversationId: conversationId || null,
    continueCount: count,
    updatedAt: new Date().toISOString(),
  };

  return safeWriteJson(guardState.file, guardState.value);
}

function clearContinueCount(guardState, taskId, conversationId) {
  const key = getConversationKey(taskId, conversationId);
  if (!guardState.value.conversations[key]) {
    return true;
  }

  delete guardState.value.conversations[key];
  return safeWriteJson(guardState.file, guardState.value);
}

function writeFinalRuntime(runtimeFile, task, context, finalStatus, notificationStatus) {
  const persistedTask = { ...task };
  delete persistedTask.planFile;
  delete persistedTask.executionFile;
  delete persistedTask.pathError;

  const now = new Date().toISOString();
  return safeWriteJson(runtimeFile, {
    ...persistedTask,
    active: false,
    status: finalStatus,
    finalStatus,
    completedAt: now,
    updatedAt: now,
    lastConversationId: context.conversationId || null,
    notificationStatus,
    notificationUpdatedAt: now,
  });
}

function updateNotificationStatus(runtimeFile, taskId, status, error) {
  const runtime = safeReadJson(runtimeFile);
  if (!runtime || runtime.taskId !== taskId) {
    return false;
  }

  const now = new Date().toISOString();
  runtime.notificationStatus = status;
  runtime.notificationUpdatedAt = now;
  if (error) {
    runtime.notificationError = sanitizeNotificationText(error, 500);
  } else {
    delete runtime.notificationError;
  }

  return safeWriteJson(runtimeFile, runtime);
}

function acquireFinalizationLock(runtimeFile) {
  const lockFile = `${runtimeFile}.stop-lock`;
  mkdirSync(path.dirname(lockFile), { recursive: true });

  try {
    const fd = openSync(lockFile, 'wx');
    writeFileSync(
      fd,
      `${JSON.stringify({ pid: process.pid, createdAt: new Date().toISOString() })}\n`,
      'utf8',
    );
    closeSync(fd);

    return () => {
      try {
        unlinkSync(lockFile);
      } catch (error) {
        if (error?.code !== 'ENOENT') {
          console.error(`[stop-guard] 无法清理 Stop 锁：${error?.message || error}`);
        }
      }
    };
  } catch (error) {
    if (error?.code !== 'EEXIST') {
      console.error(`[stop-guard] 无法取得 Stop 锁：${error?.message || error}`);
      return null;
    }

    try {
      if (Date.now() - statSync(lockFile).mtimeMs > 5 * 60 * 1000) {
        unlinkSync(lockFile);
        return acquireFinalizationLock(runtimeFile);
      }
    } catch (statError) {
      console.error(`[stop-guard] 无法检查 Stop 锁：${statError?.message || statError}`);
    }

    return null;
  }
}

async function notifySafely(workspaceRoot, data) {
  try {
    const result = await sendFeishuNotification({ workspaceRoot, ...data });
    if (result?.skipped) {
      console.error(`[stop-guard] 飞书通知已跳过：${result.reason}`);
    }
    return result;
  } catch (error) {
    const message = sanitizeNotificationText(error?.message || error, 500);
    console.error(`[stop-guard] 飞书通知失败：${message}`);
    return { skipped: false, ok: false, error: message };
  }
}

async function finalizeTask({
  workspaceRoot,
  runtimeFile,
  task,
  context,
  status,
  notifyEnabled,
  message,
}) {
  const releaseLock = acquireFinalizationLock(runtimeFile);
  if (!releaseLock) {
    return { claimed: false, reason: '另一个 Stop Hook 正在收口当前任务' };
  }

  try {
    const currentRuntime = safeReadJson(runtimeFile);
    if (
      !currentRuntime ||
      currentRuntime.active !== true ||
      currentRuntime.taskId !== task.taskId
    ) {
      return { claimed: false, reason: '当前任务已被其他 Stop Hook 收口' };
    }

    const persisted = writeFinalRuntime(
      runtimeFile,
      task,
      context,
      status,
      notifyEnabled ? 'pending' : 'disabled',
    );
    if (!persisted) {
      return { claimed: true, persisted: false, reason: '无法写入任务终态' };
    }

    if (!notifyEnabled) {
      return { claimed: true, persisted: true, notification: { skipped: true } };
    }

    const notification = await notifySafely(workspaceRoot, message);
    const notificationStatus = notification?.skipped
      ? 'skipped'
      : notification?.ok
        ? 'sent'
        : 'failed';
    updateNotificationStatus(runtimeFile, task.taskId, notificationStatus, notification?.error);

    return { claimed: true, persisted: true, notification };
  } finally {
    releaseLock();
  }
}

function outputDecision(decision, reason) {
  const payload = reason ? { decision, reason } : { decision };
  process.stdout.write(`${JSON.stringify(payload)}\n`);
}

function buildNotificationMessage(
  task,
  context,
  executionRelative,
  status,
  extraLines = [],
  error,
) {
  return {
    status,
    taskId: task.taskId,
    mode: task.mode || 'plan-execution',
    reviewRound: Number.isInteger(task.reviewRound) ? task.reviewRound : 0,
    agentSource: task.agentSource || 'antigravity',
    modelName: sanitizeNotificationText(context.modelName, 200),
    terminationReason: sanitizeNotificationText(context.terminationReason, 200),
    executionPath: executionRelative,
    extraLines,
    error: error ? sanitizeNotificationText(error, 1500) : undefined,
  };
}

async function main() {
  let context;
  try {
    context = await readStdin();
  } catch (error) {
    console.error(
      `[stop-guard] stdin 不是有效 JSON：${sanitizeNotificationText(error?.message || error, 500)}`,
    );
    outputDecision('stop', 'Stop Hook 输入无效，已停止以避免错误续跑');
    return;
  }

  const workspaceRoot = resolveWorkspaceRoot(context);
  const projectConfig = loadProjectEnv(workspaceRoot);
  const fileEnv = {
    ...readEnvFile(projectConfig.envFilePath),
    ...process.env,
  };
  const { runtimeFile, task } = getTaskContext(workspaceRoot, fileEnv);

  // 非 execute-plan 会话：完全不干预，也不发飞书。
  if (!task) {
    outputDecision('stop');
    return;
  }

  const guardEnabled = parseBoolean(fileEnv.STOP_GUARD_ENABLED, true);
  const notifyEnabled = parseBoolean(fileEnv.STOP_GUARD_NOTIFY, true);
  const maxContinues = parseNonNegativeInteger(fileEnv.STOP_GUARD_MAX_CONTINUES, 3);
  const conversationId = context.conversationId || 'unknown-conversation';
  const terminationReason = String(context.terminationReason || '').toLowerCase();
  const executionRelative = task.executionPath || 'execution.md';
  const statusInfo = task.pathError
    ? { status: 'UNKNOWN', reason: task.pathError }
    : parseExecutionStatus(task.executionFile, task.taskId, task.startedAt);
  const status = statusInfo.status;
  const guardState = getGuardState(workspaceRoot);

  if (terminationReason === 'error' || context.error) {
    await finalizeTask({
      workspaceRoot,
      runtimeFile,
      task,
      context,
      status: 'ERROR',
      notifyEnabled,
      message: buildNotificationMessage(
        task,
        context,
        executionRelative,
        'ERROR',
        [],
        context.error || 'Antigravity execution terminated with error.',
      ),
    });
    outputDecision('stop');
    return;
  }

  if (terminationReason === 'max_steps_exceeded') {
    await finalizeTask({
      workspaceRoot,
      runtimeFile,
      task,
      context,
      status: 'MAX_STEPS_EXCEEDED',
      notifyEnabled,
      message: buildNotificationMessage(task, context, executionRelative, 'MAX_STEPS_EXCEEDED', [
        '已停止自动续跑，建议人工检查任务范围、失败原因或拆分计划。',
      ]),
    });
    outputDecision('stop');
    return;
  }

  // 有效终态优先于 fullyIdle，避免已完成报告因后台命令未收尾而被再次续跑。
  if (EXECUTION_STATUSES.has(status)) {
    const extraLines = [];
    if (status === 'COMPLETED') {
      if (task.mode === 'review-fix') {
        extraLines.push(
          `Review 修复${Number.isInteger(task.reviewRound) && task.reviewRound > 0 ? ` Round ${task.reviewRound}` : ''} 已完成。`,
        );
        extraLines.push('下一步：回到 VS Code 再次执行 /review-plan 进行复审。');
      } else {
        extraLines.push('计划实施已达到 COMPLETED 终态。');
        extraLines.push('下一步：回到 VS Code 执行 /review-plan 进行独立验收。');
      }
    } else if (status === 'PARTIAL') {
      extraLines.push('任务仅部分完成，请查看 execution.md 的未完成事项。');
    } else if (status === 'BLOCKED') {
      extraLines.push('任务遇到阻塞，请查看 execution.md 的阻塞原因。');
    } else if (status === 'FAILED') {
      extraLines.push('任务执行失败，请查看 execution.md 的失败证据。');
    }

    await finalizeTask({
      workspaceRoot,
      runtimeFile,
      task,
      context,
      status,
      notifyEnabled,
      message: buildNotificationMessage(task, context, executionRelative, status, extraLines),
    });
    clearContinueCount(guardState, task.taskId, conversationId);
    outputDecision('stop');
    return;
  }

  // 只有模型主动停止且没有严格终态时才允许自动续跑。
  if (terminationReason === 'model_stop' && guardEnabled) {
    const currentCount = getContinueCount(guardState, task.taskId, conversationId);
    if (currentCount < maxContinues) {
      const persisted = setContinueCount(guardState, task.taskId, conversationId, currentCount + 1);
      if (persisted) {
        const reason =
          context.fullyIdle === false
            ? `当前任务 ${task.taskId} 仍有后台命令或异步任务未结束。请等待任务完成后继续执行。`
            : `当前 execute-plan 任务 ${task.taskId} 尚未形成可验收终态。`;
        outputDecision(
          'continue',
          [
            reason,
            `请继续读取并执行 ${task.planPath || `ai_docs/tasks/${task.taskId}/plan.md`} 中尚未完成的事项，不要重新规划，也不要重复已完成工作。`,
            `完成后必须生成 ${executionRelative}，前三行必须严格写入：`,
            '<!-- AI_EXECUTION_STATUS: COMPLETED|PARTIAL|BLOCKED|FAILED -->',
            `AI_TASK_ID: ${task.taskId}`,
            'AI_EXECUTION_FINISHED_AT: <ISO-8601>',
          ].join('\n'),
        );
        return;
      }

      await finalizeTask({
        workspaceRoot,
        runtimeFile,
        task,
        context,
        status: 'BLOCKED',
        notifyEnabled,
        message: buildNotificationMessage(task, context, executionRelative, 'BLOCKED', [
          '无法持久化自动续跑次数，已停止以避免无限循环。',
        ]),
      });
      outputDecision('stop');
      return;
    }

    clearContinueCount(guardState, task.taskId, conversationId);
    await finalizeTask({
      workspaceRoot,
      runtimeFile,
      task,
      context,
      status: 'BLOCKED',
      notifyEnabled,
      message: buildNotificationMessage(task, context, executionRelative, 'BLOCKED', [
        `Stop Guard 已达到最大自动续跑次数：${maxContinues}。`,
        `execution.md 状态：${statusInfo.reason}。`,
        '已停止自动循环，请人工检查。',
      ]),
    });
    outputDecision('stop');
    return;
  }

  clearContinueCount(guardState, task.taskId, conversationId);
  await finalizeTask({
    workspaceRoot,
    runtimeFile,
    task,
    context,
    status: 'STOPPED',
    notifyEnabled,
    message: buildNotificationMessage(task, context, executionRelative, 'STOPPED', [
      `未形成严格终态，Stop Guard 未自动续跑。原因：${statusInfo.reason}。`,
    ]),
  });
  outputDecision('stop');
}

try {
  await main();
} catch (error) {
  const message = sanitizeNotificationText(error?.message || error, 500);
  console.error(`[stop-guard] 未处理异常：${message}`);
  outputDecision('stop', 'Stop Guard 内部异常，已停止以避免错误续跑');
}
