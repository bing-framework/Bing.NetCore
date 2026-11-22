import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { spawnSync } from 'node:child_process';
import { fileURLToPath, pathToFileURL } from 'node:url';

const repositoryRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');

/** 矩阵是调度清单；只有运行收据 PASS 才表示本次验证成功。 */
export function loadMatrix() {
  return JSON.parse(fs.readFileSync(new URL('./matrix.json', import.meta.url), 'utf8'));
}

function automaticCases(matrix) {
  return matrix.cases.filter(cell => cell.status === 'enabled' && cell.ci !== false);
}

/** 只调度已接入且未显式关闭 CI 的格；手动验证仍可通过 --case 运行。 */
export function ciMatrix(matrix) {
  return { include: automaticCases(matrix).map(({ id, runner }) => ({ id, runner })) };
}

function getWorkflowIdentity() {
  const { GITHUB_REPOSITORY: repository, GITHUB_RUN_ID: runId, GITHUB_SHA: sha,
    GITHUB_RUN_ATTEMPT: attempt } = process.env;
  if (repository && runId && sha && attempt)
    return { repository, runId, sha, attempt };
}

function matchesWorkflow(source, workflow) {
  return source?.repository === workflow.repository && source?.runId === workflow.runId &&
    source?.sha === workflow.sha && /^[1-9]\d*$/u.test(source.attempt) &&
    Number(source.attempt) <= Number(workflow.attempt);
}

function expectedMarkers(cell) {
  return [cell.successMarker, ...(cell.requiredMarkers ?? [])];
}

function assertSuccessMarkers(output, markers) {
  const lines = new Set(output.split(/\r?\n/u).map(line => line.trim()));
  for (const marker of markers)
    if (!lines.has(marker)) throw new Error(`Native success marker missing: ${marker}`);
}

function hostRid(platform, arch) {
  const system = { win32: 'win', linux: 'linux', darwin: 'osx' }[platform];
  return system && arch ? `${system}-${arch}` : 'unknown';
}

// 从发布文件头识别当前矩阵的目标，避免模拟执行掩盖架构不匹配。
function readNativeRid(binary) {
  if (binary.length >= 64 && binary.readUInt16LE(0) === 0x5a4d) {
    const offset = binary.readUInt32LE(0x3c);
    if (offset >= 64 && offset + 6 <= binary.length && binary.readUInt32LE(offset) === 0x4550) {
      const machine = binary.readUInt16LE(offset + 4);
      if (machine === 0x8664) return 'win-x64';
      if (machine === 0xaa64) return 'win-arm64';
    }
  } else if (binary.length >= 20 && binary.readUInt32BE(0) === 0x7f454c46) {
    // 当前 Linux 格均为小端 ELF64 的 x64 可执行文件。
    if (binary[4] === 2 && binary[5] === 1 && binary.readUInt16LE(18) === 62) return 'linux-x64';
  } else if (binary.length >= 8 && binary.readUInt32LE(0) === 0xfeedfacf) {
    if (binary.readUInt32LE(4) === 0x0100000c) return 'osx-arm64';
  }
  return 'unknown';
}

/** 汇总同一工作流的 Native 矩阵收据和 SQL 回归结果。 */
export function summarizeCiReceipts(matrix, artifactDirectory, workflow, nativeJobResult, regressionJobResult) {
  const cases = automaticCases(matrix).map(cell => summarizeCiCase(cell, artifactDirectory, workflow));
  const passed = cases.filter(cell => cell.status === 'PASS').length;
  const regression = summarizeSqlRegression(artifactDirectory, workflow);
  const status = nativeJobResult === 'success' && regressionJobResult === 'success' && regression.status === 'PASS' &&
    cases.length > 0 && passed === cases.length ? 'PASS' : 'FAIL';
  return { status, workflow, nativeJobResult, regressionJobResult, regression, passed, total: cases.length, cases };
}

