import assert from 'node:assert/strict'
import { spawn } from 'node:child_process'
import { randomBytes } from 'node:crypto'
import { mkdir, readFile, writeFile, rm } from 'node:fs/promises'
import { resolve, sep } from 'node:path'
import { fileURLToPath } from 'node:url'
import { createServer } from 'node:net'
import { setTimeout as delay } from 'node:timers/promises'

// No provider requests. Every database, process, network and volume is task-owned.
assert.equal(process.version, 'v24.20.0', 'Use pinned Node 24.20.0')
const local = process.argv.slice(2).join(' ') === '--local'
assert.ok(local || process.argv.length === 2, 'Usage: node scripts/verify-1a.mjs [--local]')
const root = fileURLToPath(new URL('../', import.meta.url))
const project = `analysis-1a-check-${randomBytes(6).toString('hex')}`
const artifact = resolve(root, '.artifacts', project)
const password = randomBytes(32).toString('hex')
const env = { ...process.env, FORWARD_DB_PASSWORD: password, FORWARD_ISOLATED_TEST: 'true',
  Postgres__Password: password, Postgres__Database: 'analysis_1a_checks', POSTGRES_PASSWORD: password,
  ASPNETCORE_ENVIRONMENT: 'Development', Rankings__PrivateUseEnabled: 'true' }
const report = { project, runtime: local ? 'portable Windows PostgreSQL 18.6' : 'pinned Compose images', passed: [], failed: [], unavailable: [], cleanup: 'pending' }
const safe = value => String(value).replaceAll(password, '[redacted]')
let sequence = 0, started = false, owned = false
const children = []
const helpers = []
const dotnet = resolve(root, '.artifacts/dotnet-10.0.400/dotnet.exe')
const pg = resolve(root, '.artifacts/postgresql-18.6/pgsql/bin')
const dbPath = resolve(artifact, 'data')
const checksDll = resolve(root, 'backend/tests/Analysis.ForwardChecks/bin/Release/net10.0/Analysis.ForwardChecks.dll')
const workerDll = resolve(root, 'backend/src/Analysis.Worker/bin/Release/net10.0/Analysis.Worker.dll')
const base = ['compose', '--project-name', project, '--env-file', resolve(artifact, 'test.env'), '--file', 'compose.yaml']
const pass = message => { report.passed.push(message); console.log(`PASS ${message}`) }
const jsonLine = output => JSON.parse(output.split(/\r?\n/).findLast(line => line.startsWith('{')))
async function run(command, args, options = {}) {
  const result = await new Promise((resolveResult, reject) => {
    const child = spawn(command, args, { cwd: options.cwd ?? root, env, windowsHide: true, stdio: options.detachedStartup ? 'ignore' : ['pipe', 'pipe', 'pipe'] })
    let stdout = '', stderr = ''
    const timer = setTimeout(() => child.kill(), options.timeoutMs ?? 900_000)
    child.stdout?.on('data', chunk => { stdout += chunk }); child.stderr?.on('data', chunk => { stderr += chunk })
    child.on('error', error => { clearTimeout(timer); reject(error) })
    child.on('close', code => { clearTimeout(timer); resolveResult({ code, stdout: safe(stdout.trim()), stderr: safe(stderr.trim()) }) })
    child.stdin?.end(options.input)
  })
  await writeFile(resolve(artifact, `command-${++sequence}.log`), safe(`${command} ${args.join(' ')}\nexit=${result.code}\n${result.stdout}\n${result.stderr}\n`))
  if (result.code !== 0 && !options.allowFailure) throw new Error(`${command} failed (${result.code}): ${result.stdout.slice(-2000)} ${result.stderr.slice(-3500)}`)
  return result
}
const docker = (args, options) => run('docker', args, options)
const compose = (args, options) => docker([...base, ...args], options)
const checkArgs = ['run', '--rm', '--network', `${project}_data`, '--env', 'FORWARD_DB_PASSWORD', '--env', 'FORWARD_ISOLATED_TEST=true',
  '--cap-drop', 'ALL', '--security-opt', 'no-new-privileges:true', `${project}-checks`]
