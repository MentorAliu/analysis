import assert from 'node:assert/strict'
import { access, writeFile } from 'node:fs/promises'
import { setTimeout as delay } from 'node:timers/promises'
import { verifier } from './m3-verifier-support.mjs'

// Run only after current private-use terms/access review and explicit authorization.
// This verifier never retries a batch. The durable claim survives failure/restarts.
assert.deepEqual(process.argv.slice(2, 6), ['--private-use', '--country', 'XK', '--terms-reviewed-utc'])
assert.equal(process.argv.length, 7)
const reviewed = new Date(process.argv[6])
assert.ok(Number.isFinite(reviewed.valueOf()) && reviewed <= new Date() && Date.now() - reviewed < 86_400_000,
  'Requires the UTC timestamp of an actual terms/access review within 24 hours')
const ledger = new URL('../.artifacts/m3-private-acquisition.json', import.meta.url)
try { await access(ledger); throw new Error('M3 acquisition already claimed; do not repeat automatically. Inspect the retained report.') }
catch (error) { if (error.code !== 'ENOENT') throw error }
const v = verifier('private')
const end = new Date(); end.setUTCHours(0, 0, 0, 0); end.setUTCDate(end.getUTCDate() - 2)
const start = new Date(end); start.setUTCDate(start.getUTCDate() - 7)
const utc = date => date.toISOString().replace(/\.\d{3}Z$/, 'Z')
// Preserve M2's exact second-only argument format; its parser rejects .000Z before host construction.
const window = { startUtc: utc(start), endUtc: utc(end) }
assert.match(window.startUtc, /^\d{4}-\d{2}-\d{2}T00:00:00Z$/)
assert.match(window.endUtc, /^\d{4}-\d{2}-\d{2}T00:00:00Z$/)
Object.assign(v.report, { mode: 'private-use', country: 'XK', termsReviewedUtc: utc(reviewed), window, acquisition: 'not-started' })
const database = 'analysis_m2_checks'
const localWorker = args => v.docker(['run', '--rm', '--network', `${v.project}_data`,
  '--env', 'Postgres__Password', '--env', `Postgres__Database=${database}`, '--cap-drop', 'ALL',
  '--security-opt', 'no-new-privileges:true', `${v.project}-worker`, ...args], { allowFailure: true })
const dataSnapshot = async () => v.jsonLine((await v.docker(['run', '--rm', '--network', `${v.project}_data`,
  '--env', 'M2_DB_PASSWORD', '--env', 'M2_ISOLATED_TEST=true', '--cap-drop', 'ALL', '--security-opt', 'no-new-privileges:true',
  `${v.project}-m2checks`, '--private-snapshot', window.startUtc, window.endUtc])).stdout)
