import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { test } from 'node:test';

const root = path.resolve(import.meta.dirname, '../..');
const api = () => import('./verify.mjs');
const nativeCase = {
  id: 'sqlite-win-x64-raw', provider: 'sqlite', rid: 'win-x64', tfm: 'net10.0',
  status: 'enabled', runner: 'windows-2022', mode: 'raw-only',
  project: 'framework/tests/Bing.Data.Sql.Aot.RawOnly/Bing.Data.Sql.Aot.RawOnly.csproj',
  successMarker: 'RAW_ONLY_PACKAGE_NATIVE_PASS', coverage: ['raw-reader', 'parameter-binder']
};
const sqliteCrudRequiredMarkers = [
  'MUTATION_PLAN_READER_NATIVE_PASS',
  'GENERATED_RETURNING_PROJECTION_NATIVE_PASS',
  'GENERATED_ENTITY_RETURNING_SQL_NATIVE_PASS',
  'GENERATED_MUTATION_SHAPES_NATIVE_PASS',
  'GENERATED_MULTIROW_INSERT_NATIVE_PASS',
  'GENERATED_IN_DELETE_NATIVE_PASS',
  'GENERATED_PAIRED_DELETE_NATIVE_PASS',
  'GENERATED_KEY_INSERT_SQL_NATIVE_PASS',
  'GENERATED_CRUD_SQL_NATIVE_PASS',
  'GENERATED_NEGATED_RETURNING_NATIVE_PASS',
  'GENERATED_COMPUTED_RETURNING_MAPPING_NATIVE_PASS',
  'GENERATED_QUOTED_RETURNING_NATIVE_PASS',
  'GENERATED_CACHED_RETURNING_NATIVE_PASS'
];

test('显式手动格不进入 CI，默认与显式开启格继续调度且手动选择可用', async () => {
  const { ciMatrix, selectCase } = await api();
  const defaultCell = { ...nativeCase, id: 'default' };
  const enabledCell = { ...nativeCase, id: 'automatic', ci: true };
  const manualCell = { ...nativeCase, id: 'manual', ci: false };
  const plannedCell = { ...nativeCase, id: 'planned', status: 'planned', ci: true, reason: '未接入' };
  const matrix = { cases: [defaultCell, enabledCell, manualCell, plannedCell] };

  assert.deepEqual(ciMatrix(matrix), { include: [
    { id: 'default', runner: 'windows-2022' },
    { id: 'automatic', runner: 'windows-2022' }
  ] });
  assert.equal(selectCase(matrix, 'manual', 'win32', 'x64'), manualCell);
  assert.throws(() => selectCase(matrix, 'manual', 'linux', 'x64'), /host/);
  assert.throws(() => selectCase(matrix, 'planned', 'win32', 'x64'), /planned/);
});

for (const provider of ['mysql', 'postgresql']) {
  test(`${provider} Windows Native 格复用 Linux 消费合同，按数据库准备能力调度 CI`, async () => {
    const { loadMatrix, ciMatrix, selectCase, buildCommands } = await api();
    const matrix = loadMatrix();
    const cell = matrix.cases.find(candidate => candidate.id === `${provider}-win-x64`);
    const linux = matrix.cases.find(candidate => candidate.id === `${provider}-linux-x64`);
    assert.ok(cell, '必须有单独的 Windows Native 格');
    assert.equal(cell.status, 'enabled');
    assert.equal(cell.provider, provider);
    assert.equal(cell.rid, 'win-x64');
    assert.equal(cell.tfm, 'net10.0');
    assert.equal(cell.runner, 'windows-2022');
    assert.equal(cell.ci, true);
    assert.equal(cell.project, linux.project);
    assert.equal(cell.mode, linux.mode);
    assert.equal(cell.successMarker, linux.successMarker);
    assert.deepEqual(cell.requiredMarkers, linux.requiredMarkers);
    assert.deepEqual(cell.coverage, linux.coverage);
    assert.deepEqual(cell.notCovered, linux.notCovered);
    assert.equal(selectCase(matrix, cell.id, 'win32', 'x64'), cell);
    assert.throws(() => selectCase(matrix, cell.id, 'linux', 'x64'), /host/);
    assert.equal(ciMatrix(matrix).include.some(candidate => candidate.id === cell.id), true);
    const directory = path.join(root, 'output/sql-aot/windows provider');
    const commands = buildCommands(cell, { root, publishDirectory: directory });
    assert.equal(commands.publish.args[commands.publish.args.indexOf('-r') + 1], 'win-x64');
    assert.equal(commands.run.file, path.join(directory, path.basename(cell.project, '.csproj') + '.exe'));
  });
}

test('MySQL Linux Native 格验证生成式读写与异步事务并进入 CI', async () => {
  const { loadMatrix, ciMatrix, selectCase, buildCommands } = await api();
  const matrix = loadMatrix();
  const cell = matrix.cases.find(candidate => candidate.id === 'mysql-linux-x64');
  assert.ok(cell, '必须有单独的 MySQL Linux Native 格');
  assert.equal(cell.status, 'enabled');
  assert.equal(cell.provider, 'mysql');
  assert.equal(cell.rid, 'linux-x64');
  assert.equal(cell.runner, 'ubuntu-22.04');
  assert.equal(cell.tfm, 'net10.0');
  assert.equal(cell.successMarker, 'MYSQL_GENERATED_NATIVE_PASS');
  assert.deepEqual(cell.requiredMarkers, ['MYSQL_GENERATED_CRUD_NATIVE_PASS', 'MYSQL_IDENTITY_NATIVE_PASS']);
  assert.ok(fs.existsSync(path.join(root, cell.project)));
  assert.ok(ciMatrix(matrix).include.some(candidate => candidate.id === cell.id));
  assert.equal(selectCase(matrix, cell.id, 'linux', 'x64'), cell);
  assert.throws(() => selectCase(matrix, cell.id, 'win32', 'x64'), /host/);
  const commands = buildCommands(cell, { root, publishDirectory: path.join(root, 'output/sql-aot/mysql') });
  assert.equal(commands.publish.args[commands.publish.args.indexOf('-r') + 1], 'linux-x64');
  assert.equal(path.basename(commands.run.file), 'Bing.Data.Sql.Aot.MySql');
});

for (const provider of ['mysql', 'postgresql']) {
  const name = provider === 'mysql' ? 'MySQL' : 'PostgreSQL';
  const id = `${provider}-win-x64`;
  const entry = provider === 'mysql' ? 'WindowsMySql' : 'WindowsPostgreSql';
  const tests = `${entry}.Tests`;
  test(`Windows ${name} CI 在 Native 前独立建库并在 always 阶段清理和上传日志`, () => {
    const workflow = fs.readFileSync(path.join(root, '.github/workflows/sql-aot.yml'), 'utf8');
    const steps = workflow.split(/(?=^      - )/mu);
    const start = steps.findIndex(step => step.includes(`name: Start isolated Windows ${name} test database`));
    const run = steps.findIndex(step => step.includes('name: Publish and execute Native case'));
    const stop = steps.findIndex(step => step.includes(`name: Stop isolated Windows ${name} test database`));
    assert.ok(start >= 0 && start < run && stop > run);
    assert.match(steps[start], new RegExp(`if:.*matrix\\.id == '${id}'`, 'u'));
    assert.match(steps[start], /shell: pwsh/u);
    assert.match(steps[start], new RegExp(`Invoke-${entry}\\.ps1 -Action Start`, 'u'));
    assert.match(steps[stop], new RegExp(`if:.*always\\(\\).*matrix\\.id == '${id}'`, 'u'));
    assert.match(steps[stop], new RegExp(`Invoke-${entry}\\.ps1 -Action Stop`, 'u'));
    assert.match(workflow, new RegExp(`run:.*tests/${tests}\\.ps1`, 'u'));
    assert.match(workflow, new RegExp(`output/sql-aot-${provider}/\\*\\.log`, 'u'));
    assert.match(workflow, new RegExp(`output/sql-aot-${provider}/state\\.json`, 'u'));
  });
}