// Windows 作业负责解析 TRX。汇总只接受该作业已验证的原始文件。
function summarizeSqlRegression(artifactDirectory, workflow) {
  const result = { status: 'FAIL', verified: 0, total: 22 };
  const artifact = path.join(artifactDirectory, 'sql-regression');
  try {
    const runs = fs.readdirSync(artifact, { withFileTypes: true })
      .filter(entry => entry.isDirectory() && entry.name.startsWith('run-'));
    if (runs.length !== 1) throw new Error(`Expected one SQL regression run directory, found ${runs.length}.`);
    const directory = path.join(artifact, runs[0].name);
    const index = JSON.parse(fs.readFileSync(path.join(directory, 'reports.json'), 'utf8'));
    if (!matchesWorkflow(index.workflow, workflow))
      throw new Error('SQL regression reports belong to another workflow run, source revision or attempt.');

    const files = new Set(fs.readdirSync(directory, { withFileTypes: true })
      .filter(entry => entry.isFile() && entry.name.endsWith('.trx')).map(entry => entry.name));
    const reports = Object.entries(index.reports);
    const mismatches = [];
    for (const [file, expectedHash] of reports) {
      if (files.has(file) && crypto.createHash('sha256').update(fs.readFileSync(path.join(directory, file))).digest('hex') === expectedHash)
        result.verified++;
      else mismatches.push(file);
    }
    if (mismatches.length) throw new Error(`Missing or changed TRX: ${mismatches.join(', ')}`);
    if (reports.length !== result.total || files.size !== result.total)
      throw new Error(`Expected ${result.total} TRX, found ${reports.length} in the index and ${files.size} on disk.`);
    return { ...result, status: 'PASS' };
  } catch (error) {
    return { ...result, error: error.message };
  }
}

function summarizeCiCase(cell, artifactDirectory, workflow) {
  const result = { caseId: cell.id, rid: cell.rid };
  // upload-artifact 去掉多条 path 的共同根 output，download-artifact 按工件名解包。
  const artifact = path.join(artifactDirectory, `sql-aot-${cell.id}`);
  const directory = path.join(artifact, 'sql-aot', cell.id);
  const receipts = fs.existsSync(directory) ? fs.readdirSync(directory, { withFileTypes: true })
    .filter(entry => entry.isDirectory() && entry.name.startsWith('run-'))
    .map(entry => path.join(directory, entry.name, 'result.json')).filter(file => fs.existsSync(file)) : [];
  if (receipts.length === 0)
    return { ...result, status: 'MISSING', error: 'Native receipt not found.' };
  if (receipts.length > 1)
    return { ...result, status: 'INVALID', error: 'Multiple Native receipts found.' };

  const [file] = receipts;
  try {
    const receipt = JSON.parse(fs.readFileSync(file, 'utf8'));
    const source = receipt.workflow;
    if (!matchesWorkflow(source, workflow))
      throw new Error('Native receipt belongs to another workflow run, source revision or attempt.');
    if (receipt.caseId !== cell.id || receipt.provider !== cell.provider || receipt.rid !== cell.rid ||
        receipt.tfm !== cell.tfm || receipt.mode !== cell.mode || receipt.generation !== 'current-source')
      throw new Error('Native receipt does not match the current matrix case.');
    const markers = expectedMarkers(cell);
    if (JSON.stringify(receipt.successMarkers) !== JSON.stringify(markers))
      throw new Error('Native receipt does not declare the required success markers.');
    if (receipt.status === 'FAIL')
      return { ...result, status: 'FAIL', attempt: source.attempt, error: receipt.error };
    if (receipt.status !== 'PASS' || !/^[a-f0-9]{64}$/u.test(receipt.binary?.sha256) ||
        !Number.isSafeInteger(receipt.binary?.bytes) || receipt.binary.bytes <= 0)
      throw new Error('Native receipt does not have a recognized status and binary summary.');
    if (receipt.binary.rid !== cell.rid)
      throw new Error(`Native binary RID mismatch: expected ${cell.rid}; actual ${receipt.binary.rid ?? 'unknown'}`);
    const runner = hostRid(receipt.environment?.platform, receipt.environment?.arch);
    if (runner !== cell.rid)
      throw new Error(`Native runner RID mismatch: expected ${cell.rid}; actual ${runner}`);
    assertSuccessMarkers(fs.readFileSync(path.join(path.dirname(file), 'run.log'), 'utf8'), markers);
    if (cell.provider === 'oracle') {
      const fixture = path.join(artifact, 'sql-aot-oracle');
      assertSuccessMarkers(fs.readFileSync(path.join(fixture, 'ready.log'), 'utf8'), ['ORACLE_FIXTURE_READY_PASS']);
      assertSuccessMarkers(fs.readFileSync(path.join(fixture, 'stop.log'), 'utf8'), ['ORACLE_FIXTURE_CLEANUP_PASS']);
    }
    return { ...result, status: 'PASS', attempt: source.attempt, binary: receipt.binary };
  } catch (error) {
    return { ...result, status: 'INVALID', error: error.message };
  }
}

