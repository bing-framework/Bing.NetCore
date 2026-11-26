import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import crypto from 'node:crypto';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';

const root = path.resolve(import.meta.dirname, '../..');
const workflow = { repository: 'team/project', runId: '1234', sha: 'a'.repeat(40), attempt: '2' };
const expected = {
  id: 'sqlite-linux-x64-query', provider: 'sqlite', rid: 'linux-x64', tfm: 'net10.0',
  mode: 'query', status: 'enabled', ci: true, runner: 'ubuntu-22.04',
  project: 'framework/tests/Bing.Data.Sql.Aot.Smoke/Bing.Data.Sql.Aot.Smoke.csproj',
  successMarker: 'SQLITE_QUERY_NATIVE_PASS', requiredMarkers: ['SQLITE_QUERY_BINDING_PASS']
};
const sqliteCrudLegacyRequiredMarkers = [
  'MUTATION_PLAN_READER_NATIVE_PASS',
  'GENERATED_RETURNING_PROJECTION_NATIVE_PASS',
  'GENERATED_ENTITY_RETURNING_SQL_NATIVE_PASS',
  'GENERATED_MUTATION_SHAPES_NATIVE_PASS',
  'GENERATED_MULTIROW_INSERT_NATIVE_PASS',
  'GENERATED_IN_DELETE_NATIVE_PASS',
  'GENERATED_PAIRED_DELETE_NATIVE_PASS',
  'GENERATED_KEY_INSERT_SQL_NATIVE_PASS',
  'GENERATED_CRUD_SQL_NATIVE_PASS'
];
const sqliteCrudReturningMarkers = [
  'GENERATED_NEGATED_RETURNING_NATIVE_PASS',
  'GENERATED_COMPUTED_RETURNING_MAPPING_NATIVE_PASS',
  'GENERATED_QUOTED_RETURNING_NATIVE_PASS',
  'GENERATED_CACHED_RETURNING_NATIVE_PASS'
];
const sqliteCrudRequiredMarkers = [
  ...sqliteCrudLegacyRequiredMarkers,
  ...sqliteCrudReturningMarkers
];
const sqliteCrudRids = ['win-x64', 'linux-x64', 'osx-arm64', 'win-arm64'];

const oracleFixtureMarkers = {
  'ready.log': 'ORACLE_FIXTURE_READY_PASS',
  'stop.log': 'ORACLE_FIXTURE_CLEANUP_PASS'
};

const nativeEnvironments = {
  'linux-x64': { platform: 'linux', arch: 'x64' },
  'win-x64': { platform: 'win32', arch: 'x64' },
  'win-arm64': { platform: 'win32', arch: 'arm64' },
  'osx-arm64': { platform: 'darwin', arch: 'arm64' }
};

function nativeExecutableHeader(rid) {
  if (rid === 'win-x64' || rid === 'win-arm64') {
    const binary = Buffer.alloc(0x86);
    binary.writeUInt16LE(0x5a4d, 0);
    binary.writeUInt32LE(0x80, 0x3c);
    binary.writeUInt32LE(0x4550, 0x80);
    binary.writeUInt16LE(rid === 'win-arm64' ? 0xaa64 : 0x8664, 0x84);
    return binary;
  }
  if (rid === 'linux-x64') {
    const binary = Buffer.alloc(64);
    binary.writeUInt32BE(0x7f454c46, 0);
    binary[4] = 2;
    binary[5] = 1;
    binary.writeUInt16LE(62, 18);
    return binary;
  }
  if (rid === 'osx-arm64') {
    const binary = Buffer.alloc(8);
    binary.writeUInt32LE(0xfeedfacf, 0);
    binary.writeUInt32LE(0x0100000c, 4);
    return binary;
  }
  throw new Error(`Unsupported fixture RID: ${rid}`);
}

function tempDirectory(t) {
  const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'bing-aot-ci-'));
  t.after(() => fs.rmSync(directory, { recursive: true, force: true }));
  return directory;
}

