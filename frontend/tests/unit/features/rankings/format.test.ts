import { describe, expect, it } from 'vitest'
import { ageLabel, compareDecimals, exactHour, formatDecimal, millionths, utcLabel } from '@/features/rankings/format'

describe('exact decimal presentation', () => {
  it.each([
    ['0.000000', '0.00'], ['0.000001', '+0.000001'], ['-0.000001', '−0.000001'],
    ['1.005000', '+1.00'], ['1.015000', '+1.02'], ['-1.025000', '−1.02'], ['-1.035000', '−1.04'],
    ['100.000000', '+100.00'], ['-100.000000', '−100.00'], ['99.999999', '+100.00'],
  ])('rounds %s to %s with ties to even and signed tiny values', (value, expected) => {
    expect(formatDecimal(value, true)).toBe(expected)
  })
  it('preserves meaningful quality boundaries and never scales a percentage', () => {
    expect(formatDecimal('99.999999')).toBe('99.999999')
    expect(formatDecimal('100.000000')).toBe('100.00')
    expect(formatDecimal('0.000001')).toBe('0.000001')
    expect(formatDecimal('50.000000')).toBe('50.00')
  })
  it('compares exact millionths even when displays tie', () => {
    expect(millionths('-12.345678')).toBe(-12345678n)
    expect(compareDecimals('9.999999', '10.000000')).toBe(-1)
    expect(compareDecimals('1.000001', '1.000002')).toBe(-1)
    expect(compareDecimals('-1.000001', '-1.000002')).toBe(1)
    expect(compareDecimals('0.000000', '0.000000')).toBe(0)
  })
  it('preserves UTC milliseconds and uses only reported age', () => {
    expect(utcLabel('2021-01-08T00:00:00.123Z')).toBe('2021-01-08 00:00:00.123 UTC')
    expect(exactHour('2021-01-08T00:00:00.000Z')).toBe('2021-01-08T00:00:00Z')
    expect(ageLabel(90061)).toBe('1d 1h 1m 1s')
    expect(ageLabel(0)).toBe('0d 0h 0m 0s')
  })
})
