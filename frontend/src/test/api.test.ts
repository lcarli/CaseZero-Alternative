import { describe, it, expect, beforeEach, vi } from 'vitest'
import { authApi } from '../services/api'

// Mock fetch
const mockFetch = vi.fn()
global.fetch = mockFetch

describe('API Service', () => {
  beforeEach(() => {
    mockFetch.mockClear()
  })

  describe('authApi.login', () => {
    it('should make POST request to login endpoint', async () => {
      const mockResponse = {
        token: 'test-token',
        user: { id: '1', firstName: 'John', lastName: 'Doe' }
      }

      mockFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => mockResponse
      })

      const loginData = {
        email: 'test@fic-police.gov',
        password: 'password123'
      }

      const result = await authApi.login(loginData)

      expect(mockFetch).toHaveBeenCalledWith(
        'http://localhost:5000/api/auth/login',
        expect.objectContaining({
          method: 'POST',
          headers: {
            'Content-Type': 'application/json'
          },
          body: JSON.stringify(loginData)
        })
      )
      expect(result).toEqual(mockResponse)
    })

    it('should throw error on failed login', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: false,
        status: 401
      })

      const loginData = {
        email: 'test@fic-police.gov',
        password: 'wrongpassword'
      }

      await expect(authApi.login(loginData)).rejects.toThrow()
    })
  })

  describe('authApi.register', () => {
    it('should make POST request to register endpoint', async () => {
      const mockResponse = {
        message: 'User registered successfully',
        policeEmail: 'john.doe@fic-police.gov',
        personalEmail: 'john.doe@personal.com'
      }

      mockFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => mockResponse
      })

      const registerData = {
        firstName: 'John',
        lastName: 'Doe',
        personalEmail: 'john.doe@personal.com',
        password: 'password123'
      }

      const result = await authApi.register(registerData)

      expect(mockFetch).toHaveBeenCalledWith(
        'http://localhost:5000/api/auth/register',
        expect.objectContaining({
          method: 'POST',
          headers: {
            'Content-Type': 'application/json'
          },
          body: JSON.stringify(registerData)
        })
      )
      expect(result).toEqual(mockResponse)
    })
  })
})