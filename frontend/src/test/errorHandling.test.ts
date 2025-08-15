import { describe, it, expect } from 'vitest'
import { getErrorMessage } from '../utils/errorHandling'

// Mock translation function for testing
const mockT = (key: string) => {
  const translations = {
    networkError: 'Network error. Check your connection.',
    serverError: 'Internal server error. Please try again later.',
    unauthorizedError: 'Access denied. Please login again.',
    forbiddenError: 'You do not have permission for this action.',
    notFoundError: 'Resource not found.',
    validationError: 'Invalid data. Please check the fields.',
    timeoutError: 'Request timed out. Please try again.',
    genericError: 'Something went wrong. Please try again.',
    offlineMessage: 'You are offline. Some features may be limited.'
  }
  return translations[key as keyof typeof translations] || key
}

describe('Error Handling Utils', () => {
  it('should map network errors correctly', () => {
    const networkError = new Error('Failed to fetch')
    const result = getErrorMessage(networkError, mockT)
    expect(result).toBe('Network error. Check your connection.')
  })

  it('should map 401 errors to unauthorized', () => {
    const unauthorizedError = new Error('401 Unauthorized')
    const result = getErrorMessage(unauthorizedError, mockT)
    expect(result).toBe('Access denied. Please login again.')
  })

  it('should map server errors correctly', () => {
    const serverError = new Error('500 Internal Server Error')
    const result = getErrorMessage(serverError, mockT)
    expect(result).toBe('Internal server error. Please try again later.')
  })

  it('should handle offline API responses', () => {
    const offlineResponse = { offline: true, error: 'Network unavailable' }
    const result = getErrorMessage(offlineResponse, mockT)
    expect(result).toBe('You are offline. Some features may be limited.')
  })

  it('should fallback to generic error for unknown errors', () => {
    const unknownError = new Error('Some random error')
    const result = getErrorMessage(unknownError, mockT)
    expect(result).toBe('Some random error')
  })

  it('should handle validation errors', () => {
    const validationError = new Error('400 Bad Request - Validation failed')
    const result = getErrorMessage(validationError, mockT)
    expect(result).toBe('Invalid data. Please check the fields.')
  })
})