const check = args => local ? run(dotnet, [checksDll, ...args]) : docker([...checkArgs, ...args])
const worker = args => local ? run(dotnet, [workerDll, ...args]) : compose(['run', '--rm', '--no-deps', '-e', 'Postgres__Database=analysis_1a_checks', 'worker', ...args])
const snapshot = async () => jsonLine((await check(['--snapshot'])).stdout).snapshot
async function freePort() {
  const server = createServer(); await new Promise(resolveReady => server.listen(0, '127.0.0.1', resolveReady))
  const port = server.address().port; await new Promise(resolveClosed => server.close(resolveClosed)); return port
}
async function localHost(dll, port, path) {
  const child = spawn(dotnet, [dll, '--urls', `http://127.0.0.1:${port}`], { cwd: root, env, windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] })
  children.push(child); let output = ''; child.stdout.on('data', c => { output += c }); child.stderr.on('data', c => { output += c })
  child.on('error', e => { output += e.message })
  for (let i = 0; i < 100; i++) {
    try { const r = await fetch(`http://127.0.0.1:${port}${path}`, { signal: AbortSignal.timeout(1000) }); if (r.ok) return r }
    catch { /* readiness has a bounded deadline */ }
    if (child.exitCode !== null) throw new Error(safe(output.slice(-2500)))
    await delay(100)
  }
  throw new Error(`Host readiness timed out: ${safe(output.slice(-2500))}`)
}
try {
  await mkdir(artifact, { recursive: false })
  if (local) {
    assert.equal(process.platform, 'win32', 'Portable runtime path is Windows-specific')
    assert.match((await run(resolve(pg, 'postgres.exe'), ['--version'])).stdout, /18\.6/)
    assert.equal((await run(dotnet, ['--version'])).stdout, '10.0.400')
    Object.assign(env, { DOTNET_ROOT: resolve(root, '.artifacts/dotnet-10.0.400'), DOTNET_CLI_HOME: resolve(root, '.artifacts/dotnet-home'),
      NUGET_PACKAGES: resolve(root, '.artifacts/nuget'), DOTNET_CLI_TELEMETRY_OPTOUT: '1' })
    env.PATH = env.DOTNET_ROOT + ';' + env.PATH
    await run(dotnet, ['restore', 'backend/Analysis.slnx', '--locked-mode'])
    await run(dotnet, ['build', 'backend/Analysis.slnx', '-c', 'Release', '--no-restore'])
    for (const name of ['Operational', 'Catalog', 'Scoring', 'Rankings', 'Forward'])
      await run(dotnet, [resolve(root, `backend/tests/Analysis.${name}Checks/bin/Release/net10.0/Analysis.${name}Checks.dll`)])
    await run(dotnet, ['tool', 'restore'], { cwd: resolve(root, 'backend') })
    await run(dotnet, ['ef', 'migrations', 'has-pending-model-changes', '--project', 'src/Analysis.Infrastructure', '--configuration', 'Release', '--no-build'], { cwd: resolve(root, 'backend') })
    pass('Pinned locked build, M1–M4 and 1A offline executable checks, EF model consistency')
    const port = await freePort()
    Object.assign(env, { FORWARD_DB_HOST: '127.0.0.1', FORWARD_DB_PORT: String(port), Postgres__Host: `127.0.0.1:${port}`, PGPASSWORD: password })
    const pw = resolve(artifact, 'password.txt'); await writeFile(pw, password, { flag: 'wx' })
    await run(resolve(pg, 'initdb.exe'), ['-D', dbPath, '-U', 'analysis', '--encoding=UTF8', '--locale=C', '--auth=scram-sha-256', `--pwfile=${pw}`])
    owned = true
    await run(resolve(pg, 'pg_ctl.exe'), ['-D', dbPath, '-l', resolve(artifact, 'postgres.log'), '-o', `-h 127.0.0.1 -p ${port} -c timezone=UTC -c log_timezone=UTC`, '-w', 'start'], { detachedStartup: true })
    started = true
    await run(resolve(pg, 'createdb.exe'), ['-h', '127.0.0.1', '-p', String(port), '-U', 'analysis', 'analysis_1a_checks'])
  } else {
    assert.equal((await docker(['ps', '-aq', '--filter', `label=com.docker.compose.project=${project}`])).stdout, '')
    assert.equal((await docker(['volume', 'ls', '-q', '--filter', `name=^${project}_postgres-data$`])).stdout, '')
    await writeFile(resolve(artifact, 'test.env'), `POSTGRES_PASSWORD=${password}\n`, { flag: 'wx', mode: 0o600 }); owned = true
    await docker(['build', '-f', 'backend/Dockerfile', '--target', 'forwardchecks', '-t', `${project}-checks`, '.'])
    await compose(['build', 'worker'])
    await compose(['up', '-d', '--wait', '--wait-timeout', '90', 'postgres', 'redis'])
    started = true
    await compose(['exec', '-T', 'postgres', 'psql', '-X', '-v', 'ON_ERROR_STOP=1', '-U', 'analysis', '-d', 'analysis', '-c', 'CREATE DATABASE analysis_1a_checks'])
    pass('Pinned locked container build and offline executable checks')
  }
  report.database = jsonLine((await check(['--database-checks'])).stdout)
  pass('Empty/populated migrations, downgrade/reapply, concurrency, rollback, all horizons, revisions, frozen replay and SQL guards')
  const args = ['--private-use', '--country', 'XK', '--start-utc', '2021-01-08T00:00:00Z', '--end-utc', '2021-01-08T01:00:00Z']
  const before = await snapshot()
  const inspection = jsonLine((await worker(['--inspect-forward-records', ...args])).stdout).report
  assert.equal(inspection.records.length, 1); assert.equal(inspection.records[0].history.length, 26)
  assert.equal(typeof inspection.records[0].outcomes.find(o => o.result.return !== null).result.return, 'string')
  assert.equal(await snapshot(), before)
  pass('Actual worker inspection emits decimal strings and immutable history without writes')
  const issue = jsonLine((await worker(['--issue-forward-once', '--private-use', '--country', 'XK', '--as-of-utc', '2021-01-08T00:00:00Z', '--model', 'slice1-v1'])).stdout).result
  assert.equal(issue.duplicate, true)
  pass('Uncertain-response retry through actual worker returns the original issuance')
  const stable = await snapshot()
  if (local) {
    await run(resolve(pg, 'pg_ctl.exe'), ['-D', dbPath, '-m', 'fast', '-w', 'stop'])
    await run(resolve(pg, 'pg_ctl.exe'), ['-D', dbPath, '-l', resolve(artifact, 'postgres.log'), '-o', `-h 127.0.0.1 -p ${env.FORWARD_DB_PORT} -c timezone=UTC -c log_timezone=UTC`, '-w', 'start'], { detachedStartup: true })
    assert.equal(await snapshot(), stable)
    pass('PostgreSQL restart preserves exact persisted replay')
    const apiPort = await freePort(); const specResponse = await localHost(resolve(root, 'backend/src/Analysis.Api/bin/Release/net10.0/Analysis.Api.dll'), apiPort, '/api/openapi/v1.json')
    const spec = await specResponse.json(); const committed = JSON.parse(await readFile(resolve(root, 'contracts/openapi/v1.json'), 'utf8'))
    assert.deepEqual(spec, committed)
    const ranking = await fetch(`http://127.0.0.1:${apiPort}/api/v1/rankings?asOfUtc=2021-01-08T00:00:00Z`)
    assert.equal(ranking.status, 200); assert.equal((await ranking.json()).batch.recordKind, 'research-reconstruction')
    await localHost(workerDll, await freePort(), '/health/live')
    assert.equal(await snapshot(), stable)
    await run(process.execPath, ['frontend/scripts/api-contract.mjs', 'check'])
    pass('API/default worker do not issue or acquire; M4 historical label and OpenAPI/generated contract unchanged; Redis absent')
    report.unavailable.push('Linux container build/security checks and POSIX SIGTERM require the Docker engine; portable runtime does not replace those checks')
  } else {
    await compose(['stop', 'redis'])
    await worker(['--inspect-forward-records', ...args]); assert.equal(await snapshot(), stable)
    await compose(['up', '-d', '--force-recreate', '--wait', '--wait-timeout', '90', 'postgres'])
    await worker(['--inspect-forward-records', ...args]); assert.equal(await snapshot(), stable)
    pass('Redis outage and PostgreSQL restart preserve exact forward replay')
    const lockName = `${project}-lock`, workerName = `${project}-signal`; helpers.push(lockName, workerName)
    await docker(['run', '-d', '--name', lockName, ...checkArgs.slice(2, -1), `${project}-checks`, '--hold-issuance-lock'])
    for (let n = 0; ; n++) {
      if ((await docker(['logs', lockName])).stdout.includes('"held":true')) break
      assert.ok(n < 30, 'Test lock ready'); await delay(100)
    }
    await compose(['run', '-d', '--no-deps', '--name', workerName, '-e', 'Postgres__Database=analysis_1a_checks', 'worker',
      '--issue-forward-once', '--private-use', '--country', 'XK', '--as-of-utc', '2021-01-17T04:00:00Z', '--model', 'slice1-v1'])
    for (let n = 0; ; n++) {
      const blocked = await compose(['exec', '-T', 'postgres', 'psql', '-X', '-At', '-U', 'analysis', '-d', 'analysis_1a_checks', '-c',
        "SELECT count(*) FROM pg_stat_activity WHERE datname='analysis_1a_checks' AND wait_event_type='Lock' AND query LIKE '%pg_advisory_xact_lock%'"])
      if (Number(blocked.stdout) > 0) break
      assert.ok(n < 15, 'Actual worker reached locked publication'); await delay(50)
    }
    await docker(['kill', '--signal', 'TERM', workerName]); assert.equal((await docker(['wait', workerName])).stdout, '130')
    await check(['--check-signal-cancellation'])
    pass('Actual worker SIGTERM cancels blocked publication, audits cancellation and leaves no partial bundle')
    assert.notEqual((await docker(['run', '--rm', '--network', 'none', '--entrypoint', 'id', `${project}-worker`, '-u'])).stdout, '0')
    assert.equal((await docker(['run', '--rm', '--network', 'none', '--entrypoint', 'find', `${project}-worker`, '/app', '-iname', '*Checks*', '-o', '-iname', '*fixture*'])).stdout, '')
    pass('Non-root worker image excludes all test assemblies and fixtures')
  }
} catch (error) { report.failed.push(safe(error.stack)); process.exitCode = 1; console.error(safe(error.message)) }
finally {
  try {
    for (const child of children) { if (child.exitCode === null) { child.kill(); await new Promise(resolveClosed => child.once('close', resolveClosed)) } }
    if (local && owned) {
      if (started) await run(resolve(pg, 'pg_ctl.exe'), ['-D', dbPath, '-m', 'fast', '-w', 'stop'])
      assert.ok(dbPath.startsWith(resolve(root, '.artifacts') + sep) && dbPath === resolve(artifact, 'data'))
      await rm(dbPath, { recursive: true, force: true }); await rm(resolve(artifact, 'password.txt'), { force: true })
    } else if (owned) {
      for (const name of helpers) { assert.ok(name.startsWith(`${project}-`)); await docker(['rm', '--force', name], { allowFailure: true }) }
      await compose(['down', '--volumes', '--timeout', '30'])
      assert.equal((await docker(['ps', '-aq', '--filter', `label=com.docker.compose.project=${project}`])).stdout, '')
      assert.equal((await docker(['volume', 'ls', '-q', '--filter', `name=^${project}_postgres-data$`])).stdout, '')
      await rm(resolve(artifact, 'test.env'), { force: true })
    }
    report.cleanup = 'passed: task processes and disposable data removed; retained databases untouched'
  } catch (error) { report.cleanup = safe(error.message); process.exitCode = 1 }
  await writeFile(resolve(artifact, 'report.json'), JSON.stringify(report, null, 2) + '\n')
  console.log(`Report: ${resolve(artifact, 'report.json')}`)
}
