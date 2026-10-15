import { describe, it, expect, beforeEach, vi } from 'vitest'
import { authApi, casesV2Api, emailsApi } from '../services/api'

// Mock fetch
const mockFetch = vi.fn()
Object.defineProperty(globalThis, 'fetch', {
  value: mockFetch,
  writable: true
})

// Mock localStorage
const localStorageMock = {
  getItem: vi.fn(),
  setItem: vi.fn(),
  removeItem: vi.fn(),
  clear: vi.fn(),
}
Object.defineProperty(globalThis, 'localStorage', {
  value: localStorageMock,
  writable: true
})

describe('API Service', () => {
  beforeEach(() => {
    mockFetch.mockClear()
    localStorageMock.getItem.mockClear()
    localStorageMock.setItem.mockClear()
    localStorageMock.removeItem.mockClear()
    localStorageMock.clear.mockClear()
  })

  describe('authApi.login', () => {
    it('should make POST request to login endpoint', async () => {
      const mockResponse = {
        token: 'test-token',
        user: { id: '1', firstName: 'John', lastName: 'Doe' }
      }

      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        headers: { get: vi.fn((h: string) => h === 'content-type' ? 'application/json' : null) },
        json: async () => mockResponse
      })

      localStorageMock.getItem.mockReturnValue(null) // No existing token

      const loginData = {
        email: 'test@fic-police.gov',
        password: 'password123'
      }

      const result = await authApi.login(loginData)

      expect(mockFetch).toHaveBeenCalledWith(
        'http://localhost:5001/api/auth/login',
        expect.objectContaining({
          method: 'POST',
          headers: {
            'Content-Type': 'application/json'
          },
          body: JSON.stringify(loginData)
        })
      )
      expect(result).toEqual(mockResponse)
      expect(localStorageMock.setItem).toHaveBeenCalledWith('casezero_token', 'test-token')
      expect(localStorageMock.setItem).toHaveBeenCalledWith('casezero_user', JSON.stringify(mockResponse.user))
    })

    it('should throw error on failed login', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: false,
        status: 401
      })

      localStorageMock.getItem.mockReturnValue(null) // No existing token

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
        status: 200,
        headers: { get: vi.fn((h: string) => h === 'content-type' ? 'application/json' : null) },
        json: async () => mockResponse
      })

      // Mock existing token in localStorage
      localStorageMock.getItem.mockReturnValue('test-token')

      const registerData = {
        firstName: 'John',
        lastName: 'Doe',
        personalEmail: 'john.doe@personal.com',
        password: 'password123'
      }

      const result = await authApi.register(registerData)

      expect(mockFetch).toHaveBeenCalledWith(
        'http://localhost:5001/api/auth/register',
        expect.objectContaining({
          method: 'POST',
          headers: expect.objectContaining({
            'Content-Type': 'application/json',
            'Authorization': 'Bearer test-token'
          }),
          body: JSON.stringify(registerData)
        })
      )
      expect(result).toEqual(mockResponse)
    })
  })

  it('preserves numeric partial-credit scores returned by the case submission API', async () => {
    const result = {
      correct: false, score: 0.65,
      breakdown: { culpritScore: 0.4, evidenceScore: 0.1, analysisScore: 0.15, questionsScore: 0 },
      maxScores: { culprit: 0.4, evidence: 0.2, analysis: 0.2, questions: 0.2 },
      attemptsRemaining: 2, graded: true, feedbackCode: 'incorrect_attempts_remaining', explanationMarkdown: null,
    }
    mockFetch.mockResolvedValueOnce({
      ok: true, status: 200,
      headers: { get: (name: string) => name === 'content-type' ? 'application/json' : null },
      json: async () => result,
    })
    const response = await casesV2Api.submitCase('case_progression', {
      suspectId: 'suspect.one', evidenceIds: [], analysisIds: [], answers: [],
    })
    expect(response.breakdown).toEqual(result.breakdown)
    expect(mockFetch).toHaveBeenCalledWith(
      'http://localhost:5001/api/cases/case_progression/submit',
      expect.objectContaining({ method: 'POST' }),
    )
  })

  it('downloads binary email attachments with their raw IDs instead of parsing them as JSON', async () => {
    const attachment = new Blob(['%PDF-1.7'], { type: 'application/pdf' })
    mockFetch.mockResolvedValueOnce({ ok: true, status: 200, blob: async () => attachment })
    localStorageMock.getItem.mockReturnValue('test-token')

    expect(await emailsApi.downloadAttachment('case_mail', 'email.raw_01', 'asset.hidden_report')).toBe(attachment)
    expect(mockFetch).toHaveBeenCalledWith(
      'http://localhost:5001/api/cases/case_mail/emails/email.raw_01/attachments/asset.hidden_report/download',
      expect.objectContaining({ method: 'POST', headers: { Authorization: 'Bearer test-token' } }),
    )
  })

  it('opens emails using the raw server ID and fetches their full content', async () => {
    const email = { emailId: 'email.raw_01', content: 'Case briefing', attachments: ['asset.hidden_report'] }
    mockFetch
      .mockResolvedValueOnce({ ok: true, status: 204 })
      .mockResolvedValueOnce({
        ok: true, status: 200,
        headers: { get: (name: string) => name === 'content-type' ? 'application/json' : null },
        json: async () => email,
      })

    expect(await emailsApi.openEmail('case_mail', 'email.raw_01')).toEqual(email)
    expect(mockFetch.mock.calls.map(([url]) => url)).toEqual([
      'http://localhost:5001/api/cases/case_mail/emails/email.raw_01/open',
      'http://localhost:5001/api/cases/case_mail/emails/email.raw_01',
    ])
  })
})