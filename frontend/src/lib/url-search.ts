/** Flat URL values are strings, including numeric/boolean-looking identities.
 * Keep duplicate occurrences for the route's schema to reject, never pick one. */
export function parseStringSearch(search: string): Record<string, string | string[]> {
  const result: Record<string, string | string[]> = Object.create(null)
  for (const [key, value] of new URLSearchParams(search)) {
    const previous = result[key]
    result[key] = previous === undefined ? value : Array.isArray(previous) ? [...previous, value] : [previous, value]
  }
  return result
}

export function stringifyStringSearch(search: Record<string, unknown>): string {
  const parameters = new URLSearchParams()
  for (const [key, value] of Object.entries(search)) {
    if (value === undefined) continue
    if (Array.isArray(value)) for (const occurrence of value) parameters.append(key, String(occurrence))
    else parameters.set(key, String(value))
  }
  const encoded = parameters.toString()
  return encoded ? `?${encoded}` : ''
}