test('SQLite RawOnly 矩阵格要求验证泛型转换器依赖注入', async () => {
  const { loadMatrix } = await api();
  const matrix = loadMatrix();
  for (const id of ['sqlite-win-x64-raw', 'sqlite-linux-x64-raw']) {
    const cell = matrix.cases.find(candidate => candidate.id === id);
    assert.deepEqual(cell.requiredMarkers, ['GENERIC_CONVERTER_NATIVE_PASS']);
    assert.ok(cell.coverage.includes('generic-converter-constructor-injection'));
  }
});

test('四个 SQLite CRUD 矩阵格要求十三项生成式能力并阻止 IL2070 和 IL2072 回归', async () => {
  const { loadMatrix } = await api();
  const matrix = loadMatrix();
  for (const rid of ['linux-x64', 'win-x64', 'osx-arm64', 'win-arm64']) {
    const id = `sqlite-${rid}-crud`;
    const cell = matrix.cases.find(candidate => candidate.id === id);
    assert.ok(cell, `${id} 必须存在`);
    assert.equal(cell.successMarker, 'CRUD_ONLY_PACKAGE_NATIVE_PASS');
    assert.deepEqual([...(cell.requiredMarkers ?? [])].sort(), [...sqliteCrudRequiredMarkers].sort());
    for (const coverage of [
      'generated-variable-row-insert',
      'generated-in-batch-delete',
      'generated-paired-batch-delete',
      'generated-entity-returning-sql',
      'generated-negated-returning-sql',
      'generated-computed-returning-current-mapping',
      'generated-quoted-returning-columns',
      'generated-cached-returning-projection'
    ]) assert.ok(cell.coverage.includes(coverage), `${id} 缺少覆盖项 ${coverage}`);
    assert.ok(cell.coverage.includes('strict-mutation-plan-reader'));
    const project = fs.readFileSync(path.join(root, cell.project), 'utf8');
    assert.match(project, /<WarningsAsErrors>[^<]*;IL2072<\/WarningsAsErrors>/u);
    assert.match(project, /<WarningsAsErrors>[^<]*;IL2070[;<]/u);
  }
});

test('PostgreSQL Linux Native 格只在真实数据库生成读写路径接入后进入 CI', async () => {
  const { loadMatrix, ciMatrix, selectCase, buildCommands } = await api();
  const matrix = loadMatrix();
  const cell = matrix.cases.find(candidate => candidate.id === 'postgresql-linux-x64');
  assert.ok(cell, '必须有单独的 PostgreSQL Linux Native 格');
  assert.equal(cell.status, 'enabled');
  assert.equal(cell.provider, 'postgresql');
  assert.equal(cell.rid, 'linux-x64');
  assert.equal(cell.runner, 'ubuntu-22.04');
  assert.equal(cell.tfm, 'net10.0');
  assert.equal(cell.successMarker, 'POSTGRESQL_GENERATED_NATIVE_PASS');
  assert.deepEqual(cell.requiredMarkers, [
    'POSTGRESQL_GENERATED_CRUD_NATIVE_PASS',
    'POSTGRESQL_IDENTITY_RETURNING_NATIVE_PASS',
    'POSTGRESQL_JSON_TEXT_NATIVE_PASS',
    'POSTGRESQL_TYPED_JSON_NATIVE_PASS',
    'POSTGRESQL_BUILT_IN_RANGES_NATIVE_PASS',
    'POSTGRESQL_EXPLICIT_COMPOSITE_NATIVE_PASS'
  ]);
  assert.ok(cell.coverage.includes('generated-json-jsonb-text-roundtrip'));
  assert.ok(cell.coverage.includes('generated-json-jsonb-typed-roundtrip'));
  assert.ok(fs.existsSync(path.join(root, cell.project)));
  assert.ok(ciMatrix(matrix).include.some(candidate => candidate.id === cell.id));
  assert.equal(selectCase(matrix, cell.id, 'linux', 'x64'), cell);
  assert.throws(() => selectCase(matrix, cell.id, 'win32', 'x64'), /host/);
  const commands = buildCommands(cell, { root, publishDirectory: path.join(root, 'output/sql-aot/postgresql') });
  assert.equal(commands.publish.args[commands.publish.args.indexOf('-r') + 1], 'linux-x64');
  assert.equal(path.basename(commands.run.file), 'Bing.Data.Sql.Aot.PostgreSql');
});

test('PostgreSQL 的两个 x64 Native 格都要求内置范围和多范围回归通过', async () => {
  const { loadMatrix } = await api();
  const { cases } = loadMatrix();
  for (const id of ['postgresql-linux-x64', 'postgresql-win-x64']) {
    const cell = cases.find(candidate => candidate.id === id);
    assert.ok(cell.requiredMarkers.includes('POSTGRESQL_BUILT_IN_RANGES_NATIVE_PASS'), id);
    assert.ok(cell.coverage.includes('generated-built-in-ranges-roundtrip'), id);
  }
});

test('PostgreSQL 的两个 x64 Native 格都要求使用显式 SQL 读写复合类型', async () => {
  const { loadMatrix } = await api();
  const { cases } = loadMatrix();
  for (const id of ['postgresql-linux-x64', 'postgresql-win-x64']) {
    const cell = cases.find(candidate => candidate.id === id);
    assert.ok(cell.requiredMarkers.includes('POSTGRESQL_EXPLICIT_COMPOSITE_NATIVE_PASS'), id);
    assert.ok(cell.coverage.includes('explicit-composite-sql-roundtrip'), id);
  }
});

function fixture(t) {
  const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'bing-aot-tests-'));
  t.after(() => fs.rmSync(directory, { recursive: true, force: true }));
  return directory;
}

// 只构造识别 RID 所需的头字段，进程启动独立于此夹具。
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
    throw new Error('No Native header fixture for ' + rid);
  }
  return header;
}

// 测试发布/执行流程，不启动 dotnet；真实 Native 验证由同一运行器另行完成。
function fakeExecutor({ publishCode = 0, runCode = 0, marker = nativeCase.successMarker, binary = true } = {}) {
  const calls = [];
  return {
    calls,
    execute(command, logFile) {
      calls.push(command);
      let output = '10.0.100\n';
      let code = 0;
      if (command.args[0] === 'publish') {
        code = publishCode;
        output = 'publish log\n';
        if (code === 0 && binary) {
          const destination = command.args[command.args.indexOf('-o') + 1];
          fs.mkdirSync(destination, { recursive: true });
          const rid = command.args[command.args.indexOf('-r') + 1];
          const executable = path.basename(command.args[1], '.csproj') + (rid.startsWith('win-') ? '.exe' : '');
          fs.writeFileSync(path.join(destination, executable), binary === true ? nativeHeader(rid) : binary);
        }
      } else if (command.file !== 'dotnet') {
        code = runCode;
        output = marker + '\n';
      }
      fs.writeFileSync(logFile, output, 'utf8');
      return { status: code, signal: null };
    }
  };
}

