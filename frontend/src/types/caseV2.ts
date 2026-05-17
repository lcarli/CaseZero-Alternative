// Difficulty/Rank enum
export type CaseDifficulty = 'Rookie' | 'Detective' | 'Detective2' | 'Sergeant' | 'Lieutenant' | 'Captain' | 'Commander'

// Asset types
export interface Asset {
  id: string
  type: 'photo' | 'pdf' | 'audio' | 'video' | 'document' | 'image' | 'digital'
  title: string
  description?: string
  uri: string
  checksum?: string
  visibility: 'initial' | 'hidden'
  category?: string
  tags?: string[]
  metadata?: Record<string, unknown>
}

// Email types
export interface Email {
  id: string
  from: string
  to: string[] | string
  subject: string
  body: string
  sentAt: string
  priority?: 'normal' | 'high' | 'urgent'
  attachments?: string[]
  visibility: 'initial' | 'hidden'
  metadata?: Record<string, unknown>
}

// Suspect types
export interface Suspect {
  id: string
  name: string
  alias?: string
  age?: number
  occupation?: string
  relationship?: string
  description?: string
  motive?: string
  alibi?: string
  alibiVerified?: boolean
  status?: 'suspect' | 'cleared' | 'confirmed_culprit'
  background?: string
  relatedAssets?: string[]
  photo?: string
  visibility: 'initial' | 'hidden'
}

// Timeline
export interface TimelineEntry {
  time: string
  event: string
  source?: 'investigation' | 'witness' | 'sensor' | 'forensic'
  sourceAssetId?: string
  verified?: boolean
  importance?: 'low' | 'medium' | 'high' | 'critical'
}

// Notification
export interface Notification {
  level: 'info' | 'warn' | 'critical'
  message: string
}

// Solution (sanitized — no correctOptionId, no culprit, no requiredEvidenceIds)
export interface SanitizedQuestion {
  id: string
  prompt: string
  options: { id: string; label: string }[]
  weight?: number
}

export interface SanitizedSolution {
  questions: SanitizedQuestion[]
  minimumScore: number
  maxAttempts: number
}

// Case metadata
export interface CaseMetadata {
  title: string
  description: string
  location: string
  incidentDate: string
  openedAt: string
  difficulty: CaseDifficulty
  requiredRank: CaseDifficulty
  unlockMode?: 'gated' | 'all_initial'
  estimatedDurationMinutes?: number
  category?: string
  briefing?: string
  tags?: string[]
}

// ForensicsDefaults
export interface AnalysisTypeConfig {
  type: string
  durationMinutes: number
  availableFor: string[]
}

export interface ForensicsDefaults {
  analysisTypes: AnalysisTypeConfig[]
  noFindingsEmail?: {
    template: string
    from: string
    subject: string
  }
}

// Full sanitized case (what the client receives)
export interface CaseV2Sanitized {
  version: '2.0'
  caseId: string
  metadata: CaseMetadata
  assets: Asset[]
  emails: Email[]
  suspects: Suspect[]
  timeline: TimelineEntry[]
  forensicsDefaults: ForensicsDefaults
  solution: SanitizedSolution
  gameMetadata?: Record<string, unknown>
}

// Dashboard item (from GET /api/cases/dashboard)
export interface CaseDashboardItem {
  caseId: string
  title: string
  description: string
  location: string
  difficulty: CaseDifficulty
  requiredRank: CaseDifficulty
  category?: string
  tags?: string[]
  estimatedDurationMinutes?: number
}

// Submit request/result
export interface SubmitCaseRequest {
  suspectId: string
  evidenceIds: string[]
  analysisIds: string[]
  answers: { questionId: string; optionId: string }[]
}

export interface SubmitCaseBreakdown {
  culpritScore: number
  evidenceScore: number
  analysisScore: number
  questionsScore: number
}

export interface SubmitCaseScoreWeights {
  culprit: number
  evidence: number
  analysis: number
  questions: number
}

export type SubmitCaseFeedbackCode =
  | 'correct'
  | 'correct_ungraded'
  | 'incorrect_attempts_remaining'
  | 'incorrect_last_graded'
  | 'incorrect_ungraded'

export interface SubmitCaseResult {
  correct: boolean
  score: number
  breakdown: SubmitCaseBreakdown
  maxScores: SubmitCaseScoreWeights
  attemptsRemaining: number
  graded: boolean
  feedbackCode: SubmitCaseFeedbackCode
  explanationMarkdown?: string
}