export function selectCase(matrix, id, platform = process.platform, arch = process.arch) {
  const cell = matrix.cases.find(candidate => candidate.id === id);
  if (!cell) throw new Error(`Unknown matrix case: ${id}`);
  if (cell.status !== 'enabled') throw new Error(`${id} is ${cell.status}: ${cell.reason}`);
  const runner = hostRid(platform, arch);
  if (cell.rid !== runner) throw new Error(`Native execution requires matching host: ${cell.rid}; actual ${runner}`);
  return cell;
}

export function buildCommands(cell, { root = repositoryRoot, publishDirectory, configuration = 'Release', noRestore = false }) {
  const executable = path.basename(cell.project, '.csproj') + (cell.rid.startsWith('win-') ? '.exe' : '');
  return {
    sdk: { file: 'dotnet', args: ['--version'], cwd: root, timeout: 30000 },
    publish: {
      file: 'dotnet',
      args: ['publish', path.resolve(root, cell.project), '-c', configuration, '-f', cell.tfm, '-r', cell.rid,
        '-p:BingSqlAotStrict=true', '-p:BingSqlGeneratorPackageVersion=',
        // 保留具体成员告警，避免程序集级 IL2104/IL3053 摘要掩盖待评估路径。
        '-p:TrimmerSingleWarn=false',
        '-p:Optimize=true', '-p:DefineConstants=RELEASE', '-p:UseSharedCompilation=false', '-m:1', '-nr:false',
        '-o', publishDirectory, '--verbosity', 'minimal', ...(noRestore ? ['--no-restore'] : [])],
      cwd: root, timeout: 1200000
    },
    run: { file: path.join(publishDirectory, executable), args: [], cwd: root, timeout: 120000 }
  };
}

// 使用独立参数数组启动进程，日志直接写文件，避免 shell 转义及大量输出驻留内存。
function execute(command, logFile) {
  const log = fs.openSync(logFile, 'w');
  try {
    return spawnSync(command.file, command.args, {
      cwd: command.cwd, stdio: ['ignore', log, log], windowsHide: true, timeout: command.timeout,
      env: { ...process.env, DOTNET_CLI_UI_LANGUAGE: 'en-US', DOTNET_CLI_TELEMETRY_OPTOUT: '1' }
    });
  } finally {
    fs.closeSync(log);
  }
}

