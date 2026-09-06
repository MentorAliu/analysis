import assert from 'node:assert/strict'
import { spawn } from 'node:child_process'
import { randomBytes } from 'node:crypto'
import { mkdir, writeFile, readFile } from 'node:fs/promises'
import { createServer } from 'node:net'
import { get as httpGet } from 'node:http'
import { fileURLToPath } from 'node:url'
import { setTimeout as delay } from 'node:timers/promises'

assert.equal(process.version, 'v24.20.0', 'Use pinned Node 24.20.0')
const root = fileURLToPath(new URL('../', import.meta.url))
const project = `analysis-m4-check-${randomBytes(6).toString('hex')}`
const password = randomBytes(32).toString('hex')
const artifact = new URL(`../.artifacts/${project}/`, import.meta.url)
const envFile = `.artifacts/${project}/test.env`, override = `.artifacts/${project}/compose.yaml`
const env = { ...process.env, POSTGRES_PASSWORD: password, Postgres__Password: password, M4_DB_PASSWORD: password, FRONTEND_PORT: String(await freePort()), API_PORT: String(await freePort()) }
const base = ['compose', '--project-name', project, '--env-file', envFile, '--file', 'compose.yaml', '--file', override]
const report = { project, passed: [], failed: [], unavailable: [], skipped: ['Private acquisitions and retained databases are outside M4 verification'], cleanup: 'pending' }
let owned = false, sequence = 0
const helpers = []
const safe = text => String(text).replaceAll(password, '[redacted]')
async function run(command, args, options = {}) {
  const result = await new Promise((resolve, reject) => {
    const child = spawn(command, args, { cwd: root, env, windowsHide: true, stdio: ['pipe', 'pipe', 'pipe'] })
    let stdout = '', stderr = ''
    const timer = setTimeout(() => child.kill(), options.timeoutMs ?? 900_000)
    child.on('error', error => { clearTimeout(timer); reject(error) })
    child.stdout.on('data', data => { stdout += data }); child.stderr.on('data', data => { stderr += data })
    child.on('close', code => { clearTimeout(timer); resolve({ code, stdout: safe(stdout.trim()), stderr: safe(stderr.trim()) }) })
    child.stdin.end(options.input)
  })
  await writeFile(new URL(`command-${++sequence}.log`, artifact), safe(`${command} ${args.join(' ')}\nexit=${result.code}\n${result.stdout}\n${result.stderr}\n`))
  if (result.code !== 0 && !options.allowFailure) throw new Error(`${command} failed (${result.code}): ${result.stdout.slice(-2000)} ${result.stderr.slice(-2500)}`)
  return result
}
const docker = (args, options) => run('docker', args, options)
const compose = (args, options) => docker([...base, ...args], options)
const sql = statement => compose(['exec', '-T', 'postgres', 'psql', '-X', '-v', 'ON_ERROR_STOP=1', '-At', '-U', 'analysis', '-d', 'analysis_m4_checks'], { input: statement })
const pass = value => { report.passed.push(value); console.log(`PASS ${value}`) }
const baseUrl = `http://127.0.0.1:${env.API_PORT}`
async function get(path, { origin = baseUrl, ...options } = {}) {
  const response = await fetch(origin + path, { signal: AbortSignal.timeout(6000), ...options })
  const text = await response.text()
  return { status: response.status, headers: response.headers, body: response.headers.get('content-type')?.includes('json') ? JSON.parse(text) : text }
}
async function freePort() {
  const server = createServer(); await new Promise(resolve => server.listen(0, '127.0.0.1', resolve))
  const port = server.address().port; await new Promise(resolve => server.close(resolve)); return port
}
const schemaNormalize = value => Array.isArray(value) ? value.map(schemaNormalize) : value && typeof value === 'object'
  ? Object.fromEntries(Object.entries(value).sort(([a], [b]) => a < b ? -1 : a > b ? 1 : 0).map(([key, item]) => [key, schemaNormalize(item)])) : value
