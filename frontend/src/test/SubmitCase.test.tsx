import React from 'react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent, waitFor, within } from '@testing-library/react'
import '@testing-library/jest-dom'

import type { CaseDifficulty, SubmitCaseRequest, SubmitCaseResult } from '../types/caseV2'

// ──────────────────────────────────────────────────────────────
// Mutable mock state — updated by the mock's submitCase
// ──────────────────────────────────────────────────────────────

let mockSubmissionState = {
  attemptsUsed: 0,
  maxAttempts: 3,
  lastResult: undefined as SubmitCaseResult | undefined,
}
let mockLanguage = 'en-US'

const mockSuspects = [
  { id: 'suspect.marcus', name: 'Marcus Reeve', visibility: 'initial' as const, alibiVerified: false },
  { id: 'suspect.alice',  name: 'Alice Turner', visibility: 'initial' as const, alibiVerified: false },
]

const mockAssets = [
  { id: 'asset.phone_data',             title: 'Phone Data',             uri: 'file://p1', visibility: 'initial' as const, type: 'digital' as const },
  { id: 'asset.phone_forensics_report', title: 'Phone Forensics Report', uri: 'file://p2', visibility: 'initial' as const, type: 'document' as const },
]

const mockQuestions = [
  {
    id: 'q1',
    prompt: 'What was the motive?',
    options: [
      { id: 'opt-blackmail', label: 'Blackmail' },
      { id: 'opt-other',     label: 'Other'     },
    ],
  },
]

const mockCase = {
  caseId: 'case_001',
  version: '2.0' as const,
  metadata: {
    title: 'The Missing Heir', description: 'A case.', location: 'Riverside',
    incidentDate: '2024-01-01', openedAt: '2024-01-01',
    difficulty: 'Detective' as CaseDifficulty, requiredRank: 'Detective' as CaseDifficulty,
  },
  assets: mockAssets,
  emails: [],
  suspects: mockSuspects,
  timeline: [],
  forensicsDefaults: { analysisTypes: [] },
  solution: { questions: mockQuestions, minimumScore: 0.7, maxAttempts: 3 },
}

// submitCase mock updates the mutable state so the component can reflect the result
const mockSubmitCase = vi.fn(async (payload: SubmitCaseRequest): Promise<SubmitCaseResult> => {
  void payload
  return {
    correct: false,
    score: 0,
    breakdown: { culpritScore: 0, evidenceScore: 0, analysisScore: 0, questionsScore: 0 },
    maxScores: { culprit: 0.4, evidence: 0.2, analysis: 0.2, questions: 0.2 },
    attemptsRemaining: 2,
    graded: true,
    feedbackCode: 'incorrect_attempts_remaining',
    explanationMarkdown: undefined,
  }
})

// ──────────────────────────────────────────────────────────────
// Module mocks
// ──────────────────────────────────────────────────────────────

vi.mock('../contexts/CaseContext', () => ({
  useCase: () => ({
    state: { case: mockCase, submission: mockSubmissionState },
    submitCase: async (payload: SubmitCaseRequest) => {
      const result = await mockSubmitCase(payload)
      mockSubmissionState = {
        attemptsUsed: mockSubmissionState.maxAttempts - result.attemptsRemaining,
        maxAttempts: mockSubmissionState.maxAttempts,
        lastResult: result,
      }
      return result
    },
  }),
  useAssets:     () => mockAssets,
  useSuspects:   () => mockSuspects,
  useSubmission: () => mockSubmissionState,
}))

vi.mock('../services/api', () => ({
  forensicRequestApi: {
    getForensicRequests: vi.fn(async () => [] as unknown[]),
  },
}))

vi.mock('../contexts/LanguageContext', () => ({
  useLanguage: () => ({ t: (key: string) => key, currentLanguage: mockLanguage }),
  LanguageProvider: ({ children }: { children: React.ReactNode }) => children,
}))

// ──────────────────────────────────────────────────────────────
// Import after mocks
// ──────────────────────────────────────────────────────────────

import SubmitCase from '../components/apps/SubmitCase'
import { forensicRequestApi } from '../services/api'

// ──────────────────────────────────────────────────────────────
// Tests
// ──────────────────────────────────────────────────────────────

