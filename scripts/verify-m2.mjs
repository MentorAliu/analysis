import assert from 'node:assert/strict'
import { spawn } from 'node:child_process'
import { randomBytes } from 'node:crypto'
import { mkdir, writeFile } from 'node:fs/promises'
import { fileURLToPath } from 'node:url'
import { setTimeout as delay } from 'node:timers/promises'

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

  // Hold a catalog read inside this disposable database so SIGTERM exercises the
  // real one-shot host before it can construct a provider transport. No egress.
  const sql = query => compose(['exec', '-T', 'postgres', 'psql', '-U', 'analysis', '-d', 'analysis_m2_checks', '-At', '-v', 'ON_ERROR_STOP=1'], { input: query })
  const locker = compose(['exec', '-T', 'postgres', 'psql', '-U', 'analysis', '-d', 'analysis_m2_checks', '-v', 'ON_ERROR_STOP=1'], {
    input: 'SET application_name = \'m2-cancellation-lock\'; BEGIN; LOCK TABLE research."ProviderInstrumentRefs" IN ACCESS EXCLUSIVE MODE; SELECT pg_sleep(30); ROLLBACK;\n', allowFailure: true,
  })
  try {
    let locked = false
    for (let attempt = 0; attempt < 50; attempt++) {
      locked = (await sql("SELECT count(*) FROM pg_locks l JOIN pg_stat_activity a ON a.pid=l.pid WHERE a.application_name='m2-cancellation-lock' AND l.relation='research.\"ProviderInstrumentRefs\"'::regclass AND l.granted;\n")).stdout === '1'
      if (locked) break
      await delay(100)
    }
    assert.ok(locked, 'Disposable catalog lock acquired')
    const cancelling = (await compose(['run', '--detach', '--no-deps', '-e', 'Postgres__Database=analysis_m2_checks', 'worker', '--ingest-once', '--private-use', '--country', 'XK', '--start-utc', '2021-01-01T00:00:00Z', '--end-utc', '2021-01-02T00:00:00Z'])).stdout
    let waiting = false
    for (let attempt = 0; attempt < 50; attempt++) {
      waiting = (await sql("SELECT count(*) FROM pg_stat_activity WHERE datname='analysis_m2_checks' AND wait_event_type='Lock' AND query LIKE '%ProviderInstrumentRefs%';\n")).stdout === '1'
      if (waiting) break
      await delay(100)
    }
    assert.ok(waiting, 'One-shot waiting on catalog before provider access')
    await docker(['stop', '--time', '10', cancelling])
    const cancelled = JSON.parse((await docker(['inspect', '--format', '{{json .State}}', cancelling])).stdout)
    assert.equal(cancelled.ExitCode, 130)
    assert.equal(cancelled.OOMKilled, false)
    const logs = (await docker(['logs', cancelling])).stdout
    assert.match(logs, /M2 one-shot operation cancelled/)
    assert.match(logs, /RunId/)
    assert.match(logs, /CorrelationId/)
    assert.doesNotMatch(logs, /Starting bounded private research ingestion/)
    await docker(['rm', cancelling])
    pass('Private one-shot SIGTERM cancels blocked database I/O with correlated logs, exit 130 and no provider access')
  } finally {
    await sql("SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE application_name='m2-cancellation-lock' AND datname='analysis_m2_checks';\n")
    await locker
  }
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
