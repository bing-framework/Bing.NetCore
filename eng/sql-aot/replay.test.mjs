import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';

const repositoryRoot = path.resolve(import.meta.dirname, '../..');
const workflowIdentity = {
  repository: 'team/project',
  runId: '1234',
  sha: 'a'.repeat(40),
  attempt: '2'
};

function temporaryDirectory(t) {
  const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'bing-sql-aot-replay-'));
  t.after(() => fs.rmSync(directory, { recursive: true, force: true }));
  return directory;
}

function useWorkflowIdentity(t, attempt = workflowIdentity.attempt) {
  const values = {
    GITHUB_REPOSITORY: workflowIdentity.repository,
    GITHUB_RUN_ID: workflowIdentity.runId,
    GITHUB_SHA: workflowIdentity.sha,
    GITHUB_RUN_ATTEMPT: attempt
  };
  const previous = Object.fromEntries(Object.keys(values).map(key => [key, process.env[key]]));
  Object.assign(process.env, values);
  t.after(() => {
    for (const [key, value] of Object.entries(previous)) {
      if (value === undefined) delete process.env[key];
      else process.env[key] = value;
    }
  });
}

function nativeHeader(rid) {
  const header = Buffer.alloc(128);
  if (rid.startsWith('win-')) {
    header.write('MZ', 0, 'ascii');
    header.writeUInt32LE(0x60, 0x3c);
    header.write('PE\0\0', 0x60, 'ascii');
    header.writeUInt16LE(rid === 'win-arm64' ? 0xaa64 : 0x8664, 0x64);
  } else if (rid === 'linux-x64') {
    Buffer.from([0x7f, 0x45, 0x4c, 0x46, 2, 1]).copy(header);
    header.writeUInt16LE(62, 18);
  } else if (rid === 'osx-arm64') {
    header.writeUInt32LE(0xfeedfacf, 0);
    header.writeUInt32LE(0x0100000c, 4);
  } else {
    throw new Error(`Unsupported replay test RID: ${rid}`);
  }
  return header;
}

function sqliteSidecarName(rid) {
  if (rid.startsWith('win-')) return 'e_sqlite3.dll';
  if (rid.startsWith('osx-')) return 'libe_sqlite3.dylib';
  return 'libe_sqlite3.so';
}

function fakeNativeExecutor(cell, { includePackagingFiles = false } = {}) {
  return (command, logFile) => {
    let output = '10.0.100\n';
    if (command.args?.[0] === 'publish') {
      const publishDirectory = command.args[command.args.indexOf('-o') + 1];
      fs.mkdirSync(publishDirectory, { recursive: true });
      const executable = path.basename(command.args[1], '.csproj') + (cell.rid.startsWith('win-') ? '.exe' : '');
      fs.writeFileSync(path.join(publishDirectory, executable), nativeHeader(cell.rid));
      fs.writeFileSync(path.join(publishDirectory, sqliteSidecarName(cell.rid)), Buffer.from('sqlite-native-sidecar'));
      if (includePackagingFiles) {
        fs.writeFileSync(path.join(publishDirectory, `${executable}.pdb`), Buffer.from('portable symbols'));
        fs.writeFileSync(path.join(publishDirectory, 'dependency.dbg'), Buffer.from('debug symbols'));
        fs.mkdirSync(path.join(publishDirectory, `${executable}.dSYM`, 'Contents', 'Resources', 'DWARF'), { recursive: true });
        fs.writeFileSync(path.join(publishDirectory, `${executable}.dSYM`, 'Contents', 'Resources', 'DWARF', executable), Buffer.from('dSYM symbols'));
        fs.mkdirSync(path.join(publishDirectory, 'nested', 'runtime'), { recursive: true });
        fs.writeFileSync(path.join(publishDirectory, 'nested', 'runtime', 'dependency.dat'), Buffer.from('runtime dependency'));
        fs.writeFileSync(path.join(publishDirectory, 'nested', 'runtime', '.sqlite-runtime'), Buffer.from('hidden runtime dependency'));
      }
    } else if (command.file !== 'dotnet') {
      output = [cell.successMarker, ...(cell.requiredMarkers ?? [])].join('\n') + '\n';
    }
    fs.writeFileSync(logFile, output, 'utf8');
    return { status: 0, signal: null };
  };
}

