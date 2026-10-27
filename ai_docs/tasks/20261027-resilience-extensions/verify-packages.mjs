import { spawnSync, execFileSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, writeFileSync, readdirSync } from 'node:fs';
import { dirname, resolve, basename } from 'node:path';
import { fileURLToPath } from 'node:url';
import { tmpdir } from 'node:os';

const task = dirname(fileURLToPath(import.meta.url));
const root = resolve(task, '../../..');
const evidenceRoot = resolve(task, 'evidence/package-consumption');
const runId = new Date().toISOString().replaceAll(':', '-').replaceAll('.', '-');
const artifacts = resolve(evidenceRoot, runId);
const feed = resolve(artifacts, 'local-feed');
const cache = resolve(tmpdir(), `bing-ex-${Date.now()}`);
const results = [];
const auditResults = [];
const env = {
  ...process.env,
  NUGET_PACKAGES: cache,
  DOTNET_CLI_TELEMETRY_OPTOUT: '1',
  DOTNET_NOLOGO: '1'
};

mkdirSync(feed, { recursive: true });
mkdirSync(cache, { recursive: true });

function saveJson(name, value) {
  writeFileSync(resolve(artifacts, name), JSON.stringify(value, null, 2), 'utf8');
}

function saveSummary(status, error = '') {
  const commands = results.filter(x => !x.label.startsWith('verified-'));
  const passed = commands.filter(x => x.exitCode === 0).length;
  const failed = commands.filter(x => x.exitCode !== 0).length;
  const packages = [...new Set(results.filter(x => x.packageId).map(x => x.packageId))].sort();
  const vulnerable = auditResults.flatMap(x => x.vulnerabilities.map(v => ({ consumer: x.consumer, framework: x.framework, ...v })));
  const lines = [
    '# Package Consumption Verification',
    '',
    `- Run: \`${runId}\``,
    `- Status: **${status}**`,
    `- SDK: \`${results.find(x => x.label === 'sdk-version')?.stdout?.trim() ?? 'unknown'}\``,
    `- Commands: ${passed} passed, ${failed} failed`,
    `- Packed internal projects: ${packages.length}`,
    `- NuGet vulnerability findings: ${vulnerable.length}`,
    '- Consumers: BuiltIn, Fences, and Polly, each run and published for net6.0 and net8.0',
    '- Consumer projects contain no ProjectReference; restore assets are checked for project libraries.',
    `- Local feed: ${feed}; isolated NuGet cache: ${cache}.`,
    ''
  ];
  if (vulnerable.length) {
    lines.push('## Vulnerability Findings', '');
    for (const item of vulnerable)
      lines.push(`- ${item.consumer}/${item.framework}: \`${item.id ?? item.packageId ?? 'unknown'}\` ${item.severity ?? ''} ${item.advisoryUrl ?? item.url ?? ''}`.trim());
    lines.push('');
  }
  if (error) lines.push('## Failure', '', error, '');
  lines.push('## Commands And Logs', '');
  for (const result of results)
    lines.push(`- ${result.exitCode === 0 ? 'PASS' : 'FAIL'} \`${result.label}\`: \`${result.command.join(' ')}\` -> [log](./${runId}/${result.log})`);
  lines.push('', '## Package Set', '', packages.map(x => `- \`${x}\``).join('\n'), '');
  lines.push('The dependency audit is informational: any reported advisory is listed above and remains visible in its raw JSON log.', '');
  writeFileSync(resolve(evidenceRoot, 'results.md'), lines.join('\n'), 'utf8');
}

function run(label, args, runEnv = env, extra = {}) {
  if (['pack', 'restore', 'build', 'publish'].includes(args[0])) args.push('-m:1', '-nr:false');
  const result = spawnSync('dotnet', args, {
    cwd: root,
    encoding: 'utf8',
    env: runEnv,
    maxBuffer: 40 * 1024 * 1024
  });
  const stdout = result.stdout ?? '';
  const stderr = result.stderr ?? '';
  const output = [
    `Command: dotnet ${args.join(' ')}`,
    `ExitCode: ${result.status ?? 'spawn-error'}`,
    '',
    stdout,
    stderr
  ].join('\n');
  const log = `${label}.log`;
  writeFileSync(resolve(artifacts, log), output, 'utf8');
  const entry = { label, command: ['dotnet', ...args], exitCode: result.status, stdout, stderr, log, ...extra };
  results.push(entry);
  saveJson('results.json', results);
  console.log(`${label}: ${result.status === 0 ? 'PASS' : 'FAIL'}`);
  if (result.status !== 0 || result.error)
    throw new Error(`${label} failed (exit ${result.status ?? 'spawn error'}). See ${resolve(artifacts, log)}.`);
  if (/\bNU1605\b/.test(output))
    throw new Error(`${label} emitted NU1605 downgrade diagnostics. See ${resolve(artifacts, log)}.`);
  return { stdout, stderr, log };
}