test('Native 运行器保存当前工作流身份，汇总器可直接读取生成的收据', async t => {
  const { loadMatrix, runCase, summarizeCiReceipts } = await api();
  const rid = `${{ win32: 'win', linux: 'linux', darwin: 'osx' }[process.platform]}-${process.arch}`;
  const cell = loadMatrix().cases.find(candidate => candidate.id === `sqlite-${rid}-raw`);
  if (!cell) return t.skip('当前测试主机不在 Native 矩阵中');
  const values = { GITHUB_REPOSITORY: 'team/project', GITHUB_RUN_ID: '1234',
    GITHUB_SHA: 'a'.repeat(40), GITHUB_RUN_ATTEMPT: '2' };
  const previous = Object.fromEntries(Object.keys(values).map(key => [key, process.env[key]]));
  Object.assign(process.env, values);
  t.after(() => {
    for (const [key, value] of Object.entries(previous))
      if (value === undefined) delete process.env[key];
      else process.env[key] = value;
  });
  const directory = fixture(t);
  const output = path.join(directory, `sql-aot-${cell.id}`, 'sql-aot');
  const marker = [cell.successMarker, ...(cell.requiredMarkers ?? [])].join('\n');
  const result = runCase(cell, { root, output }, fakeExecutor({ marker }).execute);
  const receipt = JSON.parse(fs.readFileSync(path.join(result.directory, 'result.json'), 'utf8'));

  const workflow = { repository: 'team/project', runId: '1234', sha: 'a'.repeat(40), attempt: '2' };
  assert.deepEqual(receipt.workflow, workflow);
  const summary = summarizeCiReceipts({ cases: [cell] }, directory, workflow, 'success', 'success');
  assert.equal(summary.cases[0].status, 'PASS');
  assert.equal(summary.passed, 1);
  assert.equal(summary.regression.verified, 0);
  assert.equal(summary.status, 'FAIL');
});

test('矩阵只将已接入格交给 CI，计划格保留未覆盖原因', async () => {
  const { loadMatrix, ciMatrix } = await api();
  const matrix = loadMatrix();
  assert.equal(new Set(matrix.cases.map(cell => cell.id)).size, matrix.cases.length);
  const enabled = matrix.cases.filter(cell => cell.status === 'enabled');
  assert.deepEqual(enabled.map(cell => cell.id).sort(), [
    'mysql-linux-x64', 'mysql-win-x64', 'postgresql-linux-x64', 'postgresql-win-x64',
    'sqlite-linux-x64-crud', 'sqlite-linux-x64-generated-query', 'sqlite-linux-x64-query', 'sqlite-linux-x64-raw',
    'sqlite-win-x64-crud', 'sqlite-win-x64-generated-query', 'sqlite-win-x64-query', 'sqlite-win-x64-raw',
    'sqlite-osx-arm64-crud', 'sqlite-osx-arm64-generated-query', 'sqlite-osx-arm64-query', 'sqlite-osx-arm64-raw',
    'sqlite-win-arm64-crud', 'sqlite-win-arm64-generated-query', 'sqlite-win-arm64-query', 'sqlite-win-arm64-raw',
    'sqlserver-linux-x64', 'sqlserver-win-x64', 'oracle-linux-x64', 'oracle-win-x64'
  ].sort());
  assert.deepEqual([...new Set(enabled.map(cell => cell.provider))].sort(), ['mysql', 'oracle', 'postgresql', 'sqlite', 'sqlserver']);
  const automatic = enabled.filter(cell => cell.ci !== false);
  assert.deepEqual(ciMatrix(matrix), { include: automatic.map(({ id, runner }) => ({ id, runner })) });
  assert.equal(automatic.length, 24);
  for (const cell of matrix.cases) {
    if (cell.status === 'planned') assert.ok(cell.reason.length > 0);
    else {
      assert.ok(fs.existsSync(path.join(root, cell.project)));
      assert.ok(cell.coverage.length > 0 && cell.successMarker.length > 0);
    }
  }
  for (const provider of ['mysql', 'postgresql', 'sqlserver', 'oracle'])
    assert.ok(matrix.cases.some(cell => cell.provider === provider && cell.status === 'planned'));
});

for (const [rid, platform, runner] of [
  ['osx-arm64', 'darwin', 'macos-15'],
  ['win-arm64', 'win32', 'windows-11-arm']
]) {
  test(`SQLite ${rid} 调度四个消费场景并要求原生 ARM64 主机`, async () => {
    const { loadMatrix, ciMatrix, selectCase, buildCommands } = await api();
    const matrix = loadMatrix();
    const jobs = ciMatrix(matrix).include;
    const publishDirectory = path.join(root, 'output/sql-aot/arm64 native');
    for (const suffix of ['query', 'raw', 'crud', 'generated-query']) {
      const id = `sqlite-${rid}-${suffix}`;
      const baseline = selectCase(matrix, `sqlite-win-x64-${suffix}`, 'win32', 'x64');
      const cell = selectCase(matrix, id, platform, 'arm64');
      assert.deepEqual(cell, { ...baseline, id, rid, runner });
      assert.deepEqual(jobs.find(job => job.id === id), { id, runner });
      assert.throws(() => selectCase(matrix, id, platform, 'x64'), /matching host/u);
      assert.throws(() => selectCase(matrix, id, platform === 'win32' ? 'darwin' : 'win32', 'arm64'), /matching host/u);

      const { publish, run } = buildCommands(cell, { root, publishDirectory });
      assert.equal(publish.args[publish.args.indexOf('-r') + 1], rid);
      const extension = platform === 'win32' ? '.exe' : '';
      assert.equal(run.file, path.join(publishDirectory, path.basename(cell.project, '.csproj') + extension));
    }
  });
}

test('Native workflow 按 runner 架构选择 Node，并使用仓库指定的 .NET SDK', () => {
  const workflow = fs.readFileSync(path.join(root, '.github/workflows/sql-aot.yml'), 'utf8').replaceAll('\r\n', '\n');
  const native = workflow.slice(workflow.indexOf('\n  native:'));
  assert.match(native, /runs-on: \$\{\{ matrix\.runner \}\}/u);
  assert.match(native, /architecture: \$\{\{ runner\.arch == 'ARM64' && 'arm64' \|\| 'x64' \}\}/u);
  const { sdk } = JSON.parse(fs.readFileSync(path.join(root, 'global.json'), 'utf8'));
  const featureBand = sdk.version.replace(/\d{2}$/u, 'xx');
  assert.ok(native.includes(`dotnet-version: '${featureBand}'\n          dotnet-quality: 'ga'`));
});

