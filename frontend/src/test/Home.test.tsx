import { describe, it, expect } from 'vitest'

describe('Frontend Utilities', () => {
  it('should have basic test infrastructure working', () => {
    expect(true).toBe(true)
  })

  it('should be able to test basic JavaScript functions', () => {
    const add = (a: number, b: number) => a + b
    expect(add(2, 3)).toBe(5)
  })

  it('should be able to test object equality', () => {
    const user = { name: 'John', age: 30 }
    expect(user).toEqual({ name: 'John', age: 30 })
  })
})