import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import '@testing-library/jest-dom'
import { LanguageProvider } from '../contexts/LanguageContext'
import { translations, enUS } from '../locales'
import type { ForensicRequest } from '../services/forensicsService'
import ForensicModule from '../components/apps/ForensicModule'

const mocks = vi.hoisted(() => ({
  get: vi.fn(), request: vi.fn(), refresh: vi.fn(), view: vi.fn(), email: vi.fn(), openWindow: vi.fn(),
}))

vi.mock('../hooks/useCaseContext', () => ({
  useCase: () => ({
    caseId: 'case_live',
    state: {
      case: { forensicsDefaults: { analysisTypes: [
        { type: 'ChemicalComparison', durationMinutes: 17, availableFor: ['digital'] },
        { type: 'DNA', durationMinutes: 240, availableFor: ['photo'] },
      ] } },
      visibleAssets: [
        { id: 'asset.phone', type: 'digital', title: 'Visible phone', uri: 'case://case_live/assets/bundle/phone.pdf' },
        { id: 'asset.report', type: 'pdf', title: 'Actual report', uri: 'case://case_live/assets/bundle/report.pdf' },
      ],
      visibleEmails: [{ id: 'email.lab', from: 'Lab', subject: 'Actual laboratory result', body: 'Verified conclusion from the case' }],
    },
    refreshCase: mocks.refresh, viewAsset: mocks.view, openEmail: mocks.email,
  }),
}))
vi.mock('../hooks/useTimeContext', () => ({
  useTimeContext: () => ({ gameTime: new Date('2024-01-01T09:00:00Z'), isRunning: true, error: null }),
}))
vi.mock('../hooks/useWindowContext', () => ({ useWindowContext: () => ({ openWindow: mocks.openWindow }) }))
vi.mock('../services/forensicsService', async importOriginal => {
  const actual = await importOriginal<typeof import('../services/forensicsService')>()
  return { ...actual, forensicsService: {
    ...actual.forensicsService, getForensicRequests: mocks.get, requestForensicAnalysis: mocks.request,
  } }
})

const request = (status: ForensicRequest['status'] = 'pending'): ForensicRequest => ({
  id: '42', caseId: 'case_live', evidenceId: 'asset.phone', evidenceName: 'Visible phone',
  analysisType: 'ChemicalComparison', requestedAt: new Date('2024-01-01T08:00:00Z'),
  estimatedCompletionTime: new Date('2024-01-01T08:17:00Z'), status,
  ...(status === 'completed' ? { resultDocumentId: 'asset.report', resultEmailId: 'email.lab' } : {}),
})

function mount(language = 'en-US') {
  localStorage.setItem('casezero-language', language)
  return render(<LanguageProvider><ForensicModule /></LanguageProvider>)
}

describe('real forensic module', () => {
  beforeEach(() => {
    vi.resetAllMocks()
    localStorage.clear()
    mocks.get.mockResolvedValue([])
    mocks.request.mockResolvedValue(request())
    mocks.refresh.mockResolvedValue(undefined)
    mocks.view.mockResolvedValue(undefined)
    mocks.email.mockResolvedValue(undefined)
  })
  afterEach(() => cleanup())

  it.each(['pt-BR', 'en-US', 'es-ES', 'fr-FR'])('localizes the real request workflow in %s', async language => {
    const locale = translations[language]
    mount(language)
    await waitFor(() => expect(mocks.get).toHaveBeenCalledWith('case_live'))
    expect(screen.getByText(locale.forensicClockPolicy)).toBeInTheDocument()
    fireEvent.click(screen.getAllByRole('button', { name: locale.requestAnalysis }).at(-1)!)
    fireEvent.change(screen.getByLabelText(locale.forensicSelectEvidence), { target: { value: 'asset.phone' } })
    expect(screen.queryByRole('option', { name: /DNA/ })).not.toBeInTheDocument()
    expect(screen.getByRole('option', { name: `ChemicalComparison — ${locale.forensicDuration.replace('{minutes}', '17')}` })).toBeInTheDocument()
    fireEvent.change(screen.getByLabelText(locale.analysisType), { target: { value: 'ChemicalComparison' } })
    fireEvent.click(screen.getAllByRole('button', { name: locale.requestAnalysis }).at(-1)!)
    await waitFor(() => expect(mocks.request).toHaveBeenCalledWith('case_live', 'asset.phone', 'ChemicalComparison'))
    expect(await screen.findByText(locale.forensicPending)).toBeInTheDocument()
    expect(screen.queryByText('CASE-2024-001')).not.toBeInTheDocument()
  })

  it('does not fabricate a completed result after the deadline', async () => {
    mocks.get.mockResolvedValue([request()])
    mount()
    expect(await screen.findByText(enUS.forensicAwaitingLab)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: enUS.forensicOpenReport })).not.toBeInTheDocument()
  })

  it('opens the actual returned report and lab email, including after reopening the module', async () => {
    mocks.get.mockResolvedValue([request('completed')])
    mount()
    await waitFor(() => expect(mocks.get).toHaveBeenCalled())
    fireEvent.click(screen.getByRole('button', { name: `${enUS.analysisResults} (1)` }))
    fireEvent.click(screen.getByRole('button', { name: enUS.forensicOpenReport }))
    await waitFor(() => expect(mocks.view).toHaveBeenCalledWith('asset.report'))
    expect(mocks.openWindow).toHaveBeenCalledWith('document-asset.report', 'Actual report', expect.any(Function),
      expect.objectContaining({ caseId: 'case_live', fileData: expect.objectContaining({ id: 'asset.report', type: 'pdf' }) }))
    fireEvent.click(screen.getByRole('button', { name: enUS.forensicOpenEmail }))
    expect(await screen.findByText('Verified conclusion from the case')).toBeInTheDocument()
    expect(mocks.email).toHaveBeenCalledWith('email.lab')
  })

  it('shows API failures rather than creating a simulated request', async () => {
    mocks.request.mockRejectedValue(new Error('Rejected incompatible analysis'))
    mount()
    await waitFor(() => expect(mocks.get).toHaveBeenCalled())
    fireEvent.click(screen.getAllByRole('button', { name: enUS.requestAnalysis }).at(-1)!)
    fireEvent.change(screen.getByLabelText(enUS.forensicSelectEvidence), { target: { value: 'asset.phone' } })
    fireEvent.change(screen.getByLabelText(enUS.analysisType), { target: { value: 'ChemicalComparison' } })
    fireEvent.click(screen.getAllByRole('button', { name: enUS.requestAnalysis }).at(-1)!)
    expect(await screen.findByRole('alert')).toHaveTextContent(enUS.forensicRequestFailed)
    expect(screen.queryByText(enUS.forensicPending)).not.toBeInTheDocument()
  })
})