function fixture(t, matrixCases = [expected]) {
  const directory = tempDirectory(t);
  const sqlDirectory = path.join(directory, 'sql-regression', 'run-example');
  fs.mkdirSync(sqlDirectory, { recursive: true });
  const reports = {};
  for (let i = 0; i < 22; i++) {
    const name = `Example${i}.Tests.net8.0.trx`;
    const xml = `<TestRun name="${i}"><ResultSummary outcome="Completed"><Counters executed="1" failed="0"/></ResultSummary></TestRun>`;
    fs.writeFileSync(path.join(sqlDirectory, name), xml, 'utf8');
    reports[name] = crypto.createHash('sha256').update(xml).digest('hex');
  }
  const indexFile = path.join(sqlDirectory, 'reports.json');
  fs.writeFileSync(indexFile, JSON.stringify({ workflow, reports }), 'utf8');
  return {
    directory, sqlDirectory, indexFile, reports,
    matrix: { cases: matrixCases },
    add(cell = expected, overrides = {}, attempt = workflow.attempt, logText) {
      const runDirectory = path.join(directory, `sql-aot-${cell.id}`, 'sql-aot', cell.id,
        `run-${attempt}-${crypto.randomUUID()}`);
      fs.mkdirSync(runDirectory, { recursive: true });
      const markers = [cell.successMarker, ...(cell.requiredMarkers ?? [])];
      fs.writeFileSync(path.join(runDirectory, 'run.log'),
        (logText ?? markers.join('\n')) + '\n', 'utf8');
      let replayFiles;
      let binary = { sha256: 'b'.repeat(64), bytes: 4096, rid: cell.rid };
      if (cell.provider === 'sqlite') {
        const payloadDirectory = path.join(directory, `sql-native-payload-${cell.id}`);
        fs.mkdirSync(payloadDirectory, { recursive: true });
        const projectName = path.basename(cell.project, '.csproj');
        const executable = projectName + (cell.rid.startsWith('win-') ? '.exe' : '');
        const sidecar = `e_sqlite3${cell.rid.startsWith('win-') ? '.dll' : cell.rid.startsWith('osx-') ? '.dylib' : '.so'}`;
        const files = [
          { path: executable, contents: nativeExecutableHeader(cell.rid) },
          { path: sidecar, contents: Buffer.from(`sqlite native dependency:${cell.id}:${cell.rid}`) }
        ];
        replayFiles = files.map(file => {
          fs.writeFileSync(path.join(payloadDirectory, file.path), file.contents);
          return {
            path: file.path,
            bytes: file.contents.length,
            sha256: crypto.createHash('sha256').update(file.contents).digest('hex')
          };
        });
        binary = {
          file: path.join('runner-output', 'native', executable),
          sha256: replayFiles[0].sha256,
          bytes: replayFiles[0].bytes,
          rid: cell.rid
        };
        fs.writeFileSync(path.join(runDirectory, 'replay.json'), `${JSON.stringify({
          status: 'PASS', caseId: cell.id, workflow: { ...workflow, attempt },
          binarySha256: binary.sha256
        }, null, 2)}\n`, 'utf8');
        fs.writeFileSync(path.join(runDirectory, 'replay.log'), `${markers.join('\n')}\n`, 'utf8');
      }
      const receipt = {
        caseId: cell.id, provider: cell.provider, rid: cell.rid, tfm: cell.tfm, mode: cell.mode,
        generation: 'current-source', workflow: { ...workflow, attempt },
        successMarkers: markers, status: 'PASS',
        environment: nativeEnvironments[cell.rid],
        binary,
        ...(replayFiles ? { replayFiles } : {}),
        ...overrides
      };
      fs.writeFileSync(path.join(runDirectory, 'result.json'), `${JSON.stringify(receipt, null, 2)}\n`, 'utf8');
      return runDirectory;
    },
    addOracleLogs(cell, logs = oracleFixtureMarkers) {
      const logDirectory = path.join(directory, 'sql-aot-' + cell.id, 'sql-aot-oracle');
      fs.mkdirSync(logDirectory, { recursive: true });
      for (const [file, marker] of Object.entries(logs))
        fs.writeFileSync(path.join(logDirectory, file), marker + '\n', 'utf8');
    }
  };
}

async function summarize(fixtureData, nativeJobResult = 'success', regressionJobResult = 'success') {
  const { summarizeCiReceipts } = await import('./verify.mjs');
  assert.equal(typeof summarizeCiReceipts, 'function',
    'verify.mjs must export summarizeCiReceipts before CI receipt contracts can run');
  return summarizeCiReceipts(fixtureData.matrix, fixtureData.directory, workflow, nativeJobResult, regressionJobResult);
}

test('SQLite CI 汇总接受与下载文件一致的主程序、sidecar 和重放收据', async t => {
  const data = fixture(t);
  const runDirectory = data.add();
  const receipt = JSON.parse(fs.readFileSync(path.join(runDirectory, 'result.json'), 'utf8'));
  const payloadDirectory = path.join(data.directory, `sql-native-payload-${expected.id}`);
  const mainProgram = receipt.replayFiles.find(file => path.basename(file.path) === path.basename(receipt.binary.file));
  assert.ok(mainProgram);
  assert.equal(mainProgram.sha256, receipt.binary.sha256);
  assert.equal(mainProgram.bytes, receipt.binary.bytes);
  for (const file of receipt.replayFiles) {
    const contents = fs.readFileSync(path.join(payloadDirectory, file.path));
    assert.equal(contents.length, file.bytes);
    assert.equal(crypto.createHash('sha256').update(contents).digest('hex'), file.sha256);
  }
  const replay = JSON.parse(fs.readFileSync(path.join(runDirectory, 'replay.json'), 'utf8'));
  assert.equal(replay.status, 'PASS');
  assert.equal(replay.caseId, receipt.caseId);
  assert.deepEqual(replay.workflow, receipt.workflow);
  assert.equal(replay.binarySha256, receipt.binary.sha256);

  const result = await summarize(data);
  assert.equal(result.status, 'PASS');
  assert.equal(result.cases[0].status, 'PASS');
});