describe('SubmitCase', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockSubmissionState = { attemptsUsed: 0, maxAttempts: 3, lastResult: undefined }
    mockLanguage = 'en-US'
    mockCase.metadata.difficulty = 'Detective'
    mockCase.metadata.requiredRank = 'Detective'
    vi.mocked(forensicRequestApi.getForensicRequests).mockResolvedValue([])
  })

  it('renders visible suspects as selectable cards', () => {
    render(<SubmitCase />)
    // Suspects are now rendered as radio buttons within a radiogroup
    const group = screen.getByRole('radiogroup', { name: /suspect/i })
    expect(group).toBeInTheDocument()
    expect(screen.getByText('Marcus Reeve')).toBeInTheDocument()
    expect(screen.getByText('Alice Turner')).toBeInTheDocument()
    // Scope to the suspect group to avoid counting question-option radios too
    expect(within(group).getAllByRole('radio')).toHaveLength(mockSuspects.length)
  })

  it('renders visible assets as evidence checkboxes', () => {
    render(<SubmitCase />)
    expect(screen.getByText('Phone Data')).toBeInTheDocument()
    expect(screen.getByText('Phone Forensics Report')).toBeInTheDocument()
    expect(screen.getAllByRole('checkbox')).toHaveLength(2)
  })

  it('renders solution questions with their options', () => {
    render(<SubmitCase />)
    expect(screen.getByText('What was the motive?')).toBeInTheDocument()
    expect(screen.getByText('Blackmail')).toBeInTheDocument()
    expect(screen.getByText('Other')).toBeInTheDocument()
  })

  it('calls submitCase with the chosen payload on submit', async () => {
    mockSubmitCase.mockResolvedValueOnce({
      correct: true, score: 1.0,
      breakdown: { culpritScore: 0.4, evidenceScore: 0.2, analysisScore: 0.2, questionsScore: 0.2 },
      maxScores: { culprit: 0.4, evidence: 0.2, analysis: 0.2, questions: 0.2 },
      attemptsRemaining: 2, graded: true, feedbackCode: 'correct', explanationMarkdown: undefined,
    })

    render(<SubmitCase />)

    fireEvent.click(screen.getByRole('radio', { name: 'Marcus Reeve' }))
    fireEvent.click(screen.getAllByRole('checkbox')[0])
    fireEvent.click(screen.getByRole('button'))

    await waitFor(() => expect(mockSubmitCase).toHaveBeenCalledOnce())

    const payload: SubmitCaseRequest = mockSubmitCase.mock.calls[0][0]
    expect(payload.suspectId).toBe('suspect.marcus')
    expect(payload.evidenceIds).toContain('asset.phone_data')
  })

  it('renders score after response', async () => {
    mockSubmitCase.mockResolvedValueOnce({
      correct: true, score: 0.9,
      breakdown: { culpritScore: 0.4, evidenceScore: 0.2, analysisScore: 0.1, questionsScore: 0.2 },
      maxScores: { culprit: 0.4, evidence: 0.2, analysis: 0.2, questions: 0.2 },
      attemptsRemaining: 2, graded: true, feedbackCode: 'correct', explanationMarkdown: undefined,
    })

    const { rerender } = render(<SubmitCase />)
    fireEvent.click(screen.getByRole('radio', { name: 'Marcus Reeve' }))
    fireEvent.click(screen.getByRole('button'))

    // Wait for submitCase to resolve and re-render with updated state
    await waitFor(() => expect(mockSubmitCase).toHaveBeenCalledOnce())
    rerender(<SubmitCase />)

    expect(screen.getByText(/0\.9/)).toBeInTheDocument()
    expect(screen.getByText('submitCaseBreakdownCulprit').parentElement).toHaveTextContent('100% (0.4 / 0.4)')
    expect(screen.getByText('submitCaseBreakdownEvidence').parentElement).toHaveTextContent('100% (0.2 / 0.2)')
    expect(screen.getByText('submitCaseBreakdownAnalysis').parentElement).toHaveTextContent('50% (0.1 / 0.2)')
    expect(screen.getByText('submitCaseBreakdownQuestions').parentElement).toHaveTextContent('100% (0.2 / 0.2)')
  })

  it('button is disabled when no suspect is selected', () => {
    render(<SubmitCase />)
    expect(screen.getByRole('button')).toBeDisabled()
  })

  it('button stays enabled when attemptsRemaining is 0 (ungraded mode)', () => {
    mockSubmissionState = { attemptsUsed: 3, maxAttempts: 3, lastResult: undefined }
    render(<SubmitCase />)
    // Player can keep submitting ungraded; only the missing-suspect rule disables the button.
    expect(screen.getByRole('button')).toBeDisabled()
    fireEvent.click(screen.getByRole('radio', { name: 'Marcus Reeve' }))
    expect(screen.getByRole('button')).not.toBeDisabled()
  })

  it('renders explanationMarkdown when the case is solved on an ungraded attempt', async () => {
    const explanation = 'Marcus committed the crime because of blackmail.'
    mockSubmitCase.mockResolvedValueOnce({
      correct: true, score: 0.95,
      breakdown: { culpritScore: 0.4, evidenceScore: 0.2, analysisScore: 0.2, questionsScore: 0.15 },
      maxScores: { culprit: 0.4, evidence: 0.2, analysis: 0.2, questions: 0.2 },
      attemptsRemaining: 0, graded: false, feedbackCode: 'correct_ungraded',
      explanationMarkdown: explanation,
    })

    // Start with 1 attempt left so the button is enabled
    mockSubmissionState = { attemptsUsed: 2, maxAttempts: 3, lastResult: undefined }

    const { rerender } = render(<SubmitCase />)
    fireEvent.click(screen.getByRole('radio', { name: 'Marcus Reeve' }))
    fireEvent.click(screen.getByRole('button'))

    await waitFor(() => expect(mockSubmitCase).toHaveBeenCalledOnce())
    // After the call, mockSubmissionState.attemptsUsed becomes 3 → isExhausted = true
    rerender(<SubmitCase />)

    await waitFor(() => {
      expect(screen.getByText(explanation)).toBeInTheDocument()
    })
  })

  it('submits only selected, completed forensic analyses using their real asset/type IDs', async () => {
    vi.mocked(forensicRequestApi.getForensicRequests).mockResolvedValue([
      {
        id: 1, caseId: mockCase.caseId, userId: 'player', inputAssetId: 'asset.phone_data',
        inputAssetName: 'Phone Data', analysisType: 'Digital', status: 'completed',
        requestedAt: '2024-01-01T08:00:00Z', estimatedCompletionTime: '2024-01-01T08:15:00Z',
      },
      {
        id: 2, caseId: mockCase.caseId, userId: 'player', inputAssetId: 'asset.phone_data',
        analysisType: 'DNA', status: 'pending',
        requestedAt: '2024-01-01T08:00:00Z', estimatedCompletionTime: '2024-01-01T09:00:00Z',
      },
    ])
    render(<SubmitCase />)
    fireEvent.click(await screen.findByRole('checkbox', { name: 'Phone Data — Digital' }))
    expect(screen.queryByText('asset.phone_data:DNA')).not.toBeInTheDocument()
    fireEvent.click(screen.getByRole('radio', { name: 'Marcus Reeve' }))
    fireEvent.click(screen.getByRole('button'))
    await waitFor(() => expect(mockSubmitCase).toHaveBeenCalledWith(expect.objectContaining({
      analysisIds: ['asset.phone_data:Digital'],
    })))
  })

  it('does not expose or request interactive analyses for Rookie cases', async () => {
    mockCase.metadata.difficulty = 'Rookie'
    mockCase.metadata.requiredRank = 'Rookie'
    render(<SubmitCase />)
    expect(screen.queryByText('submitCaseAnalysisLabel')).not.toBeInTheDocument()
    expect(forensicRequestApi.getForensicRequests).not.toHaveBeenCalled()
    fireEvent.click(screen.getByRole('radio', { name: 'Marcus Reeve' }))
    fireEvent.click(screen.getByRole('button'))
    await waitFor(() => expect(mockSubmitCase).toHaveBeenCalledWith(expect.objectContaining({ analysisIds: [] })))
  })

  it.each(['pt-BR', 'en-US', 'es-ES', 'fr-FR'])('preserves four-decimal partial credit in %s', async language => {
    mockLanguage = language
    mockSubmitCase.mockResolvedValueOnce({
      correct: false, score: 0.6234,
      breakdown: { culpritScore: 0.4, evidenceScore: 0.1234, analysisScore: 0.1, questionsScore: 0 },
      maxScores: { culprit: 0.4, evidence: 0.2, analysis: 0.2, questions: 0.2 },
      attemptsRemaining: 2, graded: true, feedbackCode: 'incorrect_attempts_remaining',
    })
    const { rerender } = render(<SubmitCase />)
    fireEvent.click(screen.getByRole('radio', { name: 'Marcus Reeve' }))
    fireEvent.click(screen.getByRole('button'))
    await waitFor(() => expect(mockSubmitCase).toHaveBeenCalledOnce())
    rerender(<SubmitCase />)
    const format = new Intl.NumberFormat(language, { maximumFractionDigits: 4 })
    expect(screen.getByText(`submitCaseScore: ${format.format(0.6234)}`)).toBeInTheDocument()
    expect(screen.getByText('submitCaseBreakdownEvidence').parentElement)
      .toHaveTextContent(`62% (${format.format(0.1234)} / ${format.format(0.2)})`)
  })
})
