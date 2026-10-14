import { forensicRequestApi, type ForensicRequestDTO } from './api'
import type { Translations } from '../types/i18n'
import type { AnalysisTypeConfig, Asset } from '../types/caseV2'

export type ForensicAnalysisType = string
export type ForensicRequestStatus = 'pending' | 'in-progress' | 'completed' | 'cancelled' | 'failed'

export interface ForensicRequest {
  id: string
  caseId: string
  evidenceId: string
  evidenceName: string
  analysisType: string
  requestedAt: Date
  estimatedCompletionTime: Date
  completedAt?: Date
  status: ForensicRequestStatus
  resultDocumentId?: string
  resultEmailId?: string
  notes?: string
  failureCode?: string
}

const labels: Record<string, keyof Translations> = {
  dna: 'dnaAnalysis', fingerprint: 'fingerprintAnalysis', digital: 'digitalForensics',
  digitalforensics: 'digitalForensics', ballistics: 'ballisticsAnalysis',
  toxicology: 'toxicologyAnalysis', handwriting: 'handwritingAnalysis', voice: 'voiceAnalysis',
  trace: 'traceAnalysis',
}

export function analysisLabel(type: string, t: (key: keyof Translations) => string): string {
  const key = labels[type.toLowerCase()]
  return key ? t(key) : type
}

export function supportsAnalysis(config: AnalysisTypeConfig, asset: Asset): boolean {
  return config.durationMinutes > 0 && (config.availableFor.includes(asset.type) ||
    typeof asset.metadata?.physicalType === 'string' && config.availableFor.includes(asset.metadata.physicalType))
}

function fromDto(dto: ForensicRequestDTO): ForensicRequest {
  const isStatus = (status: string): status is ForensicRequestStatus =>
    ['pending', 'in-progress', 'completed', 'cancelled', 'failed'].includes(status)
  if (dto.id === undefined || !isStatus(dto.status))
    throw new Error('Invalid forensic request response')
  return {
    id: String(dto.id), caseId: dto.caseId, evidenceId: dto.inputAssetId,
    evidenceName: dto.inputAssetName ?? dto.inputAssetId, analysisType: dto.analysisType,
    requestedAt: new Date(dto.requestedAt), estimatedCompletionTime: new Date(dto.estimatedCompletionTime),
    completedAt: dto.completedAt ? new Date(dto.completedAt) : undefined,
    status: dto.status, resultDocumentId: dto.resultDocumentId,
    resultEmailId: dto.resultEmailId, notes: dto.notes, failureCode: dto.failureCode,
  }
}

export const forensicsService = {
  async getForensicRequests(caseId: string): Promise<ForensicRequest[]> {
    return (await forensicRequestApi.getForensicRequests(caseId)).map(fromDto)
  },
  async getPendingForensicRequests(caseId: string): Promise<ForensicRequest[]> {
    return (await forensicRequestApi.getPendingForensicRequests(caseId)).map(fromDto)
  },
  async getForensicRequest(caseId: string, id: string): Promise<ForensicRequest> {
    return fromDto(await forensicRequestApi.getForensicRequest(caseId, Number(id)))
  },
  async requestForensicAnalysis(caseId: string, evidenceId: string, analysisType: string, notes?: string): Promise<ForensicRequest> {
    return fromDto(await forensicRequestApi.createForensicRequest({ caseId, inputAssetId: evidenceId, analysisType, notes }))
  },
  async cancelForensicRequest(caseId: string, requestId: string): Promise<void> {
    await forensicRequestApi.deleteForensicRequest(caseId, Number(requestId))
  },
  getTimeRemaining(request: ForensicRequest, gameTime: Date): number {
    return Math.max(0, Math.ceil((request.estimatedCompletionTime.getTime() - gameTime.getTime()) / 60000))
  },
}