for (const scenario of [
  {
    name: '主程序缺失',
    mutate: ({ payloadDirectory, mainProgram }) => fs.unlinkSync(path.join(payloadDirectory, mainProgram.path))
  },
  {
    name: '主程序内容被篡改',
    mutate: ({ payloadDirectory, mainProgram }) => fs.writeFileSync(path.join(payloadDirectory, mainProgram.path), 'tampered main')
  },
  {
    name: 'sidecar 内容被篡改',
    mutate: ({ payloadDirectory, sidecar }) => fs.writeFileSync(path.join(payloadDirectory, sidecar.path), 'tampered sidecar')
  },
  {
    name: '重放收据缺失',
    mutate: ({ runDirectory }) => fs.unlinkSync(path.join(runDirectory, 'replay.json'))
  },
  {
    name: '重放状态失败',
    mutate: ({ runDirectory }) => {
      const replayPath = path.join(runDirectory, 'replay.json');
      const replay = JSON.parse(fs.readFileSync(replayPath, 'utf8'));
      replay.status = 'FAIL';
      fs.writeFileSync(replayPath, JSON.stringify(replay), 'utf8');
    }
  },
  {
    name: '重放来源 attempt 与构建收据不一致',
    mutate: ({ runDirectory }) => {
      const replayPath = path.join(runDirectory, 'replay.json');
      const replay = JSON.parse(fs.readFileSync(replayPath, 'utf8'));
      replay.workflow.attempt = '1';
      fs.writeFileSync(replayPath, JSON.stringify(replay), 'utf8');
    }
  },
  {
    name: '重放二进制摘要与构建收据不一致',
    mutate: ({ runDirectory }) => {
      const replayPath = path.join(runDirectory, 'replay.json');
      const replay = JSON.parse(fs.readFileSync(replayPath, 'utf8'));
      replay.binarySha256 = 'c'.repeat(64);
      fs.writeFileSync(replayPath, JSON.stringify(replay), 'utf8');
    }
  },
  {
    name: '重放日志缺少必需标记',
    mutate: ({ runDirectory }) => fs.writeFileSync(path.join(runDirectory, 'replay.log'), `${expected.successMarker}\n`, 'utf8')
  }
]) {
  test(`SQLite CI 汇总拒绝${scenario.name}`, async t => {
    const data = fixture(t);
    const runDirectory = data.add();
    const receipt = JSON.parse(fs.readFileSync(path.join(runDirectory, 'result.json'), 'utf8'));
    const payloadDirectory = path.join(data.directory, `sql-native-payload-${expected.id}`);
    const mainProgram = receipt.replayFiles.find(file => path.basename(file.path) === path.basename(receipt.binary.file));
    const sidecar = receipt.replayFiles.find(file => file !== mainProgram);
    scenario.mutate({ runDirectory, payloadDirectory, mainProgram, sidecar });

    const result = await summarize(data);
    assert.equal(result.status, 'FAIL');
    assert.notEqual(result.cases[0].status, 'PASS');
  });
}

async function currentSqliteCrudCell() {
  const platform = { win32: 'win', linux: 'linux', darwin: 'osx' }[process.platform];
  if (!platform) return null;
  const rid = `${platform}-${process.arch}`;
  const { loadMatrix } = await import('./verify.mjs');
  return loadMatrix().cases.find(cell => cell.id === `sqlite-${rid}-crud`);
}

test('CI 摘要只统计自动调度的 enabled 格', async t => {
  const manual = { ...expected, id: 'manual', ci: false };
  const planned = { ...expected, id: 'planned', status: 'planned' };
  const data = fixture(t, [expected, manual, planned]);
  data.add(expected);
  const result = await summarize(data);

  assert.equal(result.status, 'PASS');
  assert.deepEqual(result.cases.map(item => item.caseId), [expected.id]);
  assert.equal(result.cases[0].status, 'PASS');
});

test('重跑失败 Job 时可复用同一 workflow 的较早 attempt 成功收据', async t => {
  const data = fixture(t);
  data.add(expected, {}, '1');
  const result = await summarize(data);

  assert.equal(result.status, 'PASS');
  assert.equal(result.cases[0].status, 'PASS');
});

test('缺少自动格收据时汇总失败并标出缺失格', async t => {
  const data = fixture(t);
  const result = await summarize(data);

  assert.equal(result.status, 'FAIL');
  assert.deepEqual(result.cases.map(item => [item.caseId, item.status]), [[expected.id, 'MISSING']]);
});

for (const [field, value] of [
  ['repository', 'other/project'], ['runId', 'other-run'], ['sha', 'c'.repeat(40)], ['attempt', '3']
]) {
  test(`拒绝 workflow ${field} 不匹配的收据`, async t => {
    const data = fixture(t);
    data.add(expected, { workflow: { ...workflow, [field]: value } });
    const result = await summarize(data);

    assert.equal(result.status, 'FAIL');
    assert.notEqual(result.cases[0].status, 'PASS');
  });
}

test('同一自动格存在重复收据时不能判为通过', async t => {
  const data = fixture(t);
  data.add();
  data.add();
  const result = await summarize(data);

  assert.equal(result.status, 'FAIL');
  assert.notEqual(result.cases[0].status, 'PASS');
});

test('发布或运行失败的格保留原始错误', async t => {
  const data = fixture(t);
  data.add(expected, { status: 'FAIL', error: 'publish failed: exit 1', binary: undefined });
  const result = await summarize(data);

  assert.equal(result.status, 'FAIL');
  assert.equal(result.cases[0].status, 'FAIL');
  assert.equal(result.cases[0].error, 'publish failed: exit 1');
});

test('无效的 PASS 收据不能满足格验收', async t => {
  const data = fixture(t);
  data.add(expected, { binary: undefined });
  const result = await summarize(data);

  assert.equal(result.status, 'FAIL');
  assert.notEqual(result.cases[0].status, 'PASS');
});

for (const [scenario, overrides, error] of [
  ['二进制 RID 缺失', { binary: { sha256: 'b'.repeat(64), bytes: 4096 } }, /binary RID mismatch/u],
  ['二进制为 Windows x64', { binary: { sha256: 'b'.repeat(64), bytes: 4096, rid: 'win-x64' } }, /binary RID mismatch/u],
  ['运行 Node 为 Windows x64', { environment: { platform: 'win32', arch: 'x64' } }, /runner RID mismatch/u],
  ['运行 Node 平台为 macOS', { environment: { platform: 'darwin', arch: 'arm64' } }, /runner RID mismatch/u]
]) {
  test(`ARM64 汇总拒绝 PASS 收据中${scenario}`, async t => {
    const cell = { ...expected, id: 'sqlite-win-arm64-query', rid: 'win-arm64' };
    const data = fixture(t, [cell]);
    data.add(cell, overrides);
    const result = await summarize(data);

    assert.equal(result.status, 'FAIL');
    assert.equal(result.cases[0].status, 'INVALID');
    assert.match(result.cases[0].error, error);
  });
}

