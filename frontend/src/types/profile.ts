import type { DashboardActivityType, PromotionProgress } from './caseV2'

export interface ProfileAgent {
  firstName?: string
  lastName?: string
  badgeNumber?: string
  currentRank: string
  lastPromotionDate?: string | null
}

export interface ProfileCaseHistoryRow {
  caseId: string
  title: string
  difficulty: string
  totalAttempts: number
  gradedAttempts: number
  bestScore: number
  isResolved: boolean
  resolvedViaGraded: boolean
  firstAttemptAt: string
  lastAttemptAt: string
}

export interface ProfileScoreByDifficulty {
  difficulty: string
  casesPlayed: number
  casesResolved: number
  avgBestScore: number
}

export interface ProfileSubmissionTimelineRow {
  date: string
  caseId: string
  caseTitle: string
  difficulty: string
  type: DashboardActivityType
  score: number
  graded: boolean
  attemptNumber: number
}

export interface ProfileRankHistoryRow {
  previousRank: string | null
  newRank: string
  changedAt: string
  reason: string | null
}

export interface ProfileCategoryBreakdown {
  sampleSize: number
  culpritAvg: number
  evidenceAvg: number
  analysisAvg: number
  questionsAvg: number
  culpritWeight: number
  evidenceWeight: number
  analysisWeight: number
  questionsWeight: number
}

export interface ProfileStats {
  agent: ProfileAgent
  promotion: PromotionProgress
  caseHistory: ProfileCaseHistoryRow[]
  scoreByDifficulty: ProfileScoreByDifficulty[]
  submissionsTimeline: ProfileSubmissionTimelineRow[]
  rankHistory: ProfileRankHistoryRow[]
  categoryBreakdown: ProfileCategoryBreakdown
}
