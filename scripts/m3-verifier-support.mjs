import assert from 'node:assert/strict'
import { spawn } from 'node:child_process'
import { randomBytes } from 'node:crypto'
import { mkdir, writeFile } from 'node:fs/promises'
import { fileURLToPath } from 'node:url'

// Task-owned resources only. This module does no provider I/O.
export function verifier(kind) {
  assert.equal(process.version, 'v24.20.0', 'Use the pinned Node 24.20.0 runtime')
  const root = fileURLToPath(new URL('../', import.meta.url))
  const project = `analysis-m3-${kind}-${randomBytes(6).toString('hex')}`
  const password = randomBytes(32).toString('hex')
  const env = { ...process.env, POSTGRES_PASSWORD: password, Postgres__Password: password, M3_DB_PASSWORD: password, M2_DB_PASSWORD: password }
  const envFile = `.artifacts/${project}.env`
  const base = ['compose', '--project-name', project, '--env-file', envFile, '--file', 'compose.yaml']
  const report = { project, passed: [], failed: [], skipped: [], unavailable: [], cleanup: 'pending' }
  const safe = value => String(value).replaceAll(password, '[redacted]')
  const ownedHelpers = []
  let owned = false
  let commandNumber = 0
  async function docker(args, { input, allowFailure = false, timeoutMs = 900_000 } = {}) {
    const result = await new Promise((resolve, reject) => {
      const child = spawn('docker', args, { cwd: root, env, windowsHide: true, stdio: ['pipe', 'pipe', 'pipe'] })
      let stdout = '', stderr = ''
      const timer = setTimeout(() => child.kill(), timeoutMs)
      child.stdout.on('data', chunk => { stdout += chunk })
      child.stderr.on('data', chunk => { stderr += chunk })
      child.on('error', error => { clearTimeout(timer); reject(error) })
      child.on('close', code => { clearTimeout(timer); resolve({ code, stdout: safe(stdout.trim()), stderr: safe(stderr.trim()) }) })
      child.stdin.end(input)
    })
    await writeFile(new URL(`../.artifacts/${project}/command-${++commandNumber}.log`, import.meta.url),
      `docker ${args.join(' ')}\nexit=${result.code}\n${result.stdout}\n${result.stderr}\n`)
    if (result.code !== 0 && !allowFailure) throw new Error(`docker ${args.slice(0, 4).join(' ')} failed (${result.code}): ${result.stdout.slice(-1600)} ${result.stderr.slice(-1600)}`)
    return result
  }
  const compose = (args, options) => docker([...base, ...args], options)
  const jsonLine = output => JSON.parse(output.split(/\r?\n/).findLast(line => line.startsWith('{')))
  const pass = message => { report.passed.push(message); console.log(`PASS ${message}`) }
  const checkArgs = database => ['run', '--rm', '--network', `${project}_data`, '--env', 'M3_DB_PASSWORD', '--env', 'M3_ISOLATED_TEST=true',
    '--env', `M3_DATABASE=${database}`, '--cap-drop', 'ALL', '--security-opt', 'no-new-privileges:true', `${project}-m3checks`]
  const snapshot = async (database = 'analysis_m3_checks') => jsonLine((await docker([...checkArgs(database), '--snapshot'])).stdout)
  async function setup({ privateEgress = false } = {}) {
    await mkdir(new URL(`../.artifacts/${project}/`, import.meta.url), { recursive: true })
    assert.equal((await docker(['ps', '-aq', '--filter', `label=com.docker.compose.project=${project}`])).stdout, '')
    assert.equal((await docker(['volume', 'ls', '-q', '--filter', `name=^${project}_postgres-data$`])).stdout, '')
    await writeFile(new URL(`../${envFile}`, import.meta.url), `POSTGRES_PASSWORD=${password}\n`, { flag: 'wx', mode: 0o600 })
    owned = true
    if (privateEgress) base.push('--file', 'compose.m2-private.yaml')
    await compose(['config', '--quiet'])
    await docker(['build', '--file', 'backend/Dockerfile', '--target', 'm3checks', '--tag', `${project}-m3checks`, '.'])
    await compose(['build', 'worker'])
    pass('Pinned locked build, M1/M2 checks, M3 unit goldens and EF model consistency')
    await compose(['up', '--detach', '--wait', '--wait-timeout', '90', 'postgres', 'redis'])
  }
  async function finish({ retain = false } = {}) {
    if (owned) {
      try {
        for (const name of ownedHelpers) {
          assert.ok(name.startsWith(`${project}-`), 'Helper ownership')
          await docker(['rm', '--force', name], { allowFailure: true })
        }
        await compose(['down', ...(retain ? [] : ['--volumes']), '--timeout', '30'])
        assert.equal((await docker(['ps', '-aq', '--filter', `label=com.docker.compose.project=${project}`])).stdout, '')
        const volume = (await docker(['volume', 'ls', '-q', '--filter', `name=^${project}_postgres-data$`])).stdout
        assert.equal(Boolean(volume), retain)
        report.retainedVolume = volume || null
        report.localConfig = retain ? envFile : null
        report.cleanup = retain ? 'passed: task containers/networks removed; private data volume retained; existing resources untouched' : 'passed: task containers/networks/volume removed; existing resources untouched'
      } catch (error) { report.cleanup = safe(error.message); process.exitCode = 1 }
    }
    await writeFile(new URL(`../.artifacts/${project}.json`, import.meta.url), JSON.stringify(report, null, 2) + '\n')
    console.log(`Report: ${root}.artifacts/${project}.json`)
  }
  return { project, envFile, report, safe, ownedHelpers, docker, compose, jsonLine, pass, checkArgs, snapshot, setup, finish }
}
