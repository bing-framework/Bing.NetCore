import { createHash } from 'node:crypto';
import {
  existsSync, mkdirSync, readFileSync, readdirSync, realpathSync, statSync, writeFileSync
} from 'node:fs';
import os from 'node:os';
import { dirname, extname, isAbsolute, join, relative, resolve, sep } from 'node:path';
import { spawnSync } from 'node:child_process';

const WARMUPS = { builder: 2, width: 32 };
const SAMPLES = 5;
const PROCESS_TIMEOUT_MS = 5 * 60 * 1000;
const MAX_OUTPUT_BYTES = 4 * 1024 * 1024;
const MAX_IDENTITY_FILES = 4096;
const MAX_IDENTITY_BYTES = 1024 * 1024 * 1024;
const ENVIRONMENT_KEYS = [
  'DOTNET_ROOT', 'DOTNET_ROLL_FORWARD', 'DOTNET_ROLL_FORWARD_TO_PRERELEASE',
  'DOTNET_MULTILEVEL_LOOKUP', 'DOTNET_PROCESSOR_COUNT', 'DOTNET_TieredCompilation',
  'COMPlus_TieredCompilation', 'COMPlus_TieredPGO', 'COMPlus_ReadyToRun', 'PROCESSOR_COUNT'
];
const WIDTH_GROUPS = ['Compatible', 'GeneratedOnly'].flatMap(mode =>
  [1, 32, 256].flatMap(rows => [2, 8, 32].map(columns => [mode, rows, columns, 'NoTransaction'])));
const BUILDER_GROUPS = [
  ...['simple', 'cte-union-depth6', 'dynamic-filter-page', 'cte-wide-1', 'cte-wide-8', 'cte-wide-32', 'cte-wide-128']
    .flatMap(shape => [
      [shape, 'draft-mutation', 'full-tree-clone'],
      [shape, 'draft-mutation', 'clause-checkpoint'],
      [shape, 'repeat-render', 'current']
    ])
];
const PROBES = {
  builder: {
    flag: '--builder-state-probe',
    workload: 'SqlBuilderStateProbe-v1',
    expectedGroups: BUILDER_GROUPS,
    warmupField: 'warmups',
    sampleField: 'samples',
    validateHeader(data) {
      return data.batch === 32 && data.renderIterations === 100;
    },
    key(row) { return [row.shape, row.operation, row.mode]; },
    signature(row) { return [row.shape, row.operation, row.mode, row.operations, row.sqlCharacters ?? null, row.parameterCount ?? null]; },
    operationCount(row) { return row.operations; },
    expectedOperations(row, data) { return row.operation === 'repeat-render' ? data.renderIterations : data.batch; }
  },
  width: {
    flag: '--generated-width-probe',
    workload: 'SqlGeneratedWidthProbe-v1',
    expectedGroups: WIDTH_GROUPS,
    warmupField: 'warmup',
    sampleField: 'samples',
    validateHeader(data) { return data.iterations === 100 && data.groups === 18 && data.sampleRecords === 90; },
    key(row) { return [row.mode, row.rows, row.columns, row.operation]; },
    signature(row) { return [row.mode, row.rows, row.columns, row.operation, row.iterations, row.rowsPerOperation, row.lastColumnValue]; },
    operationCount(row) { return row.iterations; },
    expectedOperations(row, data) { return data.iterations; }
  }
};

function parseArgs(args) {
  const options = {};
  for (let index = 0; index < args.length; index++) {
    const name = args[index];
    if (name === '--help' || name === '-h') return { help: true };
    if (!['--before-dir', '--after-dir', '--output-dir', '--probe-relative-path', '--dotnet'].includes(name))
      throw new Error(`Unknown option: ${name}`);
    if (!args[index + 1] || args[index + 1].startsWith('--')) throw new Error(`${name} requires a value`);
    options[name.slice(2).replaceAll('-', '_')] = args[++index];
  }
  for (const key of ['before_dir', 'after_dir', 'output_dir'])
    if (!options[key]) throw new Error(`Missing --${key.replaceAll('_', '-')}`);
  return options;
}

function usage() {
  process.stdout.write('Usage: node paired-probe.mjs --before-dir <dir> --after-dir <dir> --output-dir <new-dir> [--probe-relative-path Bing.Data.Sql.Benchmarks.dll] [--dotnet <host>]\n');
}

function sha256(file) {
  return createHash('sha256').update(readFileSync(file)).digest('hex');
}

