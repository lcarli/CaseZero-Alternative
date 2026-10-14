import { beforeEach, describe, expect, it, vi } from 'vitest'
import { forensicsService } from '../services/forensicsService'

const api = vi.hoisted(() => ({ createForensicRequest: vi.fn(), getForensicRequests: vi.fn() }))
vi.mock('../services/api', () => ({ forensicRequestApi: api }))

const dto = {
  id: 42, caseId: 'case_real', userId: 'player', inputAssetId: 'asset.phone',
  inputAssetName: 'Phone', analysisType: 'ChemicalComparison',
  requestedAt: '2024-01-01T08:00:00Z', estimatedCompletionTime: '2024-01-01T08:17:00Z', status: 'pending',
}

describe('forensic requests', () => {
  beforeEach(() => { vi.resetAllMocks(); api.createForensicRequest.mockResolvedValue(dto) })

  it('posts only case, evidence, configured type and optional notes; never client deadlines or completion state', async () => {
    const result = await forensicsService.requestForensicAnalysis('case_real', 'asset.phone', 'ChemicalComparison', 'Compare records')
    expect(api.createForensicRequest).toHaveBeenCalledWith({
      caseId: 'case_real', inputAssetId: 'asset.phone', analysisType: 'ChemicalComparison', notes: 'Compare records',
    })
    expect(result.id).toBe('42')
    expect(result.analysisType).toBe('ChemicalComparison')
    expect(result.estimatedCompletionTime.getTime()).toBe(new Date(dto.estimatedCompletionTime).getTime())
  })

  it('uses backend status rather than converting an elapsed deadline into a completed result', async () => {
    api.getForensicRequests.mockResolvedValue([dto])
    const [result] = await forensicsService.getForensicRequests('case_real')
    expect(forensicsService.getTimeRemaining(result, new Date('2024-01-01T10:00:00Z'))).toBe(0)
    expect(result.status).toBe('pending')
    expect(result.resultDocumentId).toBeUndefined()
  })

  it('preserves returned report, email and explicit failure codes', async () => {
    api.getForensicRequests.mockResolvedValue([
      { ...dto, status: 'completed', resultDocumentId: 'asset.report', resultEmailId: 'email.lab' },
      { ...dto, id: 43, status: 'failed', failureCode: 'forensic_configuration_invalid' },
    ])
    const results = await forensicsService.getForensicRequests('case_real')
    expect(results[0].resultDocumentId).toBe('asset.report')
    expect(results[0].resultEmailId).toBe('email.lab')
    expect(results[1].failureCode).toBe('forensic_configuration_invalid')
  })

  it('does not turn an invalid response into a success-shaped request', async () => {
    api.getForensicRequests.mockResolvedValue([{ ...dto, status: 'invented' }])
    await expect(forensicsService.getForensicRequests('case_real')).rejects.toThrow('Invalid forensic request response')
  })
})
