import { createHmac } from 'node:crypto';
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import process from 'node:process';

/**
 * 飞书通知工具。
 *
 * 特性：
 * 1. 配置跟随项目，默认读取 <workspace>/.agents/.env.local；
 * 2. process.env 优先级高于 .env.local，便于 CI/临时覆盖；
 * 3. 不依赖 dotenv / axios 等第三方包；
 * 4. 支持飞书自定义机器人签名校验；
 * 5. 默认使用 Card JSON 2.0 消息卡片；
 * 6. 可通过 FEISHU_MESSAGE_MODE=text 回退为纯文本。
 */

const DEFAULT_ENV_FILE = '.agents/.env.local';

function stripOuterQuotes(value) {
  if (value.length >= 2) {
    const first = value[0];
    const last = value[value.length - 1];
    if ((first === '"' && last === '"') || (first === "'" && last === "'")) {
      return value.slice(1, -1);
    }
  }
  return value;
}

export function readEnvFile(filePath) {
  if (!existsSync(filePath)) {
    return {};
  }

  const result = {};
  const content = readFileSync(filePath, 'utf8');

  for (const rawLine of content.split(/\r?\n/u)) {
    let line = rawLine.trim();

    if (!line || line.startsWith('#')) {
      continue;
    }

    if (line.startsWith('export ')) {
      line = line.slice('export '.length).trim();
    }

    const separator = line.indexOf('=');

    if (separator <= 0) {
      continue;
    }

    const key = line.slice(0, separator).trim();
    let value = line.slice(separator + 1).trim();

    if (!/^[A-Za-z_][A-Za-z0-9_]*$/u.test(key)) {
      continue;
    }

    value = stripOuterQuotes(value);
    result[key] = value;
  }

  return result;
}

function parseBoolean(value, defaultValue = false) {
  if (value == null || value === '') {
    return defaultValue;
  }

  return ['1', 'true', 'yes', 'on', 'enabled'].includes(String(value).trim().toLowerCase());
}

export function loadProjectEnv(workspaceRoot) {
  const envFileRelativePath = process.env.AI_WORKFLOW_ENV_FILE || process.env.ANTIGRAVITY_ENV_FILE || DEFAULT_ENV_FILE;

  const envFilePath = path.isAbsolute(envFileRelativePath)
    ? envFileRelativePath
    : path.join(workspaceRoot, envFileRelativePath);

  const fileEnv = readEnvFile(envFilePath);
  const merged = { ...fileEnv, ...process.env };

  const messageMode = String(merged.FEISHU_MESSAGE_MODE || 'card')
    .trim()
    .toLowerCase();

  return {
    envFilePath,
    enabled: parseBoolean(merged.FEISHU_ENABLED, true),
    webhookUrl: merged.FEISHU_WEBHOOK_URL?.trim() || '',
    secret: merged.FEISHU_SECRET?.trim() || '',
    projectName: merged.FEISHU_PROJECT_NAME?.trim() || path.basename(workspaceRoot),
    messagePrefix: merged.FEISHU_MESSAGE_PREFIX?.trim() || 'AI Workflow',
    messageMode: messageMode === 'text' ? 'text' : 'card',
    timeoutMs: Number.parseInt(merged.FEISHU_TIMEOUT_MS || '10000', 10) || 10000,
  };
}

/**
 * 飞书自定义机器人签名。
 */
function createFeishuSignature(secret, timestamp) {
  const stringToSign = `${timestamp}\n${secret}`;

  return createHmac('sha256', stringToSign).update('').digest('base64');
}

function normalizeStatus(status) {
  return String(status || 'STOPPED')
    .trim()
    .toUpperCase();
}

function getStatusPresentation(status) {
  switch (normalizeStatus(status)) {
    case 'COMPLETED':
      return {
        icon: '✅',
        title: '任务执行完成',
        label: 'COMPLETED',
        template: 'green',
      };

    case 'PARTIAL':
      return {
        icon: '⚠️',
        title: '任务部分完成',
        label: 'PARTIAL',
        template: 'yellow',
      };

    case 'BLOCKED':
      return {
        icon: '⛔',
        title: '任务执行阻塞',
        label: 'BLOCKED',
        template: 'orange',
      };

    case 'FAILED':
      return {
        icon: '❌',
        title: '任务执行失败',
        label: 'FAILED',
        template: 'red',
      };

    case 'ERROR':
      return {
        icon: '❌',
        title: 'Agent 执行异常',
        label: 'ERROR',
        template: 'red',
      };

    case 'MAX_STEPS_EXCEEDED':
      return {
        icon: '⚠️',
        title: '达到最大执行步数',
        label: 'MAX_STEPS_EXCEEDED',
        template: 'orange',
      };

    default:
      return {
        icon: 'ℹ️',
        title: 'Agent 已停止',
        label: normalizeStatus(status),
        template: 'blue',
      };
  }
}

