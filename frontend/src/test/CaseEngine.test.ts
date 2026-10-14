import { describe, expect, it, vi } from 'vitest'
import { CaseEngine, type ApiClient } from '../engine/CaseEngine'
import type { CaseV2Sanitized, SubmitCaseResult } from '../types/caseV2'

function data(): CaseV2Sanitized {
  return {
    version: '2.0', caseId: 'case_engine', metadata: {
      title: 'T', description: 'D', location: 'L', incidentDate: '2024-01-01T07:00:00Z',
      openedAt: '2024-01-01T08:00:00Z', difficulty: 'Detective', requiredRank: 'Detective',
    },
    assets: [{ id: 'asset.unlocked', title: 'Unlocked report', type: 'pdf', uri: 'case://case_engine/assets/bundle/report.pdf', visibility: 'hidden' }],
    emails: [{ id: 'email.unlocked', from: 'Lab', to: [], subject: 'Report', body: 'Actual result', sentAt: '2024-01-01T08:15:00Z', visibility: 'hidden' }],
    suspects: [{ id: 'suspect.unlocked', name: 'Witness', visibility: 'hidden' }],
    timeline: [], forensicsDefaults: { analysisTypes: [] }, solution: { questions: [], minimumScore: 0.7, maxAttempts: 3 },
    notifications: [{ level: 'info', message: 'Persisted notification' }],
  }
}

describe('sanitized case progression', () => {
  it('keeps server-authorized unlocked entities and notifications on reopen', async () => {
    const getCase = vi.fn().mockResolvedValue(data())
    const api: ApiClient = { getCase, viewAsset: vi.fn(), openEmail: vi.fn(), viewSuspect: vi.fn(), submitCase: vi.fn(), postGameTime: vi.fn() }
    const engine = new CaseEngine(api)
    await engine.loadCase('case_engine')
    expect(engine.getSnapshot().visibleAssets).toHaveLength(1)
    expect(engine.getSnapshot().visibleEmails).toHaveLength(1)
    expect(engine.getSnapshot().visibleSuspects).toHaveLength(1)
    expect(engine.getSnapshot().notifications[0].message).toBe('Persisted notification')
  })

  it('refreshes reveals after actions without resetting solution attempts', async () => {
    const result: SubmitCaseResult = { correct: false, score: 0, breakdown: { culpritScore: 0, evidenceScore: 0, analysisScore: 0, questionsScore: 0 }, attemptsRemaining: 2, feedbackText: 'Wrong' }
    const getCase = vi.fn().mockResolvedValue(data())
    const viewAsset = vi.fn().mockResolvedValue(undefined)
    const api: ApiClient = { getCase, viewAsset, openEmail: vi.fn(), viewSuspect: vi.fn(), submitCase: vi.fn().mockResolvedValue(result), postGameTime: vi.fn() }
    const engine = new CaseEngine(api)
    await engine.loadCase('case_engine')
    await engine.submitCase({ suspectId: '', evidenceIds: [], analysisIds: [], answers: [] })
    await engine.viewAsset('asset.unlocked')
    expect(viewAsset).toHaveBeenCalledWith('case_engine', 'asset.unlocked')
    expect(getCase).toHaveBeenCalledTimes(2)
    expect(engine.getSnapshot().submission.attemptsUsed).toBe(1)
  })
})
