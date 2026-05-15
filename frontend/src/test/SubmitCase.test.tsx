import React from 'react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import '@testing-library/jest-dom'

import type { SubmitCaseRequest, SubmitCaseResult } from '../types/caseV2'

// ──────────────────────────────────────────────────────────────
// Mutable mock state — updated by the mock's submitCase
// ──────────────────────────────────────────────────────────────

let mockSubmissionState = {
  attemptsUsed: 0,
  maxAttempts: 3,
  lastResult: undefined as SubmitCaseResult | undefined,
}

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
    difficulty: 'Detective' as const, requiredRank: 'Rookie' as const,
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
    breakdown: { culprit: false, evidence: false, analysis: false, questions: false },
    attemptsRemaining: 2,
    feedbackText: 'wrong',
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

vi.mock('../contexts/LanguageContext', () => ({
  useLanguage: () => ({ t: (key: string) => key }),
  LanguageProvider: ({ children }: { children: React.ReactNode }) => children,
}))

// ──────────────────────────────────────────────────────────────
// Import after mocks
// ──────────────────────────────────────────────────────────────

import SubmitCase from '../components/apps/SubmitCase'

// ──────────────────────────────────────────────────────────────
// Tests
// ──────────────────────────────────────────────────────────────

describe('SubmitCase', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockSubmissionState = { attemptsUsed: 0, maxAttempts: 3, lastResult: undefined }
  })

  it('renders visible suspects as select options', () => {
    render(<SubmitCase />)
    const select = screen.getByRole('combobox')
    expect(select).toBeInTheDocument()
    expect(screen.getByText('Marcus Reeve')).toBeInTheDocument()
    expect(screen.getByText('Alice Turner')).toBeInTheDocument()
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
      breakdown: { culprit: true, evidence: true, analysis: true, questions: true },
      attemptsRemaining: 2, feedbackText: 'Case solved.', explanationMarkdown: undefined,
    })

    render(<SubmitCase />)

    fireEvent.change(screen.getByRole('combobox'), { target: { value: 'suspect.marcus' } })
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
      breakdown: { culprit: true, evidence: true, analysis: false, questions: true },
      attemptsRemaining: 2, feedbackText: 'Case solved!', explanationMarkdown: undefined,
    })

    const { rerender } = render(<SubmitCase />)
    fireEvent.change(screen.getByRole('combobox'), { target: { value: 'suspect.marcus' } })
    fireEvent.click(screen.getByRole('button'))

    // Wait for submitCase to resolve and re-render with updated state
    await waitFor(() => expect(mockSubmitCase).toHaveBeenCalledOnce())
    rerender(<SubmitCase />)

    expect(screen.getByText(/0\.9/)).toBeInTheDocument()
  })

  it('button is disabled when no suspect is selected', () => {
    render(<SubmitCase />)
    expect(screen.getByRole('button')).toBeDisabled()
  })

  it('button is disabled when attemptsRemaining is 0', () => {
    mockSubmissionState = { attemptsUsed: 3, maxAttempts: 3, lastResult: undefined }
    render(<SubmitCase />)
    expect(screen.getByRole('button')).toBeDisabled()
  })

  it('renders explanationMarkdown when attemptsRemaining reaches 0', async () => {
    const explanation = 'Marcus committed the crime because of blackmail.'
    mockSubmitCase.mockResolvedValueOnce({
      correct: false, score: 0.3,
      breakdown: { culprit: false, evidence: false, analysis: false, questions: false },
      attemptsRemaining: 0, feedbackText: 'Out of attempts.',
      explanationMarkdown: explanation,
    })

    // Start with 1 attempt left so the button is enabled
    mockSubmissionState = { attemptsUsed: 2, maxAttempts: 3, lastResult: undefined }

    const { rerender } = render(<SubmitCase />)
    fireEvent.change(screen.getByRole('combobox'), { target: { value: 'suspect.marcus' } })
    fireEvent.click(screen.getByRole('button'))

    await waitFor(() => expect(mockSubmitCase).toHaveBeenCalledOnce())
    // After the call, mockSubmissionState.attemptsUsed becomes 3 → isExhausted = true
    rerender(<SubmitCase />)

    await waitFor(() => {
      expect(screen.getByText(explanation)).toBeInTheDocument()
    })
  })
})