test('SQLite Linux x64 查询格使用现有消费工程、进入 CI 并生成 Linux Native 命令', async () => {
  const { loadMatrix, ciMatrix, selectCase, buildCommands } = await api();
  const matrix = loadMatrix();
  const cell = matrix.cases.find(candidate => candidate.id === 'sqlite-linux-x64-query');
  assert.ok(cell, 'SQLite Linux x64 查询格必须存在');
  assert.deepEqual({
    provider: cell.provider,
    rid: cell.rid,
    tfm: cell.tfm,
    runner: cell.runner,
    mode: cell.mode,
    status: cell.status,
    project: cell.project,
    successMarker: cell.successMarker
  }, {
    provider: 'sqlite',
    rid: 'linux-x64',
    tfm: 'net10.0',
    runner: 'ubuntu-22.04',
    mode: 'query',
    status: 'enabled',
    project: 'framework/tests/Bing.Data.Sql.Aot.Smoke/Bing.Data.Sql.Aot.Smoke.csproj',
    successMarker: 'PASS: generated SQLite query smoke'
  });
  const windowsQuery = matrix.cases.find(candidate => candidate.id === 'sqlite-win-x64-query');
  assert.deepEqual(cell.coverage, windowsQuery.coverage);
  assert.deepEqual(cell.notCovered, windowsQuery.notCovered);
  assert.ok(ciMatrix(matrix).include.some(candidate => candidate.id === cell.id && candidate.runner === 'ubuntu-22.04'));
  assert.equal(selectCase(matrix, cell.id, 'linux', 'x64'), cell);
  assert.throws(() => selectCase(matrix, cell.id, 'win32', 'x64'), /host/);

  const publishDirectory = path.join(root, 'output/sql-aot/linux native');
  const commands = buildCommands(cell, { root, publishDirectory });
  const runtimeOption = commands.publish.args.indexOf('-r');
  assert.notEqual(runtimeOption, -1);
  assert.equal(commands.publish.args[runtimeOption + 1], 'linux-x64');
  assert.equal(commands.run.file, path.join(publishDirectory, 'Bing.Data.Sql.Aot.Smoke'));
  assert.equal(commands.run.file.endsWith('.exe'), false);
});

for (const mode of ['raw-only', 'crud-only']) {
  test(`SQLite Linux x64 ${mode} 格复用 Windows 工程合同并生成 Linux Native 命令`, async () => {
    const { loadMatrix, ciMatrix, selectCase, buildCommands } = await api();
    const matrix = loadMatrix();
    const suffix = mode === 'raw-only' ? 'raw' : 'crud';
    const cell = matrix.cases.find(candidate => candidate.id === `sqlite-linux-x64-${suffix}`);
    assert.ok(cell, `SQLite Linux x64 ${mode} 格必须存在`);
    const windowsCell = matrix.cases.find(candidate => candidate.id === `sqlite-win-x64-${suffix}`);
    assert.ok(windowsCell, `SQLite Windows x64 ${mode} 格必须存在`);
    assert.deepEqual({
      provider: cell.provider,
      rid: cell.rid,
      tfm: cell.tfm,
      runner: cell.runner,
      mode: cell.mode,
      status: cell.status,
      project: cell.project,
      successMarker: cell.successMarker
    }, {
      provider: 'sqlite',
      rid: 'linux-x64',
      tfm: 'net10.0',
      runner: 'ubuntu-22.04',
      mode,
      status: 'enabled',
      project: windowsCell.project,
      successMarker: windowsCell.successMarker
    });
    assert.deepEqual(cell.coverage, windowsCell.coverage);
    assert.deepEqual(cell.notCovered, windowsCell.notCovered);
    assert.ok(ciMatrix(matrix).include.some(candidate => candidate.id === cell.id && candidate.runner === 'ubuntu-22.04'));
    assert.equal(selectCase(matrix, cell.id, 'linux', 'x64'), cell);
    assert.throws(() => selectCase(matrix, cell.id, 'win32', 'x64'), /host/);

    const publishDirectory = path.join(root, `output/sql-aot/linux ${mode}`);
    const commands = buildCommands(cell, { root, publishDirectory });
    const runtimeOption = commands.publish.args.indexOf('-r');
    assert.notEqual(runtimeOption, -1);
    assert.equal(commands.publish.args[runtimeOption + 1], 'linux-x64');
    assert.equal(commands.run.file, path.join(publishDirectory, path.basename(windowsCell.project, '.csproj')));
    assert.equal(commands.run.file.endsWith('.exe'), false);
  });
}

test('自动查询模板消费工程进入统一 CI 矩阵并声明准确覆盖边界', async () => {
  const { loadMatrix, ciMatrix } = await api();
  const matrix = loadMatrix();
  const cell = matrix.cases.find(candidate => candidate.id === 'sqlite-win-x64-generated-query');
  assert.ok(cell, 'GeneratedSmoke 必须有独立矩阵格');
  assert.deepEqual({ ...cell }, {
    id: 'sqlite-win-x64-generated-query', provider: 'sqlite', rid: 'win-x64', tfm: 'net10.0',
    runner: 'windows-2022', mode: 'generated-query', status: 'enabled',
    project: 'framework/tests/Bing.Data.Sql.Aot.GeneratedSmoke/Bing.Data.Sql.Aot.GeneratedSmoke.csproj',
    successMarker: 'GENERATED_SQL_SMOKE_PASS', requiredMarkers: ['AUTOMATIC_QUERY_TEMPLATE_NATIVE_PASS'],
    coverage: ['automatic-whereif-query-template', 'same-shape-sql-reuse', 'current-value-rebinding',
      'disabled-filter', 'collection-shapes-0-1-2', 'generated-tosql-sync-async'],
    notCovered: ['all-lambda-syntax', 'all-query-template-shapes', 'all-provider-native', 'production-capacity']
  });
  assert.ok(ciMatrix(matrix).include.some(candidate => candidate.id === cell.id && candidate.runner === cell.runner));
});

test('SQLite Linux x64 generated-query 格复用完整 Windows 合同并生成 Linux Native 命令', async () => {
  const { loadMatrix, ciMatrix, selectCase, buildCommands } = await api();
  const matrix = loadMatrix();
  const cell = matrix.cases.find(candidate => candidate.id === 'sqlite-linux-x64-generated-query');
  assert.ok(cell, 'SQLite Linux x64 generated-query 格必须存在');
  const windowsCell = matrix.cases.find(candidate => candidate.id === 'sqlite-win-x64-generated-query');
  assert.ok(windowsCell, 'SQLite Windows x64 generated-query 格必须存在');
  assert.deepEqual({
    provider: cell.provider,
    rid: cell.rid,
    tfm: cell.tfm,
    runner: cell.runner,
    mode: cell.mode,
    status: cell.status,
    project: cell.project,
    successMarker: cell.successMarker,
    requiredMarkers: cell.requiredMarkers
  }, {
    provider: 'sqlite',
    rid: 'linux-x64',
    tfm: 'net10.0',
    runner: 'ubuntu-22.04',
    mode: 'generated-query',
    status: 'enabled',
    project: windowsCell.project,
    successMarker: windowsCell.successMarker,
    requiredMarkers: windowsCell.requiredMarkers
  });
  assert.deepEqual(cell.coverage, windowsCell.coverage);
  assert.deepEqual(cell.notCovered, windowsCell.notCovered);
  assert.ok(ciMatrix(matrix).include.some(candidate => candidate.id === cell.id && candidate.runner === 'ubuntu-22.04'));
  assert.equal(selectCase(matrix, cell.id, 'linux', 'x64'), cell);
  assert.throws(() => selectCase(matrix, cell.id, 'win32', 'x64'), /host/);

  const publishDirectory = path.join(root, 'output/sql-aot/linux generated-query');
  const commands = buildCommands(cell, { root, publishDirectory });
  const runtimeOption = commands.publish.args.indexOf('-r');
  assert.notEqual(runtimeOption, -1);
  assert.equal(commands.publish.args[runtimeOption + 1], 'linux-x64');
  assert.equal(commands.run.file, path.join(publishDirectory, path.basename(windowsCell.project, '.csproj')));
  assert.equal(commands.run.file.endsWith('.exe'), false);
});