try {
  await mkdir(artifact, { recursive: true })
  assert.equal((await docker(['ps', '-aq', '--filter', `label=com.docker.compose.project=${project}`])).stdout, '')
  assert.equal((await docker(['volume', 'ls', '-q', '--filter', `name=^${project}_postgres-data$`])).stdout, '')
  await writeFile(new URL('test.env', artifact), `POSTGRES_PASSWORD=${password}\n`, { flag: 'wx', mode: 0o600 })
  await writeFile(new URL('compose.yaml', artifact), 'services:\n  api:\n    environment:\n      Postgres__Database: analysis_m4_checks\n')
  owned = true
  await compose(['config', '--quiet'])
  await docker(['build', '--file', 'backend/Dockerfile', '--target', 'm4checks', '--tag', `${project}-m4checks`, '.'])
  await compose(['build', 'api', 'frontend'])
  pass('Pinned locked builds and M1/M2/M3/M4 executable checks')
  await compose(['up', '-d', '--wait', '--wait-timeout', '90', 'postgres', 'redis'])
  await compose(['exec', '-T', 'postgres', 'psql', '-X', '-v', 'ON_ERROR_STOP=1', '-U', 'analysis', '-d', 'analysis', '-c', 'CREATE DATABASE analysis_m4_checks'])
  await compose(['up', '-d', '--wait', '--wait-timeout', '90', 'api', 'frontend'])
  assert.equal((await get('/api/v1/rankings')).status, 503)
  assert.equal((await get('/api/v1/rankings?sort=asc')).status, 400)
  const database = await docker(['run', '--rm', '--network', `${project}_data`, '--env', 'M4_DB_PASSWORD', '--env', 'M4_ISOLATED_TEST=true',
    '--cap-drop', 'ALL', '--security-opt', 'no-new-privileges:true', `${project}-m4checks`, '--database-checks'])
  report.database = JSON.parse(database.stdout.split(/\r?\n/).findLast(line => line.startsWith('{')))
  pass('Isolated PostgreSQL schema/read integrity, model isolation, concurrent publication, readiness and cancellation')
  const spec = await get('/api/openapi/v1.json')
  assert.equal(spec.status, 200)
  const committed = JSON.parse(await readFile(new URL('../contracts/openapi/v1.json', import.meta.url), 'utf8'))
  assert.deepEqual(schemaNormalize(spec.body), schemaNormalize(committed), 'Actual API matches committed OpenAPI')
  await writeFile(new URL('openapi.json', artifact), JSON.stringify(schemaNormalize(spec.body), null, 2) + '\n')
  await run(process.execPath, ['frontend/scripts/api-contract.mjs', 'check', fileURLToPath(new URL('openapi.json', artifact))])
  pass('Actual API/OpenAPI and generated Fetch/types/Zod have no drift')
  const exactPath = '/api/v1/rankings?asOfUtc=2021-01-08T02:00:00Z'
  const exact = await get(exactPath)
  assert.equal(exact.status, 200); assert.equal(exact.body.batch.asOfUtc, '2021-01-08T02:00:00.000Z')
  assert.deepEqual(exact.body.items.map(i => i.assetId), ['bitcoin', 'ethereum', 'solana'])
  assert.ok(exact.body.items.every(i => i.compositeScore === '0.000000'))
  assert.equal(exact.body.batch.knowledgeCutoffUtc, '2021-01-09T00:00:00.000Z')
  assert.equal(exact.headers.get('cache-control'), 'no-store')
  assert.equal((await get('/api/v1/rankings')).body.batch.asOfUtc, '2021-01-08T05:00:00.000Z')
  assert.equal((await get('/api/v1/rankings?asOfUtc=2021-01-07T23:00:00Z')).body.code, 'batch-not-found')
  assert.equal((await get('/api/v1/rankings?modelId=missing')).body.code, 'model-not-found')
  const corrupt = await get('/api/v1/rankings?modelId=synthetic-corrupt')
  assert.equal(corrupt.status, 500); assert.equal(corrupt.body.code, 'rankings-integrity-failure')
  assert.equal(corrupt.headers.get('content-type')?.split(';')[0], 'application/problem+json')
  const proxied = await get(exactPath, { origin: `http://127.0.0.1:${env.FRONTEND_PORT}` })
  assert.deepEqual(proxied.body.batch, exact.body.batch)
  assert.deepEqual(proxied.body.items, exact.body.items)
  // Node's Fetch replaces Host; use raw HTTP so this really sends an untrusted host.
  const hostStatus = await new Promise((resolve, reject) => {
    const request = httpGet(baseUrl + exactPath, { headers: { Host: 'outside.invalid' } }, response => {
      response.resume(); response.on('end', () => resolve(response.statusCode))
    })
    request.on('error', reject); request.setTimeout(6000, () => request.destroy(new Error('Host probe timed out')))
  })
  assert.equal(hostStatus, 400)
  const cors = await get(exactPath, { headers: { Origin: 'https://outside.invalid' } })
  assert.equal(cors.headers.get('access-control-allow-origin'), null)
  pass('Live local API/proxy, exact decimals/UTC, historical gaps and private host/origin boundaries')
  const before = (await sql('SELECT count(*), md5(string_agg("ScoreHash", \'\' ORDER BY "Id")) FROM research."ScoreSnapshots";')).stdout
  await compose(['stop', 'redis'])
  const noRedis = await get(exactPath)
  assert.deepEqual(noRedis.body.batch, exact.body.batch); assert.deepEqual(noRedis.body.items, exact.body.items)
  await compose(['start', 'redis'])
  pass('Redis failure does not affect persisted rankings')
  // Only this task-owned psql session is terminated; the database is never stopped.
  // Attach rejection handling immediately: terminating this owned psql session is
  // expected and must never bypass the outer resource cleanup as an unhandled rejection.
  const lock = sql(`SET application_name = '${project}-lock'; BEGIN; LOCK TABLE research."ScoringModels" IN ACCESS EXCLUSIVE MODE; SELECT pg_sleep(20); ROLLBACK;`)
    .then(() => null, error => error)
  for (let i = 0; i < 20; i++) {
    const held = await sql(`SELECT count(*) FROM pg_stat_activity WHERE application_name = '${project}-lock' AND wait_event = 'PgSleep';`)
    if (held.stdout === '1') break
    if (i === 19) throw new Error('Owned test lock did not start')
    await delay(50)
  }
  const aborted = new AbortController()
  const read = fetch(baseUrl + exactPath, { signal: aborted.signal, headers: { 'X-Correlation-ID': 'm4-cancel' } })
  const rejected = assert.rejects(read, error => error.name === 'AbortError')
  const blockedReads = async () => Number((await sql(`SELECT count(*) FROM pg_stat_activity WHERE datname = 'analysis_m4_checks' AND wait_event_type = 'Lock' AND query LIKE '%"ScoringModels"%';`)).stdout)
  for (let i = 0; ; i++) {
    if (await blockedReads() > 0) break
    assert.ok(i < 20, 'HTTP request reaches actual PostgreSQL lock before cancellation')
    await delay(50)
  }
  aborted.abort(); await rejected
  for (let i = 0; ; i++) {
    if (await blockedReads() === 0) break
    assert.ok(i < 20, 'Server cancels its database query while the test lock is still held')
    await delay(50)
  }
  await sql(`SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE application_name = '${project}-lock' AND datname = 'analysis_m4_checks';`)
  const lockResult = await lock
  assert.ok(lockResult instanceof Error && lockResult.message.includes('terminating connection due to administrator command'))
  assert.equal((await get(exactPath)).status, 200)
  assert.equal((await sql('SELECT count(*), md5(string_agg("ScoreHash", \'\' ORDER BY "Id")) FROM research."ScoreSnapshots";')).stdout, before)
  const apiLogs = (await compose(['logs', '--no-log-prefix', 'api'])).stdout.split(/\r?\n/).filter(line => line.startsWith('{')).map(line => JSON.parse(line))
  const cancelledLogs = apiLogs.filter(entry => entry.Scopes?.some(scope => scope.CorrelationId === 'm4-cancel'))
  assert.ok(cancelledLogs.some(entry => entry.State?.StatusCode === 499), 'Cancellation is logged as client disconnect')
  assert.ok(cancelledLogs.every(entry => entry.LogLevel !== 'Error' && !(entry.State?.StatusCode >= 500)), 'Cancellation is not a server failure')
  pass('Actual API database read abort releases resources and preserves persisted scores')
  const inspect = JSON.parse((await docker(['inspect', ...(await compose(['ps', '-q'])).stdout.split(/\s+/)])).stdout)
  for (const container of inspect) {
    const service = container.Config.Labels['com.docker.compose.service']
    if (['api', 'frontend'].includes(service)) assert.ok(Object.values(container.HostConfig.PortBindings).flat().every(p => p.HostIp === '127.0.0.1'))
    if (['postgres', 'redis'].includes(service)) assert.equal(Object.keys(container.HostConfig.PortBindings ?? {}).length, 0)
  }
  const production = `${project}-production`, productionPort = await freePort()
  helpers.push(production)
  await docker(['run', '--detach', '--name', production, '--network', `${project}_application`, '--network', `${project}_data`, '--publish', `127.0.0.1:${productionPort}:8080`,
    '--env', 'Postgres__Password', '--env', 'Postgres__Database=analysis_m4_checks', '--env', 'ASPNETCORE_ENVIRONMENT=Production', `${project}-api`])
  const productionOrigin = `http://127.0.0.1:${productionPort}`
  for (let i = 0; i < 30; i++) {
    try { if ((await get('/api/health/live', { origin: productionOrigin })).status === 200) break } catch { /* startup */ }
    if (i === 29) {
      await docker(['logs', production], { allowFailure: true })
      throw new Error('Production verification host did not start; see retained helper log')
    }
    await delay(200)
  }
  assert.equal((await get('/api/v1/rankings', { origin: productionOrigin })).status, 403)
  assert.equal((await get('/api/openapi/v1.json', { origin: productionOrigin })).status, 404)
  pass('Production defaults deny rankings and hide OpenAPI')
  // The normal runtime checks below inspect the image, without starting any additional API service.
  const files = await docker(['run', '--rm', '--network', 'none', '--entrypoint', 'sh', `${project}-api`, '-c', 'id -u && find /app -iname "*Checks*" -o -name Fixtures'])
  assert.equal(files.stdout, '1654')
  pass('Loopback publishing, internal data services and non-root fixture-free API image')
} catch (error) {
  report.failed.push(safe(error.message)); console.error(`FAIL ${safe(error.message)}`); process.exitCode = 1
} finally {
  if (owned) {
    try {
      for (const name of helpers) { assert.ok(name.startsWith(project + '-')); await docker(['rm', '--force', name], { allowFailure: true }) }
      await compose(['down', '--volumes', '--timeout', '30'])
      assert.equal((await docker(['ps', '-aq', '--filter', `label=com.docker.compose.project=${project}`])).stdout, '')
      assert.equal((await docker(['volume', 'ls', '-q', '--filter', `name=^${project}_postgres-data$`])).stdout, '')
      report.cleanup = 'Task containers/networks/disposable volume removed; retained resources untouched'
    } catch (error) { report.cleanup = safe(error.message); process.exitCode = 1 }
  }
  await writeFile(new URL(`../.artifacts/${project}.json`, import.meta.url), JSON.stringify(report, null, 2) + '\n')
  console.log(`Report: .artifacts/${project}.json`)
}