async function createReplayFixture(t, { sourceAttempt = '1', includePackagingFiles = false } = {}) {
  useWorkflowIdentity(t, workflowIdentity.attempt);
  const { loadMatrix, runCase, stageReplay } = await import('./verify.mjs');
  const platform = { win32: 'win', linux: 'linux', darwin: 'osx' }[process.platform];
  const rid = platform && `${platform}-${process.arch}`;
  const cell = loadMatrix().cases.find(candidate => candidate.id === `sqlite-${rid}-generated-query`);
  if (!cell) {
    t.skip(`No SQLite generated-query case for ${rid ?? process.platform}`);
    return null;
  }

  if (typeof stageReplay !== 'function') {
    assert.fail('verify.mjs must export stageReplay for Native artifact packaging');
  }

  const root = temporaryDirectory(t);
  const output = path.join(root, 'output', 'sql-aot');
  if (sourceAttempt !== workflowIdentity.attempt) process.env.GITHUB_RUN_ATTEMPT = sourceAttempt;
  const result = runCase(cell, { root, output }, fakeNativeExecutor(cell, { includePackagingFiles }));
  process.env.GITHUB_RUN_ATTEMPT = workflowIdentity.attempt;
  assert.equal(result.status, 'PASS');
  assert.ok(Array.isArray(result.replayFiles), 'successful SQLite receipt must list files needed for replay');
  assert.ok(result.replayFiles.some(file => path.basename(file.path) === path.basename(result.binary.file)));
  assert.ok(result.replayFiles.some(file => path.basename(file.path) === sqliteSidecarName(cell.rid)));

  const staged = path.join(root, 'artifact-stage');
  stageReplay(cell, { root, output, destination: staged });
  const source = path.join(root, 'downloaded-payload');
  fs.cpSync(staged, source, { recursive: true });
  return { cell, root, output, source, result };
}

test('SQLite 工件下载后复算主程序与原生依赖并重放完整成功标记', async t => {
  const fixture = await createReplayFixture(t);
  if (!fixture) return;
  const { replayCase } = await import('./verify.mjs');
  assert.equal(typeof replayCase, 'function', 'verify.mjs must export replayCase');

  const commands = [];
  const result = replayCase(fixture.cell, {
    root: fixture.root,
    output: fixture.output,
    source: fixture.source
  }, (command, logFile) => {
    commands.push(command);
    assert.equal(path.dirname(command.file), fixture.source);
    fs.writeFileSync(logFile, [fixture.cell.successMarker, ...(fixture.cell.requiredMarkers ?? [])].join('\n') + '\n', 'utf8');
    return { status: 0, signal: null };
  });

  assert.equal(result.status, 'PASS');
  assert.equal(commands.length, 1);
  assert.ok(fs.existsSync(path.join(fixture.result.directory, 'replay.json')));
  assert.ok(fs.existsSync(path.join(fixture.result.directory, 'replay.log')));
});

for (const mutation of ['binary-changed', 'sidecar-changed', 'binary-missing', 'sidecar-missing']) {
  test(`SQLite 重放在 ${mutation} 时拒绝执行`, async t => {
    const fixture = await createReplayFixture(t);
    if (!fixture) return;
    const { replayCase } = await import('./verify.mjs');
    assert.equal(typeof replayCase, 'function', 'verify.mjs must export replayCase');
    const file = fixture.result.replayFiles.find(item => mutation.startsWith('binary')
      ? path.basename(item.path) === path.basename(fixture.result.binary.file)
      : path.basename(item.path) === sqliteSidecarName(fixture.cell.rid));
    const target = path.join(fixture.source, file.path);
    if (mutation.endsWith('changed')) fs.appendFileSync(target, 'changed');
    else fs.rmSync(target);

    let invoked = false;
    const result = replayCase(fixture.cell, {
      root: fixture.root,
      output: fixture.output,
      source: fixture.source
    }, () => {
      invoked = true;
      return { status: 0, signal: null };
    });

    assert.equal(result.status, 'FAIL');
    assert.equal(invoked, false);
  });
}

test('SQLite 重放拒绝未登记文件和越出下载目录的收据路径', async t => {
  const fixture = await createReplayFixture(t);
  if (!fixture) return;
  const { replayCase } = await import('./verify.mjs');
  assert.equal(typeof replayCase, 'function', 'verify.mjs must export replayCase');

  fs.writeFileSync(path.join(fixture.source, 'unlisted.bin'), Buffer.from('unlisted'));
  let invoked = false;
  const extraFileResult = replayCase(fixture.cell, {
    root: fixture.root,
    output: fixture.output,
    source: fixture.source
  }, () => {
    invoked = true;
    return { status: 0, signal: null };
  });
  assert.equal(extraFileResult.status, 'FAIL');
  assert.equal(invoked, false);

  fs.rmSync(path.join(fixture.source, 'unlisted.bin'));
  const receiptPath = path.join(fixture.result.directory, 'result.json');
  const receipt = JSON.parse(fs.readFileSync(receiptPath, 'utf8'));
  receipt.replayFiles[0].path = '../../outside.bin';
  fs.writeFileSync(receiptPath, JSON.stringify(receipt, null, 2) + '\n', 'utf8');
  const traversalResult = replayCase(fixture.cell, {
    root: fixture.root,
    output: fixture.output,
    source: fixture.source
  }, () => {
    invoked = true;
    return { status: 0, signal: null };
  });
  assert.equal(traversalResult.status, 'FAIL');
  assert.equal(invoked, false);
});