for (const scenario of [
  { name: '完整标记齐全', output: 'AUTOMATIC_QUERY_TEMPLATE_NATIVE_PASS\r\nGENERATED_SQL_SMOKE_PASS', status: 'PASS' },
  { name: '缺少模板标记', output: 'GENERATED_SQL_SMOKE_PASS', status: 'FAIL' },
  { name: '模板标记只有子串', output: 'prefix-AUTOMATIC_QUERY_TEMPLATE_NATIVE_PASS\nGENERATED_SQL_SMOKE_PASS', status: 'FAIL' },
  { name: '缺少整体标记', output: 'AUTOMATIC_QUERY_TEMPLATE_NATIVE_PASS', status: 'FAIL' }
]) {
  test('额外能力标记合同：' + scenario.name, async t => {
    const { runCase } = await api();
    const cell = { ...nativeCase, successMarker: 'GENERATED_SQL_SMOKE_PASS', requiredMarkers: ['AUTOMATIC_QUERY_TEMPLATE_NATIVE_PASS'] };
    const result = runCase(cell, { root, output: fixture(t) }, fakeExecutor({ marker: scenario.output }).execute);
    assert.equal(result.status, scenario.status);
    assert.deepEqual(result.successMarkers, ['GENERATED_SQL_SMOKE_PASS', 'AUTOMATIC_QUERY_TEMPLATE_NATIVE_PASS']);
    assert.deepEqual(JSON.parse(fs.readFileSync(path.join(result.directory, 'result.json'), 'utf8')), result);
    if (scenario.status === 'FAIL') assert.match(result.error, /Native success marker missing:/u);
  });
}

test('当前主机 SQLite CRUD 运行器逐项要求十三个精确能力标记', async t => {
  const { loadMatrix, runCase } = await api();
  const prefix = { win32: 'win', linux: 'linux', darwin: 'osx' }[process.platform];
  if (!prefix) return t.skip('当前测试主机不在 SQLite Native 矩阵中');
  const rid = `${prefix}-${process.arch}`;
  const matrixCell = loadMatrix().cases.find(candidate => candidate.id === `sqlite-${rid}-crud`);
  if (!matrixCell) return t.skip('当前测试主机没有 SQLite CRUD 矩阵格');

  const cell = matrixCell;
  assert.deepEqual(cell.requiredMarkers, sqliteCrudRequiredMarkers);
  const markers = [cell.successMarker, ...cell.requiredMarkers];
  const run = marker => runCase(cell, { root, output: fixture(t) }, fakeExecutor({ marker }).execute);
  const complete = run(markers.join('\n'));
  assert.equal(complete.status, 'PASS', complete.error);

  for (const missing of sqliteCrudRequiredMarkers) {
    const result = run(markers.filter(marker => marker !== missing).join('\n'));
    assert.equal(result.status, 'FAIL', `缺少 ${missing} 时必须失败`);
    assert.match(result.error, new RegExp(missing, 'u'));
  }

  const prefixed = run([`prefix-${cell.successMarker}`, ...cell.requiredMarkers].join('\n'));
  assert.equal(prefixed.status, 'FAIL', '标记子串不能代替完整成功标记');
  assert.match(prefixed.error, /Native success marker missing:/u);
});

test('计划格、未知格和主机不匹配不能开始发布', async () => {
  const { loadMatrix, selectCase } = await api();
  const matrix = loadMatrix();
  const planned = matrix.cases.find(cell => cell.status === 'planned');
  assert.throws(() => selectCase(matrix, planned.id, 'win32', 'x64'), /planned/);
  assert.throws(() => selectCase(matrix, 'absent', 'win32', 'x64'), /Unknown/);
  assert.throws(() => selectCase(matrix, nativeCase.id, 'linux', 'x64'), /host/);
  assert.throws(() => selectCase(matrix, nativeCase.id, 'win32', 'arm64'), /host/);
  assert.equal(selectCase(matrix, nativeCase.id, 'win32', 'x64').id, nativeCase.id);
});

test('命令显式选择 Native、严格模式及当前源码生成器，参数使用数组传递', async () => {
  const { buildCommands } = await api();
  const commands = buildCommands(nativeCase, { root, publishDirectory: path.join(root, 'output/space name'), configuration: 'Round120', noRestore: true });
  assert.equal(commands.publish.file, 'dotnet');
  // PublishAot 由入口工程声明，不能作为全局属性传播给 netstandard 类库。
  assert.ok(commands.publish.args.includes('-p:BingSqlAotStrict=true'));
  assert.ok(commands.publish.args.includes('-p:BingSqlGeneratorPackageVersion='));
  assert.ok(commands.publish.args.includes('--no-restore'));
  assert.ok(commands.publish.args.includes('Round120'));
  assert.equal(commands.publish.args[commands.publish.args.indexOf('-o') + 1], path.join(root, 'output/space name'));
  assert.equal(commands.run.file, path.join(root, 'output/space name/Bing.Data.Sql.Aot.RawOnly.exe'));
});

test('构建参数隔离 artifacts 与 props 路径并保留空格', async t => {
  const { buildCommands } = await api();
  const rootWithSpaces = path.join(fixture(t), 'workspace with spaces');
  const publishDirectories = [
    path.join(rootWithSpaces, 'output', 'variant one', 'run one'),
    path.join(rootWithSpaces, 'output', 'variant two', 'run two')
  ];
  const commands = publishDirectories.map(publishDirectory =>
    buildCommands(nativeCase, { root: rootWithSpaces, publishDirectory }));
  const artifactsPaths = commands.map((command, index) => {
    const args = command.publish.args;
    const option = args.indexOf('--artifacts-path');
    assert.notEqual(option, -1, 'publish 必须设置 --artifacts-path');
    assert.equal(args[option + 1], path.join(path.dirname(path.resolve(publishDirectories[index])), 'build'));
    assert.ok(args.includes(`-p:CustomBeforeMicrosoftCommonTargets=${path.resolve(rootWithSpaces, 'eng/sql-aot/isolated-output.props')}`));
    return args[option + 1];
  });
  assert.notEqual(artifactsPaths[0], artifactsPaths[1]);
});

test('显式 --artifacts-path 接受含空格的绝对目录且保留 --no-restore', async t => {
  const { buildCommands } = await api();
  const rootWithSpaces = path.join(fixture(t), 'workspace with spaces');
  const artifactsPath = path.resolve(rootWithSpaces, 'artifacts with spaces', 'build output');
  const commands = buildCommands(nativeCase, {
    root: rootWithSpaces,
    publishDirectory: path.join(rootWithSpaces, 'output', 'publish'),
    artifactsPath,
    noRestore: true
  });
  const args = commands.publish.args;
  const option = args.indexOf('--artifacts-path');
  assert.notEqual(option, -1, 'publish 必须接受显式 --artifacts-path');
  assert.equal(args[option + 1], artifactsPath);
  assert.ok(args.includes('--no-restore'));
});

