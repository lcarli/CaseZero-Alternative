import { describe, it, expect, beforeEach, vi } from 'vitest'
import { caseGenerationApi } from '../services/api'

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

describe('Case Generation API', () => {
  beforeEach(() => {
    mockFetch.mockClear()
    localStorageMock.getItem.mockClear()
    localStorageMock.setItem.mockClear()
    localStorageMock.removeItem.mockClear()
    localStorageMock.clear.mockClear()
  })

  describe('caseGenerationApi.generateCase', () => {
    it('should make POST request to generate case endpoint', async () => {
      const mockResponse = {
        caseJson: '{"title":"Test Case","status":"Generated"}',
        generatedDocs: [],
        imagePrompts: [],
        evidenceManifest: {}
      }

      mockFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => mockResponse
      })

      localStorageMock.getItem.mockReturnValue('test-token')

      const generateCaseData = {
        title: 'Test Case',
        location: 'Test Location',
        incidentDateTime: '2025-01-15T10:30:00',
        pitch: 'Test pitch',
        twist: 'Test twist',
        difficulty: 'Medium',
        targetDurationMinutes: 60,
        generateImages: true
      }

      const result = await caseGenerationApi.generateCase(generateCaseData)

      expect(mockFetch).toHaveBeenCalledWith(
        'http://localhost:5000/api/casegeneration/generate',
        expect.objectContaining({
          method: 'POST',
          headers: expect.objectContaining({
            'Content-Type': 'application/json',
            'Authorization': 'Bearer test-token'
          }),
          body: JSON.stringify(generateCaseData)
        })
      )
      expect(result).toEqual(mockResponse)
    })

    it('should throw error on failed case generation', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: false,
        status: 500,
        statusText: 'Internal Server Error'
      })

      localStorageMock.getItem.mockReturnValue('test-token')

      const generateCaseData = {
        title: 'Test Case',
        location: 'Test Location',
        incidentDateTime: '2025-01-15T10:30:00',
        pitch: 'Test pitch',
        twist: 'Test twist'
      }

      await expect(caseGenerationApi.generateCase(generateCaseData)).rejects.toThrow()
    })
  })

  describe('caseGenerationApi.generateCaseJson', () => {
    it('should make POST request to generate case JSON endpoint', async () => {
      const mockResponse = {
        caseJson: '{"title":"Test Case","status":"Generated"}'
      }

      mockFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => mockResponse
      })

      localStorageMock.getItem.mockReturnValue('test-token')

      const generateCaseData = {
        title: 'Test Case',
        location: 'Test Location',
        incidentDateTime: '2025-01-15T10:30:00',
        pitch: 'Test pitch',
        twist: 'Test twist'
      }

      const result = await caseGenerationApi.generateCaseJson(generateCaseData)

      expect(mockFetch).toHaveBeenCalledWith(
        'http://localhost:5000/api/casegeneration/generate-json',
        expect.objectContaining({
          method: 'POST',
          headers: expect.objectContaining({
            'Content-Type': 'application/json',
            'Authorization': 'Bearer test-token'
          }),
          body: JSON.stringify(generateCaseData)
        })
      )
      expect(result).toEqual(mockResponse)
    })
  })
})