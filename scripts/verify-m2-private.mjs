import assert from 'node:assert/strict'
import { spawn } from 'node:child_process'
import { randomBytes } from 'node:crypto'
import { mkdir, writeFile } from 'node:fs/promises'
import { fileURLToPath } from 'node:url'

// Explicit authorization only. No provider data is fetched by normal verification/builds.
assert.equal(process.version, 'v24.20.0', 'Use the pinned Node 24.20.0 runtime')
assert.deepEqual(process.argv.slice(2), ['--private-use', '--country', 'XK'], 'Requires --private-use --country XK')
const root = fileURLToPath(new URL('../', import.meta.url))
const project = `analysis-m2-private-${randomBytes(6).toString('hex')}`
const password = randomBytes(32).toString('hex')
const env = { ...process.env, POSTGRES_PASSWORD: password, M2_DB_PASSWORD: password }
const envFile = `.artifacts/${project}.env`
const base = ['compose', '--project-name', project, '--env-file', envFile, '--file', 'compose.yaml', '--file', 'compose.m2-private.yaml']
// Three whole UTC days, ending two days ago: avoid in-progress/recently settling samples.
const end = new Date(); end.setUTCHours(0, 0, 0, 0); end.setUTCDate(end.getUTCDate() - 2)
const start = new Date(end); start.setUTCDate(start.getUTCDate() - 3)
const utc = date => date.toISOString().replace('.000Z', 'Z')
const window = { startUtc: utc(start), endUtc: utc(end) }
const report = { project, mode: 'private-use', country: 'XK', window, passed: [], failed: [], unavailable: [], cleanup: 'pending' }
const safe = value => value.replaceAll(password, '[redacted]')
let owned = false

function docker(args, { input, allowFailure = false } = {}) {
  return new Promise((resolve, reject) => {
    const child = spawn('docker', args, { cwd: root, env, windowsHide: true, stdio: ['pipe', 'pipe', 'pipe'] })
    let stdout = '', stderr = ''
    child.stdout.on('data', chunk => { stdout += chunk })
    child.stderr.on('data', chunk => { stderr += chunk })
    child.on('error', reject)
    child.on('close', code => {
      if (code && !allowFailure) reject(new Error(safe(`docker ${args.join(' ')} failed (${code}): ${stdout.slice(-1800)} ${stderr.slice(-1800)}`)))
      else resolve({ code, stdout: safe(stdout.trim()), stderr: safe(stderr.trim()) })
    })
    child.stdin.end(input)
  })
}
const compose = (args, options) => docker([...base, ...args], options)
const jsonLine = output => JSON.parse(output.split(/\r?\n/).findLast(line => line.startsWith('{')))
function pass(label) { report.passed.push(label); console.log(`PASS ${label}`) }
const checkArgs = ['run', '--rm', '--network', `${project}_data`, '--env', 'M2_DB_PASSWORD', '--env', 'M2_ISOLATED_TEST=true', '--cap-drop', 'ALL', '--security-opt', 'no-new-privileges:true', `${project}-checks`, '--private-snapshot', window.startUtc, window.endUtc]
const command = ['--ingest-once', '--private-use', '--country', 'XK', '--start-utc', window.startUtc, '--end-utc', window.endUtc]
const snapshot = async () => jsonLine((await docker(checkArgs)).stdout)
const ingest = async () => {
  const run = await compose(['run', '--rm', '--no-deps', '-e', 'Postgres__Database=analysis_m2_checks', 'worker', ...command], { allowFailure: true })
  const summary = run.stdout.split(/\r?\n/).map(line => { try { return JSON.parse(line) } catch { return null } }).find(value => value?.mode === 'private-use')
  assert.ok(summary, `No ingestion summary; exit ${run.code}. ${run.stderr.slice(-1000)}`)
  return { exitCode: run.code, ...summary }
}

