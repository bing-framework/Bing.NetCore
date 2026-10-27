import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';
const task = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(task, '../../..');
const runId = new Date().toISOString().replaceAll(':', '-').replaceAll('.', '-');
const output = path.join(task, 'evidence', 'final-tests', runId);
fs.mkdirSync(output, { recursive: true });
const environment = { ...process.env, DOTNET_CLI_UI_LANGUAGE: 'en' };
if (process.argv.includes('--local-services')) {
  environment.BING_RESILIENCE_TEST_CONNECTION_FILE = path.join(task, 'evidence/containers/connection.json');
  if (!fs.existsSync(environment.BING_RESILIENCE_TEST_CONNECTION_FILE)) throw Error('Start task-owned integration containers first.');
  environment.BING_RESILIENCE_ALLOW_DOCKER_FAULTS = '1';
}
const projects = ['Bing.Resilience.Tests', 'Bing.Http.Resilience.Tests', 'Bing.DynamicApi.Client.Resilience.Tests',
  'Bing.DynamicApi.Client.Tests', 'Bing.DynamicApi.WebApiClient.Tests', 'Bing.Resilience.Extensions.Tests'];
const results = [];
for (const project of projects) for (const framework of ['net6.0', 'net8.0']) {
  const name = project + '-' + framework;
  const args = ['test', path.join(root, 'framework/tests', project, project + '.csproj'), '-f', framework,
    '--nologo', '-m:1', '-nr:false', '-v', 'minimal', '--logger', 'trx;LogFileName=' + name + '.trx', '--results-directory', output];
  const started = Date.now();
  const command = spawnSync('dotnet', args, { cwd: root, env: environment, encoding: 'utf8', timeout: 180000, maxBuffer: 32 * 1024 * 1024, windowsHide: true });
  fs.writeFileSync(path.join(output, name + '.log'), (command.stdout ?? '') + (command.stderr ?? ''), 'utf8');
  const trxFile = path.join(output, name + '.trx');
  const trx = fs.existsSync(trxFile) ? fs.readFileSync(trxFile, 'utf8') : '';
  const tag = trx.match(/<Counters\b[^>]*>/)?.[0] ?? '';
  const count = key => Number(tag.match(new RegExp(key + '="(\\d+)"'))?.[1] ?? 0);
  const result = { project, framework, exitCode: command.status, milliseconds: Date.now() - started,
    total: count('total'), passed: count('passed'), failed: count('failed'), notExecuted: count('notExecuted'), command: ['dotnet', ...args] };
  results.push(result);
  console.log(name + ': ' + result.passed + '/' + result.total + ', exit=' + command.status);
  fs.writeFileSync(path.join(output, 'summary.json'), JSON.stringify({ runId, results }, null, 2), 'utf8');
  if (command.status !== 0 || result.total === 0 || result.failed > 0) { process.exitCode = 1; break; }
}
fs.writeFileSync(path.join(task, 'evidence/final-tests/latest.json'), JSON.stringify({ runId, output, results }, null, 2), 'utf8');
if (results.length !== projects.length * 2) process.exitCode = 1;