test('运行器失败收据不能满足自动格验收', async t => {
  const data = fixture(t);
  data.add(expected, { status: 'FAIL', error: 'publish failed' });
  const result = await summarize(data);

  assert.equal(result.status, 'FAIL');
  assert.notEqual(result.cases[0].status, 'PASS');
});

test('运行日志缺少所需标记时不能按 PASS 汇总', async t => {
  const data = fixture(t);
  data.add(expected, {}, workflow.attempt, 'some unrelated output');
  const result = await summarize(data);

  assert.equal(result.status, 'FAIL');
  assert.notEqual(result.cases[0].status, 'PASS');
});

for (const missing of sqliteCrudRequiredMarkers) {
  test(`SQLite CRUD CI 拒绝缺少 ${missing} 的 PASS 收据`, async t => {
    const cell = await currentSqliteCrudCell();
    if (!cell) return t.skip('当前测试主机没有 SQLite CRUD 矩阵格');
    const data = fixture(t, [cell]);
    const markers = [cell.successMarker, ...(cell.requiredMarkers ?? [])]
      .filter(marker => marker !== missing);
    data.add(cell, {}, workflow.attempt, markers.join('\n'));
    const result = await summarize(data);

    assert.equal(result.status, 'FAIL');
    assert.equal(result.cases[0].status, 'INVALID', `缺少 ${missing} 的收据不能有效`);
    assert.equal(result.cases[0].error, `Native success marker missing: ${missing}`);
  });
}

test('SQLite CRUD CI 接受完整十四项能力收据并单独保留 SQL 回归作业结果', async t => {
  const cell = await currentSqliteCrudCell();
  if (!cell) return t.skip('当前测试主机没有 SQLite CRUD 矩阵格');
  assert.equal(cell.successMarker, 'CRUD_ONLY_PACKAGE_NATIVE_PASS');
  assert.deepEqual([...(cell.requiredMarkers ?? [])].sort(), [...sqliteCrudRequiredMarkers].sort());
  assert.equal([cell.successMarker, ...(cell.requiredMarkers ?? [])].length, 14);
  const data = fixture(t, [cell]);
  data.add(cell);
  const result = await summarize(data, 'success', 'skipped');

  assert.equal(result.status, 'FAIL');
  assert.equal(result.cases.length, 1);
  assert.equal(result.cases[0].status, 'PASS');
  assert.equal(result.passed, 1);
  assert.equal(result.regressionJobResult, 'skipped');
});

for (const rid of sqliteCrudRids) {
  test(`SQLite CRUD CI ${rid} 校验完整 Returning 标记和运行日志`, async t => {
    const { loadMatrix } = await import('./verify.mjs');
    const cell = loadMatrix().cases.find(candidate => candidate.id === `sqlite-${rid}-crud`);
    assert.ok(cell, `必须存在 sqlite-${rid}-crud 矩阵格`);
    assert.equal(cell.successMarker, 'CRUD_ONLY_PACKAGE_NATIVE_PASS');
    const markers = [cell.successMarker, ...(cell.requiredMarkers ?? [])];
    const expectedMarkers = ['CRUD_ONLY_PACKAGE_NATIVE_PASS', ...sqliteCrudRequiredMarkers];
    assert.equal(markers.length, 14);
    assert.deepEqual([...markers].sort(), [...expectedMarkers].sort());

    await t.test('完整收据、日志和 SQL 回归通过', async t => {
      const data = fixture(t, [cell]);
      data.add(cell, {}, workflow.attempt, markers.join('\n'));
      const result = await summarize(data);

      assert.equal(result.status, 'PASS');
      assert.equal(result.cases[0].status, 'PASS');
      assert.equal(result.regression.status, 'PASS');
    });

    for (const marker of sqliteCrudReturningMarkers) {
      await t.test(`日志缺少 ${marker} 时拒绝`, async t => {
        const data = fixture(t, [cell]);
        const output = markers.filter(candidate => candidate !== marker).join('\n');
        data.add(cell, {}, workflow.attempt, output);
        const result = await summarize(data);

        assert.equal(result.status, 'FAIL');
        assert.equal(result.cases[0].status, 'INVALID');
        assert.equal(result.cases[0].error, `Native success marker missing: ${marker}`);
      });

      await t.test(`日志仅含 ${marker} 的前缀时拒绝`, async t => {
        const data = fixture(t, [cell]);
        const output = [...markers.filter(candidate => candidate !== marker), `prefix-${marker}`].join('\n');
        data.add(cell, {}, workflow.attempt, output);
        const result = await summarize(data);

        assert.equal(result.status, 'FAIL');
        assert.equal(result.cases[0].status, 'INVALID');
        assert.equal(result.cases[0].error, `Native success marker missing: ${marker}`);
      });
    }
  });

  test(`SQLite CRUD CI ${rid} 拒绝旧十项标记声明，即使日志包含全部十四项`, async t => {
    const { loadMatrix } = await import('./verify.mjs');
    const cell = loadMatrix().cases.find(candidate => candidate.id === `sqlite-${rid}-crud`);
    assert.ok(cell, `必须存在 sqlite-${rid}-crud 矩阵格`);
    const markers = [cell.successMarker, ...(cell.requiredMarkers ?? [])];
    const oldMarkers = [cell.successMarker, ...sqliteCrudLegacyRequiredMarkers];
    assert.equal(markers.length, 14);
    assert.equal(oldMarkers.length, 10);

    const data = fixture(t, [cell]);
    data.add(cell, { successMarkers: oldMarkers }, workflow.attempt, markers.join('\n'));
    const result = await summarize(data);

    assert.equal(result.status, 'FAIL');
    assert.equal(result.cases[0].status, 'INVALID');
    assert.equal(result.cases[0].error, 'Native receipt does not declare the required success markers.');
  });
}