function packageIdFromProject(project) {
  const text = readFileSync(project, 'utf8');
  const match = text.match(/<PackageId>([^<]+)<\/PackageId>/i);
  return match?.[1]?.trim() || basename(project, '.csproj');
}

const consumers = ['BuiltIn', 'Fences', 'Polly'].map(name => ({
  name,
  project: resolve(task, 'consumers', name, 'Consumer.csproj'),
  source: readFileSync(resolve(task, 'consumers', name, 'Consumer.csproj'), 'utf8')
}));
for (const consumer of consumers) {
  if (/<ProjectReference\b/i.test(consumer.source))
    throw new Error(`${consumer.name} consumer declares a ProjectReference.`);
}

const seeds = [...new Set(consumers.flatMap(x =>
  [...x.source.matchAll(/<PackageReference\s+Include="([^"]+)"/gi)].map(m => m[1])))];
const packOrder = [];
const visited = new Set();
function visit(project) {
  project = resolve(project);
  if (visited.has(project)) return;
  visited.add(project);
  if (!existsSync(project)) throw new Error(`Package project is missing: ${project}`);
  const scannedFiles = new Set();
  function scan(file) {
    file = resolve(file);
    if (scannedFiles.has(file) || !existsSync(file)) return;
    scannedFiles.add(file);
    const text = readFileSync(file, 'utf8');
    for (const match of text.matchAll(/<ProjectReference\b[^>]*Include="([^"]+)"/gi))
      visit(resolve(dirname(file), match[1].replaceAll('\\', '/')));
    for (const match of text.matchAll(/<PackageReference\b[^>]*Include="([^"]+)"/gi)) {
      const internalProject = resolve(root, 'framework/src', match[1], `${match[1]}.csproj`);
      if (existsSync(internalProject)) visit(internalProject);
    }
    for (const match of text.matchAll(/<Import\b[^>]*Project="([^"]+)"/gi)) {
      if (!match[1].includes('$(')) scan(resolve(dirname(file), match[1].replaceAll('\\', '/')));
    }
  }
  scan(project);
  packOrder.push(project);
}
for (const id of seeds) visit(resolve(root, 'framework/src', id, `${id}.csproj`));

function escapeXml(value) {
  return value.replaceAll('&', '&amp;').replaceAll('"', '&quot;').replaceAll('<', '&lt;');
}
const localPackageIds = [...new Set(packOrder.map(packageIdFromProject))].sort();
const localMappings = localPackageIds.map(id => `<package pattern="${escapeXml(id)}" />`).join('');
const config = resolve(artifacts, 'NuGet.Config');
writeFileSync(config,
  `<?xml version="1.0" encoding="utf-8"?><configuration><packageSources><clear/><add key="local" value="${escapeXml(feed)}"/><add key="nuget" value="https://api.nuget.org/v3/index.json"/></packageSources><packageSourceMapping><packageSource key="local">${localMappings}</packageSource><packageSource key="nuget"><package pattern="*" /></packageSource></packageSourceMapping></configuration>`,
  'utf8');
saveJson('manifest.json', {
  runId,
  packageVersion: '7.0.0',
  consumers: consumers.map(x => x.name),
  extensionPackages: [
    'Bing.Resilience.BuiltIn', 'Bing.Resilience.Runtime', 'Bing.Resilience.Discovery',
    'Bing.Http.Resilience.Discovery', 'Bing.Resilience.Distributed',
    'Bing.Resilience.Distributed.Redis', 'Bing.Resilience.Operations',
    'Bing.Resilience.Operations.Relational', 'Bing.Resilience.Replay',
    'Bing.Resilience.Compensation', 'Bing.Http.Resilience.Replay',
    'Bing.Resilience.Idempotency.AspNetCore'
  ],
  clientPackages: ['Bing.DynamicApi.Client.Resilience', 'Bing.DynamicApi.WebApiClient'],
  packagesToPack: packOrder.map(x => packageIdFromProject(x)),
  feed,
  cache
});

try {
  run('sdk-version', ['--version']);
  for (const project of packOrder) {
    const id = packageIdFromProject(project);
    run(`pack-${id}`, ['pack', project, '-c', 'Release', '--nologo', '-v', 'minimal', '-o', feed,
      '-p:PackageVersion=7.0.0', '-p:IncludeSymbols=false'], env, { packageId: id });
    const archive = resolve(feed, `${id}.7.0.0.nupkg`);
    if (!existsSync(archive)) throw new Error(`Pack completed but package is absent: ${archive}`);
  }

  const extensionPackages = [
    'Bing.Resilience.BuiltIn', 'Bing.Resilience.Runtime', 'Bing.Resilience.Discovery',
    'Bing.Http.Resilience.Discovery', 'Bing.Resilience.Distributed',
    'Bing.Resilience.Distributed.Redis', 'Bing.Resilience.Operations',
    'Bing.Resilience.Operations.Relational', 'Bing.Resilience.Replay',
    'Bing.Resilience.Compensation', 'Bing.Http.Resilience.Replay',
    'Bing.Resilience.Idempotency.AspNetCore'
  ];
  const forbiddenBuiltIn = /^(?:Polly(?:\.|\/|$)|Paramore\.Fences(?:\.|\/|$)|Microsoft\.Extensions\.(?:Http\.Resilience|Resilience|Http\.Polly)(?:\/|$))/i;
  const forbiddenFences = /^(?:Polly(?:\.|\/|$)|Microsoft\.Extensions\.(?:Http\.Resilience|Resilience|Http\.Polly)(?:\/|$))/i;
  const observedPackageIds = new Set();

  function dependencyIds(nuspec) {
    return [...nuspec.matchAll(/<dependency\s+[^>]*id="([^"]+)"/gi)].map(x => x[1]);
  }

  function assertBoundary(label, ids, forbidden) {
    const violations = ids.filter(id => forbidden.test(id));
    if (violations.length) throw new Error(`${label} dependency boundary violation: ${[...new Set(violations)].join(', ')}`);
  }

  for (const consumer of consumers) {
    const directIds = [...consumer.source.matchAll(/<PackageReference\s+Include="([^"]+)"/gi)].map(x => x[1]);
    const forbidden = consumer.name === 'BuiltIn' ? forbiddenBuiltIn : consumer.name === 'Fences' ? forbiddenFences : null;
    if (consumer.name === 'BuiltIn' && directIds.some(x => x === 'Bing.Resilience.Fences' || x === 'Bing.Resilience.Polly'))
      throw new Error('BuiltIn consumer must not reference an optional third-party Provider.');
    if (consumer.name === 'Fences' && directIds.includes('Bing.Resilience.Polly'))
      throw new Error('Fences consumer must not reference Polly.');

    run(`restore-${consumer.name}`, ['restore', consumer.project, '--configfile', config, '--packages', cache, '--nologo'], env);
    const assetsPath = resolve(dirname(consumer.project), 'obj/project.assets.json');
    const assets = JSON.parse(readFileSync(assetsPath, 'utf8'));
    if (Object.values(assets.libraries).some(x => x.type === 'project'))
      throw new Error(`${consumer.name} assets contain a project library.`);
    const assetIds = Object.keys(assets.libraries).map(x => x.split('/')[0]);
    for (const id of directIds) {
      if (!assetIds.includes(id)) throw new Error(`${consumer.name} assets omit direct package ${id}.`);
      observedPackageIds.add(id);
    }
    if (!assetIds.includes('Bing.DynamicApi.Client.Resilience') || !assetIds.includes('Bing.DynamicApi.WebApiClient'))
      throw new Error(`${consumer.name} assets omit one of the HTTP client adapters.`);
    for (const id of assetIds) observedPackageIds.add(id);
    if (forbidden) assertBoundary(`assets-${consumer.name}`, assetIds, forbidden);

    const localAssets = assetIds.filter(id => localPackageIds.includes(id));
    for (const id of localAssets) {
      const archive = resolve(feed, `${id}.7.0.0.nupkg`);
      if (!existsSync(archive)) throw new Error(`${consumer.name} resolved ${id} without a locally packed nupkg.`);
      const nuspec = execFileSync('tar', ['-xOf', archive, `${id}.nuspec`], { encoding: 'utf8', maxBuffer: 4 * 1024 * 1024 });
      const dependencies = dependencyIds(nuspec);
      if (forbidden) assertBoundary(`nuspec-${consumer.name}-${id}`, dependencies, forbidden);
    }

    const audit = run(`audit-${consumer.name}`, ['list', consumer.project, 'package', '--vulnerable',
      '--include-transitive', '--format', 'json', '--no-restore'], env);
    let auditDocument;
    try { auditDocument = JSON.parse(audit.stdout); }
    catch { throw new Error(`NuGet vulnerability audit for ${consumer.name} did not return JSON; see ${audit.log}.`); }
    const vulnerabilities = [];
    function collectVulnerabilities(node) {
      if (Array.isArray(node)) { for (const child of node) collectVulnerabilities(child); return; }
      if (!node || typeof node !== 'object') return;
      for (const [key, value] of Object.entries(node)) {
        if (key.toLowerCase().includes('vulnerab') && Array.isArray(value))
          for (const item of value) vulnerabilities.push(item);
        else collectVulnerabilities(value);
      }
    }
    collectVulnerabilities(auditDocument);
    auditResults.push({ consumer: consumer.name, framework: 'net6.0+net8.0', vulnerabilities });
    saveJson('audit-results.json', auditResults);

    for (const framework of ['net6.0', 'net8.0']) {
      const runResult = run(`run-${consumer.name}-${framework}`, ['run', '--project', consumer.project,
        '--framework', framework, '--configuration', 'Release', '--no-restore'], env);
      if (!runResult.stdout.includes(`PACKAGE_CONSUMER_PASS engine=${consumer.name}`))
        throw new Error(`${consumer.name}/${framework} did not print its package smoke PASS line.`);

      const publishPath = resolve(artifacts, 'publish', consumer.name, framework);
      mkdirSync(publishPath, { recursive: true });
      run(`publish-${consumer.name}-${framework}`, ['publish', consumer.project, '--framework', framework,
        '--configuration', 'Release', '--no-restore', '--no-self-contained', '-o', publishPath, '-v', 'minimal'], env);
      const depsPath = resolve(publishPath, 'Consumer.deps.json');
      if (!existsSync(depsPath)) throw new Error(`${consumer.name}/${framework} publish deps file is missing.`);
      const deps = JSON.parse(readFileSync(depsPath, 'utf8'));
      const depIds = Object.keys(deps.libraries).map(x => x.split('/')[0]);
      if (forbidden) assertBoundary(`publish-deps-${consumer.name}-${framework}`, depIds, forbidden);
      for (const id of directIds)
        if (!depIds.includes(id)) throw new Error(`${consumer.name}/${framework} published deps omit ${id}.`);
    }
  }

  for (const id of extensionPackages) {
    if (!observedPackageIds.has(id)) throw new Error(`Extension package ${id} was packed but absent from all consumer graphs.`);
  }
  for (const id of ['Bing.DynamicApi.Client.Resilience', 'Bing.DynamicApi.WebApiClient']) {
    if (!observedPackageIds.has(id)) throw new Error(`Client package ${id} was not resolved by consumers.`);
  }
  const nupkgNames = readdirSync(feed).filter(x => x.endsWith('.nupkg'));
  if (nupkgNames.length !== packOrder.length) throw new Error(`Expected ${packOrder.length} nupkg files, found ${nupkgNames.length}.`);
  saveJson('summary.json', {
    status: 'PASS',
    runId,
    sdk: results.find(x => x.label === 'sdk-version')?.stdout?.trim(),
    consumerRuns: consumers.length * 2,
    packedInternalProjects: packOrder.length,
    extensionPackages,
    clientPackages: ['Bing.DynamicApi.Client.Resilience', 'Bing.DynamicApi.WebApiClient'],
    vulnerabilityFindings: auditResults.flatMap(x => x.vulnerabilities.map(v => ({ consumer: x.consumer, ...v }))),
    packageIdsObservedInAssets: [...observedPackageIds].sort(),
    feed,
    cache
  });
  saveSummary('PASS');
  console.log(`Package consumption PASS: ${packOrder.length} packages; ${consumers.length * 2} framework consumers.`);
} catch (error) {
  saveJson('summary.json', {
    status: 'FAIL',
    runId,
    error: error instanceof Error ? error.message : String(error),
    completedCommands: results.length,
    packedInternalProjects: [...new Set(results.filter(x => x.packageId).map(x => x.packageId))],
    auditResults,
    feed,
    cache
  });
  saveSummary('FAIL', error instanceof Error ? error.message : String(error));
  console.error(error);
  process.exitCode = 1;
}