/** 每次发布使用独立目录，失败也保留收据；不更改支持清单或性能阈值。 */
export function runCase(cell, options = {}, invoke = execute) {
  const root = options.root ?? repositoryRoot;
  const parent = path.resolve(root, options.output ?? 'output/sql-aot', cell.id);
  fs.mkdirSync(parent, { recursive: true });
  const directory = fs.mkdtempSync(path.join(parent, 'run-'));
  const commands = buildCommands(cell, { ...options, root, publishDirectory: path.join(directory, 'native') });
  const workflow = getWorkflowIdentity();
  const result = {
    ...(workflow ? { workflow } : {}),
    caseId: cell.id, provider: cell.provider, rid: cell.rid, tfm: cell.tfm, mode: cell.mode,
    expected: 'EXPECTED_SUCCESS', generation: 'current-source', coverage: cell.coverage,
    successMarkers: expectedMarkers(cell),
    directory, startedAt: new Date().toISOString(), status: 'FAIL',
    environment: { platform: process.platform, arch: process.arch, node: process.version },
    steps: []
  };
  function step(name) {
    const command = commands[name];
    const log = path.join(directory, name + '.log');
    const record = { name, command, log, startedAt: new Date().toISOString() };
    result.steps.push(record);
    const execution = invoke(command, log);
    Object.assign(record, { code: execution.status, signal: execution.signal ?? null, finishedAt: new Date().toISOString() });
    if (execution.error) throw execution.error;
    if (execution.status !== 0) throw new Error(`${name} failed: exit ${execution.status}, signal ${execution.signal ?? 'none'}; ${log}`);
    return fs.readFileSync(log, 'utf8');
  }
  try {
    result.environment.dotnetSdk = step('sdk').trim();
    step('publish');
    if (fs.existsSync(commands.run.file.replace(/\.exe$/u, '') + '.dll'))
      throw new Error('Publish produced a managed entry assembly; Native AOT is required.');
    const binary = fs.readFileSync(commands.run.file);
    const rid = readNativeRid(binary);
    if (rid !== cell.rid) throw new Error(`Native binary RID mismatch: expected ${cell.rid}; actual ${rid}`);
    result.binary = { file: commands.run.file, rid, bytes: binary.length, sha256: crypto.createHash('sha256').update(binary).digest('hex') };
    assertSuccessMarkers(step('run'), result.successMarkers);
    result.status = 'PASS';
  } catch (error) {
    result.error = error.message;
  }
  result.finishedAt = new Date().toISOString();
  fs.writeFileSync(path.join(directory, 'result.json'), JSON.stringify(result, null, 2) + '\n', 'utf8');
  return result;
}

function main(args) {
  const matrix = loadMatrix();
  if (args.length === 1 && args[0] === '--ci-matrix') {
    process.stdout.write(JSON.stringify(ciMatrix(matrix)) + '\n');
    return;
  }
  if (args.length === 1 && args[0] === '--list') {
    process.stdout.write(JSON.stringify(matrix, null, 2) + '\n');
    return;
  }
  if (args.length === 4 && args[0] === '--ci-summary') {
    const workflow = getWorkflowIdentity();
    if (!workflow) throw new Error('--ci-summary requires the GitHub Actions run identity.');
    const result = summarizeCiReceipts(matrix, args[1], workflow, args[2], args[3]);
    process.stdout.write(JSON.stringify(result, null, 2) + '\n');
    if (result.status !== 'PASS') process.exitCode = 1;
    return;
  }
  const options = {};
  let id;
  for (let index = 0; index < args.length; index++) {
    const option = args[index];
    if (option === '--no-restore') options.noRestore = true;
    else if (['--case', '--output', '--configuration'].includes(option)) {
      const value = args[++index];
      if (!value || value.startsWith('--')) throw new Error(`Missing value: ${option}`);
      if (option === '--case') id = value;
      else options[option.slice(2)] = value;
    } else throw new Error(`Unknown option: ${option}`);
  }
  if (!id) throw new Error('Usage: node eng/sql-aot/verify.mjs --case <id> [--output <directory>] [--configuration <name>] [--no-restore]\n' +
    '       node eng/sql-aot/verify.mjs --ci-summary <artifact-directory> <native-job-result> <sql-regression-job-result>');
  const result = runCase(selectCase(matrix, id), options);
  process.stdout.write(JSON.stringify(result, null, 2) + '\n');
  if (result.status !== 'PASS') process.exitCode = 1;
}

if (process.argv[1] && import.meta.url === pathToFileURL(path.resolve(process.argv[1])).href) {
  try { main(process.argv.slice(2)); }
  catch (error) { process.stderr.write(error.message + '\n'); process.exitCode = 1; }
}
