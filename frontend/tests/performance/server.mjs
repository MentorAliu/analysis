// Test-only production-bundle server. Real loopback HTTP permits CDP shaping.
import { createServer } from 'node:http'
import { readFile } from 'node:fs/promises'
import { resolve, extname, sep } from 'node:path'
import { gzipSync } from 'node:zlib'
const root = resolve('dist')
const fixture = JSON.parse(await readFile('tests/unit/features/rankings/fixtures/rankings.json', 'utf8'))
const mime = { '.js': 'text/javascript', '.css': 'text/css', '.html': 'text/html', '.svg': 'image/svg+xml', '.ico': 'image/x-icon' }
const server = createServer(async (request, response) => {
  const url = new URL(request.url, 'http://127.0.0.1:4175')
  if (url.pathname === '/api/v1/rankings') {
    const data = structuredClone(fixture)
    data.batch.model.id = url.searchParams.get('modelId') ?? 'slice1-v1'
    const hour = url.searchParams.get('asOfUtc')
    if (hour) { data.selection = 'exact'; data.requestedAsOfUtc = hour; data.batch.asOfUtc = hour.replace('Z', '.000Z') }
    return setTimeout(() => {
      response.writeHead(200, { 'content-type': 'application/json', 'cache-control': 'no-store' })
      response.end(JSON.stringify(data))
    }, 200)
  }
  if (url.pathname.startsWith('/api/')) { response.writeHead(404); response.end(); return }
  try {
    const path = resolve(root, '.' + decodeURIComponent(url.pathname))
    if (path !== root && !path.startsWith(root + sep)) { response.writeHead(400); response.end(); return }
    const file = extname(path) ? path : resolve(root, 'index.html')
    const bytes = await readFile(file)
    const gzip = /gzip/.test(request.headers['accept-encoding'] ?? '')
    response.writeHead(200, { 'content-type': mime[extname(file)] ?? 'application/octet-stream', 'cache-control': 'no-store', ...(gzip ? { 'content-encoding': 'gzip' } : {}) })
    response.end(gzip ? gzipSync(bytes) : bytes)
  } catch { response.writeHead(404); response.end() }
})
server.listen(4175, '127.0.0.1')
process.on('SIGTERM', () => server.close())