try {
  assert.equal((await docker(['ps', '-aq', '--filter', `label=com.docker.compose.project=${project}`])).stdout, '')
  assert.equal((await docker(['volume', 'ls', '-q', '--filter', `name=^${project}_postgres-data$`])).stdout, '')
  await mkdir(new URL('../.artifacts/', import.meta.url), { recursive: true })
  await writeFile(new URL(`../${envFile}`, import.meta.url), `POSTGRES_PASSWORD=${password}\n`, { flag: 'wx', mode: 0o600 })
  owned = true
  await compose(['config', '--quiet'])
  await docker(['build', '--file', 'backend/Dockerfile', '--target', 'm2checks', '--tag', `${project}-checks`, '.'])
  await compose(['build', 'worker'])
  pass('Pinned locked build, offline contract/safety checks and EF model consistency')
  await compose(['up', '--detach', '--wait', '--wait-timeout', '90', 'postgres', 'redis'])
  await compose(['exec', '-T', 'postgres', 'psql', '-U', 'analysis', '-d', 'analysis', '-v', 'ON_ERROR_STOP=1'], { input: 'CREATE DATABASE analysis_m2_checks;\n' })
  await compose(['run', '--rm', '--no-deps', '-e', 'Postgres__Database=analysis_m2_checks', 'worker', '--migrate'])
  const uid = await compose(['run', '--rm', '--no-deps', '--entrypoint', 'id', 'worker', '-u'])
  assert.equal(uid.stdout, '1654')
  pass('Explicit EF migrations and non-root private worker; no service ports published')

  report.firstRun = await ingest()
  report.firstSnapshot = await snapshot()
  console.log(`Live read: ${report.firstSnapshot.observationCount} observations; ${report.firstSnapshot.quarantine.length} quarantine entries`)
  pass('Stored observations replay exactly from private payload bytes with decimal, UTC and unit validation')
  if (report.firstRun.exitCode === 0 && report.firstSnapshot.completeCoverage) {
    report.secondRun = await ingest()
    report.secondSnapshot = await snapshot()
    assert.equal(report.secondRun.exitCode, 0, 'Identical-window run failed')
    assert.ok(report.secondRun.results.every(item => item.Inserted === 0 && item.Duplicates > 0), 'Replay must insert no duplicate facts')
    assert.equal(report.secondSnapshot.observationSnapshot, report.firstSnapshot.observationSnapshot)
    assert.ok(report.secondSnapshot.completeCoverage)
    pass('All eleven required data series have coverage; identical-window ingestion preserves exact observations')
  } else {
    report.unavailable.push('Full live coverage and second live run: inspect safe provider errors/coverage below; no regional bypass or automatic repeated batch')
    process.exitCode = 1
  }

  const before = await snapshot()
  await compose(['run', '--rm', '--no-deps', '-e', 'Postgres__Database=analysis_m2_checks', 'worker', '--migrate'])
  assert.equal((await snapshot()).databaseSnapshot, before.databaseSnapshot)
  await compose(['up', '--detach', '--no-deps', '--force-recreate', '--wait', '--wait-timeout', '90', 'postgres'])
  assert.equal((await snapshot()).databaseSnapshot, before.databaseSnapshot)
  pass('Migrations and PostgreSQL recreation preserve the full private database snapshot')
} catch (error) {
  report.failed.push(safe(error.message)); console.error(`FAIL ${safe(error.message)}`); process.exitCode = 1
} finally {
  if (owned) {
    try {
      // Keep the newly collected private database and its ignored local configuration.
      await compose(['down', '--timeout', '30'])
      assert.equal((await docker(['ps', '-aq', '--filter', `label=com.docker.compose.project=${project}`])).stdout, '')
      const volume = (await docker(['volume', 'ls', '-q', '--filter', `name=^${project}_postgres-data$`])).stdout
      report.retainedVolume = volume || null
      report.localConfig = envFile
      report.cleanup = 'passed: task containers/networks stopped and removed; private volume retained; unrelated resources untouched'
    } catch (error) { report.cleanup = safe(error.message); process.exitCode = 1 }
  }
  await mkdir(new URL('../.artifacts/', import.meta.url), { recursive: true })
  const file = new URL(`../.artifacts/${project}.json`, import.meta.url)
  await writeFile(file, JSON.stringify(report, null, 2) + '\n')
  console.log(`Report: ${fileURLToPath(file)}`)
}