function within(child, parent) {
  const pathFromParent = relative(parent, child);
  return pathFromParent === '' || (pathFromParent !== '..' && !pathFromParent.startsWith(`..${sep}`) && !isAbsolute(pathFromParent));
}

function outputDirectory(path) {
  const absolute = resolve(path);
  if (existsSync(absolute)) {
    if (!statSync(absolute).isDirectory() || readdirSync(absolute).length !== 0)
      throw new Error(`Output directory must exist and be empty: ${absolute}`);
    return realpathSync(absolute);
  }
  const parent = dirname(absolute);
  if (!existsSync(parent)) throw new Error(`Output parent directory must exist: ${parent}`);
  return join(realpathSync(parent), absolute.slice(parent.length + 1));
}

function findExecutable(name) {
  if (isAbsolute(name) || name.includes('/') || name.includes('\\')) {
    const file = resolve(name);
    return existsSync(file) && statSync(file).isFile() ? realpathSync(file) : null;
  }
  const locator = process.platform === 'win32' ? 'where.exe' : 'which';
  const result = spawnSync(locator, [name], { encoding: 'utf8', windowsHide: true, timeout: 10000 });
  const file = result.status === 0 ? result.stdout.split(/\r?\n/).find(line => line.trim())?.trim() : null;
  return file && existsSync(file) ? realpathSync(file) : null;
}

function candidateIdentity(directory, probeRelativePath) {
  const probe = resolve(directory, probeRelativePath);
  if (!within(probe, directory) || !existsSync(probe) || !statSync(probe).isFile())
    throw new Error(`Benchmark assembly not found inside candidate directory: ${probe}`);
  const files = [];
  let bytes = 0;
  function visit(folder, depth) {
    if (depth > 8) throw new Error(`Candidate identity scan exceeds depth 8: ${directory}`);
    for (const item of readdirSync(folder, { withFileTypes: true })) {
      const file = join(folder, item.name);
      if (item.isSymbolicLink()) continue;
      if (item.isDirectory()) visit(file, depth + 1);
      else if (item.isFile() && (extname(item.name).toLowerCase() === '.dll' || item.name.endsWith('.deps.json') || item.name.endsWith('.runtimeconfig.json'))) {
        const size = statSync(file).size;
        if (files.length >= MAX_IDENTITY_FILES || (bytes += size) > MAX_IDENTITY_BYTES)
          throw new Error(`Candidate identity scan exceeded its fixed limit: ${directory}`);
        files.push({ path: relative(directory, file), bytes: size, sha256: sha256(file) });
      }
    }
  }
  visit(directory, 0);
  files.sort((left, right) => left.path.localeCompare(right.path));
  if (!files.some(file => resolve(directory, file.path) === probe)) throw new Error(`Probe assembly absent from identity: ${probe}`);
  return {
    directory,
    probeAssembly: relative(directory, probe),
    probeAssemblySha256: sha256(probe),
    identityFileCount: files.length,
    identityBytes: bytes,
    files
  };
}

function rawKey(config, row) {
  return JSON.stringify(config.key(row));
}

function validateRaw(probeName, data, label) {
  const config = PROBES[probeName];
  if (data.workload !== config.workload) throw new Error(`${label}: expected ${config.workload}, got ${data.workload}`);
  if (data[config.warmupField] !== WARMUPS[probeName] || data[config.sampleField] !== SAMPLES || !config.validateHeader(data))
    throw new Error(`${label}: probe warmup/sample/iteration settings changed`);
  if (typeof data.framework !== 'string' || !data.framework || !Array.isArray(data.results))
    throw new Error(`${label}: missing runtime identity or raw samples`);
  const expected = new Set(config.expectedGroups.map(group => JSON.stringify(group)));
  const groups = new Map();
  for (const row of data.results) {
    const key = rawKey(config, row);
    if (!expected.has(key)) throw new Error(`${label}: unexpected group ${key}`);
    if (!Number.isInteger(row.sample) || row.sample < 0 || row.sample >= SAMPLES)
      throw new Error(`${label}: invalid sample index for ${key}`);
    for (const metric of ['nsPerOperation', 'bytesPerOperation'])
      if (!Number.isFinite(row[metric]) || row[metric] < 0) throw new Error(`${label}: invalid ${metric} for ${key}`);
    const entries = groups.get(key) ?? [];
    entries.push(row);
    groups.set(key, entries);
  }
  if (groups.size !== expected.size) throw new Error(`${label}: expected ${expected.size} groups, got ${groups.size}`);
  for (const [key, rows] of groups) {
    if (rows.length !== SAMPLES || new Set(rows.map(row => row.sample)).size !== SAMPLES)
      throw new Error(`${label}: expected sample indices 0..${SAMPLES - 1} for ${key}`);
    if (rows.some(row => config.operationCount(row) !== config.expectedOperations(row, data)))
      throw new Error(`${label}: operation count changed for ${key}`);
  }
  return groups;
}