test('SQLite CRUD CI 拒绝只声明旧两个标记的 PASS 收据', async t => {
  const cell = await currentSqliteCrudCell();
  if (!cell) return t.skip('当前测试主机没有 SQLite CRUD 矩阵格');
  const data = fixture(t, [cell]);
  const oldMarkers = ['CRUD_ONLY_PACKAGE_NATIVE_PASS', 'MUTATION_PLAN_READER_NATIVE_PASS'];
  data.add(cell, { successMarkers: oldMarkers }, workflow.attempt, oldMarkers.join('\n'));
  const result = await summarize(data);

  assert.equal(result.status, 'FAIL');
  assert.equal(result.cases[0].status, 'INVALID');
});

for (const rid of ['linux-x64', 'win-x64']) {
  test('Oracle ' + rid + ' 的 CI 汇总要求同一工件内有就绪和清理日志', async t => {
    const { loadMatrix } = await import('./verify.mjs');
    const cell = loadMatrix().cases.find(cell => cell.id === 'oracle-' + rid);
    const data = fixture(t, [cell]);
    data.add(cell);

    const withoutLogs = await summarize(data);
    assert.equal(withoutLogs.cases[0].status, 'INVALID');
    assert.match(withoutLogs.cases[0].error, /ready\.log/u);

    data.addOracleLogs(cell, { 'ready.log': oracleFixtureMarkers['ready.log'] });
    const withoutCleanup = await summarize(data);
    assert.equal(withoutCleanup.cases[0].status, 'INVALID');
    assert.match(withoutCleanup.cases[0].error, /stop\.log/u);

    data.addOracleLogs(cell, { 'stop.log': 'failed before ' + oracleFixtureMarkers['stop.log'] });
    const invalidCleanup = await summarize(data);
    assert.equal(invalidCleanup.cases[0].status, 'INVALID');
    assert.match(invalidCleanup.cases[0].error, /ORACLE_FIXTURE_CLEANUP_PASS/u);

    data.addOracleLogs(cell);
    const valid = await summarize(data);
    assert.equal(valid.cases[0].status, 'PASS');
    assert.equal(valid.status, 'PASS');
  });
}

for (const [field, value] of [['provider', 'oracle'], ['rid', 'win-x64'], ['tfm', 'net8.0'], ['mode', 'crud-only']]) {
  test(`拒绝 ${field} 与矩阵声明不一致的收据`, async t => {
    const data = fixture(t);
    data.add(expected, { [field]: value });
    const result = await summarize(data);

    assert.equal(result.status, 'FAIL');
    assert.notEqual(result.cases[0].status, 'PASS');
  });
}

test('Native Job 失败时即使所有 case 收据有效也不能整体通过', async t => {
  const data = fixture(t);
  data.add();
  const result = await summarize(data, 'failure');

  assert.equal(result.status, 'FAIL');
  assert.equal(result.nativeJobResult, 'failure');
  assert.equal(result.cases[0].status, 'PASS');
});

test('Native Job 被取消时不能整体通过', async t => {
  const data = fixture(t);
  data.add();
  const result = await summarize(data, 'cancelled');

  assert.equal(result.status, 'FAIL');
  assert.equal(result.nativeJobResult, 'cancelled');
});

for (const regressionJobResult of ['failure', 'cancelled', 'skipped']) {
  test(`SQL 回归作业为 ${regressionJobResult} 时拒绝整体验收并保留 Native 结果`, async t => {
    const data = fixture(t);
    data.add();
    const result = await summarize(data, 'success', regressionJobResult);

    assert.equal(result.status, 'FAIL');
    assert.equal(result.regressionJobResult, regressionJobResult);
    assert.equal(result.nativeJobResult, 'success');
    assert.equal(result.cases[0].status, 'PASS');
  });
}

test('SQL 作业成功但未下载 SQL 工件时拒绝整体验收', async t => {
  const data = fixture(t);
  data.add();
  fs.renameSync(path.dirname(data.sqlDirectory), path.join(data.directory, 'uncollected'));
  const result = await summarize(data);

  assert.equal(result.status, 'FAIL');
  assert.equal(result.regression.verified, 0);
  assert.equal(result.cases[0].status, 'PASS');
});

for (const scenario of ['文件缺失', '同名文件被替换', '清单和文件都少一份']) {
  test(`SQL 回归报告${scenario}时仅确认其余 21 份`, async t => {
    const data = fixture(t);
    data.add();
    const [file, other] = Object.keys(data.reports);
    const target = path.join(data.sqlDirectory, file);
    if (scenario === '同名文件被替换') fs.copyFileSync(path.join(data.sqlDirectory, other), target);
    else fs.unlinkSync(target);
    if (scenario === '清单和文件都少一份') {
      delete data.reports[file];
      fs.writeFileSync(data.indexFile, JSON.stringify({ workflow, reports: data.reports }), 'utf8');
    }
    const result = await summarize(data);

    assert.equal(result.status, 'FAIL');
    assert.equal(result.regression.verified, 21);
    assert.equal(result.regression.total, 22);
    if (scenario !== '清单和文件都少一份') assert.ok(result.regression.error.includes(file));
  });
}

for (const [label, source, accepted] of [
  ['其他 run', { ...workflow, runId: '1233' }, false],
  ['其他提交', { ...workflow, sha: 'b'.repeat(40) }, false],
  ['未来 attempt', { ...workflow, attempt: '3' }, false],
  ['同次 run 的较早 attempt', { ...workflow, attempt: '1' }, true]
]) {
  test(`SQL 回归工件来自${label}时${accepted ? '可以复用' : '拒绝整体验收'}`, async t => {
    const data = fixture(t);
    data.add();
    fs.writeFileSync(data.indexFile, JSON.stringify({ workflow: source, reports: data.reports }), 'utf8');
    const result = await summarize(data);

    assert.equal(result.status, accepted ? 'PASS' : 'FAIL');
    assert.equal(result.regression.verified, accepted ? 22 : 0);
  });
}