test('SQLite 发布包排除调试符号、保留嵌套依赖与隐藏文件并校验摘要', async t => {
  const fixture = await createReplayFixture(t, { includePackagingFiles: true });
  if (!fixture) return;
  const { replayCase } = await import('./verify.mjs');
  assert.equal(typeof replayCase, 'function', 'verify.mjs must export replayCase');

  const listedPaths = fixture.result.replayFiles.map(file => file.path);
  assert.equal(listedPaths.some(name => /\.(?:pdb|dbg)$/iu.test(name) || /\.dSYM(?:\/|$)/u.test(name)), false);
  const nestedDependency = 'nested/runtime/dependency.dat';
  const hiddenDependency = 'nested/runtime/.sqlite-runtime';
  assert.ok(listedPaths.includes(nestedDependency), 'ordinary nested runtime dependency must be packaged');
  assert.ok(listedPaths.includes(hiddenDependency), 'nested hidden runtime dependency must be in the manifest');
  assert.equal(fs.existsSync(path.join(fixture.source, 'nested', 'runtime', 'dependency.dat')), true);
  assert.equal(fs.existsSync(path.join(fixture.source, ...hiddenDependency.split('/'))), true,
    'staging must preserve the nested hidden runtime dependency');
  assert.equal(fs.existsSync(path.join(fixture.source, `${path.basename(fixture.result.binary.file)}.pdb`)), false);
  assert.equal(fs.existsSync(path.join(fixture.source, 'dependency.dbg')), false);
  assert.equal(fs.existsSync(path.join(fixture.source, `${path.basename(fixture.result.binary.file)}.dSYM`)), false);

  const validReplay = replayCase(fixture.cell, {
    root: fixture.root,
    output: fixture.output,
    source: fixture.source
  }, (command, logFile) => {
    fs.writeFileSync(logFile, [fixture.cell.successMarker, ...(fixture.cell.requiredMarkers ?? [])].join('\n') + '\n', 'utf8');
    return { status: 0, signal: null };
  });
  assert.equal(validReplay.status, 'PASS');

  fs.appendFileSync(path.join(fixture.source, ...hiddenDependency.split('/')), 'changed');
  let invoked = false;
  const result = replayCase(fixture.cell, {
    root: fixture.root,
    output: fixture.output,
    source: fixture.source
  }, () => {
    invoked = true;
    return { status: 0, signal: null };
  });
  assert.equal(result.status, 'FAIL');
  assert.equal(invoked, false);
});

test('Replay CLI 支持独立 output 并对篡改的下载内容返回失败', async t => {
  useWorkflowIdentity(t);
  const { loadMatrix, runCase } = await import('./verify.mjs');
  const platform = { win32: 'win', linux: 'linux', darwin: 'osx' }[process.platform];
  const rid = platform && `${platform}-${process.arch}`;
  const cell = loadMatrix().cases.find(candidate => candidate.id === `sqlite-${rid}-generated-query`);
  if (!cell) {
    t.skip(`No SQLite generated-query case for ${rid ?? process.platform}`);
    return;
  }

  const root = temporaryDirectory(t);
  const output = path.join(root, 'cli-output');
  const result = runCase(cell, { root: repositoryRoot, output }, fakeNativeExecutor(cell));
  assert.equal(result.status, 'PASS');
  const destination = path.join(root, 'staged-payload');
  const cli = (...args) => spawnSync(process.execPath, [
    'eng/sql-aot/verify.mjs', ...args, '--output', output
  ], {
    cwd: repositoryRoot,
    encoding: 'utf8',
    env: { ...process.env }
  });

  const staged = cli('--stage-replay', cell.id, destination);
  assert.equal(staged.status, 0, staged.stderr || staged.stdout);
  const binary = path.join(destination, path.basename(result.binary.file));
  assert.equal(fs.existsSync(binary), true);
  fs.appendFileSync(binary, 'changed');

  const replayed = cli('--replay', cell.id, destination);
  assert.notEqual(replayed.status, 0, replayed.stderr || replayed.stdout);
  const replayReceipt = JSON.parse(fs.readFileSync(path.join(result.directory, 'replay.json'), 'utf8'));
  assert.equal(replayReceipt.status, 'FAIL');
});

