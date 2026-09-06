/** Only accepts decimals already validated at the generated transport boundary. */
export function millionths(value: string): bigint { return BigInt(value.replace('.', '')) }
export function compareDecimals(left: string, right: string): number {
  const a = millionths(left), b = millionths(right)
  return a < b ? -1 : a > b ? 1 : 0
}
export function exactScore(value: string): string { return millionths(value) > 0n ? `+${value}` : value }
export function formatDecimal(value: string, signed = false): string {
  const exact = millionths(value), magnitude = exact < 0n ? -exact : exact
  let cents = magnitude / 10_000n
  const remainder = magnitude % 10_000n
  if (remainder > 5_000n || (remainder === 5_000n && cents % 2n === 1n)) cents++
  const preserve = (exact !== 0n && cents === 0n) || (!signed && magnitude < 100_000_000n && cents === 10_000n)
  const digits = preserve ? value.replace('-', '') : `${cents / 100n}.${String(cents % 100n).padStart(2, '0')}`
  return `${exact < 0n ? '−' : signed && exact > 0n ? '+' : ''}${digits}`
}
export function scoreDirection(value: string | null): 'positive' | 'negative' | 'neutral' {
  if (value === null) return 'neutral'
  const exact = millionths(value)
  return exact > 0n ? 'positive' : exact < 0n ? 'negative' : 'neutral'
}
export function utcLabel(value: string): string { return `${value.replace('T', ' ').replace(/Z$/, '')} UTC` }
export function exactHour(value: string): string { return value.replace('.000Z', 'Z') }
export function ageLabel(seconds: number): string {
  const days = Math.floor(seconds / 86400), hours = Math.floor(seconds % 86400 / 3600)
  const minutes = Math.floor(seconds % 3600 / 60), remainder = seconds % 60
  return `${days}d ${hours}h ${minutes}m ${remainder}s`
}