// 只读取 benchmark-smoke 的上传步骤，避免其他 Job 的相同规则掩盖遗漏。
function benchmarkSmokeUpload() {
  const workflowFile = fs.readFileSync(path.join(root, '.github/workflows/sql-aot.yml'), 'utf8');
  const job = workflowFile.split(/^  benchmark-smoke:\r?\n/mu)[1]?.split(/^  [\w-]+:\r?$/mu)[0];
  assert.ok(job, 'benchmark-smoke Job 必须存在');
  const step = job.split(/(?=^      - )/mu)
    .find(item => item.includes('uses: actions/upload-artifact@') &&
      /^          name: sql-benchmark-smoke\r?$/mu.test(item));
  assert.ok(step, 'benchmark-smoke 必须上传自己的工件');
  const paths = step.match(/^          path: \|\r?\n((?:^            .+\r?\n)+)/mu)?.[1];
  assert.ok(paths, '需要显式列出工件路径');
  return { step, paths: paths.trim().split(/\r?\n/u).map(line => line.trim()) };
}

for (const [extension, evidence] of [
  ['json', '收据和候选身份'], ['log', '采集与转换日志'],
  ['nettrace', '可重放的原始 CPU trace'], ['jsonl', '测量窗口样本'],
  ['csv', '方法归因表'], ['stderr', '子进程标准错误']
]) {
  test(`性能 CI 保留${evidence}`, () => {
    assert.ok(benchmarkSmokeUpload().paths.includes(`artifacts/benchmarks/sql-smoke/**/*.${extension}`),
      `缺少 ${extension} 上传路径，诊断证据将丢失`);
  });
}

test('性能 CI 上传限于诊断证据，不包含可重建的 ETLX 或构建目录', () => {
  const paths = benchmarkSmokeUpload().paths.filter(item => item.startsWith('artifacts/'));
  assert.deepEqual([...paths].sort(), ['json', 'log', 'nettrace', 'jsonl', 'csv', 'stderr']
    .map(extension => `artifacts/benchmarks/sql-smoke/**/*.${extension}`).sort());
});

test('性能 CI 在测试失败时仍保留诊断工件十四天', () => {
  const { step } = benchmarkSmokeUpload();
  assert.match(step, /^        if: always\(\)\r?$/mu);
  assert.match(step, /^          retention-days: 14\r?$/mu);
});

test('独立 SQL 验收分支推送直接触发 Native AOT 工作流', () => {
  const workflowFile = fs.readFileSync(path.join(root, '.github/workflows/sql-aot.yml'), 'utf8');
  const push = workflowFile.match(/^  push:\r?\n((?:^    .+\r?\n)+)/mu)?.[1];

  assert.ok(push, '需要支持验收分支的 push 事件');
  assert.deepEqual(push.trim().split(/\r?\n/u).map(line => line.trim()), [
    'branches:', "- 'codex/sql-aot-rc'"
  ]);
});

test('性能工具单独变更也会触发 SQL 工作流', () => {
  const workflowFile = fs.readFileSync(path.join(root, '.github/workflows/sql-aot.yml'), 'utf8');
  const filters = workflowFile.split('  pull_request:')[1].split('\njobs:')[0];
  const patterns = [...filters.matchAll(/^\s*- '([^']+)'/gmu)].map(match => match[1]);
  for (const file of ['eng/perf/BuildProcessTree.cs', 'eng/perf/Invoke-SqlCleanBuildPeak.ps1',
    'eng/perf/SqlCpuTrace/Program.cs', 'eng/perf/tests/CpuAttributionTests.cs'])
    assert.ok(patterns.some(pattern => path.posix.matchesGlob(file, pattern)), file);
});

test('sql-regression 工作流独立运行 Windows 多目标回归并发布 TRX', () => {
  const workflowFile = fs.readFileSync(path.join(root, '.github/workflows/sql-aot.yml'), 'utf8');
  const lines = workflowFile.split(/\r?\n/u);
  const jobStart = lines.indexOf('  sql-regression:');
  const jobsStart = lines.indexOf('jobs:');
  const pullRequestStart = lines.indexOf('  pull_request:');

  assert.notEqual(jobStart, -1, '需要独立的 sql-regression Job');
  assert.notEqual(jobsStart, -1, '需要 workflow jobs 节点');
  assert.notEqual(pullRequestStart, -1, '需要 pull_request 路径过滤');

  const nextJob = lines.findIndex((line, index) => index > jobStart && /^  [\w-]+:\s*$/u.test(line));
  const job = lines.slice(jobStart, nextJob === -1 ? lines.length : nextJob).join('\n');
  const pullRequest = lines.slice(pullRequestStart, jobsStart).join('\n');

  assert.match(job, /runs-on:\s*windows-/u);
  assert.match(job, /uses:\s*actions\/setup-dotnet@v4/u);
  for (const sdk of ['10.0.4xx', '8.0.x', '6.0.x'])
    assert.ok(job.includes(sdk), `sql-regression Job 需要安装 .NET SDK ${sdk}`);
  assert.match(job, /Invoke-SqlRegressionTests\.ps1/u);
  assert.match(job, /actions\/upload-artifact@v4/u);
  assert.match(job, /if:\s*(?:\$\{\{\s*)?always\(\)(?:\s*\}\})?/u);
  assert.match(job, /name:\s*sql-regression\b/u);
  assert.match(job, /output\/sql-regression\/\*\*\/\*\.trx/u);
  assert.ok(job.includes('output/sql-regression/**/reports.json'));
  assert.match(job, /overwrite:\s*true/u);
  assert.match(job, /if-no-files-found:\s*error/u);

  for (const changedPath of [
    'eng/ci/Invoke-SqlRegressionTests.ps1',
    'eng/ci/tests/Invoke-SqlRegressionTests.Tests.ps1',
    'framework/tests/Bing.Data.Sql*/**',
    'framework/tests/Bing.Dapper*/**',
    'framework/tests/Bing.Uow.Tests/**'
  ])
    assert.ok(pullRequest.includes(changedPath), `pull_request paths 需要包含 ${changedPath}`);
});

