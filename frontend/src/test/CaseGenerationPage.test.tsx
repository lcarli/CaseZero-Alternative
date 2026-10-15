import { StrictMode } from 'react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import '@testing-library/jest-dom'
import { MemoryRouter } from 'react-router-dom'
import { LanguageProvider } from '../contexts/LanguageContext'
import { translations, enUS } from '../locales'
import type { GenerateCaseStatus } from '../services/api'
import CaseGenerationPage from '../pages/CaseGenerationPage'

const api = vi.hoisted(() => ({ start: vi.fn(), getJob: vi.fn() }))
vi.mock('../services/api', () => ({ caseGenerationApi: api }))

const completed = (): GenerateCaseStatus => ({
  jobId: 'job-quality',
  status: 'done',
  runtimeStatus: 'Completed',
  result: {
    JobId: 'job-quality',
    CaseId: 'case_quality',
    OutputPath: '/cases/case_quality/case.json',
    ValidationErrorsCount: 0,
    AssetsRenderedPdfs: 5,
    AssetsRenderedImages: 0,
    AssetsSkipped: 0,
    BlobsPublished: 6,
    HasErrors: false,
    RedTeamVerdict: 'ok',
    StageLatencyMs: { plotOutline: 10, caseBible: 10, qualityGate: 10, renderAssets: 10, publishToBlob: 10, persistCase: 10 },
    Quality: {
      Approved: true,
      Preview: false,
      PersistenceRequested: true,
      Persisted: true,
      PublicationRequested: true,
      Published: true,
      SolverMode: 'diagnostic',
      Issues: []
    }
  }
})

function renderPage(language = 'en-US') {
  localStorage.setItem('casezero-language', language)
  return render(
    <StrictMode>
      <MemoryRouter>
        <LanguageProvider><CaseGenerationPage /></LanguageProvider>
      </MemoryRouter>
    </StrictMode>
  )
}

function expectStat(label: string, value: string) {
  expect(screen.getByText(label).parentElement).toHaveTextContent(value)
}