function quantile(values, probability) {
  const sorted = [...values].sort((left, right) => left - right);
  const position = (sorted.length - 1) * probability;
  const lower = Math.floor(position);
  const upper = Math.ceil(position);
  return sorted[lower] + (sorted[upper] - sorted[lower]) * (position - lower);
}

function summary(values) {
  const sorted = [...values].sort((left, right) => left - right);
  const middle = Math.floor(sorted.length / 2);
  return {
    count: values.length,
    median: values.length % 2 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2,
    min: sorted[0],
    max: sorted.at(-1),
    p95: quantile(sorted, 0.95),
    rawValues: values
  };
}

function aggregate(rows) {
  return {
    nsPerOperation: summary(rows.map(row => row.nsPerOperation)),
    bytesPerOperation: summary(rows.map(row => row.bytesPerOperation))
  };
}

function percentChange(before, after) {
  return before === 0 ? null : (after - before) * 100 / before;
}

function activePowerPlan() {
  if (process.platform !== 'win32')
    return { query: 'not available on this platform', exitCode: null, schemeGuid: null };
  const result = spawnSync('powercfg.exe', ['/getactivescheme'], { windowsHide: true, timeout: 10000 });
  const output = Buffer.isBuffer(result.stdout) ? result.stdout.toString('ascii') : '';
  const schemeGuid = output.match(/\b[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}\b/i)?.[0]?.toLowerCase() ?? null;
  return {
    query: 'powercfg.exe /getactivescheme; ASCII GUID extracted from OEM output bytes',
    exitCode: result.status,
    schemeGuid,
    error: result.error?.message ?? null
  };
}

function probeIdentity(directory, relativePath, probeName, pair, candidate) {
  return {
    directory,
    probeAssembly: relative(directory, resolve(directory, relativePath)),
    probe: probeName,
    pair,
    candidate
  };
}

function environment() {
  const profilerNames = process.platform === 'win32'
    ? ['dotnet-trace', 'PerfView', 'wpr.exe', 'xperf.exe']
    : ['dotnet-trace', 'perf', 'perfetto'];
  return {
    hostname: os.hostname(),
    platform: process.platform,
    osRelease: os.release(),
    architecture: process.arch,
    cpuModel: os.cpus()[0]?.model ?? null,
    logicalCpuCount: os.cpus().length,
    nodeVersion: process.version,
    cwd: process.cwd(),
    relevantEnvironment: Object.fromEntries(ENVIRONMENT_KEYS.filter(key => process.env[key] !== undefined).map(key => [key, process.env[key]])),
    powerPlan: activePowerPlan(),
    cpuProfilerAvailability: profilerNames.map(name => ({ name, path: findExecutable(name) }))
  };
}

function writeJson(path, value) {
  writeFileSync(path, JSON.stringify(value, null, 2) + '\n', 'utf8');
}

function processRecord(probeName, candidate, pair, orderPosition, dirs, output, dotnet, probeRelativePath) {
  const fileStem = `${probeName}-pair-${String(pair).padStart(2, '0')}-${candidate}`;
  const rawFile = `${fileStem}.json`;
  const identityFile = probeName === 'width' ? `${fileStem}-identity.json` : null;
  const assembly = resolve(dirs[candidate], probeRelativePath);
  const args = [assembly, PROBES[probeName].flag, '--output', join(output, rawFile)];
  if (identityFile) args.push('--identity-output', join(output, identityFile));
  return {
    probe: probeName,
    candidate,
    pair,
    orderPosition,
    command: dotnet,
    arguments: args,
    rawFile,
    identityFile,
    stdoutFile: `${fileStem}.stdout.log`,
    stderrFile: `${fileStem}.stderr.log`
  };
}

const options = parseArgs(process.argv.slice(2));
if (options.help) {
  usage();
  process.exit(0);
}