test('GitHub Actions 汇总同时接收 SQL 回归与 Native 结果并在失败时保留总报告', () => {
  const workflowFile = fs.readFileSync(path.join(root, '.github/workflows/sql-aot.yml'), 'utf8');
  const summary = workflowFile.slice(workflowFile.indexOf('\n  native-summary:'));

  assert.match(summary, /needs:\s*\[matrix,\s*native,\s*sql-regression\]/u);
  assert.match(summary, /SQL_REGRESSION_JOB_RESULT:\s*\$\{\{\s*needs\.sql-regression\.result\s*\}\}/u);
  assert.match(summary, /--ci-summary output\/sql-aot-download "\$NATIVE_JOB_RESULT" "\$SQL_REGRESSION_JOB_RESULT"/u);
  assert.match(summary, /if:\s*\$\{\{\s*always\(\)\s*\}\}/u);
  assert.match(summary, /actions\/download-artifact@v4/u);
  assert.match(summary, /pattern:\s*sql-aot-\*/u);
  assert.match(summary, /name: sql-regression\s+path: output\/sql-aot-download\/sql-regression/u);
  assert.match(summary, /merge-multiple:\s*false/u);
  assert.match(summary, /name:\s*sql-native-summary/u);
  assert.match(summary, /actions\/upload-artifact@v4/u);
  assert.match(summary, /if:\s*always\(\)/u);
});

test('实际 CLI 只在 SQL 回归成功且同一 run 的 Native 收据齐全时通过', async t => {
  const { loadMatrix } = await import('./verify.mjs');
  const matrix = loadMatrix();
  const data = fixture(t, matrix.cases);
  const automatic = matrix.cases.filter(cell => cell.status === 'enabled' && cell.ci !== false);
  for (const cell of automatic) {
    data.add(cell);
    if (cell.provider === 'oracle')
      data.addOracleLogs(cell);
  }
  const env = { ...process.env, GITHUB_REPOSITORY: workflow.repository, GITHUB_RUN_ID: workflow.runId,
    GITHUB_SHA: workflow.sha, GITHUB_RUN_ATTEMPT: workflow.attempt };
  const run = (directory, regressionJobResult = 'success') => spawnSync(process.execPath,
    ['eng/sql-aot/verify.mjs', '--ci-summary', directory, 'success', regressionJobResult], { cwd: root, env, encoding: 'utf8' });
  const pass = run(data.directory);
  assert.equal(pass.status, 0, pass.stderr);
  const report = JSON.parse(pass.stdout);
  assert.equal(report.status, 'PASS');
  assert.equal(report.regressionJobResult, 'success');
  assert.equal(report.passed, automatic.length);
  assert.equal(report.regression.verified, 22);

  const regressionFailed = run(data.directory, 'failure');
  assert.equal(regressionFailed.status, 1, regressionFailed.stderr);
  const failedReport = JSON.parse(regressionFailed.stdout);
  assert.equal(failedReport.status, 'FAIL');
  assert.equal(failedReport.regressionJobResult, 'failure');
  assert.equal(failedReport.passed, automatic.length);

  fs.unlinkSync(data.indexFile);
  const noIndex = run(data.directory);
  assert.equal(noIndex.status, 1, noIndex.stderr);
  const withoutIndex = JSON.parse(noIndex.stdout);
  assert.equal(withoutIndex.passed, automatic.length);
  assert.equal(withoutIndex.regression.verified, 0);

  const fail = run(path.join(data.directory, 'empty'));
  assert.equal(fail.status, 1, fail.stderr);
  assert.equal(JSON.parse(fail.stdout).cases.filter(cell => cell.status === 'MISSING').length, automatic.length);
});

// 在真实 Bash 中执行工作流的清理命令，通过进程退出码验证 Docker 失败传播。
const bash = process.platform === 'win32'
  ? path.join(process.env.ProgramFiles ?? 'C:/Program Files', 'Git/bin/bash.exe')
  : 'bash';
const containerId = 'a'.repeat(64);

function runCleanup(script, dockerState, cwd = root, env = {}) {
  return spawnSync(bash, ['--noprofile', '--norc', '-e', '-o', 'pipefail', '-c', `
    docker() {
      case "$1" in
        ps)
          if [ "$DOCKER_STATE" = list-error ]; then return 12; fi
          if [ "$DOCKER_STATE" != absent ]; then printf '%s\\n' "${containerId}"; fi
          return 0
          ;;
        rm)
          printf 'removed %s\\n' "\${!#}"
          if [ "$DOCKER_STATE" = remove-error ]; then return 13; fi
          ;;
      esac
    }
    ${script}`], { cwd, env: { ...process.env, ...env, DOCKER_STATE: dockerState }, encoding: 'utf8' });
}