describe('CaseGenerationPage', () => {
  beforeEach(() => {
    vi.resetAllMocks()
    localStorage.clear()
    api.start.mockResolvedValue({ jobId: 'job-quality', status: 'queued', statusUri: '/jobs/job-quality' })
    api.getJob.mockResolvedValue(completed())
  })

  afterEach(() => {
    cleanup()
    vi.restoreAllMocks()
  })

  it.each(['pt-BR', 'en-US', 'es-ES', 'fr-FR'])('localizes form, phases, and results in %s', async language => {
    const locale = translations[language]
    renderPage(language)
    expect(screen.getByRole('heading', { name: locale.generateNewCase })).toBeInTheDocument()
    expect(screen.getByRole('combobox', { name: locale.language })).toHaveValue(language)
    expect(screen.getByPlaceholderText(locale.generationTitlePlaceholder)).toBeInTheDocument()
    expect(screen.getByRole('option', { name: locale.generationRankRookie })).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: locale.generateCaseBtn }))
    await screen.findByText(locale.generationSolverDiagnostic)
    expect(screen.getByText(locale.generationQualityScope)).toBeInTheDocument()
    expectStat(locale.generationApproved, locale.generationYes)
    expectStat(locale.generationPersisted, locale.generationYes)
    expectStat(locale.generationPublished, locale.generationYes)
    expect(screen.getByRole('listitem', { name: `${locale.generationPhasePublish}: ${locale.completed}` })).toBeInTheDocument()
    expect(screen.getByRole('listitem', { name: `${locale.generationPhaseCaseBible}: ${locale.completed}` })).toBeInTheDocument()
    expect(screen.getByRole('listitem', { name: `${locale.generationPhaseAutoFix}: ${locale.generationNotRun}` })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: locale.generationAnother })).toBeInTheDocument()
    expect(Object.entries(locale).filter(([key]) => key.startsWith('generation')).every(([, value]) => value.trim().length > 0)).toBe(true)
  })

  it.each(['timeline', 'rendering', 'publication'])('retains review blockers in area %s without inventing execution failures', async area => {
    const failed = completed()
    failed.status = 'failed'
    failed.error = 'red_team_needs_review: Conflicting timestamps'
    failed.result!.HasErrors = true
    failed.result!.RedTeamVerdict = 'needs_review'
    failed.result!.StageLatencyMs = { qualityGate: 10 }
    failed.result!.Quality = {
      ...failed.result!.Quality!,
      Approved: false, Persisted: false, Published: false,
      Issues: [{
        Code: 'red_team_finding', Severity: 'medium', Area: area,
        Reference: 'asset.scene', Message: 'Conflicting timestamps', Blocking: true
      }]
    }
    api.getJob.mockResolvedValue(failed)
    renderPage()
    fireEvent.click(screen.getByRole('button', { name: enUS.generateCaseBtn }))
    expect(await screen.findByRole('alert')).toHaveTextContent(enUS.caseGenerationError)
    expectStat(enUS.generationApproved, enUS.generationNo)
    expectStat(enUS.generationPersisted, enUS.generationNo)
    expectStat(enUS.generationPublished, enUS.generationNo)
    expect(screen.getByText('asset.scene')).toBeInTheDocument()
    const diagnostic = screen.getByText(enUS.generationIssueReviewFinding).closest('li')
    expect(diagnostic).toHaveTextContent(enUS.generationSeverityMedium)
    expect(diagnostic).toHaveTextContent(enUS.generationBlocking)
    expect(screen.getByRole('listitem', { name: `${enUS.generationPhaseQualityGate}: ${enUS.failed}` })).toBeInTheDocument()
    expect(screen.getByRole('listitem', { name: `${enUS.generationPhaseRender}: ${enUS.generationNotRun}` })).toBeInTheDocument()
    expect(api.getJob).toHaveBeenCalledTimes(1)
  })

  it.each(['pt-BR', 'en-US', 'es-ES', 'fr-FR'])('localizes canonical repair results in %s', async language => {
    const locale = translations[language]
    const repaired = completed()
    repaired.result!.Quality!.Revision = 1
    repaired.result!.Quality!.ReviewedRevision = 1
    repaired.result!.RefineAttempted = true
    repaired.result!.Repair = {
      Attempted: true, Accepted: true, BaseRevision: 0, CandidateRevision: 1,
      ChangedComponents: ['asset.report_access', 'email.lab_access', 'solution'],
      OriginalBlockers: ['red_team_finding: Conflicting timestamps']
    }
    repaired.result!.StageLatencyMs = { refineCase: 10, revalidateRepair: 10, qualityGate: 10 }
    api.getJob.mockResolvedValue(repaired)
    renderPage(language)
    fireEvent.click(screen.getByRole('button', { name: locale.generateCaseBtn }))
    await screen.findByText(locale.generationSolverDiagnostic)
    expectStat(locale.generationRevision, '1')
    expectStat(locale.generationReviewedRevision, '1')
    expectStat(locale.generationRepairAccepted, locale.generationYes)
    expect(screen.getByText(locale.generationRepairChanges)).toBeInTheDocument()
    expect(screen.getByText(locale.generationRepairOriginalIssues)).toBeInTheDocument()
    expect(screen.getByText('asset.report_access, email.lab_access, solution')).toBeInTheDocument()
    expect(screen.getByRole('listitem', { name: `${locale.generationPhaseRepairReview}: ${locale.completed}` })).toBeInTheDocument()
  })

  it.each(['pt-BR', 'en-US', 'es-ES', 'fr-FR'])('localizes verified artifact bundle results in %s', async language => {
    const locale = translations[language]
    const bundled = completed()
    bundled.result!.Bundle = {
      BundleId: '0123456789abcdef0123456789abcdef', Revision: 1, Verified: true,
      AssetsVerified: 5, TotalBytes: 4096, ManifestChecksum: `sha256:${'a'.repeat(64)}`
    }
    bundled.result!.StageLatencyMs = { verifyBundle: 10, preparePublication: 10, publishToBlob: 10, persistCase: 10 }
    api.getJob.mockResolvedValue(bundled)
    renderPage(language)
    fireEvent.click(screen.getByRole('button', { name: locale.generateCaseBtn }))
    await screen.findByText(locale.generationSolverDiagnostic)
    expectStat(locale.generationBundleVerified, locale.generationYes)
    expectStat(locale.generationBundleRevision, '1')
    expectStat(locale.generationBundleAssets, '5')
    expectStat(locale.generationBundleBytes, '4096')
    expect(screen.getByText(locale.generationBundleId)).toBeInTheDocument()
    expect(screen.getByText(locale.generationBundleChecksum)).toBeInTheDocument()
    expect(screen.getByText(bundled.result!.Bundle.ManifestChecksum)).toBeInTheDocument()
    expect(screen.getByRole('listitem', { name: `${locale.generationPhaseVerifyBundle}: ${locale.completed}` })).toBeInTheDocument()
    expect(screen.getByRole('listitem', { name: `${locale.generationPhasePreparePublication}: ${locale.completed}` })).toBeInTheDocument()
  })

  it.each([
    ['bundle_invalid', 'artifacts', 'generationIssueBundle', 'generationPhaseVerifyBundle'],
    ['asset_integrity_failed', 'artifacts', 'generationIssueIntegrity', 'generationPhaseVerifyBundle'],
    ['asset_file_missing', 'artifacts', 'generationIssueMissingFile', 'generationPhaseVerifyBundle'],
    ['asset_integrity_failed', 'publication', 'generationIssueIntegrity', 'generationPhasePublish'],
    ['publication_conflict', 'publication', 'generationIssuePublicationConflict', 'generationPhasePublish'],
    ['publication_conflict', 'persistence', 'generationIssuePublicationConflict', 'generationPhasePreparePublication']
  ] as const)('shows the actual artifact failure phase for %s in %s', async (code, area, label, phase) => {
    const failed = completed()
    failed.status = 'failed'
    failed.result!.HasErrors = true
    failed.result!.Quality = {
      ...failed.result!.Quality!, Approved: false, Persisted: false, Published: false,
      Issues: [{ Code: code, Area: area, Message: 'Original technical detail', Severity: 'high', Blocking: true }]
    }
    failed.result!.StageLatencyMs = { verifyBundle: 10, ...(area === 'artifacts' ? {} : { preparePublication: 10, publishToBlob: 10 }) }
    api.getJob.mockResolvedValue(failed)
    renderPage()
    fireEvent.click(screen.getByRole('button', { name: enUS.generateCaseBtn }))
    await screen.findByRole('alert')
    expect(screen.getByText(enUS[label])).toBeInTheDocument()
    expect(screen.getByRole('listitem', { name: `${enUS[phase]}: ${enUS.failed}` })).toBeInTheDocument()
  })

  it.each([
    ['repair_invalid', 'generationPhaseRefine'],
    ['repair_rejected', 'generationPhaseRepairReview']
  ] as const)('marks the actual failed repair phase for %s', async (code, phase) => {
    const failed = completed()
    failed.status = 'failed'
    failed.result!.HasErrors = true
    failed.result!.Quality!.Approved = false
    failed.result!.Quality!.Persisted = false
    failed.result!.Quality!.Published = false
    failed.result!.Quality!.Issues = [{
      Code: code, Severity: 'high', Area: 'refine', Message: 'Repair did not resolve blockers', Blocking: true
    }]
    failed.result!.StageLatencyMs = { refineCase: 10, ...(code === 'repair_rejected' ? { revalidateRepair: 10 } : {}) }
    api.getJob.mockResolvedValue(failed)
    renderPage()
    fireEvent.click(screen.getByRole('button', { name: enUS.generateCaseBtn }))
    await screen.findByRole('alert')
    expect(screen.getByText(enUS.generationIssueRepair)).toBeInTheDocument()
    expect(screen.getByRole('listitem', { name: `${enUS[phase]}: ${enUS.failed}` })).toBeInTheDocument()
    expect(screen.getByRole('listitem', { name: `${enUS.generationPhaseRender}: ${enUS.generationNotRun}` })).toBeInTheDocument()
  })

  it('shows a failed render rather than marking it complete', async () => {
    const failed = completed()
    failed.status = 'failed'
    failed.result!.HasErrors = true
    failed.result!.StageLatencyMs = { qualityGate: 10, renderAssets: 10 }
    failed.result!.Quality!.Approved = false
    failed.result!.Quality!.Persisted = false
    failed.result!.Quality!.Published = false
    failed.result!.Quality!.Issues = [{
      Code: 'asset_render_failed', Severity: 'high', Area: 'rendering',
      Message: 'PDF failure', Blocking: true
    }]
    api.getJob.mockResolvedValue(failed)
    renderPage()
    fireEvent.click(screen.getByRole('button', { name: enUS.generateCaseBtn }))
    await screen.findByRole('alert')
    expect(screen.getByRole('listitem', { name: `${enUS.generationPhaseRender}: ${enUS.failed}` })).toBeInTheDocument()
    expect(screen.getByRole('listitem', { name: `${enUS.generationPhasePublish}: ${enUS.generationNotRun}` })).toBeInTheDocument()
    expect(screen.getByText(enUS.generationIssueRender)).toBeInTheDocument()
  })

  it('requests a draft preview and never claims it was saved or published', async () => {
    const preview = completed()
    preview.result!.Quality = {
      ...preview.result!.Quality!,
      Preview: true, PersistenceRequested: false, Persisted: false,
      PublicationRequested: false, Published: false
    }
    preview.result!.StageLatencyMs = { qualityGate: 10 }
    api.getJob.mockResolvedValue(preview)
    renderPage()
    fireEvent.click(screen.getByRole('checkbox', { name: enUS.generationSaveLocally }))
    fireEvent.click(screen.getByRole('button', { name: enUS.generateCaseBtn }))
    await screen.findByText(enUS.generationSolverDiagnostic)
    expect(api.start).toHaveBeenCalledWith(expect.objectContaining({ writeToDisk: false }))
    expectStat(enUS.generationApproved, enUS.generationYes)
    expectStat(enUS.generationPersisted, enUS.generationNo)
    expectStat(enUS.generationPublished, enUS.generationNo)
    expectStat(enUS.generationOutputMode, enUS.generationPreview)
    expect(screen.getByRole('listitem', { name: `${enUS.generationPhaseRender}: ${enUS.generationNotRun}` })).toBeInTheDocument()
  })

  it('omits blank optional fields instead of submitting an invalid strict request', async () => {
    renderPage()
    fireEvent.change(screen.getByPlaceholderText(enUS.generationTitlePlaceholder), { target: { value: ' ' } })
    fireEvent.change(screen.getByPlaceholderText(enUS.generationLocationPlaceholder), { target: { value: 'temporary' } })
    fireEvent.change(screen.getByPlaceholderText(enUS.generationLocationPlaceholder), { target: { value: '' } })
    fireEvent.change(screen.getByPlaceholderText(enUS.generationThemePlaceholder), { target: { value: '  records fraud  ' } })
    fireEvent.click(screen.getByRole('button', { name: enUS.generateCaseBtn }))
    await screen.findByText(enUS.generationSolverDiagnostic)
    expect(api.start).toHaveBeenCalledWith(expect.objectContaining({
      title: undefined, location: undefined, caseId: undefined, theme: 'records fraud'
    }))
  })

  it('uses the same text and seed limits as the HTTP contract', () => {
    renderPage()
    expect(screen.getByPlaceholderText(enUS.generationTitlePlaceholder)).toHaveAttribute('maxlength', '200')
    expect(screen.getByPlaceholderText(enUS.generationLocationPlaceholder)).toHaveAttribute('maxlength', '300')
    expect(screen.getByPlaceholderText(enUS.generationThemePlaceholder)).toHaveAttribute('maxlength', '2000')
    expect(screen.getByPlaceholderText(enUS.generationAuto)).toHaveAttribute('maxlength', '128')
    expect(screen.getByPlaceholderText(enUS.generationRandom)).toHaveAttribute('min', '-2147483648')
    expect(screen.getByPlaceholderText(enUS.generationRandom)).toHaveAttribute('max', '2147483647')
  })

  it('distinguishes local-only success from publication', async () => {
    const local = completed()
    local.result!.Quality!.PublicationRequested = false
    local.result!.Quality!.Published = false
    local.result!.BlobsPublished = 0
    api.getJob.mockResolvedValue(local)
    renderPage()
    fireEvent.click(screen.getByRole('button', { name: enUS.generateCaseBtn }))
    await screen.findByText(enUS.generationSolverDiagnostic)
    expectStat(enUS.generationApproved, enUS.generationYes)
    expectStat(enUS.generationPersisted, enUS.generationYes)
    expectStat(enUS.generationPublished, enUS.generationNo)
    expectStat(enUS.generationOutputMode, enUS.generationLocalOnly)
  })

  it('does not invent approval or publication flags for historical results', async () => {
    const historical = completed()
    delete historical.result!.Quality
    api.getJob.mockResolvedValue(historical)
    renderPage()
    fireEvent.click(screen.getByRole('button', { name: enUS.generateCaseBtn }))
    await screen.findByText(enUS.generationSolverDiagnostic)
    expectStat(enUS.generationApproved, enUS.generationUnknown)
    expectStat(enUS.generationPersisted, enUS.generationUnknown)
    expectStat(enUS.generationPublished, enUS.generationUnknown)
    expectStat(enUS.generationBundleVerified, enUS.generationUnknown)
    expectStat(enUS.generationBundleAssets, enUS.generationUnknown)
  })

  it('shows failed status instead of crashing on an invalid stored quality report', async () => {
    const invalid = completed()
    api.getJob.mockResolvedValue({
      ...invalid,
      status: 'failed',
      error: 'Generation completed with an invalid quality report.',
      result: { ...invalid.result, Quality: { ...invalid.result!.Quality, Issues: null } }
    })
    renderPage()
    fireEvent.click(screen.getByRole('button', { name: enUS.generateCaseBtn }))
    expect(await screen.findByRole('alert')).toHaveTextContent(enUS.caseGenerationError)
    expectStat(enUS.generationApproved, enUS.generationUnknown)
    expectStat(enUS.generationPublished, enUS.generationUnknown)
  })

  it('clears a transient polling error when the next poll succeeds', async () => {
    const timers = vi.spyOn(window, 'setTimeout')
    api.getJob.mockRejectedValueOnce(new Error('Network unavailable')).mockResolvedValueOnce(completed())
    renderPage()
    fireEvent.click(screen.getByRole('button', { name: enUS.generateCaseBtn }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Network unavailable')
    const retry = timers.mock.calls.find(([, delay]) => delay === 5000)?.[0]
    if (typeof retry !== 'function') throw new Error('Polling retry was not scheduled')
    await act(async () => { retry() })
    await screen.findByText(enUS.generationSolverDiagnostic)
    expect(screen.queryByText('Network unavailable')).not.toBeInTheDocument()
    expect(api.getJob).toHaveBeenCalledTimes(2)
  })

  it('does not schedule another poll after unmount', async () => {
    const timers = vi.spyOn(window, 'setTimeout')
    let resolvePoll: (value: GenerateCaseStatus) => void = () => { throw new Error('Poll not started') }
    api.getJob.mockReturnValueOnce(new Promise<GenerateCaseStatus>(resolve => { resolvePoll = resolve }))
    const page = renderPage()
    fireEvent.click(screen.getByRole('button', { name: enUS.generateCaseBtn }))
    await waitFor(() => expect(api.getJob).toHaveBeenCalledTimes(1))
    page.unmount()
    await act(async () => { resolvePoll({ jobId: 'job-quality', status: 'running', currentPhase: 'plotOutline' }) })
    expect(timers.mock.calls.filter(([, delay]) => delay === 2500 || delay === 5000)).toHaveLength(0)
  })

  it('keeps attempt/stage metrics and prior retries alongside the quality and bundle reports', async () => {
    const retried = completed()
    retried.currentAttempt = 2
    retried.maxAttempts = 3
    retried.stages = [
      { id: 'caseDesign', status: 'completed', attempt: 2, durationMs: 100 },
      { id: 'finalValidation', status: 'completed', attempt: 2, durationMs: 50 },
      { id: 'finalization', status: 'completed', attempt: 2, durationMs: 75 },
    ]
    retried.attempts = [{
      number: 1, status: 'failed', startedAt: '2026-10-14T12:00:00Z',
      completedAt: '2026-10-14T12:00:01Z', stages: [], error: 'First attempt failed review',
    }]
    Object.assign(retried.result!, {
      AttemptCount: 2, MaxAttempts: 3, GraphValidationPassed: true, SolverSucceeded: true,
      SolverScore: 0.85, RepairOperationCount: 2,
      Bundle: {
        BundleId: 'verified-bundle', Revision: 1, Verified: true,
        AssetsVerified: 5, TotalBytes: 4096, ManifestChecksum: `sha256:${'b'.repeat(64)}`,
      },
    })
    api.getJob.mockResolvedValue(retried)
    renderPage()
    fireEvent.click(screen.getByRole('button', { name: enUS.generateCaseBtn }))
    await screen.findByText(enUS.generationSolverDiagnostic)
    expect(screen.getByText(enUS.generationAttemptHistory)).toBeInTheDocument()
    expect(screen.getByText(`${enUS.generationAttempt} 1 · ${enUS.failed}`)).toBeInTheDocument()
    expect(screen.getByText(new RegExp(enUS.stageCaseDesign))).toBeInTheDocument()
    expectStat(enUS.generationAttempt, '2 / 3')
    expectStat(enUS.generationSolverScore, '0.85')
    expectStat(enUS.generationRepairOperations, '2')
    expectStat(enUS.generationApproved, enUS.generationYes)
    expectStat(enUS.generationBundleVerified, enUS.generationYes)
  })

  it('shows the scheduled retry and current stage while generation is still running', async () => {
    api.getJob.mockResolvedValue({
      jobId: 'job-quality', status: 'running', currentStageId: 'finalValidation',
      currentPhase: 'qualityGate', currentAttempt: 2, maxAttempts: 3, progressPercent: 70,
      nextRetryAt: '2026-10-14T12:01:00Z', retryReason: 'Review retry',
      stages: [{ id: 'finalValidation', status: 'running', attempt: 2 }],
    } satisfies GenerateCaseStatus)
    renderPage()
    fireEvent.click(screen.getByRole('button', { name: enUS.generateCaseBtn }))
    await screen.findByText(enUS.generationRetryScheduled)
    expect(screen.getByText('Review retry')).toBeInTheDocument()
    expect(screen.getByText(new RegExp(enUS.stageFinalValidation))).toBeInTheDocument()
    expect(screen.queryByText(enUS.generationApproved)).not.toBeInTheDocument()
  })

  it.each(['pt-BR', 'en-US', 'es-ES', 'fr-FR'])('distinguishes player-visible model evaluation from graph diagnostics in %s', async language => {
    const locale = translations[language]
    const investigated = completed()
    investigated.result!.Quality!.SolverMode = 'player-visible'
    investigated.result!.SolverSucceeded = true
    investigated.result!.StageLatencyMs = {
      prepareMaterials: 5, playerInvestigation: 25, blockingReview: 15,
      targetedRepair: 10, renderAssets: 20, verifyBundle: 5, publishToBlob: 10, persist: 5,
    }
    api.getJob.mockResolvedValue(investigated)
    renderPage(language)
    fireEvent.click(screen.getByRole('button', { name: locale.generateCaseBtn }))
    await screen.findByText(locale.generationPlayerInvestigationScope)
    expect(screen.getByText(locale.generationPlayerQualityScope)).toBeInTheDocument()
    expect(screen.queryByText(locale.generationSolverDiagnostic)).not.toBeInTheDocument()
    for (const label of [
      locale.generationPhasePrepareMaterials, locale.generationPhasePlayerInvestigation,
      locale.generationPhaseBlockingReview, locale.generationPhaseRefine, locale.generationPhaseRender,
      locale.generationPhaseVerifyBundle, locale.generationPhasePublish, locale.generationPhasePersist,
    ]) {
      expect(screen.getByRole('listitem', { name: `${label}: ${locale.completed}` })).toBeInTheDocument()
    }
    expectStat(locale.generationSolutionWitness, locale.generationYes)
  })

  it.each([
    ['prepareMaterials', 'generationPhasePrepareMaterials'],
    ['playerInvestigation', 'generationPhasePlayerInvestigation'],
    ['blockingReview', 'generationPhaseBlockingReview'],
    ['verifyBundle', 'generationPhaseVerifyBundle'],
    ['persist', 'generationPhasePersist'],
    ['targetedRepair', 'generationPhaseRefine'],
  ] as const)('maps current phase %s without showing an untranslated identifier', async (phase, label) => {
    api.getJob.mockResolvedValue({ jobId: 'job-quality', status: 'running', currentPhase: phase } satisfies GenerateCaseStatus)
    renderPage()
    fireEvent.click(screen.getByRole('button', { name: enUS.generateCaseBtn }))
    await screen.findByText(new RegExp(enUS[label].replace(/[.*+?^${}()|[\]\\]/g, '\\$&')))
    expect(screen.queryByText(phase)).not.toBeInTheDocument()
  })
})
