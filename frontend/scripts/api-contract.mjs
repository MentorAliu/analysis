import assert from 'node:assert/strict'
import { mkdtemp, readFile, readdir, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { createClient } from '@hey-api/openapi-ts'
import config from '../openapi-ts.config.mjs'

assert.equal(process.version, 'v24.20.0', 'Use pinned Node 24.20.0')
const [mode, comparisonPath] = process.argv.slice(2)
assert.ok(['generate', 'check', 'normalize'].includes(mode), 'Expected generate, check or normalize')
const normalize = value => Array.isArray(value) ? value.map(normalize) : value && typeof value === 'object'
  ? Object.fromEntries(Object.entries(value).sort(([a], [b]) => a < b ? -1 : a > b ? 1 : 0).map(([key, item]) => [key, normalize(item)])) : value
const serialized = value => JSON.stringify(normalize(value), null, 2) + '\n'
const schema = JSON.parse(await readFile(config.input, 'utf8'))
function inspect(value) {
  if (!value || typeof value !== 'object') return
  if ('$ref' in value) assert.ok(value.$ref.startsWith('#/'), 'External schema references are forbidden')
  Object.values(value).forEach(inspect)
}
inspect(schema)
assert.match(schema.openapi, /^3\.1\./)
assert.deepEqual(schema.servers, [{ url: '/' }], 'Generated transport stays same-origin')
if (mode === 'normalize') {
  await writeFile(config.input, serialized(schema))
} else if (mode === 'generate') {
  await createClient(config)
} else {
  if (comparisonPath) assert.equal(serialized(JSON.parse(await readFile(comparisonPath, 'utf8'))), serialized(schema), 'Running API/OpenAPI drift')
  const temporary = await mkdtemp(join(tmpdir(), 'analysis-m4-codegen-'))
  async function files(root, prefix = '') {
    const entries = await readdir(join(root, prefix), { withFileTypes: true })
    return (await Promise.all(entries.map(e => e.isDirectory() ? files(root, join(prefix, e.name)) : [join(prefix, e.name)]))).flat().sort()
  }
  try {
    await createClient({ ...config, output: { ...config.output, path: temporary } })
    const expected = await files(temporary), actual = await files(config.output.path)
    assert.deepEqual(actual, expected, 'Generated file inventory drift')
    for (const file of expected) assert.equal((await readFile(join(config.output.path, file), 'utf8')).replaceAll('\r\n', '\n'),
      (await readFile(join(temporary, file), 'utf8')).replaceAll('\r\n', '\n'), `Generated drift: ${file}`)
    console.log(`PASS OpenAPI/generated contract (${expected.length} files)`)
  } finally {
    // mkdtemp gives a task-owned absolute path; remove only that exact temporary directory.
    assert.ok(temporary.startsWith(join(tmpdir(), 'analysis-m4-codegen-')))
    await rm(temporary, { recursive: true, force: true })
  }
}