for (const provider of ['PostgreSQL', 'MySQL']) {
  test(`Linux ${provider} 清理容器时允许缺失并传播 Docker 错误`, () => {
    const workflowFile = fs.readFileSync(path.join(root, '.github/workflows/sql-aot.yml'), 'utf8');
    const step = workflowFile.split(/(?=^      - )/mu)
      .find(step => step.includes(`name: Remove isolated ${provider} test database`));
    const [run, ...lines] = step.slice(step.indexOf('        run: ')).trimEnd().split(/\r?\n/u);
    const script = run.trim() === 'run: |' ? lines.map(line => line.slice(10)).join('\n') : run.trim().slice(5);

    const absent = runCleanup(script, 'absent');
    assert.equal(absent.status, 0, absent.stderr);
    assert.equal(absent.stdout, '');
    const present = runCleanup(script, 'present');
    assert.equal(present.status, 0, present.stderr);
    assert.equal(present.stdout.trim(), 'removed ' + containerId);
    assert.equal(runCleanup(script, 'list-error').status, 12);
    assert.equal(runCleanup(script, 'remove-error').status, 13);
  });
}

const oracleScript = path.join(root, 'eng/sql-aot/Invoke-LinuxOracle.sh').split(path.sep).join('/');

for (const [dockerState, exitCode] of [['present', 0], ['absent', 0], ['list-error', 12], ['remove-error', 13]]) {
  test('Oracle 清理日志和容器收据随 Docker 结果变化 ' + dockerState, t => {
    const directory = tempDirectory(t);
    const logs = path.join(directory, 'output/sql-aot-oracle');
    const receipt = path.join(directory, 'sql-aot-oracle-container');
    const stopLog = path.join(logs, 'stop.log');
    fs.mkdirSync(logs, { recursive: true });
    fs.writeFileSync(receipt, containerId + '\n', 'utf8');
    const env = { RUNNER_TEMP: directory.split(path.sep).join('/'), FIXTURE_PATH: oracleScript };
    const result = runCleanup('source "$FIXTURE_PATH" stop', dockerState, directory, env);

    assert.equal(result.status, exitCode, result.stderr);
    if (exitCode === 0) {
      assert.equal(fs.existsSync(receipt), false);
      const lines = fs.readFileSync(stopLog, 'utf8').trim().split(/\r?\n/u);
      assert.equal(lines.at(-1), oracleFixtureMarkers['stop.log']);
    } else {
      assert.equal(fs.readFileSync(receipt, 'utf8').trim(), containerId);
      if (fs.existsSync(stopLog))
        assert.doesNotMatch(fs.readFileSync(stopLog, 'utf8'), /ORACLE_FIXTURE_CLEANUP_PASS/u);
    }
  });
}

test('Windows Oracle 将工作目录和服务地址交给已准备的 WSL2 Docker', t => {
  const directory = tempDirectory(t);
  const environment = path.join(directory, 'github.env');
  const env = { RUNNER_TEMP: directory, GITHUB_ENV: environment, WSL_DISTRIBUTION: 'Ubuntu',
    WSL_IP: '172.18.10.2', WSLENV: 'LANG' };
  const mock = String.raw`docker() { return 98; }
  wsl.exe() {
    printf '%s\n' "$WSLENV" "$@"
    return 17
  }`;
  for (const action of ['start', 'stop']) {
    const result = spawnSync(bash, ['--noprofile', '--norc', '-c',
      mock + '\nsource eng/sql-aot/Invoke-LinuxOracle.sh ' + action],
    { cwd: root, env: { ...process.env, ...env }, encoding: 'utf8', timeout: 10000 });
    assert.equal(result.status, 17, result.stderr);
    assert.deepEqual(result.stdout.trim().split(/\r?\n/u), [
      'LANG:RUNNER_TEMP/p:GITHUB_ENV/p:WSL_IP', '--distribution', 'Ubuntu', '--user', 'root',
      '--exec', 'bash', 'eng/sql-aot/Invoke-LinuxOracle.sh', action
    ]);
  }
});

for (const [runtime, ip] of [['Linux', '127.0.0.1'], ['Windows WSL2', '172.18.10.2']]) {
  test('Oracle ' + runtime + ' 使用当前 Docker 主机地址和动态端口', t => {
    const directory = tempDirectory(t);
    const environment = path.join(directory, 'github.env');
    const env = { GITHUB_ENV: environment.split(path.sep).join('/'),
      RUNNER_TEMP: directory.split(path.sep).join('/'), WSL_DISTRIBUTION: '',
      WSL_IP: runtime === 'Linux' ? '' : ip, EXPECTED_IP: ip, FIXTURE_PATH: oracleScript };
    const script = String.raw`
      openssl() { printf '%s' 0123456789abcdef; }
      docker() {
        case "$1" in
          create)
            [[ "$2" == -p && "$3" == "$EXPECTED_IP::1521" ]] || return 18
            printf '%s\n' "${containerId}"
            ;;
          inspect) printf '%s\n' healthy ;;
          exec) cat >/dev/null ;;
          port) printf '%s:49152\n' "$EXPECTED_IP" ;;
        esac
      }
      source "$FIXTURE_PATH" start`;
    const result = spawnSync(bash, ['--noprofile', '--norc', '-c', script],
      { cwd: directory, env: { ...process.env, ...env }, encoding: 'utf8', timeout: 10000 });
    assert.equal(result.status, 0, result.stderr);
    assert.equal(fs.readFileSync(environment, 'utf8'),
      'RUN_ORACLE_INTEGRATION_TESTS=true\nConnectionStrings__OracleConnection=Data Source=' + ip +
      ':49152/FREEPDB1;User Id=BING_SQL_AOT_TEST;Password=Bing10123456789abcdef;Pooling=false\n');
    assert.equal(fs.readFileSync(path.join(directory, 'sql-aot-oracle-container'), 'utf8').trim(), containerId);
    assert.equal(fs.readFileSync(path.join(directory, 'output/sql-aot-oracle/ready.log'), 'utf8'),
      'healthy\n' + oracleFixtureMarkers['ready.log'] + '\n');
  });
}