const scoreStart = new Date(end.valueOf() - 25 * 3_600_000)
const replay = async () => {
  const result = await localWorker(['--replay-scores', '--model', 'slice1-v1', '--start-utc', utc(scoreStart), '--end-utc', utc(end)])
  assert.equal(result.code, 0, 'Local replay failed')
  const summary = v.jsonLine(result.stdout)
  assert.equal(summary.Batches, 25); assert.equal(summary.Scores, 75); assert.deepEqual(summary.MissingPeriods, [])
  return summary
}
try {
  await v.setup({ privateEgress: true })
  await v.docker(['build', '--file', 'backend/Dockerfile', '--target', 'm2checks', '--tag', `${v.project}-m2checks`, '.'])
  await v.compose(['exec', '-T', 'postgres', 'psql', '-U', 'analysis', '-d', 'analysis', '-v', 'ON_ERROR_STOP=1'],
    { input: `CREATE DATABASE ${database};\n` })
  assert.equal((await localWorker(['--migrate'])).code, 0)
  assert.equal((await v.docker(['network', 'inspect', `${v.project}_data`, '--format', '{{.Internal}}'])).stdout, 'true')
  v.pass('Fresh isolated private database, reviewed EF migration and internal-only scoring network')

  // Claim before the ONLY provider-facing worker execution. Do not remove on failure.
  await writeFile(ledger, JSON.stringify({ project: v.project, window, claimedAtUtc: utc(new Date()),
    termsReviewedUtc: utc(reviewed), policy: 'one acquisition; no automatic repeated batch' }, null, 2) + '\n', { flag: 'wx', mode: 0o600 })
  v.report.acquisition = 'claimed'
  const ingestion = await v.compose(['run', '--rm', '--no-deps', '-e', `Postgres__Database=${database}`, 'worker',
    '--ingest-once', '--private-use', '--country', 'XK', '--start-utc', window.startUtc, '--end-utc', window.endUtc],
  { allowFailure: true, timeoutMs: 360_000 })
  v.report.acquisition = 'executed-once'
  const ingestionSummary = ingestion.stdout.split(/\r?\n/).map(line => { try { return JSON.parse(line) } catch { return null } })
    .find(value => value?.mode === 'private-use')
  assert.ok(ingestionSummary, `Ingestion returned no safe summary (exit ${ingestion.code}); acquisition will not be repeated`)
  v.report.ingestion = { exitCode: ingestion.code, ...ingestionSummary }
  v.report.observations = await dataSnapshot()
  // Whole-second K must be later than every millisecond ingestion timestamp.
  await delay(1100)
  v.report.knowledgeCutoffUtc = utc(new Date())
  console.log(`Private acquisition: ${v.report.observations.observationCount} observations across ${v.report.observations.coverage.length} series`)
  if (ingestion.code === 0 && v.report.observations.completeCoverage) v.pass('One seven-day acquisition: all eleven M2 series; raw payload/decimal/unit/UTC replay passed')
  else { v.report.unavailable.push('Seven-day history coverage incomplete; no batch retry, provider substitution or relaxed readiness'); process.exitCode = 1 }
  const beforeScoring = await v.snapshot(database)
  const summaries = []
  for (let hour = 0; hour < 25; hour++) {
    const result = await localWorker(['--score-once', '--private-use', '--country', 'XK', '--as-of-utc',
      utc(new Date(scoreStart.valueOf() + hour * 3_600_000)), '--knowledge-cutoff-utc', v.report.knowledgeCutoffUtc, '--model', 'slice1-v1'])
    assert.ok([0, 3].includes(result.code), `Scoring hour ${hour} failed (${result.code})`)
    const summary = v.jsonLine(result.stdout); assert.equal(summary.duplicate, false); summaries.push(summary)
    if ((hour + 1) % 5 === 0) console.log(`Persisted ${hour + 1}/25 hourly three-asset bundles using only the internal database network`)
  }
  v.report.scoreRuns = summaries
  v.report.scores = await v.snapshot(database)
  assert.equal(v.report.scores.m2Hash, beforeScoring.m2Hash, 'Scoring changed M2 data')
  assert.equal(v.report.scores.scores, 75); assert.equal(v.report.scores.features, 1575)
  if (v.report.scores.ready === 75 && v.report.scores.complete === 75 && v.report.scores.unusableApplicableFeatures === 0)
    v.pass('75 ready complete scores, 1575 feature states, all applicable inputs usable and all M2 observations preserved')
  else { v.report.unavailable.push('Private score gate incomplete: inspect feature states; catalog/formulas/thresholds unchanged'); process.exitCode = 1 }

  await v.compose(['stop', 'redis'])
  for (const original of summaries) {
    const result = await localWorker(['--score-once', '--private-use', '--country', 'XK', '--as-of-utc', utc(new Date(original.asOfUtc)),
      '--knowledge-cutoff-utc', v.report.knowledgeCutoffUtc, '--model', 'slice1-v1'])
    assert.ok([0, 3].includes(result.code))
    const summary = v.jsonLine(result.stdout); assert.equal(summary.duplicate, true); assert.equal(summary.batchId, original.batchId)
  }
  v.report.replay = await replay()
  assert.deepEqual(await v.snapshot(database), v.report.scores)
  v.pass('All 25 duplicate commands reuse stored bundles; exact read-only replay, no provider egress and no Redis')
  assert.equal((await localWorker(['--migrate'])).code, 0)
  await v.compose(['up', '--detach', '--no-deps', '--force-recreate', '--wait', '--wait-timeout', '90', 'postgres'])
  assert.deepEqual(await v.snapshot(database), v.report.scores)
  await replay()
  assert.equal((await dataSnapshot()).databaseSnapshot, v.report.observations.databaseSnapshot)
  v.pass('PostgreSQL recreation and migration reapplication preserve private observation/model/feature/score hashes; replay remains exact')
} catch (error) {
  v.report.failed.push(v.safe(error.message)); console.error(`FAIL ${v.safe(error.message)}`); process.exitCode = 1
} finally { await v.finish({ retain: true }) }
