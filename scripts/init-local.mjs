import { randomBytes } from 'node:crypto'
import { readFile, writeFile } from 'node:fs/promises'

const root = new URL('../', import.meta.url)
const template = await readFile(new URL('.env.example', root), 'utf8')
try {
  await writeFile(new URL('.env', root), template.replace('POSTGRES_PASSWORD=', `POSTGRES_PASSWORD=${randomBytes(32).toString('hex')}`), { flag: 'wx', mode: 0o600 })
  console.log('Created ignored .env with a generated local password. Existing data is unchanged.')
} catch (error) {
  if (error.code !== 'EEXIST') throw error
  console.log('Existing .env preserved.')
}
