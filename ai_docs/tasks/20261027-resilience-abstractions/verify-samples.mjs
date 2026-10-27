import { spawnSync } from 'node:child_process';
import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
const task = dirname(fileURLToPath(import.meta.url));
const root = resolve(task, '../../..');
const artifacts = resolve(task, 'evidence/samples');
mkdirSync(artifacts, { recursive: true });
const jobs = [
  ['generic-net6', ['run', '--project', 'samples/Bing.Samples.Resilience', '--framework', 'net6.0']],
  ['generic-net8', ['run', '--project', 'samples/Bing.Samples.Resilience', '--framework', 'net8.0']],
  ['http-retry', ['run', '--project', 'samples/Bing.Samples.HttpResilience']],
  ['http-hedging', ['run', '--project', 'samples/Bing.Samples.HttpResilience', '--no-build', '--', '--hedging']],
  ['resource-probe', ['run', '--project', 'samples/Bing.Samples.HttpResilience', '--no-build', '--', '--probe']]
];
const results = [];
for (const [label, args] of jobs) {
  const run = spawnSync('dotnet', args, { cwd: root, encoding: 'utf8', timeout: 120000, maxBuffer: 8 * 1024 * 1024 });
  writeFileSync(resolve(artifacts, label + '.log'), (run.stdout ?? '') + (run.stderr ?? ''), 'utf8');
  results.push({ label, command: ['dotnet', ...args], exitCode: run.status });
  console.log(label + ': ' + (run.status === 0 ? 'PASS' : 'FAIL'));
  if (run.status !== 0) console.log((run.stdout ?? '') + (run.stderr ?? ''));
}
writeFileSync(resolve(artifacts, 'results.json'), JSON.stringify(results, null, 2), 'utf8');
process.exitCode = results.some(r => r.exitCode !== 0) ? 1 : 0;
