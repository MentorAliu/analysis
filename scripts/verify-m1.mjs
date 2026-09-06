import assert from 'node:assert/strict'
import { spawn } from 'node:child_process'
import { randomBytes } from 'node:crypto'
import { mkdir, writeFile } from 'node:fs/promises'
import { createServer } from 'node:net'
import { fileURLToPath } from 'node:url'
import { setTimeout as delay } from 'node:timers/promises'

// Always create a fresh project. Never interrupt a developer's existing stack.
const root = fileURLToPath(new URL('../', import.meta.url))
const project = `analysis-m1-check-${randomBytes(6).toString('hex')}`
const env = { ...process.env, POSTGRES_PASSWORD: randomBytes(32).toString('hex'), FRONTEND_PORT: String(await freePort()), API_PORT: String(await freePort()) }
const base = ['compose', '--project-name', project, '--file', 'compose.yaml']
const report = { project, node: process.version, passed: [], failed: [], cleanup: 'pending' }
const api = `http://127.0.0.1:${env.API_PORT}`
const frontend = `http://127.0.0.1:${env.FRONTEND_PORT}`
let owned = false

function docker(args, { input, allowFailure = false } = {}) {
  return new Promise((resolve, reject) => {
    const child = spawn('docker', args, { cwd: root, env, windowsHide: true, stdio: ['pipe', 'pipe', 'pipe'] })
    let stdout = '', stderr = ''
    child.stdout.on('data', data => { stdout += data })
    child.stderr.on('data', data => { stderr += data })
    child.on('error', reject)
    child.on('close', code => {
      if (code !== 0 && !allowFailure) reject(new Error(`docker ${args.filter(x => x !== env.POSTGRES_PASSWORD).join(' ')} failed (${code}): ${stderr.slice(-3000)}`))
      else resolve({ code, stdout: stdout.trim(), stderr: stderr.trim() })
    })
    child.stdin.end(input)
  })
}
const compose = (args, options) => docker([...base, ...args], options)
function pass(name) { report.passed.push(name); console.log(`PASS ${name}`) }
async function eventually(action, label) {
  const deadline = Date.now() + 45000
  let last
  do {
    try { return await action() } catch (error) { last = error; await delay(1000) }
  } while (Date.now() < deadline)
  throw new Error(`${label}: ${last?.message}`)
}
async function get(path, origin = api, options = {}) {
  const response = await fetch(origin + path, { ...options, signal: AbortSignal.timeout(6000) })
  return { status: response.status, headers: response.headers, body: await response.json() }
}
async function worker(kind = 'ready') {
  const result = await compose(['exec', '-T', 'worker', 'dotnet', 'Analysis.Worker.dll', '--healthcheck', `/health/${kind}`], { allowFailure: true })
  return { status: result.code === 0 ? 200 : 503, body: JSON.parse(result.stdout) }
}
async function checkBoth(status, overall, dependency, dependencyStatus) {
  for (const response of [await get('/api/health/ready'), await worker()]) {
    assert.equal(response.status, status)
    assert.equal(response.body.status, overall)
    if (dependency) assert.equal(response.body.checks[dependency].status, dependencyStatus)
    assert.match(response.body.checkedAtUtc, /(?:Z|\+00:00)$/)
  }
  assert.equal((await get('/api/health/live')).status, 200)
  assert.equal((await worker('live')).status, 200)
}
async function ids() { return (await compose(['ps', '--all', '--quiet'])).stdout.split(/\s+/).filter(Boolean) }
async function inspect() { return JSON.parse((await docker(['inspect', ...await ids()])).stdout) }
async function freePort() {
  const server = createServer()
  await new Promise((resolve, reject) => { server.once('error', reject); server.listen(0, '127.0.0.1', resolve) })
  const port = server.address().port
  await new Promise(resolve => server.close(resolve))
  return port
}

