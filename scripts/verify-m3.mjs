import assert from 'node:assert/strict'
import { verifier } from './m3-verifier-support.mjs'

assert.deepEqual(process.argv.slice(2), [])
const v = verifier('check')
const database = 'analysis_m3_checks'
const score = ['--score-once', '--private-use', '--country', 'XK', '--as-of-utc', '2021-01-08T00:00:00Z', '--knowledge-cutoff-utc', '2021-01-09T00:00:00Z', '--model', 'slice1-v1']
const worker = args => v.compose(['run', '--rm', '--no-deps', '-e', `Postgres__Database=${database}`, 'worker', ...args], { allowFailure: true })
try {
  await v.setup()
  await v.compose(['exec', '-T', 'postgres', 'psql', '-U', 'analysis', '-d', 'analysis', '-v', 'ON_ERROR_STOP=1'], { input: `CREATE DATABASE ${database};\n` })
  const checks = await v.docker([...v.checkArgs(database), '--database-checks'])
  v.report.databaseChecks = v.jsonLine(checks.stdout)
  v.pass('Independent vectors, populated-M2 migration, exact replay, concurrency, sealed snapshots, SQL integrity and cancelled writes')
  const before = await v.snapshot()
  const result = await worker(score)
  assert.equal(result.code, 0, result.stdout)
  assert.equal(v.jsonLine(result.stdout).duplicate, true)
  assert.match(result.stdout, /"runId":"[a-f0-9]{32}"/)
  const range = ['--replay-scores', '--model', 'slice1-v1', '--start-utc', '2021-01-08T00:00:00Z', '--end-utc', '2021-01-08T02:00:00Z']
  assert.equal((await worker(range)).code, 0)
  assert.equal((await worker(['--score-once'])).code, 2)
  assert.equal((await worker(score.map(x => x === '2021-01-09T00:00:00Z' ? '2021-01-08T23:00:00Z' : x))).code, 2)
  assert.deepEqual(await v.snapshot(), before)
  await v.compose(['stop', 'redis'])
  assert.equal((await worker(score)).code, 0)
  assert.deepEqual(await v.snapshot(), before)
  v.pass('Actual worker commands, safe correlation, malformed/cutoff refusal and Redis-independent replay')

  const holder = `${v.project}-lock-holder`, cancelledWorker = `${v.project}-cancel-worker`
  v.ownedHelpers.push(holder, cancelledWorker)
  const holdArgs = v.checkArgs(database)
  await v.docker(['run', '--detach', '--name', holder, ...holdArgs.slice(2, -1), holdArgs.at(-1), '--hold-lock'])
  for (let n = 0; n < 40; n++) {
    if ((await v.docker(['logs', holder])).stdout.includes('"status":"locked"')) break
    await new Promise(resolve => setTimeout(resolve, 250))
    assert.notEqual(n, 39, 'Lock helper readiness')
  }
  const cancelCommand = score.map(x => x === '2021-01-08T00:00:00Z' ? '2021-01-08T02:00:00Z' : x)
  await v.compose(['run', '--detach', '--name', cancelledWorker, '--no-deps', '-e', `Postgres__Database=${database}`, 'worker', ...cancelCommand])
  let waiting = false
  for (let n = 0; n < 50; n++) {
    const status = await v.compose(['exec', '-T', 'postgres', 'psql', '-U', 'analysis', '-d', database, '-tA', '-c', "SELECT count(*) FROM pg_stat_activity WHERE datname = current_database() AND wait_event = 'advisory' AND state = 'active'"])
    if (Number(status.stdout) > 0) { waiting = true; break }
    await new Promise(resolve => setTimeout(resolve, 200))
  }
  assert.ok(waiting, 'Worker reached actual database lock wait')
  await v.docker(['kill', '--signal', 'SIGTERM', cancelledWorker])
  const stopped = await v.docker(['wait', cancelledWorker])
  assert.equal(stopped.stdout, '130')
  assert.deepEqual(await v.snapshot(), before)
  v.pass('SIGTERM interrupts actual blocked worker database write with exit 130 and no partial persistence')

  assert.equal((await worker(['--migrate'])).code, 0)
  await v.compose(['up', '--detach', '--no-deps', '--force-recreate', '--wait', '--wait-timeout', '90', 'postgres'])
  assert.deepEqual(await v.snapshot(), before)
  assert.equal((await worker(range)).code, 0)
  v.report.snapshot = before
  v.pass('Migration reapplication and PostgreSQL recreation preserve exact observations/features/scores; replay still exact')
} catch (error) {
  v.report.failed.push(v.safe(error.message)); console.error(`FAIL ${v.safe(error.message)}`); process.exitCode = 1
} finally { await v.finish() }
