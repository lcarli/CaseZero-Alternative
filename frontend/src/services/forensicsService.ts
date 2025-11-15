import type { ForensicRequest, ForensicAnalysisType } from '../types/case'
import { forensicRequestApi, type ForensicRequestDTO } from './api'

/**
 * Forensic analysis durations in game time (minutes)
 * These represent realistic forensic laboratory processing times
 */
const FORENSIC_DURATIONS: Record<ForensicAnalysisType, { min: number; max: number }> = {
  DNA: { min: 240, max: 360 },              // 4-6 hours
  Fingerprint: { min: 120, max: 180 },      // 2-3 hours
  DigitalForensics: { min: 360, max: 720 }, // 6-12 hours
  Ballistics: { min: 180, max: 300 }        // 3-5 hours
}

/**
 * Calculate a random duration within the specified range for an analysis type
 */
export function calculateForensicDuration(analysisType: ForensicAnalysisType): number {
  const { min, max } = FORENSIC_DURATIONS[analysisType]
  return Math.floor(Math.random() * (max - min + 1)) + min
}

/**
 * Calculate the estimated completion time for a forensic request
 */
export function calculateEstimatedCompletionTime(
  requestedAt: Date,
  analysisType: ForensicAnalysisType
): Date {
  const durationMinutes = calculateForensicDuration(analysisType)
  const completionTime = new Date(requestedAt)
  completionTime.setMinutes(completionTime.getMinutes() + durationMinutes)
  return completionTime
}

/**
 * Check if a forensic request is complete based on current game time
 */
export function isForensicRequestComplete(
  request: ForensicRequest,
  currentGameTime: Date
): boolean {
  return currentGameTime >= request.estimatedCompletionTime
}

/**
 * Convert ForensicRequestDTO from API to ForensicRequest
 */
function dtoToForensicRequest(dto: ForensicRequestDTO): ForensicRequest {
  return {
    id: dto.id?.toString() || '',
    caseId: dto.caseId,
    evidenceId: dto.evidenceId,
    evidenceName: dto.evidenceName,
    analysisType: dto.analysisType,
    requestedAt: new Date(dto.requestedAt),
    estimatedCompletionTime: new Date(dto.estimatedCompletionTime),
    completedAt: dto.completedAt ? new Date(dto.completedAt) : undefined,
    status: dto.status,
    resultDocumentId: dto.resultDocumentId,
    notes: dto.notes
  }
}

/**
 * Convert ForensicRequest to ForensicRequestDTO for API
 */
function forensicRequestToDto(request: ForensicRequest): ForensicRequestDTO {
  return {
    id: request.id ? parseInt(request.id) : undefined,
    caseId: request.caseId,
    evidenceId: request.evidenceId,
    evidenceName: request.evidenceName,
    analysisType: request.analysisType,
    requestedAt: request.requestedAt.toISOString(),
    estimatedCompletionTime: request.estimatedCompletionTime.toISOString(),
    completedAt: request.completedAt?.toISOString(),
    status: request.status,
    resultDocumentId: request.resultDocumentId,
    notes: request.notes
  }
}

/**
 * Forensic Request Service
 * Handles all forensic request operations with proper time calculations
 */
export const forensicsService = {
  /**
   * Get all forensic requests for a case
   */
  async getForensicRequests(caseId: string): Promise<ForensicRequest[]> {
    const dtos = await forensicRequestApi.getForensicRequests(caseId)
    return dtos.map(dtoToForensicRequest)
  },

  /**
   * Get pending forensic requests for a case
   */
  async getPendingForensicRequests(caseId: string): Promise<ForensicRequest[]> {
    const dtos = await forensicRequestApi.getPendingForensicRequests(caseId)
    return dtos.map(dtoToForensicRequest)
  },

  /**
   * Get a specific forensic request
   */
  async getForensicRequest(caseId: string, id: string): Promise<ForensicRequest> {
    const dto = await forensicRequestApi.getForensicRequest(caseId, parseInt(id))
    return dtoToForensicRequest(dto)
  },

  /**
   * Request a new forensic analysis
   */
  async requestForensicAnalysis(
    caseId: string,
    evidenceId: string,
    evidenceName: string,
    analysisType: ForensicAnalysisType,
    currentGameTime: Date,
    notes?: string
  ): Promise<ForensicRequest> {
    const estimatedCompletionTime = calculateEstimatedCompletionTime(
      currentGameTime,
      analysisType
    )

    const newRequest: ForensicRequest = {
      id: '', // Will be assigned by backend
      caseId,
      evidenceId,
      evidenceName,
      analysisType,
      requestedAt: currentGameTime,
      estimatedCompletionTime,
      status: 'pending',
      notes
    }

    const dto = await forensicRequestApi.createForensicRequest(
      forensicRequestToDto(newRequest)
    )

    return dtoToForensicRequest(dto)
  },

  /**
   * Complete a forensic request
   */
  async completeForensicRequest(
    caseId: string,
    requestId: string,
    currentGameTime: Date,
    resultDocumentId: string
  ): Promise<void> {
    const request = await this.getForensicRequest(caseId, requestId)
    
    request.status = 'completed'
    request.completedAt = currentGameTime
    request.resultDocumentId = resultDocumentId

    await forensicRequestApi.updateForensicRequest(
      caseId,
      parseInt(requestId),
      forensicRequestToDto(request)
    )
  },

  /**
   * Check for completed forensic requests based on current game time
   * Returns requests that have just become ready
   */
  async checkCompletedRequests(
    caseId: string,
    currentGameTime: Date
  ): Promise<ForensicRequest[]> {
    const pending = await this.getPendingForensicRequests(caseId)
    
    return pending.filter(request => 
      isForensicRequestComplete(request, currentGameTime) &&
      request.status === 'pending'
    )
  },

  /**
   * Cancel a forensic request
   */
  async cancelForensicRequest(caseId: string, requestId: string): Promise<void> {
    await forensicRequestApi.deleteForensicRequest(caseId, parseInt(requestId))
  },

  /**
   * Get time remaining for a forensic request in minutes
   */
  getTimeRemaining(request: ForensicRequest, currentGameTime: Date): number {
    const remaining = request.estimatedCompletionTime.getTime() - currentGameTime.getTime()
    return Math.max(0, Math.floor(remaining / 1000 / 60)) // Convert to minutes
  },

  /**
   * Get formatted time remaining string
   */
  getFormattedTimeRemaining(request: ForensicRequest, currentGameTime: Date): string {
    const minutes = this.getTimeRemaining(request, currentGameTime)
    
    if (minutes === 0) {
      return 'Ready'
    }

    const hours = Math.floor(minutes / 60)
    const mins = minutes % 60

    if (hours > 0) {
      return `${hours}h ${mins}m`
    }

    return `${mins}m`
  }
}
