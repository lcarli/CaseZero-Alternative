import type { CaseData } from '../types/case'
import { casesApi, casesV1Api } from './api'
import type { CaseV1 } from '../types/caseV1'

/**
 * Service to load case data from the backend API
 * This provides secure access to case data without exposing sensitive information in the DOM
 */
export class CaseDataService {
  private static caseCache = new Map<string, CaseData>()

  /**
   * Check if a case ID is v1.0 format (case_*)
   */
  private static isV1Case(caseId: string): boolean {
    return caseId.startsWith('case_')
  }

  /**
   * Convert CaseV1 (blob format) to CaseData (legacy format)
   */
  private static convertV1ToCaseData(caseV1: CaseV1): CaseData {
    // Map v1.0 assets to evidences
    const evidences = caseV1.assets.map(asset => ({
      id: asset.assetId,
      name: asset.name,
      type: asset.type,
      fileName: asset.filePath,
      category: asset.category,
      priority: 'Medium' as const,
      description: asset.description,
      location: caseV1.metadata.location || '',
      isUnlocked: asset.visibility === 'initial',
      requiresAnalysis: false,
      dependsOn: [],
      linkedSuspects: [],
      unlockConditions: {
        immediate: asset.visibility === 'initial'
      }
    }))

    // Map v1.0 suspects
    const suspects = caseV1.suspects.map(suspect => ({
      id: suspect.suspectId,
      name: suspect.name,
      alias: suspect.alias,
      age: suspect.age,
      occupation: suspect.occupation,
      description: suspect.background,
      relationship: suspect.relationship,
      motive: suspect.motive,
      alibi: suspect.alibi,
      alibiVerified: suspect.alibiVerified,
      behavior: '',
      backgroundInfo: suspect.background,
      linkedEvidence: suspect.knownEvidence || [],
      comments: '',
      isActualCulprit: false,
      status: 'PersonOfInterest' as const,
      unlockConditions: {
        immediate: true
      }
    }))

    return {
      caseId: caseV1.caseId,
      metadata: {
        title: caseV1.metadata.title,
        description: caseV1.metadata.description,
        startDateTime: caseV1.metadata.incidentDate,
        location: caseV1.metadata.location,
        incidentDateTime: caseV1.metadata.incidentDate,
        victimInfo: caseV1.metadata.victim ? {
          name: caseV1.metadata.victim.name,
          age: caseV1.metadata.victim.age,
          occupation: caseV1.metadata.victim.occupation,
          causeOfDeath: caseV1.metadata.victim.causeOfDeath
        } : undefined,
        briefing: caseV1.metadata.briefing,
        difficulty: caseV1.metadata.difficulty,
        estimatedDuration: `${caseV1.metadata.estimatedTimeMinutes} minutes`,
        minRankRequired: caseV1.metadata.requiredRank
      },
      evidences,
      suspects,
      forensicAnalyses: [],
      temporalEvents: [],
      timeline: [],
      solution: {
        culprit: '',
        keyEvidence: '',
        explanation: '',
        requiredEvidence: [],
        minimumScore: 0
      },
      unlockLogic: {
        progressionRules: [],
        analysisRules: []
      },
      gameMetadata: {
        version: caseV1.version,
        createdAt: new Date().toISOString(),
        createdBy: 'System',
        tags: [],
        difficulty: caseV1.metadata?.difficulty?.toString() || 'Medium',
        estimatedPlayTime: caseV1.metadata?.estimatedTimeMinutes ? `${caseV1.metadata.estimatedTimeMinutes} minutes` : '60 minutes'
      }
    }
  }

  /**
   * Load case data from the backend API
   */
  static async loadCase(caseId: string): Promise<CaseData> {
    // Check cache first
    if (this.caseCache.has(caseId)) {
      return this.caseCache.get(caseId)!
    }

    try {
      let caseData: CaseData

      // Check if this is a v1.0 case (from blob storage)
      if (this.isV1Case(caseId)) {
        console.log(`Loading v1.0 case from blob: ${caseId}`)
        const caseV1 = await casesV1Api.getCase(caseId)
        caseData = this.convertV1ToCaseData(caseV1)
      } else {
        // Legacy case format
        console.log(`Loading legacy case: ${caseId}`)
        caseData = await casesApi.getCaseData(caseId)
      }
      
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