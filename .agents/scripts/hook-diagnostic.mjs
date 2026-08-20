import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';

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

function resolveWorkspaceRoot(context) {
    const candidates = Array.isArray(context?.workspacePaths)
        ? context.workspacePaths
        : [];

    // 多根工作区优先选择包含 .agents 的项目，避免日志写入错误根目录。
    for (const candidate of candidates) {
        if (typeof candidate !== 'string' || !candidate.trim()) {
            continue;
        }

        const workspaceRoot = path.resolve(candidate);
        if (fs.existsSync(path.join(workspaceRoot, '.agents'))) {
            return workspaceRoot;
        }
    }

    const currentDirectory = path.resolve(process.cwd());
    if (
        path.basename(currentDirectory) === '.agents' &&
        fs.existsSync(path.join(currentDirectory, 'hooks.json'))
    ) {
        return path.dirname(currentDirectory);
    }

    if (fs.existsSync(path.join(currentDirectory, '.agents'))) {
        return currentDirectory;
    }

    const firstCandidate = candidates.find(
        (candidate) => typeof candidate === 'string' && candidate.trim(),
    );
    return firstCandidate ? path.resolve(firstCandidate) : currentDirectory;
}

async function main() {
    let context = {};
    let inputParseFailed = false;

    try {
        context = await readStdin();
    } catch {
        // 诊断 Hook 不能因为输入异常阻断 Antigravity 的正常工具流程。
        inputParseFailed = true;
    }

    const workspaceRoot = resolveWorkspaceRoot(context);
    const runtimeDirectory = path.join(workspaceRoot, '.agents', 'runtime');
    const toolCall = context?.toolCall;
    const toolName =
        typeof toolCall?.name === 'string' ? toolCall.name : 'unknown';
    const stepIndex = Number.isInteger(context?.stepIdx)
        ? context.stepIdx
        : 'unknown';

    fs.mkdirSync(runtimeDirectory, { recursive: true });
    fs.appendFileSync(
        path.join(runtimeDirectory, 'hooks.log'),
        [
            '==============================',
            new Date().toISOString(),
            'EVENT=PostToolUse',
            `workspaceRoot=${workspaceRoot}`,
            `processCwd=${process.cwd()}`,
            `toolName=${toolName}`,
            `stepIdx=${stepIndex}`,
            `errorPresent=${Boolean(context?.error)}`,
            `inputParseFailed=${inputParseFailed}`,
            '',
        ].join('\n'),
        'utf8',
    );

    // stdout 是 Hook 协议通道，只返回官方要求的空 JSON，不混入诊断文本。
    process.stdout.write('{}\n');
}

main().catch((error) => {
    console.error(`[hook-diagnostic] ${error?.stack || error}`);
    process.stdout.write('{}\n');
});