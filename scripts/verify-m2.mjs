import assert from 'node:assert/strict'
import { spawn } from 'node:child_process'
import { randomBytes } from 'node:crypto'
import { mkdir, writeFile } from 'node:fs/promises'
import { fileURLToPath } from 'node:url'

const root = fileURLToPath(new URL('../', import.meta.url))
const project = `analysis-m2-check-${randomBytes(6).toString('hex')}`
const password = randomBytes(32).toString('hex')
const env = { ...process.env, POSTGRES_PASSWORD: password, M2_DB_PASSWORD: password }
const base = ['compose', '--project-name', project, '--env-file', '.env.example', '--file', 'compose.yaml']
const report = { project, mode: 'offline', passed: [], failed: [], cleanup: 'pending' }
let owned = false
const safe = value => value.replaceAll(password, '[redacted]')

function docker(args, { input, allowFailure = false } = {}) {
  return new Promise((resolve, reject) => {
    const child = spawn('docker', args, { cwd: root, env, windowsHide: true, stdio: ['pipe', 'pipe', 'pipe'] })
    let stdout = '', stderr = ''
    child.stdout.on('data', chunk => { stdout += chunk })
    child.stderr.on('data', chunk => { stderr += chunk })
    child.on('error', reject)
    child.on('close', code => {
      if (code && !allowFailure) reject(new Error(safe(`docker ${args.join(' ')} failed (${code}): ${stdout.slice(-2000)} ${stderr.slice(-2000)}`)))
      else resolve({ code, stdout: safe(stdout.trim()), stderr: safe(stderr.trim()) })
    })
    child.stdin.end(input)
  })
}
const compose = (args, options) => docker([...base, ...args], options)
function pass(label) { report.passed.push(label); console.log(`PASS ${label}`) }
const jsonLine = output => JSON.parse(output.trim().split(/\r?\n/).findLast(line => line.startsWith('{')))
const checkArgs = ['run', '--rm', '--network', `${project}_data`, '--env', 'M2_DB_PASSWORD', '--env', 'M2_ISOLATED_TEST=true', '--cap-drop', 'ALL', '--security-opt', 'no-new-privileges:true', `${project}-checks`]

try {
  assert.equal((await docker(['ps', '-aq', '--filter', `label=com.docker.compose.project=${project}`])).stdout, '')
  assert.equal((await docker(['volume', 'ls', '-q', '--filter', `name=^${project}_postgres-data$`])).stdout, '')
  owned = true
  await compose(['config', '--quiet'])
  await docker(['build', '--file', 'backend/Dockerfile', '--target', 'm2checks', '--tag', `${project}-checks`, '.'])
  await compose(['build', 'worker'])
  pass('Locked restore/build, M1 operational checks, offline M2 checks and EF migration drift check')
  await compose(['up', '--detach', '--wait', '--wait-timeout', '90', 'postgres', 'redis'])
  await compose(['exec', '-T', 'postgres', 'psql', '-U', 'analysis', '-d', 'analysis', '-v', 'ON_ERROR_STOP=1'], { input: 'CREATE DATABASE analysis_m2_checks;\n' })
  const checked = jsonLine((await docker([...checkArgs, '--database'])).stdout)
  assert.equal(checked.database, 'passed')
  assert.deepEqual(checked.failed, [])
  report.passed.push(...checked.passed)
  report.databaseSnapshot = checked.databaseSnapshot
  pass('Disposable PostgreSQL integration, migrations, precision, lineage, concurrency, quarantine and cancellation')

  await compose(['run', '--rm', '--no-deps', '-e', 'Postgres__Database=analysis_m2_checks', 'worker', '--migrate'])
  assert.equal(jsonLine((await docker([...checkArgs, '--verify-persistence'])).stdout).databaseSnapshot, checked.databaseSnapshot)
  pass('Explicit non-root worker migration command preserves populated M2 database')
  await compose(['up', '--detach', '--no-deps', '--force-recreate', '--wait', '--wait-timeout', '90', 'postgres'])
  assert.equal(jsonLine((await docker([...checkArgs, '--verify-persistence'])).stdout).databaseSnapshot, checked.databaseSnapshot)
  pass('Exact catalog/observations/payloads/quarantine snapshot survives PostgreSQL container recreation')

  const refusal = await compose(['run', '--rm', '--no-deps', 'worker', '--ingest-once'], { allowFailure: true })
  assert.equal(refusal.code, 2)
  assert.match(refusal.stderr, /Live ingestion is disabled/)
  pass('Worker refuses live ingestion before any provider access')
  await compose(['up', '--detach', '--wait', '--wait-timeout', '90', 'worker'])
  const probe = await compose(['exec', '-T', 'worker', 'dotnet', 'Analysis.Worker.dll', '--healthcheck', '/health/ready'])
  assert.equal(jsonLine(probe.stdout).status, 'Healthy')
  await compose(['stop', '--timeout', '15', 'worker'])
  const workerId = (await compose(['ps', '-aq', 'worker'])).stdout
  const state = JSON.parse((await docker(['inspect', '--format', '{{json .State}}', workerId])).stdout)
  assert.equal(state.ExitCode, 0)
  assert.equal(state.OOMKilled, false)
  pass('M2 worker default startup stays operational-only and shuts down gracefully')
} catch (error) {
  report.failed.push(safe(error.message))
  console.error(`FAIL ${safe(error.message)}`)
  process.exitCode = 1
} finally {
  if (owned) {
    try {
      await compose(['down', '--volumes', '--remove-orphans'])
      assert.equal((await docker(['ps', '-aq', '--filter', `label=com.docker.compose.project=${project}`])).stdout, '')
      assert.equal((await docker(['volume', 'ls', '-q', '--filter', `name=^${project}_postgres-data$`])).stdout, '')
      report.cleanup = 'passed: only fresh task containers, networks and scratch volume removed'
    } catch (error) { report.cleanup = safe(error.message); process.exitCode = 1 }
  }
  await mkdir(new URL('../.artifacts/', import.meta.url), { recursive: true })
  const reportFile = new URL(`../.artifacts/${project}.json`, import.meta.url)
  await writeFile(reportFile, JSON.stringify(report, null, 2) + '\n')
  console.log(`Report: ${fileURLToPath(reportFile)}`)
}