test('SQLite 重放拒绝不匹配的 RID 和工作流源码身份', async t => {
  const fixture = await createReplayFixture(t);
  if (!fixture) return;
  const { replayCase } = await import('./verify.mjs');
  assert.equal(typeof replayCase, 'function', 'verify.mjs must export replayCase');
  let invoked = false;
  const replay = (cell = fixture.cell) => replayCase(cell, {
    root: fixture.root,
    output: fixture.output,
    source: fixture.source
  }, () => {
    invoked = true;
    return { status: 0, signal: null };
  });

  const otherRid = fixture.cell.rid === 'win-x64' ? 'linux-x64' : 'win-x64';
  assert.equal(replay({ ...fixture.cell, rid: otherRid }).status, 'FAIL');
  assert.equal(invoked, false);

  process.env.GITHUB_SHA = 'c'.repeat(40);
  assert.equal(replay().status, 'FAIL');
  assert.equal(invoked, false);
});

test('同一工作流较早 attempt 的成功工件仍能通过重放', async t => {
  const fixture = await createReplayFixture(t, { sourceAttempt: '1' });
  if (!fixture) return;
  const { replayCase } = await import('./verify.mjs');
  assert.equal(typeof replayCase, 'function', 'verify.mjs must export replayCase');
  const result = replayCase(fixture.cell, {
    root: fixture.root,
    output: fixture.output,
    source: fixture.source
  }, (command, logFile) => {
    fs.writeFileSync(logFile, [fixture.cell.successMarker, ...(fixture.cell.requiredMarkers ?? [])].join('\n') + '\n', 'utf8');
    return { status: 0, signal: null };
  });
  assert.equal(result.status, 'PASS');
  assert.equal(result.workflow.attempt, '1');
});

for (const scenario of ['nonzero-exit', 'missing-marker']) {
  test(`SQLite 重放在 ${scenario} 时记录失败`, async t => {
    const fixture = await createReplayFixture(t);
    if (!fixture) return;
    const { replayCase } = await import('./verify.mjs');
    assert.equal(typeof replayCase, 'function', 'verify.mjs must export replayCase');
    const result = replayCase(fixture.cell, {
      root: fixture.root,
      output: fixture.output,
      source: fixture.source
    }, (command, logFile) => {
      fs.writeFileSync(logFile, scenario === 'missing-marker' ? 'partial\n' : 'failed\n', 'utf8');
      return { status: scenario === 'nonzero-exit' ? 1 : 0, signal: null };
    });
    assert.equal(result.status, 'FAIL');
  });
}

test('Native 矩阵对 SQLite 执行工件合同测试及上传、下载、重放链', () => {
  const workflowText = fs.readFileSync(path.join(repositoryRoot, '.github/workflows/sql-aot.yml'), 'utf8');
  const matrixJob = workflowText.split(/^  matrix:\s*$/mu)[1]?.split(/^  native:\s*$/mu)[0] ?? '';
  assert.match(matrixJob, /eng\/sql-aot\/replay\.test\.mjs/u,
    'Native matrix contract step must run replay.test.mjs');

  const nativeJob = workflowText.split(/^  native:\s*$/mu)[1]?.split(/^  native-summary:/mu)[0] ?? '';
  const steps = nativeJob.split(/(?=^      - )/mu);
  const publish = steps.findIndex(step => step.includes('name: Publish and execute Native case'));
  const stage = steps.findIndex(step => step.includes('name: Stage SQLite replay payload'));
  const upload = steps.findIndex(step => step.includes('name: Upload SQLite replay payload'));
  const download = steps.findIndex(step => step.includes('name: Download SQLite replay payload'));
  const replay = steps.findIndex(step => step.includes('name: Replay downloaded SQLite payload'));
  assert.ok(publish >= 0 && publish < stage && stage < upload && upload < download && download < replay,
    'SQLite payload must be staged, uploaded, downloaded and replayed after Native execution');
  for (const index of [stage, upload, download, replay]) {
    assert.match(steps[index], /if:.*startsWith\s*\(\s*matrix\.id,\s*'sqlite-'\s*\)/u,
      'SQLite replay steps must be restricted to SQLite matrix cases');
  }
  assert.match(steps[stage], /--stage-replay\s+\$\{\{\s*matrix\.id\s*\}\}/u);
  assert.match(steps[upload], /sql-native-payload-\$\{\{\s*matrix\.id\s*\}\}/u);
  assert.match(steps[upload], /include-hidden-files:\s*true/u,
    'SQLite payload upload must explicitly retain nested hidden dependencies');
  assert.match(steps[download], /actions\/download-artifact@v4/u);
  assert.match(steps[replay], /--replay\s+\$\{\{\s*matrix\.id\s*\}\}/u);

  const summaryJob = workflowText.split(/^  native-summary:/mu)[1] ?? '';
  assert.match(summaryJob, /sql-native-payload-\*/u,
    'CI summary must download SQLite payloads to recalculate their binary digests');
});
