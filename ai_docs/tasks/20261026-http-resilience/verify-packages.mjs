import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, resolve, basename } from 'node:path';
import { fileURLToPath } from 'node:url';
const task = dirname(fileURLToPath(import.meta.url));
const root = resolve(task, '../../..');
const artifactsArg = process.argv.indexOf('--artifacts');
const artifacts = artifactsArg < 0 ? resolve(task, 'artifacts') : resolve(root, process.argv[artifactsArg + 1]);
const feed = resolve(artifacts, 'local-feed');
const cache = resolve(artifacts, 'packages');
mkdirSync(feed, { recursive: true });
const projects = new Set();
function visit(project) {
  project = resolve(project);
  if (projects.has(project)) return;
  projects.add(project);
  const assetsPath = resolve(dirname(project), "obj/project.assets.json");
  if (existsSync(assetsPath)) {
    const assets = JSON.parse(readFileSync(assetsPath, "utf8"));
    for (const dependency of Object.values(assets.libraries))
      if (dependency.type === "project") visit(resolve(dirname(project), dependency.msbuildProject));
  }
  const source = readFileSync(project, 'utf8');
  for (const match of source.matchAll(/<ProjectReference\s+Include="([^"]+)"/g))
    visit(resolve(dirname(project), match[1].replaceAll('\\', '/')));
  projects.add(project);
}
visit(resolve(root, 'framework/src/Bing.DynamicApi.Client.Resilience/Bing.DynamicApi.Client.Resilience.csproj'));
visit(resolve(root, 'framework/src/Bing.DynamicApi.WebApiClient/Bing.DynamicApi.WebApiClient.csproj'));
const results = process.argv.includes("--consume-only") && existsSync(resolve(artifacts, "packages.json"))
  ? JSON.parse(readFileSync(resolve(artifacts, "packages.json"), "utf8")).filter(r => r.label.startsWith("pack-")) : [];
function run(label, args, env = process.env) {
  // 隔离 MSBuild 节点，避免复用其他进程初始化过的 NuGet 目录缓存。
  if (['pack', 'restore', 'build'].includes(args[0])) args.push('-m:1', '-nr:false');
  const result = spawnSync('dotnet', args, { cwd: root, encoding: 'utf8', env, maxBuffer: 20 * 1024 * 1024 });
  writeFileSync(resolve(artifacts, label + '.log'), (result.stdout ?? '') + (result.stderr ?? ''), 'utf8');
  results.push({ label, command: ['dotnet', ...args], exitCode: result.status });
  console.log(label + ': ' + (result.status === 0 ? 'PASS' : 'FAIL'));
  if (result.status !== 0) {
    writeFileSync(resolve(artifacts, 'packages.json'), JSON.stringify(results, null, 2), 'utf8');
    console.log((result.stdout ?? '') + (result.stderr ?? ''));
    process.exit(1);
  }
}
for (const project of projects)
  if (!process.argv.includes("--consume-only")) run('pack-' + basename(project, '.csproj'), ['pack', project, '-c', 'Release', '--nologo', '-v', 'minimal', '-o', feed]);
const escapeXml = value => value.replaceAll('&', '&amp;').replaceAll('"', '&quot;');
const localMappings = [...projects].map(p => '      <package pattern="' + basename(p, '.csproj') + '" />').join('\n');
const config = resolve(artifacts, 'consumer.nuget.config');
writeFileSync(config, '<?xml version="1.0" encoding="utf-8"?>\n<configuration>\n  <packageSources><clear/><add key="local" value="' +
  escapeXml(feed) + '"/><add key="nuget" value="https://api.nuget.org/v3/index.json"/></packageSources>\n' +
  '  <packageSourceMapping>\n    <packageSource key="local">\n' + localMappings +
  '\n    </packageSource>\n    <packageSource key="nuget"><package pattern="*" /></packageSource>\n  </packageSourceMapping>\n</configuration>\n', 'utf8');
const consumer = resolve(task, 'consumer/Consumer.csproj');
const env = { ...process.env, NUGET_PACKAGES: cache };
run('consumer-restore', ['restore', consumer, '--configfile', config, '--packages', cache, '--nologo'], env);
for (const tfm of ['net6.0', 'net8.0'])
  run('consumer-' + tfm, ['run', '--project', consumer, '--framework', tfm, '--no-restore'], env);
for (const name of ['Bing.Http.Resilience', 'Bing.DynamicApi.Client.Resilience'])
  run('audit-' + name, ['list', resolve(root, 'framework/src', name, name + '.csproj'), 'package', '--vulnerable', '--include-transitive', '--format', 'json', '--no-restore']);
writeFileSync(resolve(artifacts, 'packages.json'), JSON.stringify(results, null, 2), 'utf8');