test('全部 Native 格展开逐成员裁剪和 AOT 告警，保留严格模式和项目错误门禁', async () => {
  const { buildCommands, loadMatrix } = await api();
  for (const cell of loadMatrix().cases.filter(candidate => candidate.status === 'enabled')) {
    const { publish } = buildCommands(cell, { root, publishDirectory: path.join(root, 'output/diagnostics') });
    assert.ok(publish.args.includes('-p:TrimmerSingleWarn=false'), cell.id);
    assert.ok(publish.args.includes('-p:BingSqlAotStrict=true'), cell.id);
    assert.equal(publish.args.some(value => /^-p:(NoWarn|WarningsAsErrors|SuppressTrimAnalysisWarnings)=/u.test(value)), false, cell.id);
  }
});

test('Native 成功仍原样保留逐成员告警，失败日志同样不得丢失', async t => {
  const { runCase } = await api();
  const warning = "ILC : Trim analysis warning IL2026: Example.Json.Read(): RequiresUnreferencedCode.\nILC : AOT analysis warning IL3050: Example.DI.Build(): RequiresDynamicCode.\n";
  for (const publishCode of [0, 1]) {
    const fake = fakeExecutor({ publishCode });
    const result = runCase(nativeCase, { root, output: fixture(t) }, (command, log) => {
      const execution = fake.execute(command, log);
      if (command.args[0] === 'publish') fs.writeFileSync(log, warning, 'utf8');
      return execution;
    });
    assert.equal(result.status, publishCode === 0 ? 'PASS' : 'FAIL');
    const publish = result.steps.find(step => step.name === 'publish');
    assert.equal(fs.readFileSync(publish.log, 'utf8'), warning);
    assert.deepEqual(JSON.parse(fs.readFileSync(path.join(result.directory, 'result.json'), 'utf8')), result);
  }
});

test('发布后执行成功且标记匹配才记录 PASS，并保存二进制身份', async t => {
  const { runCase } = await api();
  const directory = fixture(t);
  const fake = fakeExecutor();
  const result = runCase(nativeCase, { root, output: directory, configuration: 'Round120' }, fake.execute);
  assert.equal(result.status, 'PASS');
  assert.equal(result.binary.sha256.length, 64);
  assert.equal(result.binary.bytes, nativeHeader('win-x64').length);
  assert.equal(result.environment.dotnetSdk, '10.0.100');
  assert.equal(result.steps.length, 3);
  assert.ok(fs.existsSync(path.join(result.directory, 'result.json')));
  assert.deepEqual(JSON.parse(fs.readFileSync(path.join(result.directory, 'result.json'), 'utf8')), result);
});

for (const rid of ['win-x64', 'win-arm64', 'linux-x64', 'osx-arm64']) {
  test(`Native 运行器从实际产物记录 ${rid}`, async t => {
    const { runCase } = await api();
    const result = runCase({ ...nativeCase, rid }, { root, output: fixture(t) }, fakeExecutor().execute);

    assert.equal(result.status, 'PASS', result.error);
    assert.equal(result.binary.rid, rid);
    assert.equal(result.steps.at(-1).name, 'run');
  });
}

for (const [name, binary, actual] of [
  ['x64 PE', nativeHeader('win-x64'), 'win-x64'],
  ['ARM64 Mach-O', nativeHeader('osx-arm64'), 'osx-arm64'],
  ['截断的 PE', nativeHeader('win-arm64').subarray(0, 100), 'unknown']
]) {
  test(`Native 运行器拒绝在 Windows ARM64 格执行${name}`, async t => {
    const { runCase } = await api();
    const cell = { ...nativeCase, id: 'sqlite-win-arm64-raw', rid: 'win-arm64' };
    const fake = fakeExecutor({ binary });
    const result = runCase(cell, { root, output: fixture(t) }, fake.execute);

    assert.equal(result.status, 'FAIL');
    assert.equal(result.error, `Native binary RID mismatch: expected win-arm64; actual ${actual}`);
    assert.deepEqual(result.steps.map(step => step.name), ['sdk', 'publish']);
    const receipt = JSON.parse(fs.readFileSync(path.join(result.directory, 'result.json'), 'utf8'));
    assert.equal(receipt.error, result.error);
  });
}

test('发布失败保留失败日志且不执行旧二进制', async t => {
  const { runCase } = await api();
  const fake = fakeExecutor({ publishCode: 1 });
  const result = runCase(nativeCase, { root, output: fixture(t) }, fake.execute);
  assert.equal(result.status, 'FAIL');
  assert.equal(result.steps.at(-1).code, 1);
  assert.equal(fake.calls.some(command => command.file.endsWith('.exe')), false);
  assert.equal(fs.readFileSync(result.steps.at(-1).log, 'utf8'), 'publish log\n');
});

for (const scenario of [
  { name: '运行失败', options: { runCode: 2 } },
  { name: '缺少成功标记', options: { marker: 'unrelated' } },
  { name: '成功标记只是子串', options: { marker: 'prefix-' + nativeCase.successMarker } },
  { name: '未产出可执行文件', options: { binary: false } }
]) {
  test(scenario.name + '不能记录通过，失败也写入收据', async t => {
    const { runCase } = await api();
    const fake = fakeExecutor(scenario.options);
    const result = runCase(nativeCase, { root, output: fixture(t) }, fake.execute);
    assert.equal(result.status, 'FAIL');
    assert.ok(result.error.length > 0);
    assert.equal(JSON.parse(fs.readFileSync(path.join(result.directory, 'result.json'), 'utf8')).status, 'FAIL');
  });
}

test('重复运行使用独立产物目录，避免读取上一次成功输出', async t => {
  const { runCase } = await api();
  const output = fixture(t);
  const first = runCase(nativeCase, { root, output }, fakeExecutor().execute);
  const second = runCase(nativeCase, { root, output }, fakeExecutor({ binary: false }).execute);
  assert.notEqual(first.directory, second.directory);
  assert.equal(first.status, 'PASS');
  assert.equal(second.status, 'FAIL');
});

for (const project of ['RawOnly', 'CrudOnly', 'PackageModels']) {
  test(project + ' 默认使用源码 Generator，显式版本保留包消费路径', () => {
    const file = path.join(root, `framework/tests/Bing.Data.Sql.Aot.${project}/Bing.Data.Sql.Aot.${project}.csproj`);
    const source = fs.readFileSync(file, 'utf8');
    assert.match(source, /ProjectReference[^>]+Bing.Data.Sql.Generators.csproj[^>]+OutputItemType="Analyzer"[^>]+ReferenceOutputAssembly="false"[^>]+BingSqlGeneratorPackageVersion/u);
    assert.match(source, /PackageReference Include="Bing.Data.Sql.Generators" Version="\$\(BingSqlGeneratorPackageVersion\)"/u);
    assert.doesNotMatch(source, /Version="0.1.0"/u);
    assert.match(source, /Import[^>]+Bing.Data.Sql.Generators.props[^>]+BingSqlGeneratorPackageVersion/u);
    if (project !== 'PackageModels') assert.doesNotMatch(source, /InterceptorsNamespaces/u);
  });
}