try {
  assert.equal((await docker(['ps', '-aq', '--filter', `label=com.docker.compose.project=${project}`])).stdout, '')
  assert.equal((await docker(['volume', 'ls', '-q', '--filter', `label=com.docker.compose.project=${project}`])).stdout, '')
  owned = true
  await compose(['config', '--quiet'])
  console.log(`Starting isolated project ${project}; application ports ${env.FRONTEND_PORT}/${env.API_PORT}`)
  await compose(['up', '-d', '--build', '--wait', '--wait-timeout', '120'])
  const initial = await inspect()
  assert.equal(initial.length, 5)
  assert(initial.every(container => container.State.Health?.Status === 'healthy'))
  pass('Compose configuration, locked builds and five healthy services')
  await checkBoth(200, 'Healthy')
  const earlier = (await worker()).body.checks['worker-loop'].data.lastProgressUtc
  await delay(2500)
  assert.notEqual((await worker()).body.checks['worker-loop'].data.lastProgressUtc, earlier)
  pass('API/worker live and ready; worker loop advances; UTC health timestamps')

  const openapi = await get('/api/openapi/v1.json')
  assert.equal(openapi.status, 200)
  assert.match(openapi.body.openapi, /^3\.1\./)
  assert.deepEqual(Object.keys(openapi.body.paths).sort(), ['/api/health/live', '/api/health/ready'])
  assert.equal((await get('/api/openapi/v1.json', frontend)).status, 200)
  assert.equal((await get('/api/health/ready', frontend)).body.status, 'Healthy')
  assert.equal((await fetch(frontend, { signal: AbortSignal.timeout(6000) })).status, 200)
  pass('Development OpenAPI 3.1 health contract, Vite /api proxy and frontend HTTP loading')

  const correlation = 'm1-system-check'
  const missing = await get('/api/missing', api, { headers: { 'X-Correlation-ID': correlation, Accept: 'application/json' } })
  assert.equal(missing.status, 404)
  assert.match(missing.headers.get('content-type'), /application\/problem\+json/)
  assert.equal(missing.headers.get('X-Correlation-ID'), correlation)
  assert.equal(missing.body.correlationId, correlation)
  assert.equal(missing.body.status, 404)
  assert.equal(missing.body.traceId.length, 32)
  pass('HTTP problem details and request correlation')

  for (const service of ['api', 'worker', 'frontend']) {
    assert.notEqual((await compose(['exec', '-T', service, 'id', '-u'])).stdout, '0')
  }
  for (const container of initial.filter(item => ['postgres', 'redis', 'worker'].includes(item.Config.Labels['com.docker.compose.service']))) {
    assert(Object.values(container.NetworkSettings.Ports ?? {}).every(binding => binding === null))
  }
  const dataNetwork = JSON.parse((await docker(['network', 'inspect', `${project}_data`])).stdout)[0]
  assert.equal(dataNetwork.Internal, true)
  const pg = initial.find(item => item.Config.Labels['com.docker.compose.service'] === 'postgres')
  assert(pg.Mounts.some(mount => mount.Type === 'volume' && mount.Destination === '/var/lib/postgresql'))
  assert(pg.Config.Env.includes('PGDATA=/var/lib/postgresql/18/docker'))
  const sql = async text => (await compose(['exec', '-T', 'postgres', 'psql', '-v', 'ON_ERROR_STOP=1', '-U', 'analysis', '-d', 'analysis', '-At'], { input: text })).stdout
  assert.equal(await sql('SHOW timezone;'), 'UTC')
  pass('Non-root application users, private data services and PostgreSQL 18 volume/UTC layout')

  await compose(['stop', 'redis'])
  await eventually(() => checkBoth(200, 'Degraded', 'redis', 'Degraded'), 'Redis outage')
  await compose(['start', 'redis'])
  await eventually(() => checkBoth(200, 'Healthy'), 'Redis recovery')
  pass('Redis loss degrades readiness without failing liveness; automatic recovery')

  await compose(['stop', 'postgres'])
  await eventually(() => checkBoth(503, 'Unhealthy', 'postgres', 'Unhealthy'), 'PostgreSQL outage')
  await compose(['start', 'postgres'])
  await eventually(() => checkBoth(200, 'Healthy'), 'PostgreSQL recovery')
  for (const service of ['api', 'worker']) {
    const current = (await inspect()).find(item => item.Config.Labels['com.docker.compose.service'] === service)
    assert.equal(current.Id, initial.find(item => item.Config.Labels['com.docker.compose.service'] === service).Id)
    assert.equal(current.RestartCount, 0)
  }
  pass('PostgreSQL loss fails readiness while liveness stays healthy; recovery without host restart')

  // This SQL exists only in the new disposable verification database, never in application startup/migrations.
  const sentinel = randomBytes(16).toString('hex')
  await sql(`CREATE TABLE m1_persistence_probe (value text NOT NULL); INSERT INTO m1_persistence_probe VALUES ('${sentinel}');`)
  await compose(['up', '-d', '--no-deps', '--force-recreate', '--wait', '--wait-timeout', '60', 'postgres'])
  assert.notEqual((await inspect()).find(item => item.Config.Labels['com.docker.compose.service'] === 'postgres').Id, pg.Id)
  assert.equal(await sql('SELECT value FROM m1_persistence_probe;'), sentinel)
  await sql('DROP TABLE m1_persistence_probe;')
  await eventually(() => checkBoth(200, 'Healthy'), 'PostgreSQL recreation recovery')
  pass('PostgreSQL committed sentinel survives container recreation on the same volume')

  await compose(['stop', '--timeout', '15'])
  const stopped = await inspect()
  report.shutdown = stopped.map(item => ({ service: item.Config.Labels['com.docker.compose.service'], exitCode: item.State.ExitCode, status: item.State.Status, oomKilled: item.State.OOMKilled }))
  // Vite 8.2.2 awaits server.close(), then intentionally preserves SIGTERM's
  // conventional 128 + 15 exit status. Other services must exit zero; no SIGKILL.
  assert(report.shutdown.every(item => item.status === 'exited' && !item.oomKilled &&
    (item.service === 'frontend' ? [0, 143].includes(item.exitCode) : item.exitCode === 0)), JSON.stringify(report.shutdown))
  const logs = (await compose(['logs', '--no-color', '--no-log-prefix', 'api', 'worker'])).stdout
  assert(!logs.includes(env.POSTGRES_PASSWORD))
  const entries = logs.split('\n').filter(line => line.startsWith('{')).map(line => JSON.parse(line))
  assert(entries.length > 0 && entries.every(entry => /Z$/.test(entry.Timestamp)))
  assert(entries.some(entry => entry.Scopes?.some(scope => scope.CorrelationId === correlation)))
  const started = entries.find(entry => entry.Message?.startsWith('Worker lifecycle started'))
  const ended = entries.find(entry => entry.Message === 'Worker lifecycle stopped gracefully')
  assert(started && ended)
  assert.equal(started.Scopes.find(scope => scope.RunId)?.RunId, ended.Scopes.find(scope => scope.RunId)?.RunId)
  pass('All five services exit cleanly; worker cancellation and correlated UTC JSON logs; no password leakage')
} catch (error) {
  report.failed.push(error.message.replaceAll(env.POSTGRES_PASSWORD, '[redacted]'))
  console.error(`FAIL ${report.failed.at(-1)}`)
  process.exitCode = 1
} finally {
  if (owned) {
    // The random project was proven absent before creation; only its new scratch volume is removed.
    await compose(['down', '--volumes', '--timeout', '15'])
    assert.equal((await docker(['ps', '-aq', '--filter', `label=com.docker.compose.project=${project}`])).stdout, '')
    assert.equal((await docker(['volume', 'ls', '-q', '--filter', `label=com.docker.compose.project=${project}`])).stdout, '')
    report.cleanup = 'passed: task-created containers, networks and scratch volume removed'
  }
  await mkdir(new URL('../.artifacts/', import.meta.url), { recursive: true })
  await writeFile(new URL(`../.artifacts/${project}.json`, import.meta.url), JSON.stringify(report, null, 2) + '\n')
  console.log(`Report: .artifacts/${project}.json`)
}