let reportPath;
let manifest;
try {
  const dirs = { before: realpathSync(options.before_dir), after: realpathSync(options.after_dir) };
  if (!statSync(dirs.before).isDirectory() || !statSync(dirs.after).isDirectory()) throw new Error('Candidate paths must be directories');
  if (within(dirs.before, dirs.after) || within(dirs.after, dirs.before)) throw new Error('Before and after directories must be independent and non-overlapping');
  const output = outputDirectory(options.output_dir);
  if (within(output, dirs.before) || within(output, dirs.after)) throw new Error('Output directory must be outside both candidates');
  mkdirSync(output, { recursive: true });
  reportPath = join(output, 'paired-probe.json');
  if (existsSync(reportPath)) throw new Error(`Refusing to overwrite report: ${reportPath}`);

  const probeRelativePath = options.probe_relative_path ?? 'Bing.Data.Sql.Benchmarks.dll';
  const dotnet = findExecutable(options.dotnet ?? process.env.DOTNET_HOST_PATH ?? 'dotnet');
  if (!dotnet) throw new Error('Could not resolve the .NET host executable');
  const candidates = {
    before: candidateIdentity(dirs.before, probeRelativePath),
    after: candidateIdentity(dirs.after, probeRelativePath)
  };
  const order = [
    { pair: 1, candidates: ['before', 'after'] },
    { pair: 2, candidates: ['after', 'before'] }
  ];
  manifest = {
    schema: 'SqlBuilderPairedProbe-v1',
    status: 'IN_PROGRESS',
    startedAt: new Date().toISOString(),
    probes: Object.fromEntries(Object.entries(PROBES).map(([name, config]) => [name, {
      workload: config.workload,
      warmupsPerProcess: WARMUPS[name],
      samplesPerProcess: SAMPLES,
      processesPerCandidate: 2,
      samplesPerCandidateAndGroup: 2 * SAMPLES
    }])),
    formalOrderPerProbe: order.map(run => ({ pair: run.pair, order: run.candidates })),
    statistics: {
      p95: 'Linear interpolation at (n - 1) * 0.95 over ascending raw values.',
      median: 'Middle value, or arithmetic mean of the two middle values.',
      changesAreDescriptive: true
    },
    processLimits: { timeoutMs: PROCESS_TIMEOUT_MS, maxCapturedStdoutStderrBytes: MAX_OUTPUT_BYTES },
    environment: environment(),
    dotnetHost: { path: dotnet, sha256: sha256(dotnet) },
    candidates,
    runs: [],
    measurements: [],
    comparisons: [],
    limitations: [
      'Uses existing SqlBuilderStateProbe-v1 and SqlGeneratedWidthProbe-v1; no workload is copied or changed.',
      'No performance threshold, pass/fail gate, or production capacity claim is inferred.',
      'CPU frequency, temperature, affinity, and unrelated machine load are not controlled.',
      'Per-thread B/op is not process peak memory. Both candidate directories must use the same probe harness and dependency layout.'
    ]
  };
  writeJson(reportPath, manifest);

  const grouped = Object.fromEntries(Object.keys(PROBES).map(name => [name, { before: new Map(), after: new Map() }]));
  for (const probeName of Object.keys(PROBES)) {
    for (const pair of order) {
      for (let index = 0; index < pair.candidates.length; index++) {
        const candidate = pair.candidates[index];
        const record = processRecord(probeName, candidate, pair.pair, index + 1, dirs, output, dotnet, probeRelativePath);
        record.startedAt = new Date().toISOString();
        const started = Date.now();
        const result = spawnSync(dotnet, record.arguments, {
          cwd: dirs[candidate],
          env: { ...process.env, DOTNET_CLI_TELEMETRY_OPTOUT: '1' },
          encoding: 'utf8',
          timeout: PROCESS_TIMEOUT_MS,
          maxBuffer: MAX_OUTPUT_BYTES,
          windowsHide: true
        });
        record.endedAt = new Date().toISOString();
        record.elapsedMs = Date.now() - started;
        record.processId = result.pid ?? null;
        record.exitCode = result.status ?? null;
        record.signal = result.signal ?? null;
        record.error = result.error?.message ?? null;
        writeFileSync(join(output, record.stdoutFile), result.stdout ?? '', 'utf8');
        writeFileSync(join(output, record.stderrFile), result.stderr ?? '', 'utf8');
        manifest.runs.push(record);
        writeJson(reportPath, manifest);
        if (record.exitCode !== 0 || record.signal || record.error)
          throw new Error(`${probeName}/${candidate}/pair-${pair.pair} failed: exit=${record.exitCode}, signal=${record.signal ?? 'none'}, ${record.error ?? ''}`);

        const rawPath = join(output, record.rawFile);
        if (!existsSync(rawPath)) throw new Error(`Missing raw output: ${rawPath}`);
        const raw = JSON.parse(readFileSync(rawPath, 'utf8'));
        const groups = validateRaw(probeName, raw, `${probeName}/${candidate}/pair-${pair.pair}`);
        record.framework = raw.framework;
        record.rawResultCount = raw.results.length;
        record.samplePlan = { warmups: raw[PROBES[probeName].warmupField], samples: raw.samples };
        if (record.identityFile) {
          const identityPath = join(output, record.identityFile);
          if (!existsSync(identityPath)) throw new Error(`Missing identity output: ${identityPath}`);
          const identity = JSON.parse(readFileSync(identityPath, 'utf8'));
          if (identity.probe !== PROBES[probeName].workload || identity.framework !== raw.framework)
            throw new Error(`Width identity does not match raw run: ${record.identityFile}`);
          record.binaryIdentity = identity;
        }
        for (const [key, rows] of groups) {
          const all = grouped[probeName][candidate].get(key) ?? [];
          all.push(...rows.map(row => ({ ...row, candidate, pair: pair.pair, orderPosition: index + 1, processId: record.processId })));
          grouped[probeName][candidate].set(key, all);
        }
        writeJson(reportPath, manifest);
      }
    }
  }

  const runtimes = [...new Set(manifest.runs.map(run => run.framework))];
  if (runtimes.length !== 1) throw new Error(`.NET FrameworkDescription differs across processes: ${runtimes.join(', ')}`);
  for (const probeName of Object.keys(PROBES)) {
    const beforeGroups = grouped[probeName].before;
    const afterGroups = grouped[probeName].after;
    if (beforeGroups.size !== PROBES[probeName].expectedGroups.length || afterGroups.size !== beforeGroups.size)
      throw new Error(`${probeName}: before/after group counts differ`);
    for (const [key, beforeRows] of beforeGroups) {
      const afterRows = afterGroups.get(key);
      if (!afterRows || beforeRows.length !== 10 || afterRows.length !== 10)
        throw new Error(`${probeName}: expected ten samples per candidate for ${key}`);
      const signature = rows => rows.map(row => PROBES[probeName].signature(row));
      if (JSON.stringify(signature(beforeRows)) !== JSON.stringify(signature(afterRows)))
        throw new Error(`${probeName}: workload/output shape differs between candidates for ${key}`);
      const before = aggregate(beforeRows);
      const after = aggregate(afterRows);
      const pairs = [1, 2].map(pair => {
        const pairBefore = aggregate(beforeRows.filter(row => row.pair === pair));
        const pairAfter = aggregate(afterRows.filter(row => row.pair === pair));
        return {
          pair,
          before: pairBefore,
          after: pairAfter,
          medianChangePercent: {
            nsPerOperation: percentChange(pairBefore.nsPerOperation.median, pairAfter.nsPerOperation.median),
            bytesPerOperation: percentChange(pairBefore.bytesPerOperation.median, pairAfter.bytesPerOperation.median)
          }
        };
      });
      manifest.measurements.push({ probe: probeName, key, candidate: 'before', ...before });
      manifest.measurements.push({ probe: probeName, key, candidate: 'after', ...after });
      manifest.comparisons.push({
        probe: probeName,
        key,
        before,
        after,
        medianChangePercent: {
          nsPerOperation: percentChange(before.nsPerOperation.median, after.nsPerOperation.median),
          bytesPerOperation: percentChange(before.bytesPerOperation.median, after.bytesPerOperation.median)
        },
        pairs
      });
    }
  }
  manifest.completedAt = new Date().toISOString();
  manifest.status = 'COMPLETE';
  writeJson(reportPath, manifest);
  process.stdout.write(`Paired probes complete: ${reportPath}\n`);
  process.stdout.write(`Runtime: ${runtimes[0]}; comparisons: ${manifest.comparisons.length}; samples per candidate/group: 10\n`);
} catch (error) {
  if (manifest && reportPath) {
    manifest.status = 'FAILED';
    manifest.failedAt = new Date().toISOString();
    manifest.error = error instanceof Error ? error.message : String(error);
    writeJson(reportPath, manifest);
  }
  process.stderr.write(`${error instanceof Error ? error.stack : String(error)}\n`);
  process.exitCode = 1;
}