test('CLI 的 CI 矩阵可解析，非法参数返回非零', async () => {
  const { ciMatrix, loadMatrix } = await api();
  const script = path.join(root, 'eng/sql-aot/verify.mjs');
  const result = spawnSync(process.execPath, [script, '--ci-matrix'], { encoding: 'utf8' });
  assert.equal(result.status, 0);
  assert.deepEqual(JSON.parse(result.stdout), ciMatrix(loadMatrix()));
  const invalid = spawnSync(process.execPath, [script, '--cas', nativeCase.id], { encoding: 'utf8' });
  assert.notEqual(invalid.status, 0);
  assert.match(invalid.stderr, /Unknown option/);
});

test('CLI 识别显式 artifacts 路径后仍在发布前拒绝未知格', t => {
  const result = spawnSync(process.execPath, [path.join(root, 'eng/sql-aot/verify.mjs'),
    '--case', 'missing-case', '--artifacts-path', path.join(fixture(t), 'build with spaces'), '--no-restore'],
  { encoding: 'utf8' });
  assert.equal(result.status, 1);
  assert.match(result.stderr, /Unknown matrix case: missing-case/u);
});

test('PublishAot 只在消费工程启用，不能强制所有依赖框架执行 Native 编译', async () => {
  const { buildCommands, loadMatrix } = await api();
  const commands = buildCommands(nativeCase, { root, publishDirectory: path.join(root, 'output/native') });
  assert.equal(commands.publish.args.some(value => value.startsWith('-p:PublishAot=')), false);
  for (const cell of loadMatrix().cases.filter(cell => cell.status === 'enabled'))
    assert.match(fs.readFileSync(path.join(root, cell.project), 'utf8'), /<PublishAot[^>]*>true<\/PublishAot>/u);
});

test('publish 成功但产出托管入口不能标记为 Native 通过', async t => {
  const { runCase } = await api();
  const fake = fakeExecutor();
  const result = runCase(nativeCase, { root, output: fixture(t) }, (command, log) => {
    const execution = fake.execute(command, log);
    if (command.args[0] === 'publish') {
      const directory = command.args[command.args.indexOf('-o') + 1];
      fs.writeFileSync(path.join(directory, 'Bing.Data.Sql.Aot.RawOnly.dll'), 'managed fixture', 'utf8');
    }
    return execution;
  });
  assert.equal(result.status, 'FAIL');
  assert.match(result.error, /managed/i);
  assert.equal(fake.calls.some(command => command.file.endsWith('.exe')), false);
});

test('SQL Server Windows Native 格进入 windows-2022 CI 并验收完整生成读写', async () => {
  const { loadMatrix, ciMatrix, selectCase, buildCommands } = await api();
  const matrix = loadMatrix();
  const cell = selectCase(matrix, 'sqlserver-win-x64', 'win32', 'x64');
  assert.equal(cell.provider, 'sqlserver');
  assert.equal(cell.mode, 'generated-crud-query');
  assert.equal(cell.tfm, 'net10.0');
  assert.equal(cell.ci, true);
  assert.equal(cell.successMarker, 'SQLSERVER_SHARED_ROLLBACK_PASS');
  assert.deepEqual(cell.requiredMarkers, ['SQLSERVER_GENERATED_CRUD_PASS', 'SQLSERVER_IDENTITY_OUTPUT_PASS',
    'SQLSERVER_EXTENDED_COLUMNS_NATIVE_PASS', 'SQLSERVER_OUTPUT_PROJECTION_NATIVE_PASS']);
  assert.deepEqual(ciMatrix(matrix).include.find(candidate => candidate.id === cell.id), { id: cell.id, runner: 'windows-2022' });
  assert.throws(() => selectCase(matrix, cell.id, 'linux', 'x64'), /host/);
  assert.ok(fs.existsSync(path.join(root, cell.project)));
  const commands = buildCommands(cell, { root, publishDirectory: path.join(root, 'output/sql-aot/sqlserver') });
  assert.equal(path.basename(commands.run.file), 'Bing.Data.Sql.Aot.SqlServer.exe');
});

test('SQL Server 两个 Native 格同时验收扩展列和 OUTPUT DTO', async () => {
  const { loadMatrix, selectCase } = await api();
  const matrix = loadMatrix();
  for (const [id, platform] of [['sqlserver-win-x64', 'win32'], ['sqlserver-linux-x64', 'linux']]) {
    const cell = selectCase(matrix, id, platform, 'x64');
    assert.ok(cell.requiredMarkers.includes('SQLSERVER_EXTENDED_COLUMNS_NATIVE_PASS'), id);
    assert.ok(cell.requiredMarkers.includes('SQLSERVER_OUTPUT_PROJECTION_NATIVE_PASS'), id);
    assert.ok(cell.coverage.includes('generated-max-xml-variant-columns'), id);
    assert.ok(cell.coverage.includes('static-output-dto-and-rollback'), id);
  }
});

test('Oracle Windows Native 格进入 Windows 2025 自动 CI 矩阵', async () => {
  const { loadMatrix, ciMatrix, selectCase, buildCommands } = await api();
  const matrix = loadMatrix();
  const cell = selectCase(matrix, 'oracle-win-x64', 'win32', 'x64');
  assert.equal(cell.provider, 'oracle');
  assert.equal(cell.project, 'framework/tests/Bing.Data.Sql.Aot.Oracle/Bing.Data.Sql.Aot.Oracle.csproj');
  assert.equal(cell.rid, 'win-x64');
  assert.equal(cell.tfm, 'net10.0');
  assert.equal(cell.mode, 'generated-crud-query');
  assert.equal(cell.runner, 'windows-2025');
  assert.equal(cell.ci, true);
  assert.equal(cell.successMarker, 'ORACLE_GENERATED_CRUD_PASS');
  assert.deepEqual(cell.requiredMarkers, ['ORACLE_SHARED_ROLLBACK_PASS', 'ORACLE_IDENTITY_RETURNING_PASS',
    'ORACLE_DML_RETURNING_INTO_PASS', 'ORACLE_BUILDER_RETURNING_PASS', 'ORACLE_BUILDER_BULK_RETURNING_PASS']);
  assert.ok(fs.existsSync(path.join(root, cell.project)));
  assert.deepEqual(ciMatrix(matrix).include.find(candidate => candidate.id === cell.id), {
    id: cell.id, runner: 'windows-2025'
  });
  assert.throws(() => selectCase(matrix, cell.id, 'linux', 'x64'), /host/);
  const commands = buildCommands(cell, { root, publishDirectory: path.join(root, 'output/sql-aot/oracle') });
  assert.equal(commands.publish.args[commands.publish.args.indexOf('-r') + 1], 'win-x64');
  assert.equal(path.basename(commands.run.file), 'Bing.Data.Sql.Aot.Oracle.exe');
});

