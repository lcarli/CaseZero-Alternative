import type { CaseData } from '../types/case'
import { casesApi } from './api'

/**
 * Service to load case data from the backend API
 * This provides secure access to case data without exposing sensitive information in the DOM
 */
export class CaseDataService {
  private static caseCache = new Map<string, CaseData>()

  /**
   * Load case data from the backend API
   */
  static async loadCase(caseId: string): Promise<CaseData> {
    // Check cache first
    if (this.caseCache.has(caseId)) {
      return this.caseCache.get(caseId)!
    }

    try {
      // Call the secure backend API endpoint using the existing API service
      const caseData: CaseData = await casesApi.getCaseData(caseId)
      
      // Cache the loaded case
      this.caseCache.set(caseId, caseData)
      
      return caseData
    } catch (error) {
      console.error(`Error loading case ${caseId}:`, error)
      throw error
    }
  }

  /**
   * Get list of available cases 
   * In the future, this could query the backend API
   */
  static async getAvailableCases(): Promise<string[]> {
    // For now, return known case IDs
    // In the future, this could call: GET /api/cases to get available cases for the user
    return [
      'CASE-2024-001',
      'CASE-2024-002', 
      'CASE-2024-003'
    ]
  }

  /**
   * Clear case cache (useful for development)
   */
  static clearCache(): void {
    this.caseCache.clear()
  }

  /**
   * Preload a case into cache
   */
  static async preloadCase(caseId: string): Promise<void> {
    await this.loadCase(caseId)
  }
}