/**
 * 清理动态通知内容，避免 Token、Cookie、Webhook 和本地敏感路径进入共享群。
 */
export function sanitizeNotificationText(value, maxLength = 2000) {
  if (value == null) {
    return '';
  }

  return String(value)
    .replace(
      /((?:authorization|proxy-authorization|cookie|set-cookie|x-api-key|api[-_ ]?key|token|secret|password|webhook))\s*[=:]\s*(?:bearer\s+)?[^\s,;]+/giu,
      '$1=[REDACTED]',
    )
    .replace(/https?:\/\/[^\s)]+/giu, (url) => {
      try {
        const parsed = new URL(url);
        if (parsed.pathname.includes('/hook/') || parsed.search) {
          return `${parsed.origin}${parsed.pathname.includes('/hook/') ? '/[REDACTED]' : parsed.pathname}`;
        }
      } catch {
        return '[REDACTED_URL]';
      }

      return url;
    })
    .replaceAll('\\', '/')
    .replaceAll('<', '＜')
    .replaceAll('>', '＞')
    .slice(0, maxLength);
}

function safeMd(value, maxLength = 2000) {
  return sanitizeNotificationText(value, maxLength);
}

function mdRow(label, value) {
  if (value == null || value === '') {
    return null;
  }

  return `**${label}**  ${safeMd(value)}`;
}

function getModeLabel(mode) {
  const normalized = String(mode || 'plan-execution').trim().toLowerCase();

  if (normalized === 'review-fix') {
    return 'REVIEW_FIX';
  }

  if (normalized === 'plan-execution') {
    return 'PLAN_EXECUTION';
  }

  return sanitizeNotificationText(mode, 100) || 'UNKNOWN';
}

export function buildFeishuText({
  projectName,
  messagePrefix,
  status,
  taskId,
  mode,
  reviewRound,
  agentSource,
  modelName,
  terminationReason,
  executionPath,
  error,
  extraLines = [],
}) {
  const p = getStatusPresentation(status);

  const lines = [`${p.icon} ${p.label} · ${messagePrefix}`, '', `项目：${projectName}`];

  if (taskId) {
    lines.push(`任务：${taskId}`);
  }

  lines.push(`模式：${getModeLabel(mode)}`);

  if (agentSource) {
    lines.push(`执行器：${String(agentSource).toUpperCase()}`);
  }

  if (getModeLabel(mode) === 'REVIEW_FIX' && Number.isInteger(reviewRound) && reviewRound > 0) {
    lines.push(`修复轮次：Round ${reviewRound}`);
  }

  if (modelName) {
    lines.push(`模型：${modelName}`);
  }

  if (terminationReason) {
    lines.push(`终止原因：${terminationReason}`);
  }

  if (executionPath) {
    lines.push(`执行报告：${executionPath}`);
  }

  if (error) {
    lines.push('', `错误：${sanitizeNotificationText(error, 1500)}`);
  }

  if (Array.isArray(extraLines) && extraLines.length > 0) {
    lines.push('', ...extraLines.filter(Boolean));
  }

  return lines.join('\n');
}

/**
 * 构建飞书 Card JSON 2.0。
 *
 * 设计原则：
 * - Header 用状态色区分成功 / 阻塞 / 失败；
 * - 关键信息使用 Markdown 分组，避免纯文本堆叠；
 * - 错误信息单独成块；
 * - extraLines 用于“下一步”等行动提示。
 */
export function buildFeishuCard({
  projectName,
  messagePrefix,
  status,
  taskId,
  mode,
  reviewRound,
  agentSource,
  modelName,
  terminationReason,
  executionPath,
  error,
  extraLines = [],
}) {
  const p = getStatusPresentation(status);

  const summaryRows = [
    mdRow('项目', projectName),
    mdRow('任务', taskId),
    mdRow('模式', getModeLabel(mode)),
    agentSource ? mdRow('执行器', String(agentSource).toUpperCase()) : null,
    getModeLabel(mode) === 'REVIEW_FIX' && Number.isInteger(reviewRound) && reviewRound > 0
      ? mdRow('修复轮次', `Round ${reviewRound}`)
      : null,
    mdRow('状态', `${p.icon} ${p.label}`),
  ].filter(Boolean);

  const detailRows = [
    mdRow('模型', modelName),
    mdRow('终止原因', terminationReason),
    mdRow('执行报告', executionPath),
  ].filter(Boolean);

  const elements = [];

  if (summaryRows.length > 0) {
    elements.push({
      tag: 'div',
      text: {
        tag: 'lark_md',
        content: summaryRows.join('\n'),
      },
    });
  }

  if (detailRows.length > 0) {
    elements.push({ tag: 'hr' });
    elements.push({
      tag: 'div',
      text: {
        tag: 'lark_md',
        content: detailRows.join('\n'),
      },
    });
  }

  if (error) {
    elements.push({ tag: 'hr' });
    elements.push({
      tag: 'div',
      text: {
        tag: 'lark_md',
        content: `**❌ 错误信息**\n${safeMd(error, 1500)}`,
      },
    });
  }

  const lines = Array.isArray(extraLines)
    ? extraLines.filter(Boolean).map((line) => safeMd(line, 800))
    : [];

  if (lines.length > 0) {
    elements.push({ tag: 'hr' });
    elements.push({
      tag: 'div',
      text: {
        tag: 'lark_md',
        content: `**下一步 / 说明**\n${lines.map((x) => `• ${x}`).join('\n')}`,
      },
    });
  }

  return {
    schema: '2.0',
    header: {
      template: p.template,
      title: {
        tag: 'plain_text',
        content: `${p.icon} ${p.title}`,
      },
      subtitle: {
        tag: 'plain_text',
        content: messagePrefix,
      },
    },
    body: {
      elements,
    },
  };
}

