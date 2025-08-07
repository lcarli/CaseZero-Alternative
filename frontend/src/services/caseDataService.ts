import type { CaseData } from '../types/case'

/**
 * Service to load case data from JSON files
 * This allows dynamic loading of cases without code changes
 */
export class CaseDataService {
  private static caseCache = new Map<string, CaseData>()

  /**
   * Load case data from the JSON file
   */
  static async loadCase(caseId: string): Promise<CaseData> {
    // Check cache first
    if (this.caseCache.has(caseId)) {
      return this.caseCache.get(caseId)!
    }

    try {
      // In development, we'll load from the public folder
      // In production, this could be an API call
      const response = await fetch(`/cases/${caseId}/case.json`)
      
      if (!response.ok) {
        throw new Error(`Failed to load case ${caseId}: ${response.statusText}`)
      }

      const caseData: CaseData = await response.json()
      
      // Cache the loaded case
      this.caseCache.set(caseId, caseData)
      
      return caseData
    } catch (error) {
      console.error(`Error loading case ${caseId}:`, error)
      throw error
    }
  }

  /**
   * Get list of available cases (for development)
   * In production, this would come from the backend API
   */
  static async getAvailableCases(): Promise<string[]> {
    // For now, return known case IDs
    // In the future, this could scan a directory or call an API
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