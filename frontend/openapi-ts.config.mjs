import { fileURLToPath } from 'node:url'

export default {
  input: fileURLToPath(new URL('../contracts/openapi/v1.json', import.meta.url)),
  output: { path: fileURLToPath(new URL('./src/lib/api/generated', import.meta.url)), postProcess: [] },
  plugins: [
    '@hey-api/typescript',
    '@hey-api/client-fetch',
    { name: 'zod', compatibilityVersion: 4, requests: true, responses: true, definitions: true,
      // M1 operational timestamps may use +00:00. M4's schema patterns still require Z.
      dates: { offset: true, local: false } },
    { name: '@hey-api/sdk', validator: { request: 'zod', response: 'zod' }, transformer: false },
  ],
}