test('Oracle Linux Native 格复用 Windows 消费工程并进入 Ubuntu CI', async () => {
  const { loadMatrix, ciMatrix, selectCase, buildCommands } = await api();
  const matrix = loadMatrix();
  const cell = selectCase(matrix, 'oracle-linux-x64', 'linux', 'x64');
  assert.equal(cell.provider, 'oracle');
  assert.equal(cell.project, 'framework/tests/Bing.Data.Sql.Aot.Oracle/Bing.Data.Sql.Aot.Oracle.csproj');
  assert.equal(cell.rid, 'linux-x64');
  assert.equal(cell.runner, 'ubuntu-22.04');
  assert.equal(cell.tfm, 'net10.0');
  assert.equal(cell.mode, 'generated-crud-query');
  assert.equal(cell.ci, true);
  assert.equal(cell.successMarker, 'ORACLE_GENERATED_CRUD_PASS');
  assert.deepEqual(cell.requiredMarkers, ['ORACLE_SHARED_ROLLBACK_PASS', 'ORACLE_IDENTITY_RETURNING_PASS',
    'ORACLE_DML_RETURNING_INTO_PASS', 'ORACLE_BUILDER_RETURNING_PASS', 'ORACLE_BUILDER_BULK_RETURNING_PASS']);
  assert.ok(fs.existsSync(path.join(root, cell.project)));
  assert.deepEqual(ciMatrix(matrix).include.find(candidate => candidate.id === cell.id), { id: cell.id, runner: 'ubuntu-22.04' });
  assert.throws(() => selectCase(matrix, cell.id, 'win32', 'x64'), /host/);
  const commands = buildCommands(cell, { root, publishDirectory: path.join(root, 'output/sql-aot/oracle') });
  assert.equal(commands.publish.args[commands.publish.args.indexOf('-r') + 1], 'linux-x64');
  assert.equal(path.basename(commands.run.file), 'Bing.Data.Sql.Aot.Oracle');
});

test('Oracle 两个 Native 格都要求 Builder RETURNING 投影通过', async () => {
  const { loadMatrix } = await api();
  const { cases } = loadMatrix();
  for (const id of ['oracle-win-x64', 'oracle-linux-x64']) {
    const cell = cases.find(candidate => candidate.id === id);
    assert.ok(cell.requiredMarkers.includes('ORACLE_BUILDER_RETURNING_PASS'), id);
    assert.ok(cell.requiredMarkers.includes('ORACLE_BUILDER_BULK_RETURNING_PASS'), id);
    assert.ok(cell.coverage.includes('builder-dml-returning-static-projection'), id);
    assert.ok(cell.coverage.includes('builder-bulk-update-delete-returning-static-projection'), id);
    assert.ok(cell.notCovered.includes('oracle-bulk-returning-extended-types'), id);
    assert.equal(cell.notCovered.includes('oracle-multi-row-returning-into'), false, id);
  }
});

test('Oracle Linux 与 Windows CI 使用共享隔离库并在 always 阶段清理', () => {
  const workflow = fs.readFileSync(path.join(root, '.github/workflows/sql-aot.yml'), 'utf8');
  const steps = workflow.split(/(?=^      - )/mu);
  const wsl = steps.findIndex(step => step.includes('name: Prepare WSL2 Docker for Oracle'));
  const start = steps.findIndex(step => step.includes('Invoke-LinuxOracle.sh start'));
  const run = steps.findIndex(step => step.includes('name: Publish and execute Native case'));
  const stop = steps.findIndex(step => step.includes('Invoke-LinuxOracle.sh stop'));
  assert.ok(wsl >= 0 && wsl < start && start < run && stop > run);
  assert.match(steps[wsl], /if: \$\{\{ matrix\.id == 'oracle-win-x64' \}\}/u);
  assert.match(steps[wsl], /uses: Particular\/setup-wsl-action@19b7628eaf38f79a6822e185735f61d99badcfa2 # v1\.3\.0/u);
  assert.match(steps[wsl], /enable-cache: 'false'/u);
  assert.match(steps[start], /matrix\.id == 'oracle-linux-x64' \|\| matrix\.id == 'oracle-win-x64'/u);
  assert.match(steps[stop], /always\(\).*matrix\.id == 'oracle-linux-x64' \|\| matrix\.id == 'oracle-win-x64'/u);
  assert.match(workflow, /output\/sql-aot-oracle\/\*\.log/u);
});

test('Windows SQL Server CI 验证具名 LocalDB 生命周期并保留实例收据', () => {
  const workflow = fs.readFileSync(path.join(root, '.github/workflows/sql-aot.yml'), 'utf8');
  const steps = workflow.split(/(?=^      - )/mu);
  const start = steps.findIndex(step => step.includes('name: Start isolated Windows SQL Server test database'));
  const run = steps.findIndex(step => step.includes('name: Publish and execute Native case'));
  const stop = steps.findIndex(step => step.includes('name: Stop isolated Windows SQL Server test database'));
  assert.ok(start >= 0 && start < run && stop > run);
  assert.match(steps[start], /if:.*matrix\.id == 'sqlserver-win-x64'/u);
  assert.match(steps[start], /shell: pwsh/u);
  assert.match(steps[start], /Invoke-WindowsSqlServer\.ps1 -Action Start/u);
  assert.match(steps[stop], /if:.*always\(\).*matrix\.id == 'sqlserver-win-x64'/u);
  assert.match(steps[stop], /Invoke-WindowsSqlServer\.ps1 -Action Stop/u);
  assert.match(workflow, /run:.*tests\/WindowsSqlServer\.Tests\.ps1/u);
  assert.match(workflow, /output\/sql-aot-sqlserver\/instance\.txt/u);
});

test('SQL Server Linux CI 准备独立测试数据库，执行同一消费合同并清理', async () => {
  const { loadMatrix, ciMatrix, selectCase } = await api();
  const matrix = loadMatrix();
  const linux = selectCase(matrix, 'sqlserver-linux-x64', 'linux', 'x64');
  const windows = selectCase(matrix, 'sqlserver-win-x64', 'win32', 'x64');
  assert.deepEqual(ciMatrix(matrix).include.find(cell => cell.id === linux.id), { id: linux.id, runner: 'ubuntu-22.04' });
  for (const key of ['project', 'mode', 'tfm', 'successMarker', 'requiredMarkers', 'coverage', 'notCovered'])
    assert.deepEqual(linux[key], windows[key], key);
  const workflow = fs.readFileSync(path.join(root, '.github/workflows/sql-aot.yml'), 'utf8');
  const steps = workflow.split(/(?=^      - )/mu);
  const start = steps.findIndex(step => step.includes('name: Start isolated SQL Server test database'));
  const run = steps.findIndex(step => step.includes('name: Publish and execute Native case'));
  const stop = steps.findIndex(step => step.includes('name: Remove isolated SQL Server test database'));
  assert.ok(start >= 0 && start < run && stop > run);
  assert.match(steps[start], /matrix\.id == 'sqlserver-linux-x64'/u);
  assert.match(steps[start], /127\.0\.0\.1::1433/u);
  assert.match(steps[start], /CREATE DATABASE bing_sql_aot_test/u);
  assert.match(steps[start], /RUN_SQLSERVER_INTEGRATION_TESTS=true/u);
  assert.match(steps[start], /ConnectionStrings__SqlServerConnection=/u);
  assert.match(steps[stop], /always\(\) && matrix\.id == 'sqlserver-linux-x64'/u);
  assert.match(steps[stop], /docker stop "\$id"/u);
  assert.match(workflow, /output\/sql-aot-sqlserver\/\*\.log/u);
});
