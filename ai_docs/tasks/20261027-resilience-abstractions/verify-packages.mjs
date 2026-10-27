import { spawnSync, execFileSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, writeFileSync, readdirSync } from 'node:fs';
import { dirname, resolve, basename } from 'node:path';
import { fileURLToPath } from 'node:url';
import { tmpdir } from 'node:os';
const task = dirname(fileURLToPath(import.meta.url));
const root = resolve(task, '../../..');
const runId = new Date().toISOString().replaceAll(':', '-').replaceAll('.', '-');
const artifacts = resolve(task, 'evidence/packages', runId);
mkdirSync(resolve(task, 'evidence'), { recursive: true });
writeFileSync(resolve(task, 'evidence/package-evidence.json'), JSON.stringify({ runId, artifacts }, null, 2), 'utf8');
const feed = resolve(artifacts, 'local-feed');
// Windows NuGet 依赖路径可能很长，隔离缓存使用较短的临时根目录。
const cache = resolve(tmpdir(), 'bing-ra-' + Date.now());
writeFileSync(resolve(task, 'evidence/package-evidence.json'), JSON.stringify({ runId, artifacts, cache }, null, 2), 'utf8');
mkdirSync(feed, { recursive: true });
const results = [];
function save() { writeFileSync(resolve(artifacts, 'results.json'), JSON.stringify(results, null, 2), 'utf8'); }
function run(label, args, env = process.env) {
  if (['pack', 'restore', 'build', 'publish'].includes(args[0])) args.push('-m:1', '-nr:false');
  const result = spawnSync('dotnet', args, { cwd: root, encoding: 'utf8', env, maxBuffer: 30 * 1024 * 1024 });
  writeFileSync(resolve(artifacts, label + '.log'), (result.stdout ?? '') + (result.stderr ?? ''), 'utf8');
  results.push({ label, command: ['dotnet', ...args], exitCode: result.status });
  save();
  console.log(label + ': ' + (result.status === 0 ? 'PASS' : 'FAIL'));
  if (result.status !== 0) { console.log((result.stdout ?? '') + (result.stderr ?? '')); process.exit(1); }
}
const projects = new Set();
function visit(project) {
  project = resolve(project);
  if (projects.has(project)) return;
  projects.add(project);
  const assetsFile = resolve(dirname(project), 'obj/project.assets.json');
  if (existsSync(assetsFile)) {
    const assets = JSON.parse(readFileSync(assetsFile, 'utf8'));
    for (const item of Object.values(assets.libraries))
      if (item.type === 'project') visit(resolve(dirname(project), item.msbuildProject));
  }
  for (const match of readFileSync(project, 'utf8').matchAll(/<ProjectReference\s+Include="([^"]+)"/g))
    visit(resolve(dirname(project), match[1].replaceAll('\\', '/')));
}
for (const name of ['Bing.Resilience.Fences', 'Bing.Resilience.Polly', 'Bing.DynamicApi.Client.Resilience', 'Bing.DynamicApi.WebApiClient'])
  visit(resolve(root, 'framework/src', name, name + '.csproj'));
for (const project of projects)
  run('pack-' + basename(project, '.csproj'), ['pack', project, '-c', 'Release', '--nologo', '-v', 'minimal', '-o', feed]);
const escapeXml = s => s.replaceAll('&', '&amp;').replaceAll('"', '&quot;');
const mapping = [...projects].map(p => '<package pattern="' + basename(p, '.csproj') + '" />').join('\n');
const config = resolve(artifacts, 'NuGet.Config');
writeFileSync(config, '<?xml version="1.0" encoding="utf-8"?><configuration><packageSources><clear/>' +
  '<add key="local" value="' + escapeXml(feed) + '"/><add key="nuget" value="https://api.nuget.org/v3/index.json"/>' +
  '</packageSources><packageSourceMapping><packageSource key="local">' + mapping +
  '</packageSource><packageSource key="nuget"><package pattern="*" /></packageSource></packageSourceMapping></configuration>', 'utf8');
const env = { ...process.env, NUGET_PACKAGES: cache };
const forbidden = /^(Polly(?:\.|\/|$)|Microsoft\.Extensions\.(?:Http\.Resilience|Resilience|Http\.Polly)(?:\/|$))/i;
function checkFree(label, names) {
  const bad = names.filter(name => forbidden.test(name));
  if (bad.length) throw new Error(label + ': forbidden dependencies: ' + bad.join(', '));
  results.push({ label, exitCode: 0, inspected: names.length }); save();
}
for (const engine of ['Fences', 'Polly']) {
  for (const kind of ['Generic', 'Http']) {
    const name = engine + '.' + kind;
    const project = resolve(task, 'consumers', name, 'Consumer.csproj');
    run('restore-' + name, ['restore', project, '--configfile', config, '--packages', cache, '--nologo'], env);
    const assets = JSON.parse(readFileSync(resolve(dirname(project), 'obj/project.assets.json'), 'utf8'));
    const libraries = Object.keys(assets.libraries);
    if (Object.values(assets.libraries).some(x => x.type === 'project')) throw new Error(name + ': ProjectReference leaked');
    if (kind === 'Generic' && libraries.some(x => /^Bing\.Core\//.test(x))) throw new Error(name + ': Bing.Core leaked');
    if (engine === 'Fences') checkFree('assets-' + name, libraries);
    for (const tfm of ['net6.0', 'net8.0']) {
      run('run-' + name + '-' + tfm, ['run', '--project', project, '--framework', tfm, '--no-restore'], env);
      const publish = resolve(artifacts, 'publish', name, tfm);
      run('publish-' + name + '-' + tfm, ['publish', project, '--framework', tfm, '--no-restore', '--no-self-contained', '-o', publish, '-v', 'minimal'], env);
      if (engine === 'Fences') {
        const deps = JSON.parse(readFileSync(resolve(publish, 'Consumer.deps.json'), 'utf8'));
        checkFree('deps-' + name + '-' + tfm, Object.keys(deps.libraries));
      }
    }
    run('audit-' + name, ['list', project, 'package', '--vulnerable', '--include-transitive', '--format', 'json', '--no-restore'], env);
  }
}
for (const name of ['Bing.Resilience.Abstractions', 'Bing.Resilience', 'Bing.Resilience.Fences', 'Bing.Http.Resilience', 'Bing.DynamicApi.Client.Resilience']) {
  const archive = readdirSync(feed).find(f => f === name + '.7.0.0.nupkg');
  if (!archive) throw new Error('Missing package ' + name);
  const nuspec = execFileSync('tar', ['-xOf', resolve(feed, archive), name + '.nuspec'], { encoding: 'utf8' });
  writeFileSync(resolve(artifacts, name + '.nuspec'), nuspec, 'utf8');
  checkFree('nuspec-' + name, [...nuspec.matchAll(/<dependency id="([^"]+)"/g)].map(m => m[1]));
}
console.log('All package consumers and dependency boundaries PASS');