function buildPayload(config, message) {
  if (config.messageMode === 'text') {
    return {
      msg_type: 'text',
      content: {
        text: buildFeishuText({
          projectName: config.projectName,
          messagePrefix: config.messagePrefix,
          ...message,
        }),
      },
    };
  }

  return {
    msg_type: 'interactive',
    card: buildFeishuCard({
      projectName: config.projectName,
      messagePrefix: config.messagePrefix,
      ...message,
    }),
  };
}

/**
 * 发送飞书通知。
 *
 * 注意：
 * - 只向 stderr 输出诊断；
 * - 不向 stdout 输出任何内容，避免破坏 Stop Hook JSON 协议。
 */
export async function sendFeishuNotification({ workspaceRoot, ...message }) {
  const config = loadProjectEnv(workspaceRoot);

  if (!config.enabled) {
    return {
      skipped: true,
      reason: 'FEISHU_ENABLED=false',
    };
  }

  if (!config.webhookUrl) {
    return {
      skipped: true,
      reason: `${config.envFilePath} 未配置 FEISHU_WEBHOOK_URL`,
    };
  }

  const payload = buildPayload(config, message);

  if (config.secret) {
    const timestamp = Math.floor(Date.now() / 1000).toString();

    payload.timestamp = timestamp;
    payload.sign = createFeishuSignature(config.secret, timestamp);
  }

  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), config.timeoutMs);

  try {
    const response = await fetch(config.webhookUrl, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json; charset=utf-8',
      },
      body: JSON.stringify(payload),
      signal: controller.signal,
    });

    const raw = await response.text();
    let body = null;

    try {
      body = raw ? JSON.parse(raw) : null;
    } catch {
      body = null;
    }

    if (!response.ok) {
      throw new Error(`飞书 HTTP ${response.status}: ${raw.slice(0, 1000)}`);
    }

    // 兼容飞书不同返回结构。
    const code = body?.code ?? body?.StatusCode ?? body?.statusCode;

    if (code != null && Number(code) !== 0) {
      const msg = body?.msg ?? body?.StatusMessage ?? body?.statusMessage ?? raw;

      throw new Error(`飞书返回失败 code=${code}: ${String(msg).slice(0, 1000)}`);
    }

    return {
      skipped: false,
      ok: true,
      mode: config.messageMode,
      response: body ?? raw,
    };
  } finally {
    clearTimeout(timeout);
  }
}

function findWorkspaceRootFromArgs() {
  const workspaceIndex = process.argv.indexOf('--workspace');

  if (workspaceIndex >= 0 && process.argv[workspaceIndex + 1]) {
    return path.resolve(process.argv[workspaceIndex + 1]);
  }

  return process.cwd();
}

async function runCli() {
  const isSendTest = process.argv.includes('--send-test');
  const isDryRun = process.argv.includes('--dry-run');

  if (!isSendTest && !isDryRun) {
    return;
  }

  const workspaceRoot = findWorkspaceRootFromArgs();

  try {
    const message = {
      status: 'COMPLETED',
      taskId: 'feishu-card-test',
      mode: 'review-fix',
      reviewRound: 1,
      agentSource: 'copilot',
      modelName: 'manual-test',
      terminationReason: 'manual_test',
      executionPath: 'ai_docs/tasks/feishu-card-test/execution.md',
      extraLines: [
        '实现已经完成，可以回到 VS Code 执行 /review-plan。',
        '本次未执行 git commit / git push。',
      ],
    };

    if (isDryRun) {
      const config = loadProjectEnv(workspaceRoot);
      process.stdout.write(`${JSON.stringify(buildPayload(config, message), null, 2)}\n`);
      return;
    }

    const result = await sendFeishuNotification({ workspaceRoot, ...message });

    if (result.skipped) {
      console.error(`[feishu] 已跳过：${result.reason}`);
      process.exitCode = 2;
      return;
    }

    console.error(`[feishu] 测试消息发送成功，模式：${result.mode}。`);
  } catch (error) {
    console.error(`[feishu] 测试消息发送失败：${error?.stack || error}`);
    process.exitCode = 1;
  }
}

await runCli();
