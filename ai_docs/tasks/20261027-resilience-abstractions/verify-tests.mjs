import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
const task = dirname(fileURLToPath(import.meta.url));
const root = resolve(task, '../../..');
const artifactsArg = process.argv.indexOf('--artifacts');
const artifacts = artifactsArg < 0 ? resolve(task, 'evidence/final-tests') : resolve(root, process.argv[artifactsArg + 1]);
mkdirSync(artifacts, { recursive: true });
const results = process.argv.includes("--affected") && existsSync(resolve(artifacts, "tests.json"))
  ? JSON.parse(readFileSync(resolve(artifacts, "tests.json"), "utf8")).filter(r => !r.command[2].includes("Resilience.Tests")) : [];
for (const name of ['Bing.Resilience.Tests', 'Bing.Http.Resilience.Tests', 'Bing.DynamicApi.Client.Resilience.Tests', 'Bing.DynamicApi.Client.Tests', 'Bing.DynamicApi.WebApiClient.Tests']) {
  if (process.argv.includes('--affected') && !name.includes('Resilience.Tests')) continue;
  const args = ['test', 'framework/tests/' + name + '/' + name + '.csproj', '--nologo', '-m:1', '-nr:false', '-v', 'minimal',
    '--logger', 'trx', '--results-directory', resolve(artifacts, name)];
  const run = spawnSync('dotnet', args, { cwd: root, encoding: 'utf8' });
  writeFileSync(resolve(artifacts, name + '.log'), (run.stdout ?? '') + (run.stderr ?? ''), 'utf8');
  results.push({ command: ['dotnet', ...args], exitCode: run.status, completedAt: new Date().toISOString() });
  console.log(name + ': ' + (run.status === 0 ? 'PASS' : 'FAIL'));
  if (run.status !== 0) console.log((run.stdout ?? '') + (run.stderr ?? ''));
}
writeFileSync(resolve(artifacts, 'tests.json'), JSON.stringify(results, null, 2), 'utf8');
process.exitCode = results.some(r => r.exitCode !== 0) ? 1 : 0;
